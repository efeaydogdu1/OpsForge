using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Application;
using OpsForge.Infrastructure.Persistence;

namespace OpsForge.Worker;

public sealed class GitHubAccountSyncJob(
    OpsForgeDbContext db,
    WorkerCurrentUserContext currentUser,
    ISender sender,
    ILogger<GitHubAccountSyncJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.UserGitHubTokensSet
            .Where(x => x.IsActive)
            .Select(x => x.UserId)
            .Distinct()
            .Join(db.UsersSet, tokenUserId => tokenUserId, user => user.Id, (_, user) => user)
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            logger.LogInformation("GitHub sync skipped because there are no active GitHub tokens.");
            return;
        }

        foreach (var user in users)
        {
            currentUser.SetUser(user);

            try
            {
                var result = await sender.Send(new SyncGitHubAccountCommand(), cancellationToken);
                logger.LogInformation(
                    "Synced GitHub for {Email}: {Repos} repos, {ServicesCreated} services created, {Environments} environments, {Deployments} CI/CD processes, {Issues} issues.",
                    user.Email,
                    result.RepositoriesImported,
                    result.ServicesCreated,
                    result.EnvironmentsImported,
                    result.DeploymentsImported,
                    result.IssuesImported);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GitHub sync failed for {Email}.", user.Email);
                throw;
            }
        }
    }
}
