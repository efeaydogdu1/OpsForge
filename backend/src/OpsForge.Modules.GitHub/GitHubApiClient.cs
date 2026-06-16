using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpsForge.Application;

namespace OpsForge.Modules.GitHub;

public sealed class GitHubApiClient(HttpClient httpClient, GitHubOptions options) : IGitHubApiClient
{
    public async Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        if (!GitHubRepositoryUrlParser.TryParse(repositoryUrl, out var owner, out var repositoryName, out var normalizedUrl))
        {
            throw new InvalidOperationException("Invalid GitHub repository URL.");
        }

        return await GetRepositoryMetadataAsync(owner, repositoryName, normalizedUrl, cancellationToken);
    }

    public async Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string owner, string repositoryName, string repositoryUrl, CancellationToken cancellationToken = default)
    {
        ConfigureClient();

        var repoResponse = await httpClient.GetFromJsonAsync<GitHubRepositoryResponse>($"/repos/{owner}/{repositoryName}", cancellationToken)
            ?? throw new InvalidOperationException("GitHub repository metadata not found.");

        var branch = string.IsNullOrWhiteSpace(repoResponse.DefaultBranch) ? "main" : repoResponse.DefaultBranch;

        var commits = await httpClient.GetFromJsonAsync<List<GitHubCommitResponse>>($"/repos/{owner}/{repositoryName}/commits?sha={Uri.EscapeDataString(branch)}&per_page=1", cancellationToken)
            ?? [];

        var latestCommit = commits.FirstOrDefault();

        return new GitHubRepositoryMetadata(
            repoResponse.Owner.Login,
            repoResponse.Name,
            repositoryUrl,
            repoResponse.DefaultBranch,
            repoResponse.Description,
            repoResponse.Private ? "private" : "public",
            repoResponse.Language,
            latestCommit?.Sha,
            latestCommit?.Commit?.Author?.Date,
            latestCommit?.Commit?.Message);
    }

    private void ConfigureClient()
    {
        if (httpClient.BaseAddress is null)
        {
            httpClient.BaseAddress = new Uri(options.ApiBaseUrl);
        }

        httpClient.DefaultRequestHeaders.UserAgent.Clear();
        httpClient.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse(options.UserAgent));

        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        httpClient.DefaultRequestHeaders.Remove("X-GitHub-Api-Version");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        httpClient.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }
    }

    private sealed class GitHubRepositoryResponse
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required string DefaultBranch { get; set; }
        public bool Private { get; set; }
        public string? Language { get; set; }
        public required GitHubOwnerResponse Owner { get; set; }
    }

    private sealed class GitHubOwnerResponse
    {
        public required string Login { get; set; }
    }

    private sealed class GitHubCommitResponse
    {
        public required string Sha { get; set; }
        public GitHubCommitDetailsResponse? Commit { get; set; }
    }

    private sealed class GitHubCommitDetailsResponse
    {
        public string? Message { get; set; }
        public GitHubCommitAuthorResponse? Author { get; set; }
    }

    private sealed class GitHubCommitAuthorResponse
    {
        public DateTime? Date { get; set; }
    }
}
