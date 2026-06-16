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

        public Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string repositoryUrl, CancellationToken cancellationToken = default)
        {
            if (!OpsForge.Modules.GitHub.GitHubRepositoryUrlParser.TryParse(repositoryUrl, out var owner, out var repositoryName, out var normalizedUrl))
            {
                throw new InvalidOperationException("Invalid GitHub repository URL.");
            }

            return GetRepositoryMetadataAsync(owner, repositoryName, normalizedUrl, cancellationToken);
        }

        public Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string owner, string repositoryName, string repositoryUrl, CancellationToken cancellationToken = default)
        {
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
    }
}
