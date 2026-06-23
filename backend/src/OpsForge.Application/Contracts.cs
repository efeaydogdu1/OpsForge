using MediatR;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed record AuthResponse(Guid UserId, string Email, string DisplayName, string Role, string AccessToken, string RefreshToken);

public sealed record RegisterCommand(string Email, string Password, string DisplayName) : IRequest<AuthResponse>;
public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;

public sealed record UserGitHubTokenDto(Guid Id, string Name, string TokenLastFour, bool IsDefault, bool IsActive, DateTime CreatedAtUtc, DateTime? LastUsedAtUtc);
public sealed record CreateUserGitHubTokenCommand(string Name, string Token, bool IsDefault) : IRequest<UserGitHubTokenDto>;
public sealed record UpdateUserGitHubTokenCommand(Guid Id, string Name, bool IsDefault, bool IsActive) : IRequest<UserGitHubTokenDto>;
public sealed record DeleteUserGitHubTokenCommand(Guid Id) : IRequest<Unit>;

public sealed record TeamDto(Guid Id, string Name, string? Description, bool IsDeleted, IReadOnlyCollection<TeamMemberDto> Members);
public sealed record TeamMemberDto(Guid UserId, string Email, TeamMemberRole Role);
public sealed record CreateTeamCommand(string Name, string? Description) : IRequest<TeamDto>;
public sealed record UpdateTeamCommand(Guid Id, string Name, string? Description) : IRequest<TeamDto>;
public sealed record DeleteTeamCommand(Guid Id) : IRequest<Unit>;
public sealed record AddTeamMemberCommand(Guid TeamId, Guid UserId, TeamMemberRole Role) : IRequest<Unit>;
public sealed record RemoveTeamMemberCommand(Guid TeamId, Guid UserId) : IRequest<Unit>;

// Services
public sealed record ServiceDto(Guid Id, string Name, string? Description, Guid? OwnerTeamId, string Criticality, string? RepositoryUrl, bool IsDeleted);
public sealed record CreateServiceCommand(string Name, string? Description, Guid? OwnerTeamId, ServiceCriticality Criticality, string? RepositoryUrl) : IRequest<ServiceDto>;
public sealed record UpdateServiceCommand(Guid Id, string Name, string? Description, Guid? OwnerTeamId, ServiceCriticality Criticality, string? RepositoryUrl) : IRequest<ServiceDto>;
public sealed record DeleteServiceCommand(Guid Id) : IRequest<Unit>;

// Environments
public sealed record EnvironmentDto(Guid Id, Guid ServiceId, string Name, string Kind, string? Url, bool IsDeleted);
public sealed record CreateEnvironmentCommand(Guid ServiceId, string Name, EnvironmentKind Kind, string? Url) : IRequest<EnvironmentDto>;
public sealed record UpdateEnvironmentCommand(Guid Id, string Name, EnvironmentKind Kind, string? Url) : IRequest<EnvironmentDto>;
public sealed record DeleteEnvironmentCommand(Guid Id) : IRequest<Unit>;

// Infrastructure
public sealed record InfrastructureAssetDto(Guid Id, string Name, string AssetType, string? Provider, string? ResourceIdentifier, bool IsDeleted, IReadOnlyCollection<Guid> LinkedServiceIds);
public sealed record CreateInfrastructureAssetCommand(string Name, InfrastructureAssetType AssetType, string? Provider, string? ResourceIdentifier) : IRequest<InfrastructureAssetDto>;
public sealed record UpdateInfrastructureAssetCommand(Guid Id, string Name, InfrastructureAssetType AssetType, string? Provider, string? ResourceIdentifier) : IRequest<InfrastructureAssetDto>;
public sealed record DeleteInfrastructureAssetCommand(Guid Id) : IRequest<Unit>;
public sealed record LinkAssetToServiceCommand(Guid AssetId, Guid ServiceId) : IRequest<Unit>;
public sealed record UnlinkAssetFromServiceCommand(Guid AssetId, Guid ServiceId) : IRequest<Unit>;

// Deployments
public sealed record DeploymentDto(Guid Id, Guid ServiceId, Guid EnvironmentId, string Version, string CommitHash, string? ReleaseNotes, DateTime DeploymentDateUtc, Guid DeployedByUserId, bool IsDeleted);
public sealed record CreateDeploymentCommand(Guid ServiceId, Guid EnvironmentId, string Version, string CommitHash, string? ReleaseNotes) : IRequest<DeploymentDto>;
public sealed record UpdateDeploymentCommand(Guid Id, Guid ServiceId, Guid EnvironmentId, string Version, string CommitHash, string? ReleaseNotes) : IRequest<DeploymentDto>;
public sealed record DeleteDeploymentCommand(Guid Id) : IRequest<Unit>;

// Incidents
public sealed record IncidentDto(
    Guid Id,
    string Title,
    string Description,
    string Severity,
    string Status,
    Guid ServiceId,
    Guid? EnvironmentId,
    Guid? DeploymentId,
    Guid ReportedByUserId,
    DateTime OccurredAtUtc,
    DateTime? ResolvedAtUtc,
    bool IsDeleted);
public sealed record CreateIncidentCommand(string Title, string Description, IncidentSeverity Severity, IncidentStatus Status, Guid ServiceId, Guid? EnvironmentId, Guid? DeploymentId) : IRequest<IncidentDto>;
public sealed record UpdateIncidentCommand(Guid Id, string Title, string Description, IncidentSeverity Severity, IncidentStatus Status, Guid ServiceId, Guid? EnvironmentId, Guid? DeploymentId, DateTime? ResolvedAtUtc) : IRequest<IncidentDto>;
public sealed record DeleteIncidentCommand(Guid Id) : IRequest<Unit>;

// Issues
public sealed record IssueDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    string Source,
    Guid ServiceId,
    Guid? EnvironmentId,
    Guid? DeploymentId,
    string? ExternalUrl,
    int? ExternalNumber,
    string? ExternalState,
    DateTime? ExternalCreatedAtUtc,
    DateTime? ExternalUpdatedAtUtc,
    bool IsDeleted);
public sealed record CreateIssueCommand(string Title, string? Description, IssueStatus Status, Guid ServiceId, Guid? EnvironmentId, Guid? DeploymentId, string? ExternalUrl) : IRequest<IssueDto>;
public sealed record UpdateIssueCommand(Guid Id, string Title, string? Description, IssueStatus Status, Guid ServiceId, Guid? EnvironmentId, Guid? DeploymentId, string? ExternalUrl) : IRequest<IssueDto>;
public sealed record DeleteIssueCommand(Guid Id) : IRequest<Unit>;

// Audit
public sealed record AuditLogDto(Guid Id, Guid? UserId, DateTime TimestampUtc, string Action, string EntityType, string EntityId, string? Details);
public interface IAuditService
{
    Task LogAsync(AuditAction action, string entityType, string entityId, Guid? userId, string? details = null, CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    string CreateAccessToken(AppUser user);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
}

public interface ISecretProtector
{
    string Protect(string value);
    string Unprotect(string protectedValue);
}

public interface IAppDbContext
{
    IQueryable<AppUser> Users { get; }
    IQueryable<RefreshToken> RefreshTokens { get; }
    IQueryable<UserGitHubToken> UserGitHubTokens { get; }
    IQueryable<Team> Teams { get; }
    IQueryable<TeamMember> TeamMembers { get; }
    IQueryable<Service> Services { get; }
    IQueryable<ServiceRepositoryLink> ServiceRepositoryLinks { get; }
    IQueryable<RepositorySyncRun> RepositorySyncRuns { get; }
    IQueryable<ServiceEnvironment> ServiceEnvironments { get; }
    IQueryable<InfrastructureAsset> InfrastructureAssets { get; }
    IQueryable<ServiceInfrastructureLink> ServiceInfrastructureLinks { get; }
    IQueryable<Deployment> Deployments { get; }
    IQueryable<Incident> Incidents { get; }
    IQueryable<Issue> Issues { get; }
    IQueryable<AuditLogEntry> AuditLogs { get; }

    EntityEntry<TEntity> Add<TEntity>(TEntity entity) where TEntity : class;
    EntityEntry<TEntity> Update<TEntity>(TEntity entity) where TEntity : class;
    EntityEntry<TEntity> Remove<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
