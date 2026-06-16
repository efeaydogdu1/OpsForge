using Microsoft.AspNetCore.Mvc.Testing;

namespace OpsForge.IntegrationTests;

public class ApiSmokeTests : IClassFixture<WebApplicationFactory<OpsForge.Api.ApiMarker>>
{
    private readonly WebApplicationFactory<OpsForge.Api.ApiMarker> _factory;

    public ApiSmokeTests(WebApplicationFactory<OpsForge.Api.ApiMarker> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        response.EnsureSuccessStatusCode();
    }
}
