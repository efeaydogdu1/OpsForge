using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsForge.Application;
using OpsForge.Domain;

namespace OpsForge.IntegrationTests;

public sealed class GitHubIntegrationTests(OpsForgeWebFactory factory) : IClassFixture<OpsForgeWebFactory>
{
    private readonly FakeGitHubApiClient _fakeClient = new();

    [Theory]
    [InlineData("https://github.com/acme/ops-repo", "acme", "ops-repo", "https://github.com/acme/ops-repo")]
    [InlineData("https://github.com/acme/ops-repo.git", "acme", "ops-repo", "https://github.com/acme/ops-repo")]
    [InlineData("git@github.com:acme/ops-repo.git", "acme", "ops-repo", "https://github.com/acme/ops-repo")]
    public void RepositoryUrlParser_AcceptsSupportedGitHubUrls(string url, string expectedOwner, string expectedName, string expectedNormalizedUrl)
    {
        var parsed = OpsForge.Modules.GitHub.GitHubRepositoryUrlParser.TryParse(url, out var owner, out var name, out var normalizedUrl);

        Assert.True(parsed);
        Assert.Equal(expectedOwner, owner);
        Assert.Equal(expectedName, name);
        Assert.Equal(expectedNormalizedUrl, normalizedUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com/acme/ops-repo")]
    [InlineData("https://github.com/acme")]
    [InlineData("not-a-url")]
    public void RepositoryUrlParser_RejectsInvalidUrls(string url)
    {
        var parsed = OpsForge.Modules.GitHub.GitHubRepositoryUrlParser.TryParse(url, out _, out _, out _);

        Assert.False(parsed);
    }

    private HttpClient CreateClient()
    {
        var clientFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGitHubApiClient>();
                services.AddSingleton<IGitHubApiClient>(_fakeClient);
            });
        });

        return clientFactory.CreateClient();
    }

    [Fact]
    public async Task PreviewRepositoryMetadata_ParsesUrl_AndReturnsMetadata()
    {
        using var client = CreateClient();
        await AuthorizeAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/github/preview", new PreviewRepositoryMetadataCommand("https://github.com/acme/ops-repo.git"));
        response.EnsureSuccessStatusCode();

        var metadata = await response.Content.ReadFromJsonAsync<GitHubRepositoryMetadataDto>();
        Assert.NotNull(metadata);
        Assert.Equal("acme", metadata!.Owner);
        Assert.Equal("ops-repo", metadata.Name);
        Assert.Equal("https://github.com/acme/ops-repo", metadata.Url);
        Assert.NotNull(metadata.LatestCommitSha);
    }

    [Fact]
    public async Task PreviewRepositoryMetadata_UsesCurrentUsersDefaultGitHubToken()
    {
        using var client = CreateClient();
        await AuthorizeAsync(client);

        var tokenResponse = await client.PostAsJsonAsync("/api/v1/me/github-tokens", new
        {
            name = "Personal",
            token = "ghp_testtoken1234",
            isDefault = true
        });
        tokenResponse.EnsureSuccessStatusCode();

        var tokens = await client.GetFromJsonAsync<UserGitHubTokenDto[]>("/api/v1/me/github-tokens");
        Assert.NotNull(tokens);
        var savedToken = Assert.Single(tokens!);
        Assert.Equal("1234", savedToken.TokenLastFour);

        var response = await client.PostAsJsonAsync("/api/v1/github/preview", new PreviewRepositoryMetadataCommand("https://github.com/acme/ops-repo"));
        response.EnsureSuccessStatusCode();

        Assert.Equal("ghp_testtoken1234", _fakeClient.LastAccessToken);
    }


    [Fact]
    public async Task PreviewRepositoryMetadata_InvalidUrl_ReturnsServerError()
    {
        using var client = CreateClient();
        await AuthorizeAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/github/preview", new PreviewRepositoryMetadataCommand("https://example.com/not-github/repo"));
        Assert.True(response.StatusCode == HttpStatusCode.InternalServerError || response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LinkAndSyncRepository_StoresMetadataAndSyncHistory()
    {
        using var client = CreateClient();
        var auth = await AuthorizeAsync(client);

        var teamRes = await client.PostAsJsonAsync("/api/v1/teams", new CreateTeamCommand($"Team_{Guid.NewGuid():N}", "GitHub Team"));
        teamRes.EnsureSuccessStatusCode();
        var team = await teamRes.Content.ReadFromJsonAsync<TeamDto>();
        Assert.NotNull(team);

        var memberRes = await client.PostAsJsonAsync($"/api/v1/teams/{team!.Id}/members", new AddTeamMemberCommand(team.Id, auth.UserId, TeamMemberRole.Lead));
        memberRes.EnsureSuccessStatusCode();

        var serviceRes = await client.PostAsJsonAsync("/api/v1/services", new CreateServiceCommand($"Svc_{Guid.NewGuid():N}", "GitHub linked service", team.Id, ServiceCriticality.Medium, null));
        serviceRes.EnsureSuccessStatusCode();
        var service = await serviceRes.Content.ReadFromJsonAsync<ServiceDto>();
        Assert.NotNull(service);

        var linkRes = await client.PostAsJsonAsync($"/api/v1/github/services/{service!.Id}/link", new { repositoryUrl = "https://github.com/acme/ops-repo" });
        linkRes.EnsureSuccessStatusCode();
        var linkData = await linkRes.Content.ReadFromJsonAsync<GitHubRepositoryMetadataDto>();
        Assert.NotNull(linkData);
        Assert.Equal("acme", linkData!.Owner);
        Assert.Equal("ops-repo", linkData.Name);

        var syncRes = await client.PostAsJsonAsync($"/api/v1/github/services/{service.Id}/sync", new { });
        syncRes.EnsureSuccessStatusCode();
        var syncRun = await syncRes.Content.ReadFromJsonAsync<RepositorySyncRunDto>();
        Assert.NotNull(syncRun);
        Assert.True(syncRun!.IsSuccess);
        Assert.Equal("acme", syncRun.Owner);

        var historyRes = await client.GetAsync($"/api/v1/github/services/{service.Id}/sync-runs");
        historyRes.EnsureSuccessStatusCode();
        var history = await historyRes.Content.ReadFromJsonAsync<RepositorySyncRunDto[]>();
        Assert.NotNull(history);
        Assert.True(history!.Length >= 2);

        var servicesList = await client.GetFromJsonAsync<ServiceDto[]>("/api/v1/services");
        var linkedService = servicesList!.FirstOrDefault(x => x.Id == service.Id);
        Assert.NotNull(linkedService);
        Assert.Equal("https://github.com/acme/ops-repo", linkedService!.RepositoryUrl);
    }

    [Fact]
    public async Task SyncAccount_ImportsRepositoriesIntoRegistry()
    {
        using var client = CreateClient();
        await AuthorizeAsync(client);

        var tokenResponse = await client.PostAsJsonAsync("/api/v1/me/github-tokens", new
        {
            name = "Personal",
            token = "ghp_accountsync1234",
            isDefault = true
        });
        tokenResponse.EnsureSuccessStatusCode();

        var syncResponse = await client.PostAsJsonAsync("/api/v1/github/sync-account", new { });
        syncResponse.EnsureSuccessStatusCode();

        var result = await syncResponse.Content.ReadFromJsonAsync<GitHubAccountSyncResultDto>();
        Assert.NotNull(result);
        Assert.True(result!.RepositoriesImported >= 2);
        Assert.True(result.ServicesCreated >= 2);
        Assert.True(result.EnvironmentsImported >= 1);
        Assert.True(result.DeploymentsImported >= 1);
        Assert.True(result.IssuesImported >= 1);

        var services = await client.GetFromJsonAsync<ServiceDto[]>("/api/v1/services");
        Assert.Contains(services!, x => x.Name == "acme/ops-repo");
        Assert.Contains(services!, x => x.Name == "acme/web-repo");

        var environments = await client.GetFromJsonAsync<EnvironmentDto[]>("/api/v1/environments");
        Assert.Contains(environments!, x => x.Name == "production");
        Assert.Contains(environments!, x => x.Name == "main");
        Assert.Contains(environments!, x => x.Name == "develop");
        Assert.All(environments!.Where(x => x.Name is "production" or "main" or "develop"), x => Assert.Null(x.Url));

        var deployments = await client.GetFromJsonAsync<DeploymentDto[]>("/api/v1/deployments");
        Assert.Contains(deployments!, x => x.CommitHash == "deploysha001");
        Assert.Contains(deployments!, x => x.CommitHash == "workflowsha001" && x.Version == "Build");

        var issues = await client.GetFromJsonAsync<IssueDto[]>("/api/v1/issues");
        Assert.Contains(issues!, x => x.ExternalNumber == 42 && x.ExternalUrl == "https://github.com/acme/ops-repo/issues/42");

        var infrastructure = await client.GetFromJsonAsync<InfrastructureAssetDto[]>("/api/v1/infrastructure");
        Assert.DoesNotContain(infrastructure!, x => x.Provider == "GitHub");
    }


    private static async Task<AuthResponse> AuthorizeAsync(HttpClient client)
    {
        var email = $"gh_user_{Guid.NewGuid():N}@test.com";
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterCommand(email, "Password123!", "GitHub User"));
        registerResponse.EnsureSuccessStatusCode();

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Auth response cannot be null.");

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }

    private sealed class FakeGitHubApiClient : IGitHubApiClient
    {
        private int _counter;
        public string? LastAccessToken { get; private set; }

        public Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string repositoryUrl, string? accessToken = null, CancellationToken cancellationToken = default)
        {
            if (!OpsForge.Modules.GitHub.GitHubRepositoryUrlParser.TryParse(repositoryUrl, out var owner, out var repositoryName, out var normalizedUrl))
            {
                throw new InvalidOperationException("Invalid GitHub repository URL.");
            }

            return GetRepositoryMetadataAsync(owner, repositoryName, normalizedUrl, accessToken, cancellationToken);
        }

        public Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string owner, string repositoryName, string repositoryUrl, string? accessToken = null, CancellationToken cancellationToken = default)
        {
            LastAccessToken = accessToken;
            _counter++;
            return Task.FromResult(new GitHubRepositoryMetadata(
                owner,
                repositoryName,
                repositoryUrl,
                "main",
                "Mock repository for integration tests",
                "public",
                "C#",
                $"mocksha{_counter:000}",
                DateTime.UtcNow,
                $"Mock commit {_counter}"));
        }

        public Task<IReadOnlyCollection<GitHubRepositoryMetadata>> ListRepositoriesAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            LastAccessToken = accessToken;
            IReadOnlyCollection<GitHubRepositoryMetadata> repositories =
            [
                new("acme", "ops-repo", "https://github.com/acme/ops-repo", "main", "Ops repo", "private", "C#", "mocksha101", DateTime.UtcNow, "Mock import commit"),
                new("acme", "web-repo", "https://github.com/acme/web-repo", "main", "Web repo", "private", "TypeScript", "mocksha102", DateTime.UtcNow, "Mock import commit")
            ];

            return Task.FromResult(repositories);
        }

        public Task<IReadOnlyCollection<string>> ListRepositoryEnvironmentsAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<string> environments = repositoryName == "ops-repo"
                ? ["production", "staging"]
                : ["preview"];

            return Task.FromResult(environments);
        }

        public Task<IReadOnlyCollection<string>> ListRepositoryBranchesAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<string> branches = repositoryName == "ops-repo"
                ? ["main", "develop"]
                : ["main"];

            return Task.FromResult(branches);
        }

        public Task<IReadOnlyCollection<GitHubRepositoryDeployment>> ListRepositoryDeploymentsAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<GitHubRepositoryDeployment> deployments = repositoryName == "ops-repo"
                ? [new("production", "main", "deploysha001", "Production deployment", DateTime.UtcNow.AddDays(-1), "octocat")]
                : [];

            return Task.FromResult(deployments);
        }

        public Task<IReadOnlyCollection<GitHubRepositoryDeployment>> ListRepositoryWorkflowRunsAsync(string owner, string repositoryName, string defaultBranch, string accessToken, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<GitHubRepositoryDeployment> runs = repositoryName == "ops-repo"
                ? [new("main", "Build", "workflowsha001", "Build: Mock workflow run (completed/success)", DateTime.UtcNow, "octocat")]
                : [];

            return Task.FromResult(runs);
        }

        public Task<IReadOnlyCollection<GitHubRepositoryIssue>> ListRepositoryIssuesAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<GitHubRepositoryIssue> issues = repositoryName == "ops-repo"
                ? [new(42, "Mock GitHub issue", "Synced issue body", "open", "https://github.com/acme/ops-repo/issues/42", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow)]
                : [];

            return Task.FromResult(issues);
        }
    }
}
