using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class ServiceHandlers(IAppDbContext db, IAuditService audit)
    : IRequestHandler<CreateServiceCommand, ServiceDto>,
      IRequestHandler<UpdateServiceCommand, ServiceDto>,
      IRequestHandler<DeleteServiceCommand, Unit>
{
    public async Task<ServiceDto> Handle(CreateServiceCommand request, CancellationToken cancellationToken)
    {
        var entity = new Service
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            OwnerTeamId = request.OwnerTeamId,
            Criticality = request.Criticality,
            RepositoryUrl = request.RepositoryUrl?.Trim()
        };

        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Create, nameof(Service), entity.Id.ToString(), null, null, cancellationToken);

        return MapToDto(entity);
    }

    public async Task<ServiceDto> Handle(UpdateServiceCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Services.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.OwnerTeamId = request.OwnerTeamId;
        entity.Criticality = request.Criticality;
        entity.RepositoryUrl = request.RepositoryUrl?.Trim();
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Update, nameof(Service), entity.Id.ToString(), null, null, cancellationToken);

        return MapToDto(entity);
    }

    public async Task<Unit> Handle(DeleteServiceCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Services.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.IsDeleted = true;
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Delete, nameof(Service), entity.Id.ToString(), null, null, cancellationToken);
        return Unit.Value;
    }

    private static ServiceDto MapToDto(Service s) =>
        new(s.Id, s.Name, s.Description, s.OwnerTeamId, s.Criticality.ToString(), s.RepositoryUrl, s.IsDeleted);
}
