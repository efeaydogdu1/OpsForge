using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class TeamMemberHandlers(IAppDbContext db)
    : IRequestHandler<AddTeamMemberCommand, Unit>,
      IRequestHandler<RemoveTeamMemberCommand, Unit>
{
    public async Task<Unit> Handle(AddTeamMemberCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.TeamMembers
            .FirstOrDefaultAsync(x => x.TeamId == request.TeamId && x.UserId == request.UserId, cancellationToken);

        if (existing is null)
        {
            db.Add(new TeamMember { TeamId = request.TeamId, UserId = request.UserId, Role = request.Role });
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            existing.Role = request.Role;
            db.Update(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }

    public async Task<Unit> Handle(RemoveTeamMemberCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.TeamMembers
            .FirstOrDefaultAsync(x => x.TeamId == request.TeamId && x.UserId == request.UserId, cancellationToken);

        if (existing is not null)
        {
            db.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}
