using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class DeploymentHandlers(IAppDbContext db, IAuditService audit, ICurrentUserContext currentUser)
    : IRequestHandler<CreateDeploymentCommand, DeploymentDto>,
      IRequestHandler<UpdateDeploymentCommand, DeploymentDto>,
      IRequestHandler<DeleteDeploymentCommand, Unit>
{
    public async Task<DeploymentDto> Handle(CreateDeploymentCommand request, CancellationToken cancellationToken)
    {
        var entity = new Deployment
        {
            ServiceId = request.ServiceId,
            EnvironmentId = request.EnvironmentId,
            Version = request.Version.Trim(),
            CommitHash = request.CommitHash.Trim(),
            ReleaseNotes = request.ReleaseNotes?.Trim(),
            DeploymentDateUtc = DateTime.UtcNow,
            DeployedByUserId = currentUser.UserId ?? Guid.Empty
        };

        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Deployment, nameof(Deployment), entity.Id.ToString(), currentUser.UserId,
            $"v{entity.Version} to env {entity.EnvironmentId}", cancellationToken);

        return MapToDto(entity);
    }

    public async Task<DeploymentDto> Handle(UpdateDeploymentCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Deployments.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.ServiceId = request.ServiceId;
        entity.EnvironmentId = request.EnvironmentId;
        entity.Version = request.Version.Trim();
        entity.CommitHash = request.CommitHash.Trim();
        entity.ReleaseNotes = request.ReleaseNotes?.Trim();
        db.Update(entity);

        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Update, nameof(Deployment), entity.Id.ToString(), currentUser.UserId,
            $"Updated CI/CD process {entity.Version}", cancellationToken);

        return MapToDto(entity);
    }

    public async Task<Unit> Handle(DeleteDeploymentCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Deployments.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.IsDeleted = true;
        db.Update(entity);

        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Delete, nameof(Deployment), entity.Id.ToString(), currentUser.UserId,
            "Deleted CI/CD process", cancellationToken);

        return Unit.Value;
    }

    private static DeploymentDto MapToDto(Deployment d) =>
        new(d.Id, d.ServiceId, d.EnvironmentId, d.Version, d.CommitHash, d.ReleaseNotes, d.DeploymentDateUtc, d.DeployedByUserId, d.IsDeleted);
}

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
}
