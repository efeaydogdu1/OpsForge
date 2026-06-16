namespace OpsForge.Modules.GitHub;

public sealed class GitHubOptions
{
    public string ApiBaseUrl { get; set; } = "https://api.github.com";
    public string? Token { get; set; }
    public string UserAgent { get; set; } = "OpsForge/1.0";
}
