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

public sealed record LinkServiceRepositoryCommand(Guid ServiceId, string RepositoryUrl) : IRequest<GitHubRepositoryMetadataDto>;
public sealed record PreviewRepositoryMetadataCommand(string RepositoryUrl) : IRequest<GitHubRepositoryMetadataDto>;
public sealed record SyncLinkedRepositoryCommand(Guid ServiceId) : IRequest<RepositorySyncRunDto>;

public interface IGitHubApiClient
{
    Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string repositoryUrl, CancellationToken cancellationToken = default);
    Task<GitHubRepositoryMetadata> GetRepositoryMetadataAsync(string owner, string repositoryName, string repositoryUrl, CancellationToken cancellationToken = default);
}

public interface IGitHubRepositoryUrlParser
{
    bool TryParse(string repositoryUrl, out string owner, out string repositoryName, out string normalizedUrl);
}
