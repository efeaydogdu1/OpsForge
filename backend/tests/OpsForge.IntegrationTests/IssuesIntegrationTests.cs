using System.Net.Http.Json;
using OpsForge.Application;
using OpsForge.Domain;

namespace OpsForge.IntegrationTests;

public sealed class IssuesIntegrationTests(OpsForgeWebFactory factory) : IClassFixture<OpsForgeWebFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> AuthorizeAsync()
    {
        var email = $"issue_user_{Guid.NewGuid():N}@test.com";
        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterCommand(email, "Password123!", "Issue User"));
        var data = await reg.Content.ReadFromJsonAsync<AuthResponse>();
        return data!.AccessToken;
    }

    [Fact]
    public async Task CreateIssue_ThenList_IncludesGitHubResourceLinks()
    {
        var token = await AuthorizeAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var svcRes = await _client.PostAsJsonAsync("/api/v1/services",
            new CreateServiceCommand($"IssueSvc_{Guid.NewGuid():N}", null, null, ServiceCriticality.Medium, "https://github.com/acme/issue-svc"));
        var svc = await svcRes.Content.ReadFromJsonAsync<ServiceDto>();

        var envRes = await _client.PostAsJsonAsync("/api/v1/environments",
            new CreateEnvironmentCommand(svc!.Id, "main", EnvironmentKind.Production, null));
        var env = await envRes.Content.ReadFromJsonAsync<EnvironmentDto>();

        var deploymentRes = await _client.PostAsJsonAsync("/api/v1/deployments",
            new CreateDeploymentCommand(svc.Id, env!.Id, "Build", "abcdef123456", "Mock CI run"));
        var deployment = await deploymentRes.Content.ReadFromJsonAsync<DeploymentDto>();

        var issueRes = await _client.PostAsJsonAsync("/api/v1/issues",
            new CreateIssueCommand("Retry failure", "Manual issue linked to CI.", IssueStatus.Open, svc.Id, env.Id, deployment!.Id, null));
        issueRes.EnsureSuccessStatusCode();

        var issues = await _client.GetFromJsonAsync<IssueResponseDto[]>("/api/v1/issues");
        var issue = Assert.Single(issues!, x => x.Title == "Retry failure");
        Assert.Equal("Manual", issue.Source);
        Assert.Equal("https://github.com/acme/issue-svc", issue.ServiceGitHubUrl);
        Assert.Equal("https://github.com/acme/issue-svc/tree/main", issue.EnvironmentGitHubUrl);
        Assert.Equal("https://github.com/acme/issue-svc/commit/abcdef123456", issue.DeploymentGitHubUrl);

        var updateRes = await _client.PutAsJsonAsync($"/api/v1/issues/{issue.Id}",
            new UpdateIssueCommand(issue.Id, "Retry failure fixed", "Closed after follow-up.", IssueStatus.Closed, svc.Id, env.Id, deployment.Id, null));
        updateRes.EnsureSuccessStatusCode();

        var updated = await _client.GetFromJsonAsync<IssueResponseDto[]>("/api/v1/issues");
        Assert.Contains(updated!, x => x.Id == issue.Id && x.Status == "Closed");

        var delete = await _client.DeleteAsync($"/api/v1/issues/{issue.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await _client.GetFromJsonAsync<IssueResponseDto[]>("/api/v1/issues");
        Assert.DoesNotContain(afterDelete!, x => x.Id == issue.Id);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    private sealed record IssueResponseDto(
        Guid Id,
        string Title,
        string? Description,
        string Status,
        string Source,
        Guid ServiceId,
        string ServiceName,
        string? ServiceGitHubUrl,
        Guid? EnvironmentId,
        string? EnvironmentName,
        string? EnvironmentGitHubUrl,
        Guid? DeploymentId,
        string? DeploymentVersion,
        string? DeploymentGitHubUrl,
        string? ExternalUrl,
        int? ExternalNumber,
        string? ExternalState,
        DateTime? ExternalCreatedAtUtc,
        DateTime? ExternalUpdatedAtUtc);
}
