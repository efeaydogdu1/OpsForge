using System.Net;
using System.Net.Http.Json;
using OpsForge.Application;

namespace OpsForge.IntegrationTests;

public sealed class TeamsIntegrationTests(OpsForgeWebFactory factory) : IClassFixture<OpsForgeWebFactory>
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
    public async Task CreateTeam_ThenList_ContainsTeam()
    {
        var token = await AuthorizeAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var create = await _client.PostAsJsonAsync("/api/v1/teams",
            new CreateTeamCommand("Integration Team", "Created in test"));
        create.EnsureSuccessStatusCode();
        var team = await create.Content.ReadFromJsonAsync<TeamDto>();
        Assert.NotNull(team);
        Assert.Equal("Integration Team", team!.Name);

        var list = await _client.GetFromJsonAsync<TeamDto[]>("/api/v1/teams");
        Assert.Contains(list!, t => t.Id == team.Id);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateTeam_ChangesName()
    {
        var token = await AuthorizeAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var create = await _client.PostAsJsonAsync("/api/v1/teams",
            new CreateTeamCommand($"Team_{Guid.NewGuid():N}", null));
        var team = await create.Content.ReadFromJsonAsync<TeamDto>();

        var update = await _client.PutAsJsonAsync($"/api/v1/teams/{team!.Id}",
            new UpdateTeamCommand(team.Id, "Renamed Team", "Updated desc"));
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<TeamDto>();
        Assert.Equal("Renamed Team", updated!.Name);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task DeleteTeam_SoftDeletes()
    {
        var token = await AuthorizeAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var create = await _client.PostAsJsonAsync("/api/v1/teams",
            new CreateTeamCommand($"DeleteTeam_{Guid.NewGuid():N}", null));
        var team = await create.Content.ReadFromJsonAsync<TeamDto>();

        var del = await _client.DeleteAsync($"/api/v1/teams/{team!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
