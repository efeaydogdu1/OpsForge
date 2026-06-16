using System.Net.Http.Json;
using OpsForge.Application;

namespace OpsForge.IntegrationTests;

public sealed class ServicesIntegrationTests(OpsForgeWebFactory factory) : IClassFixture<OpsForgeWebFactory>
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
    public async Task CreateService_ThenList_ContainsService()
    {
        var token = await AuthorizeAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var create = await _client.PostAsJsonAsync("/api/v1/services",
            new CreateServiceCommand($"Svc_{Guid.NewGuid():N}", "Test service", null, OpsForge.Domain.ServiceCriticality.High, null));
        create.EnsureSuccessStatusCode();
        var svc = await create.Content.ReadFromJsonAsync<ServiceDto>();
        Assert.NotNull(svc);
        Assert.Equal("High", svc!.Criticality);

        var list = await _client.GetFromJsonAsync<ServiceDto[]>("/api/v1/services");
        Assert.Contains(list!, s => s.Id == svc.Id);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
