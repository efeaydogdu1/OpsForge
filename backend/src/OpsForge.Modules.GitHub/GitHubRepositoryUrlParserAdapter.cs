using OpsForge.Application;

namespace OpsForge.Modules.GitHub;

public sealed class GitHubRepositoryUrlParserAdapter : IGitHubRepositoryUrlParser
{
    public bool TryParse(string repositoryUrl, out string owner, out string repositoryName, out string normalizedUrl)
    {
        return GitHubRepositoryUrlParser.TryParse(repositoryUrl, out owner, out repositoryName, out normalizedUrl);
    }
}
