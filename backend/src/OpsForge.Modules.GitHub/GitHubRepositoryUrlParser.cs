using System.Text.RegularExpressions;

namespace OpsForge.Modules.GitHub;

public static class GitHubRepositoryUrlParser
{
    private static readonly Regex SshPattern = new("^git@github\\.com:(?<owner>[^/]+)/(?<repo>[^/]+?)(\\.git)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParse(string repositoryUrl, out string owner, out string repositoryName, out string normalizedUrl)
    {
        owner = string.Empty;
        repositoryName = string.Empty;
        normalizedUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return false;
        }

        var trimmed = repositoryUrl.Trim();

        var sshMatch = SshPattern.Match(trimmed);
        if (sshMatch.Success)
        {
            owner = sshMatch.Groups["owner"].Value;
            repositoryName = sshMatch.Groups["repo"].Value;
            normalizedUrl = $"https://github.com/{owner}/{repositoryName}";
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        owner = segments[0];
        repositoryName = segments[1];

        if (repositoryName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repositoryName = repositoryName[..^4];
        }

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repositoryName))
        {
            return false;
        }

        normalizedUrl = $"https://github.com/{owner}/{repositoryName}";
        return true;
    }
}
