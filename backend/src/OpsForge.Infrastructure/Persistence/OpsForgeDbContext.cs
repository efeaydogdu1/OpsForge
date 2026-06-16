using Microsoft.EntityFrameworkCore;
using OpsForge.Application;
using OpsForge.Domain;

namespace OpsForge.Infrastructure.Persistence;

public sealed class OpsForgeDbContext(DbContextOptions<OpsForgeDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<AppUser> UsersSet => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokensSet => Set<RefreshToken>();
    public DbSet<Team> TeamsSet => Set<Team>();
    public DbSet<TeamMember> TeamMembersSet => Set<TeamMember>();
    public DbSet<Service> ServicesSet => Set<Service>();
    public DbSet<ServiceEnvironment> ServiceEnvironmentsSet => Set<ServiceEnvironment>();
    public DbSet<ServiceRepositoryLink> ServiceRepositoryLinksSet => Set<ServiceRepositoryLink>();
    public DbSet<RepositorySyncRun> RepositorySyncRunsSet => Set<RepositorySyncRun>();
    public DbSet<InfrastructureAsset> InfrastructureAssetsSet => Set<InfrastructureAsset>();
    public DbSet<ServiceInfrastructureLink> ServiceInfrastructureLinksSet => Set<ServiceInfrastructureLink>();
    public DbSet<Deployment> DeploymentsSet => Set<Deployment>();
    public DbSet<AuditLogEntry> AuditLogsSet => Set<AuditLogEntry>();

    IQueryable<AppUser> IAppDbContext.Users => UsersSet;
    IQueryable<RefreshToken> IAppDbContext.RefreshTokens => RefreshTokensSet;
    IQueryable<Team> IAppDbContext.Teams => TeamsSet;
    IQueryable<TeamMember> IAppDbContext.TeamMembers => TeamMembersSet;
    IQueryable<Service> IAppDbContext.Services => ServicesSet;
    IQueryable<ServiceEnvironment> IAppDbContext.ServiceEnvironments => ServiceEnvironmentsSet;
    IQueryable<InfrastructureAsset> IAppDbContext.InfrastructureAssets => InfrastructureAssetsSet;
    IQueryable<ServiceInfrastructureLink> IAppDbContext.ServiceInfrastructureLinks => ServiceInfrastructureLinksSet;
    IQueryable<Deployment> IAppDbContext.Deployments => DeploymentsSet;
    IQueryable<AuditLogEntry> IAppDbContext.AuditLogs => AuditLogsSet;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(builder =>
        {
            builder.HasIndex(x => x.Email).IsUnique();
            builder.Property(x => x.Role).HasConversion<int>();
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<RefreshToken>(builder =>
        {
            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasOne<AppUser>()
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Team>(builder =>
        {
            builder.HasIndex(x => x.Name).IsUnique();
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Service>(builder =>
        {
            builder.HasIndex(x => x.Name).IsUnique();
            builder.Property(x => x.Criticality).HasConversion<int>();
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<ServiceEnvironment>(builder =>
        {
            builder.Property(x => x.Kind).HasConversion<int>();
            builder.HasIndex(x => new { x.ServiceId, x.Name }).IsUnique();
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<ServiceRepositoryLink>(builder =>
        {
            builder.HasIndex(x => x.ServiceId).IsUnique();
            builder.HasIndex(x => new { x.RepositoryOwner, x.RepositoryName });
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<RepositorySyncRun>(builder =>
        {
            builder.HasIndex(x => new { x.ServiceId, x.StartedAtUtc });
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<InfrastructureAsset>(builder =>
        {
            builder.Property(x => x.AssetType).HasConversion<int>();
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<ServiceInfrastructureLink>(builder =>
        {
            builder.HasIndex(x => new { x.ServiceId, x.InfrastructureAssetId }).IsUnique();
        });

        modelBuilder.Entity<AuditLogEntry>(builder =>
        {
            builder.Property(x => x.Action).HasConversion<int>();
            builder.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
