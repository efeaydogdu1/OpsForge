using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class TeamHandlers(IAppDbContext db)
    : IRequestHandler<CreateTeamCommand, TeamDto>, IRequestHandler<UpdateTeamCommand, TeamDto>, IRequestHandler<DeleteTeamCommand, Unit>
{
    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var entity = new Team
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim()
        };

        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new TeamDto(entity.Id, entity.Name, entity.Description, entity.IsDeleted, []);
    }

    public async Task<TeamDto> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Teams.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new TeamDto(entity.Id, entity.Name, entity.Description, entity.IsDeleted, []);
    }

    public async Task<Unit> Handle(DeleteTeamCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Teams.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.IsDeleted = true;
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
