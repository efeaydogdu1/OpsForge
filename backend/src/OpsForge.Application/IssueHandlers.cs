using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class IssueHandlers(IAppDbContext db, IAuditService audit, ICurrentUserContext currentUser)
    : IRequestHandler<CreateIssueCommand, IssueDto>,
      IRequestHandler<UpdateIssueCommand, IssueDto>,
      IRequestHandler<DeleteIssueCommand, Unit>
{
    public async Task<IssueDto> Handle(CreateIssueCommand request, CancellationToken cancellationToken)
    {
        var entity = new Issue
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = request.Status,
            Source = IssueSource.Manual,
            ServiceId = request.ServiceId,
            EnvironmentId = request.EnvironmentId,
            DeploymentId = request.DeploymentId,
            ExternalUrl = string.IsNullOrWhiteSpace(request.ExternalUrl) ? null : request.ExternalUrl.Trim(),
            CreatedByUserId = currentUser.UserId
        };

        await ValidateLinksAsync(entity, cancellationToken);
        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Create, nameof(Issue), entity.Id.ToString(), currentUser.UserId, entity.Title, cancellationToken);

        return Map(entity);
    }

    public async Task<IssueDto> Handle(UpdateIssueCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Issues.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Status = request.Status;
        entity.ServiceId = request.ServiceId;
        entity.EnvironmentId = request.EnvironmentId;
        entity.DeploymentId = request.DeploymentId;
        entity.ExternalUrl = string.IsNullOrWhiteSpace(request.ExternalUrl) ? entity.ExternalUrl : request.ExternalUrl.Trim();

        await ValidateLinksAsync(entity, cancellationToken);
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Update, nameof(Issue), entity.Id.ToString(), currentUser.UserId, entity.Title, cancellationToken);

        return Map(entity);
    }

    public async Task<Unit> Handle(DeleteIssueCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Issues.FirstAsync(x => x.Id == request.Id, cancellationToken);
        entity.IsDeleted = true;
        db.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Delete, nameof(Issue), entity.Id.ToString(), currentUser.UserId, entity.Title, cancellationToken);
        return Unit.Value;
    }

    private async Task ValidateLinksAsync(Issue issue, CancellationToken cancellationToken)
    {
        _ = await db.Services.FirstAsync(x => x.Id == issue.ServiceId, cancellationToken);

        if (issue.EnvironmentId.HasValue)
        {
            var environment = await db.ServiceEnvironments.FirstAsync(x => x.Id == issue.EnvironmentId.Value, cancellationToken);
            if (environment.ServiceId != issue.ServiceId)
            {
                throw new InvalidOperationException("Environment must belong to the selected service.");
            }
        }

        if (issue.DeploymentId.HasValue)
        {
            var deployment = await db.Deployments.FirstAsync(x => x.Id == issue.DeploymentId.Value, cancellationToken);
            if (deployment.ServiceId != issue.ServiceId)
            {
                throw new InvalidOperationException("CI/CD process must belong to the selected service.");
            }

            if (issue.EnvironmentId.HasValue && deployment.EnvironmentId != issue.EnvironmentId.Value)
            {
                throw new InvalidOperationException("CI/CD process must belong to the selected environment.");
            }
        }
    }

    private static IssueDto Map(Issue issue) =>
        new(
            issue.Id,
            issue.Title,
            issue.Description,
            issue.Status.ToString(),
            issue.Source.ToString(),
            issue.ServiceId,
            issue.EnvironmentId,
            issue.DeploymentId,
            issue.ExternalUrl,
            issue.ExternalNumber,
            issue.ExternalState,
            issue.ExternalCreatedAtUtc,
            issue.ExternalUpdatedAtUtc,
            issue.IsDeleted);
}
