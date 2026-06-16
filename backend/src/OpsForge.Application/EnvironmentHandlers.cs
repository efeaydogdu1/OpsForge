using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class EnvironmentHandlers(IAppDbContext db, IAuditService audit)
    : IRequestHandler<CreateEnvironmentCommand, EnvironmentDto>,
      IRequestHandler<UpdateEnvironmentCommand, EnvironmentDto>,
      IRequestHandler<DeleteEnvironmentCommand, Unit>
{
    public async Task<EnvironmentDto> Handle(CreateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var entity = new ServiceEnvironment
        {
            ServiceId = request.ServiceId,
            Name = request.Name.Trim(),
            Kind = request.Kind,
            Url = request.Url?.Trim()
        };

        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Create, nameof(ServiceEnvironment), entity.Id.ToString(), null, null, cancellationToken);

        return MapToDto(entity);
    }

    public async Task<EnvironmentDto> Handle(UpdateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.ServiceEnvironments.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.Name = request.Name.Trim();
        entity.Kind = request.Kind;
        entity.Url = request.Url?.Trim();
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Update, nameof(ServiceEnvironment), entity.Id.ToString(), null, null, cancellationToken);

        return MapToDto(entity);
    }

    public async Task<Unit> Handle(DeleteEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.ServiceEnvironments.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.IsDeleted = true;
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Delete, nameof(ServiceEnvironment), entity.Id.ToString(), null, null, cancellationToken);
        return Unit.Value;
    }

    private static EnvironmentDto MapToDto(ServiceEnvironment e) =>
        new(e.Id, e.ServiceId, e.Name, e.Kind.ToString(), e.Url, e.IsDeleted);
}
