using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class InfrastructureHandlers(IAppDbContext db, IAuditService audit)
    : IRequestHandler<CreateInfrastructureAssetCommand, InfrastructureAssetDto>,
      IRequestHandler<UpdateInfrastructureAssetCommand, InfrastructureAssetDto>,
      IRequestHandler<DeleteInfrastructureAssetCommand, Unit>,
      IRequestHandler<LinkAssetToServiceCommand, Unit>,
      IRequestHandler<UnlinkAssetFromServiceCommand, Unit>
{
    public async Task<InfrastructureAssetDto> Handle(CreateInfrastructureAssetCommand request, CancellationToken cancellationToken)
    {
        var entity = new InfrastructureAsset
        {
            Name = request.Name.Trim(),
            AssetType = request.AssetType,
            Provider = request.Provider?.Trim(),
            ResourceIdentifier = request.ResourceIdentifier?.Trim()
        };

        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Create, nameof(InfrastructureAsset), entity.Id.ToString(), null, null, cancellationToken);

        return new InfrastructureAssetDto(entity.Id, entity.Name, entity.AssetType.ToString(), entity.Provider, entity.ResourceIdentifier, entity.IsDeleted, []);
    }

    public async Task<InfrastructureAssetDto> Handle(UpdateInfrastructureAssetCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.InfrastructureAssets.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.Name = request.Name.Trim();
        entity.AssetType = request.AssetType;
        entity.Provider = request.Provider?.Trim();
        entity.ResourceIdentifier = request.ResourceIdentifier?.Trim();
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Update, nameof(InfrastructureAsset), entity.Id.ToString(), null, null, cancellationToken);

        var linkedIds = await db.ServiceInfrastructureLinks
            .Where(x => x.InfrastructureAssetId == entity.Id)
            .Select(x => x.ServiceId)
            .ToListAsync(cancellationToken);

        return new InfrastructureAssetDto(entity.Id, entity.Name, entity.AssetType.ToString(), entity.Provider, entity.ResourceIdentifier, entity.IsDeleted, linkedIds);
    }

    public async Task<Unit> Handle(DeleteInfrastructureAssetCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.InfrastructureAssets.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.IsDeleted = true;
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Delete, nameof(InfrastructureAsset), entity.Id.ToString(), null, null, cancellationToken);
        return Unit.Value;
    }

    public async Task<Unit> Handle(LinkAssetToServiceCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.ServiceInfrastructureLinks
            .FirstOrDefaultAsync(x => x.InfrastructureAssetId == request.AssetId && x.ServiceId == request.ServiceId, cancellationToken);

        if (existing is null)
        {
            db.Add(new ServiceInfrastructureLink { InfrastructureAssetId = request.AssetId, ServiceId = request.ServiceId });
            await db.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }

    public async Task<Unit> Handle(UnlinkAssetFromServiceCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.ServiceInfrastructureLinks
            .FirstOrDefaultAsync(x => x.InfrastructureAssetId == request.AssetId && x.ServiceId == request.ServiceId, cancellationToken);

        if (existing is not null)
        {
            db.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}
