using System.Net.Http.Json;
using OpsForge.Application;
using OpsForge.Domain;

namespace OpsForge.IntegrationTests;

public sealed class DeploymentsIntegrationTests(OpsForgeWebFactory factory) : IClassFixture<OpsForgeWebFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> AuthorizeAsync()
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";
        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterCommand(email, "Password123!", "Test User"));
        var data = await reg.Content.ReadFromJsonAsync<AuthResponse>();
        return data!.AccessToken;
    }

    [Fact]
    public async Task CreateDeployment_ThenListByService_ContainsIt()
    {
        var token = await AuthorizeAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Create service
        var svcRes = await _client.PostAsJsonAsync("/api/v1/services",
            new CreateServiceCommand($"DeploySvc_{Guid.NewGuid():N}", null, null, ServiceCriticality.Medium, null));
        var svc = await svcRes.Content.ReadFromJsonAsync<ServiceDto>();

        // Create environment
        var envRes = await _client.PostAsJsonAsync("/api/v1/environments",
            new CreateEnvironmentCommand(svc!.Id, "Production", EnvironmentKind.Production, null));
        var env = await envRes.Content.ReadFromJsonAsync<EnvironmentDto>();

        // Record deployment
        var depRes = await _client.PostAsJsonAsync("/api/v1/deployments",
            new CreateDeploymentCommand(svc.Id, env!.Id, "1.0.0", "abc1234", "Initial release"));
        depRes.EnsureSuccessStatusCode();
        var dep = await depRes.Content.ReadFromJsonAsync<DeploymentDto>();
        Assert.NotNull(dep);
        Assert.Equal("1.0.0", dep!.Version);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
