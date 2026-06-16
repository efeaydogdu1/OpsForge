using OpsForge.Application;
using OpsForge.Domain;

namespace OpsForge.Infrastructure.Services;

public sealed class AuditService(IAppDbContext db) : IAuditService
{
    public async Task LogAsync(AuditAction action, string entityType, string entityId, Guid? userId, string? details = null, CancellationToken cancellationToken = default)
    {
        var entry = new AuditLogEntry
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = details
        };

        db.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }
}
