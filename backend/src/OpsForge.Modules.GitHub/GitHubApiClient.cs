using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpsForge.Application;

namespace OpsForge.Modules.GitHub;

public sealed class GitHubApiClient(HttpClient httpClient, GitHubOptions options) : IGitHubApiClient
{
    public async Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string repositoryUrl, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        if (!GitHubRepositoryUrlParser.TryParse(repositoryUrl, out var owner, out var repositoryName, out var normalizedUrl))
        {
            throw new InvalidOperationException("Invalid GitHub repository URL.");
        }

        return await GetRepositoryMetadataAsync(owner, repositoryName, normalizedUrl, accessToken, cancellationToken);
    }

    public async Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string owner, string repositoryName, string repositoryUrl, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        ConfigureClient(accessToken);

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

    public async Task<IReadOnlyCollection<GitHubRepositoryMetadata>> ListRepositoriesAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ConfigureClient(accessToken);

        var repos = new List<GitHubRepositoryResponse>();
        try
        {
            for (var page = 1; page <= 10; page++)
            {
                var pageItems = await httpClient.GetFromJsonAsync<List<GitHubRepositoryResponse>>(
                    $"/user/repos?per_page=100&page={page}&affiliation=owner,collaborator,organization_member&sort=updated",
                    cancellationToken) ?? [];

                repos.AddRange(pageItems);
                if (pageItems.Count < 100)
                {
                    break;
                }
            }
        }
        catch (HttpRequestException ex) when (IsSkippableGitHubStatus(ex.StatusCode))
        {
            return [];
        }

        var result = new List<GitHubRepositoryMetadata>();
        foreach (var repo in repos)
        {
            try
            {
                var url = string.IsNullOrWhiteSpace(repo.HtmlUrl)
                    ? $"https://github.com/{repo.Owner.Login}/{repo.Name}"
                    : repo.HtmlUrl;

                result.Add(await GetRepositoryMetadataAsync(repo.Owner.Login, repo.Name, url, accessToken, cancellationToken));
            }
            catch (HttpRequestException ex) when (IsSkippableGitHubStatus(ex.StatusCode))
            {
                continue;
            }
        }

        return result;
    }

    public async Task<IReadOnlyCollection<string>> ListRepositoryBranchesAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default)
    {
        ConfigureClient(accessToken);

        try
        {
            var branches = await httpClient.GetFromJsonAsync<List<GitHubBranchResponse>>(
                $"/repos/{owner}/{repositoryName}/branches?per_page=100",
                cancellationToken) ?? [];

            return branches
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyCollection<string>> ListRepositoryEnvironmentsAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default)
    {
        ConfigureClient(accessToken);

        try
        {
            var response = await httpClient.GetFromJsonAsync<GitHubEnvironmentsResponse>(
                $"/repos/{owner}/{repositoryName}/environments?per_page=100",
                cancellationToken);

            return response?.Environments.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyCollection<GitHubRepositoryDeployment>> ListRepositoryDeploymentsAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default)
    {
        ConfigureClient(accessToken);

        try
        {
            var deployments = await httpClient.GetFromJsonAsync<List<GitHubDeploymentResponse>>(
                $"/repos/{owner}/{repositoryName}/deployments?per_page=50",
                cancellationToken) ?? [];

            return deployments
                .Where(x => !string.IsNullOrWhiteSpace(x.Environment) && !string.IsNullOrWhiteSpace(x.Sha))
                .Select(x => new GitHubRepositoryDeployment(
                    x.Environment,
                    string.IsNullOrWhiteSpace(x.Ref) ? x.Sha : x.Ref,
                    x.Sha,
                    x.Description,
                    x.CreatedAtUtc ?? DateTime.UtcNow,
                    x.Creator?.Login))
                .ToList();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyCollection<GitHubRepositoryDeployment>> ListRepositoryWorkflowRunsAsync(string owner, string repositoryName, string defaultBranch, string accessToken, CancellationToken cancellationToken = default)
    {
        ConfigureClient(accessToken);

        try
        {
            var response = await httpClient.GetFromJsonAsync<GitHubWorkflowRunsResponse>(
                $"/repos/{owner}/{repositoryName}/actions/runs?per_page=50",
                cancellationToken);

            return response?.WorkflowRuns
                .Where(x => !string.IsNullOrWhiteSpace(x.HeadSha))
                .Select(x =>
                {
                    var branch = string.IsNullOrWhiteSpace(x.HeadBranch) ? defaultBranch : x.HeadBranch;
                    var name = string.IsNullOrWhiteSpace(x.Name) ? "GitHub Actions workflow" : x.Name;
                    var state = string.IsNullOrWhiteSpace(x.Conclusion) ? x.Status : $"{x.Status}/{x.Conclusion}";
                    var title = string.IsNullOrWhiteSpace(x.DisplayTitle) ? name : x.DisplayTitle;
                    var description = $"{name}: {title} ({state})";
                    if (!string.IsNullOrWhiteSpace(x.HtmlUrl))
                    {
                        description = $"{description} - {x.HtmlUrl}";
                    }

                    return new GitHubRepositoryDeployment(
                        branch,
                        name,
                        x.HeadSha,
                        description,
                        x.RunStartedAtUtc ?? x.CreatedAtUtc ?? DateTime.UtcNow,
                        x.Actor?.Login);
                })
                .ToList() ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyCollection<GitHubRepositoryIssue>> ListRepositoryIssuesAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default)
    {
        ConfigureClient(accessToken);

        try
        {
            var issues = await httpClient.GetFromJsonAsync<List<GitHubIssueResponse>>(
                $"/repos/{owner}/{repositoryName}/issues?state=all&per_page=50",
                cancellationToken) ?? [];

            return issues
                .Where(x => x.PullRequest is null && !string.IsNullOrWhiteSpace(x.HtmlUrl))
                .Select(x => new GitHubRepositoryIssue(
                    x.Number,
                    x.Title,
                    x.Body,
                    x.State,
                    x.HtmlUrl,
                    x.CreatedAtUtc,
                    x.UpdatedAtUtc))
                .ToList();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    private void ConfigureClient(string? accessToken)
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
        var token = string.IsNullOrWhiteSpace(accessToken) ? options.Token : accessToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static bool IsSkippableGitHubStatus(System.Net.HttpStatusCode? statusCode) =>
        statusCode is System.Net.HttpStatusCode.Forbidden
            or System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.TooManyRequests
            or System.Net.HttpStatusCode.NotFound;

    private sealed class GitHubRepositoryResponse
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
        [JsonPropertyName("default_branch")]
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

    private sealed class GitHubEnvironmentsResponse
    {
        public List<GitHubEnvironmentResponse> Environments { get; set; } = [];
    }

    private sealed class GitHubEnvironmentResponse
    {
        public required string Name { get; set; }
    }

    private sealed class GitHubBranchResponse
    {
        public required string Name { get; set; }
    }

    private sealed class GitHubDeploymentResponse
    {
        public string Environment { get; set; } = "production";
        public string Ref { get; set; } = string.Empty;
        public string Sha { get; set; } = string.Empty;
        public string? Description { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAtUtc { get; set; }
        public GitHubOwnerResponse? Creator { get; set; }
    }

    private sealed class GitHubWorkflowRunsResponse
    {
        [JsonPropertyName("workflow_runs")]
        public List<GitHubWorkflowRunResponse> WorkflowRuns { get; set; } = [];
    }

    private sealed class GitHubWorkflowRunResponse
    {
        public string? Name { get; set; }
        [JsonPropertyName("display_title")]
        public string? DisplayTitle { get; set; }
        [JsonPropertyName("head_branch")]
        public string? HeadBranch { get; set; }
        [JsonPropertyName("head_sha")]
        public string HeadSha { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string? Conclusion { get; set; }
        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAtUtc { get; set; }
        [JsonPropertyName("run_started_at")]
        public DateTime? RunStartedAtUtc { get; set; }
        public GitHubOwnerResponse? Actor { get; set; }
    }

    private sealed class GitHubIssueResponse
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public string State { get; set; } = string.Empty;
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;
        [JsonPropertyName("created_at")]
        public DateTime CreatedAtUtc { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAtUtc { get; set; }
        [JsonPropertyName("pull_request")]
        public object? PullRequest { get; set; }
    }
}
