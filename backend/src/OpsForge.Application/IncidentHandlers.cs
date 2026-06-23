using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class IncidentHandlers(IAppDbContext db, IAuditService audit, ICurrentUserContext currentUser)
    : IRequestHandler<CreateIncidentCommand, IncidentDto>,
      IRequestHandler<UpdateIncidentCommand, IncidentDto>,
      IRequestHandler<DeleteIncidentCommand, Unit>
{
    public async Task<IncidentDto> Handle(CreateIncidentCommand request, CancellationToken cancellationToken)
    {
        var entity = new Incident
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Severity = request.Severity,
            Status = request.Status,
            ServiceId = request.ServiceId,
            EnvironmentId = request.EnvironmentId,
            DeploymentId = request.DeploymentId,
            ReportedByUserId = currentUser.UserId ?? Guid.Empty,
            OccurredAtUtc = DateTime.UtcNow,
            ResolvedAtUtc = request.Status == IncidentStatus.Resolved ? DateTime.UtcNow : null
        };

        await ValidateLinksAsync(entity, cancellationToken);
        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Create, nameof(Incident), entity.Id.ToString(), currentUser.UserId, entity.Title, cancellationToken);

        return Map(entity);
    }

    public async Task<IncidentDto> Handle(UpdateIncidentCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Incidents.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.Title = request.Title.Trim();
        entity.Description = request.Description.Trim();
        entity.Severity = request.Severity;
        entity.Status = request.Status;
        entity.ServiceId = request.ServiceId;
        entity.EnvironmentId = request.EnvironmentId;
        entity.DeploymentId = request.DeploymentId;
        entity.ResolvedAtUtc = request.Status == IncidentStatus.Resolved
            ? request.ResolvedAtUtc ?? DateTime.UtcNow
            : null;

        await ValidateLinksAsync(entity, cancellationToken);
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Update, nameof(Incident), entity.Id.ToString(), currentUser.UserId, entity.Title, cancellationToken);

        return Map(entity);
    }

    public async Task<Unit> Handle(DeleteIncidentCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Incidents.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.IsDeleted = true;
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Delete, nameof(Incident), entity.Id.ToString(), currentUser.UserId, entity.Title, cancellationToken);
        return Unit.Value;
    }

    private async Task ValidateLinksAsync(Incident incident, CancellationToken cancellationToken)
    {
        _ = await db.Services.FirstAsync(x => x.Id == incident.ServiceId, cancellationToken);

        if (incident.EnvironmentId.HasValue)
        {
            var environment = await db.ServiceEnvironments.FirstAsync(x => x.Id == incident.EnvironmentId.Value, cancellationToken);
            if (environment.ServiceId != incident.ServiceId)
            {
                throw new InvalidOperationException("Environment must belong to the selected service.");
            }
        }

        if (incident.DeploymentId.HasValue)
        {
            var deployment = await db.Deployments.FirstAsync(x => x.Id == incident.DeploymentId.Value, cancellationToken);
            if (deployment.ServiceId != incident.ServiceId)
            {
                throw new InvalidOperationException("CI/CD process must belong to the selected service.");
            }

            if (incident.EnvironmentId.HasValue && deployment.EnvironmentId != incident.EnvironmentId.Value)
            {
                throw new InvalidOperationException("CI/CD process must belong to the selected environment.");
            }
        }
    }

    private static IncidentDto Map(Incident incident) =>
        new(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Severity.ToString(),
            incident.Status.ToString(),
            incident.ServiceId,
            incident.EnvironmentId,
            incident.DeploymentId,
            incident.ReportedByUserId,
            incident.OccurredAtUtc,
            incident.ResolvedAtUtc,
            incident.IsDeleted);
}
