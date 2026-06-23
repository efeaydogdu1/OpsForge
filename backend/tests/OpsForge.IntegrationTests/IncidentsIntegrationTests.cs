using System.Net.Http.Json;
using OpsForge.Application;
using OpsForge.Domain;

namespace OpsForge.IntegrationTests;

public sealed class IncidentsIntegrationTests(OpsForgeWebFactory factory) : IClassFixture<OpsForgeWebFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> AuthorizeAsync()
    {
        var email = $"incident_user_{Guid.NewGuid():N}@test.com";
        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterCommand(email, "Password123!", "Incident User"));
        var data = await reg.Content.ReadFromJsonAsync<AuthResponse>();
        return data!.AccessToken;
    }

    [Fact]
    public async Task CreateIncident_ThenList_IncludesGitHubResourceLinks()
    {
        var token = await AuthorizeAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var svcRes = await _client.PostAsJsonAsync("/api/v1/services",
            new CreateServiceCommand($"IncidentSvc_{Guid.NewGuid():N}", null, null, ServiceCriticality.High, "https://github.com/acme/incident-svc"));
        var svc = await svcRes.Content.ReadFromJsonAsync<ServiceDto>();

        var envRes = await _client.PostAsJsonAsync("/api/v1/environments",
            new CreateEnvironmentCommand(svc!.Id, "main", EnvironmentKind.Production, null));
        var env = await envRes.Content.ReadFromJsonAsync<EnvironmentDto>();

        var deploymentRes = await _client.PostAsJsonAsync("/api/v1/deployments",
            new CreateDeploymentCommand(svc.Id, env!.Id, "Build", "abcdef123456", "Mock CI run"));
        var deployment = await deploymentRes.Content.ReadFromJsonAsync<DeploymentDto>();

        var incidentRes = await _client.PostAsJsonAsync("/api/v1/incidents",
            new CreateIncidentCommand("API outage", "This happened during the mock CI run.", IncidentSeverity.High, IncidentStatus.Investigating, svc.Id, env.Id, deployment!.Id));
        incidentRes.EnsureSuccessStatusCode();

        var incidents = await _client.GetFromJsonAsync<IncidentResponseDto[]>("/api/v1/incidents");
        var incident = Assert.Single(incidents!, x => x.Title == "API outage");
        Assert.Equal("https://github.com/acme/incident-svc", incident.ServiceGitHubUrl);
        Assert.Equal("https://github.com/acme/incident-svc/tree/main", incident.EnvironmentGitHubUrl);
        Assert.Equal("https://github.com/acme/incident-svc/commit/abcdef123456", incident.DeploymentGitHubUrl);

        var delete = await _client.DeleteAsync($"/api/v1/incidents/{incident.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await _client.GetFromJsonAsync<IncidentResponseDto[]>("/api/v1/incidents");
        Assert.DoesNotContain(afterDelete!, x => x.Id == incident.Id);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    private sealed record IncidentResponseDto(
        Guid Id,
        string Title,
        string Description,
        string Severity,
        string Status,
        Guid ServiceId,
        string ServiceName,
        string? ServiceGitHubUrl,
        Guid? EnvironmentId,
        string? EnvironmentName,
        string? EnvironmentGitHubUrl,
        Guid? DeploymentId,
        string? DeploymentVersion,
        string? DeploymentGitHubUrl,
        Guid ReportedByUserId,
        DateTime OccurredAtUtc,
        DateTime? ResolvedAtUtc);
}
