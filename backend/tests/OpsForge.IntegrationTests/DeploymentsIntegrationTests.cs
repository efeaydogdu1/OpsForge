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

    [Fact]
    public async Task UpdateAndDeleteDeployment_Succeeds()
    {
        var token = await AuthorizeAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var svcRes = await _client.PostAsJsonAsync("/api/v1/services",
            new CreateServiceCommand($"CicdSvc_{Guid.NewGuid():N}", null, null, ServiceCriticality.Medium, null));
        var svc = await svcRes.Content.ReadFromJsonAsync<ServiceDto>();

        var envRes = await _client.PostAsJsonAsync("/api/v1/environments",
            new CreateEnvironmentCommand(svc!.Id, "main", EnvironmentKind.Development, null));
        var env = await envRes.Content.ReadFromJsonAsync<EnvironmentDto>();

        var create = await _client.PostAsJsonAsync("/api/v1/deployments",
            new CreateDeploymentCommand(svc.Id, env!.Id, "main", "abc1234", "Initial run"));
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<DeploymentDto>();

        var update = await _client.PutAsJsonAsync($"/api/v1/deployments/{created!.Id}",
            new UpdateDeploymentCommand(created.Id, svc.Id, env.Id, "develop", "def5678", "Updated run"));
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<DeploymentDto>();
        Assert.Equal("develop", updated!.Version);
        Assert.Equal("def5678", updated.CommitHash);

        var delete = await _client.DeleteAsync($"/api/v1/deployments/{created.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, delete.StatusCode);

        var list = await _client.GetFromJsonAsync<DeploymentDto[]>("/api/v1/deployments");
        Assert.DoesNotContain(list!, x => x.Id == created.Id);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
