using System.Net;
using System.Net.Http.Json;
using OpsForge.Application;

namespace OpsForge.IntegrationTests;

public sealed class AuthIntegrationTests(OpsForgeWebFactory factory) : IClassFixture<OpsForgeWebFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_ThenLogin_Succeeds()
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";

        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterCommand(email, "Password123!", "Test User"));
        reg.EnsureSuccessStatusCode();

        var regData = await reg.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(regData);
        Assert.Equal(email, regData!.Email);
        Assert.NotEmpty(regData.AccessToken);

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginCommand(email, "Password123!"));
        login.EnsureSuccessStatusCode();

        var loginData = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginData);
        Assert.Equal(email, loginData!.Email);
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns_400_Or_500()
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterCommand(email, "Password123!", "Test User"));

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginCommand(email, "WrongPassword!"));

        Assert.True(login.StatusCode >= HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var res = await _client.GetAsync("/api/v1/teams");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithToken_Returns200()
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";
        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterCommand(email, "Password123!", "Test User"));
        var regData = await reg.Content.ReadFromJsonAsync<AuthResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", regData!.AccessToken);

        var res = await _client.GetAsync("/api/v1/teams");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
