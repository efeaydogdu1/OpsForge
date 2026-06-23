using MediatR;

namespace OpsForge.Application;

public sealed record GitHubRepositoryMetadata(
    string Owner,
    string Name,
    string Url,
    string? DefaultBranch,
    string? Description,
    string? Visibility,
    string? PrimaryLanguage,
    string? LatestCommitSha,
    DateTime? LatestCommitDateUtc,
    string? LatestCommitMessage);

public sealed record GitHubRepositoryMetadataDto(
    string Owner,
    string Name,
    string Url,
    string? DefaultBranch,
    string? Description,
    string? Visibility,
    string? PrimaryLanguage,
    string? LatestCommitSha,
    DateTime? LatestCommitDateUtc,
    string? LatestCommitMessage);

public sealed record RepositorySyncRunDto(
    Guid Id,
    Guid ServiceId,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    bool IsSuccess,
    string? ErrorMessage,
    string Owner,
    string Name,
    string Url,
    string? DefaultBranch,
    string? Description,
    string? Visibility,
    string? PrimaryLanguage,
    string? LatestCommitSha,
    DateTime? LatestCommitDateUtc,
    string? LatestCommitMessage);

public sealed record GitHubAccountSyncResultDto(
    int RepositoriesImported,
    int ServicesCreated,
    int ServicesUpdated,
    int EnvironmentsImported,
    int DeploymentsImported,
    int IssuesImported);

public sealed record GitHubRepositoryDeployment(
    string Environment,
    string Ref,
    string Sha,
    string? Description,
    DateTime CreatedAtUtc,
    string? CreatorLogin);

public sealed record GitHubRepositoryIssue(
    int Number,
    string Title,
    string? Body,
    string State,
    string Url,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record LinkServiceRepositoryCommand(Guid ServiceId, string RepositoryUrl) : IRequest<GitHubRepositoryMetadataDto>;
public sealed record PreviewRepositoryMetadataCommand(string RepositoryUrl) : IRequest<GitHubRepositoryMetadataDto>;
public sealed record SyncLinkedRepositoryCommand(Guid ServiceId) : IRequest<RepositorySyncRunDto>;
public sealed record SyncGitHubAccountCommand : IRequest<GitHubAccountSyncResultDto>;

public interface IGitHubApiClient
{
    Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string repositoryUrl, string? accessToken = null, CancellationToken cancellationToken = default);
    Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string owner, string repositoryName, string repositoryUrl, string? accessToken = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<GitHubRepositoryMetadata>> ListRepositoriesAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> ListRepositoryBranchesAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> ListRepositoryEnvironmentsAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<GitHubRepositoryDeployment>> ListRepositoryDeploymentsAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<GitHubRepositoryDeployment>> ListRepositoryWorkflowRunsAsync(string owner, string repositoryName, string defaultBranch, string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<GitHubRepositoryIssue>> ListRepositoryIssuesAsync(string owner, string repositoryName, string accessToken, CancellationToken cancellationToken = default);
}

public interface IGitHubRepositoryUrlParser
{
    bool TryParse(string repositoryUrl, out string owner, out string repositoryName, out string normalizedUrl);
}
