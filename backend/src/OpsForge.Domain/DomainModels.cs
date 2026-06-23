namespace OpsForge.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}

public enum UserRole
{
    Admin = 1,
    TeamLead = 2,
    Engineer = 3
}

public enum TeamMemberRole
{
    Lead = 1,
    Member = 2
}

public enum ServiceCriticality
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum EnvironmentKind
{
    Development = 1,
    Test = 2,
    Uat = 3,
    Production = 4
}

public enum InfrastructureAssetType
{
    SqlDatabase = 1,
    Redis = 2,
    AppService = 3,
    VirtualMachine = 4,
    StorageAccount = 5,
    KeyVault = 6
}

public enum AuditAction
{
    Login = 1,
    Create = 2,
    Update = 3,
    Delete = 4,
    Deployment = 5
}

public enum IncidentSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum IncidentStatus
{
    Open = 1,
    Investigating = 2,
    Mitigated = 3,
    Resolved = 4
}

public enum IssueStatus
{
    Open = 1,
    Closed = 2
}

public enum IssueSource
{
    Manual = 1,
    GitHub = 2
}

public sealed class AppUser : AuditableEntity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; } = UserRole.Engineer;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

public sealed class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}

public sealed class UserGitHubToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string EncryptedToken { get; set; }
    public required string TokenLastFour { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAtUtc { get; set; }
}

public sealed class Team : AuditableEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<TeamMember> Members { get; set; } = [];
}

public sealed class TeamMember : Entity
{
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;
}

public sealed class Service : AuditableEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid? OwnerTeamId { get; set; }
    public ServiceCriticality Criticality { get; set; } = ServiceCriticality.Medium;
    public string? RepositoryUrl { get; set; }
    public ICollection<ServiceEnvironment> Environments { get; set; } = [];
    public ICollection<ServiceInfrastructureLink> InfrastructureLinks { get; set; } = [];
}

public sealed class ServiceRepositoryLink : AuditableEntity
{
    public Guid ServiceId { get; set; }
    public required string RepositoryOwner { get; set; }
    public required string RepositoryName { get; set; }
    public required string RepositoryUrl { get; set; }
    public string? DefaultBranch { get; set; }
    public string? Description { get; set; }
    public string? Visibility { get; set; }
    public string? PrimaryLanguage { get; set; }
    public string? LatestCommitSha { get; set; }
    public DateTime? LatestCommitDateUtc { get; set; }
    public string? LatestCommitMessage { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
}

public sealed class RepositorySyncRun : AuditableEntity
{
    public Guid ServiceId { get; set; }
    public Guid ServiceRepositoryLinkId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public required string RepositoryOwner { get; set; }
    public required string RepositoryName { get; set; }
    public required string RepositoryUrl { get; set; }
    public string? DefaultBranch { get; set; }
    public string? Description { get; set; }
    public string? Visibility { get; set; }
    public string? PrimaryLanguage { get; set; }
    public string? LatestCommitSha { get; set; }
    public DateTime? LatestCommitDateUtc { get; set; }
    public string? LatestCommitMessage { get; set; }
}

public sealed class ServiceEnvironment : AuditableEntity
{
    public Guid ServiceId { get; set; }
    public required string Name { get; set; }
    public EnvironmentKind Kind { get; set; }
    public string? Url { get; set; }
}

public sealed class InfrastructureAsset : AuditableEntity
{
    public required string Name { get; set; }
    public InfrastructureAssetType AssetType { get; set; }
    public string? Provider { get; set; }
    public string? ResourceIdentifier { get; set; }
    public ICollection<ServiceInfrastructureLink> LinkedServices { get; set; } = [];
}

public sealed class ServiceInfrastructureLink : Entity
{
    public Guid ServiceId { get; set; }
    public Guid InfrastructureAssetId { get; set; }
}

public sealed class Deployment : AuditableEntity
{
    public Guid ServiceId { get; set; }
    public Guid EnvironmentId { get; set; }
    public required string Version { get; set; }
    public required string CommitHash { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTime DeploymentDateUtc { get; set; }
    public Guid DeployedByUserId { get; set; }
}

public sealed class Incident : AuditableEntity
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public Guid ServiceId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public Guid? DeploymentId { get; set; }
    public Guid ReportedByUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public sealed class Issue : AuditableEntity
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public IssueStatus Status { get; set; } = IssueStatus.Open;
    public IssueSource Source { get; set; } = IssueSource.Manual;
    public Guid ServiceId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public Guid? DeploymentId { get; set; }
    public string? ExternalUrl { get; set; }
    public int? ExternalNumber { get; set; }
    public string? ExternalState { get; set; }
    public DateTime? ExternalCreatedAtUtc { get; set; }
    public DateTime? ExternalUpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class AuditLogEntry : AuditableEntity
{
    public Guid? UserId { get; set; }
    public required AuditAction Action { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public string? DetailsJson { get; set; }
}
