using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class GitHubHandlers(
    IAppDbContext db,
    IGitHubRepositoryUrlParser parser,
    IGitHubApiClient gitHubClient,
    IAuditService audit,
    ICurrentUserContext currentUser,
    ISecretProtector secretProtector)
    : IRequestHandler<PreviewRepositoryMetadataCommand, GitHubRepositoryMetadataDto>,
      IRequestHandler<LinkServiceRepositoryCommand, GitHubRepositoryMetadataDto>,
      IRequestHandler<SyncLinkedRepositoryCommand, RepositorySyncRunDto>,
      IRequestHandler<SyncGitHubAccountCommand, GitHubAccountSyncResultDto>
{
    public async Task<GitHubRepositoryMetadataDto> Handle(PreviewRepositoryMetadataCommand request, CancellationToken cancellationToken)
    {
        if (!parser.TryParse(request.RepositoryUrl, out var owner, out var repositoryName, out var normalizedUrl))
        {
            throw new ArgumentException("Invalid GitHub repository URL.", nameof(request));
        }

        var accessToken = await GetCurrentUserAccessTokenAsync(cancellationToken);
        var metadata = await gitHubClient.GetRepositoryMetadataAsync(owner, repositoryName, normalizedUrl, accessToken, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return MapMetadata(metadata);
    }

    public async Task<GitHubRepositoryMetadataDto> Handle(LinkServiceRepositoryCommand request, CancellationToken cancellationToken)
    {
        var service = await GetAllowedServiceAsync(request.ServiceId, cancellationToken);

        if (!parser.TryParse(request.RepositoryUrl, out var owner, out var repositoryName, out var normalizedUrl))
        {
            throw new ArgumentException("Invalid GitHub repository URL.", nameof(request));
        }

        var accessToken = await GetCurrentUserAccessTokenAsync(cancellationToken);
        var metadata = await gitHubClient.GetRepositoryMetadataAsync(owner, repositoryName, normalizedUrl, accessToken, cancellationToken);
        var link = await db.ServiceRepositoryLinks.FirstOrDefaultAsync(x => x.ServiceId == service.Id, cancellationToken);

        if (link is null)
        {
            link = new ServiceRepositoryLink
            {
                ServiceId = service.Id,
                RepositoryOwner = metadata.Owner,
                RepositoryName = metadata.Name,
                RepositoryUrl = metadata.Url
            };
            db.Add(link);
        }

        ApplyMetadata(link, metadata);
        service.RepositoryUrl = metadata.Url;
        db.Update(service);

        var run = CreateSyncRun(service.Id, link.Id, DateTime.UtcNow, metadata, true, null);
        db.Add(run);

        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(
            AuditAction.Update,
            nameof(ServiceRepositoryLink),
            service.Id.ToString(),
            currentUser.UserId,
            $"Linked GitHub repository {metadata.Owner}/{metadata.Name}",
            cancellationToken);

        return MapMetadata(metadata);
    }

    public async Task<RepositorySyncRunDto> Handle(SyncLinkedRepositoryCommand request, CancellationToken cancellationToken)
    {
        var service = await GetAllowedServiceAsync(request.ServiceId, cancellationToken);
        var link = await db.ServiceRepositoryLinks.FirstOrDefaultAsync(x => x.ServiceId == service.Id, cancellationToken)
            ?? throw new InvalidOperationException("Service does not have a linked GitHub repository.");

        var startedAt = DateTime.UtcNow;
        RepositorySyncRun run;

        try
        {
            var accessToken = await GetCurrentUserAccessTokenAsync(cancellationToken);
            var metadata = await gitHubClient.GetRepositoryMetadataAsync(link.RepositoryOwner, link.RepositoryName, link.RepositoryUrl, accessToken, cancellationToken);
            ApplyMetadata(link, metadata);
            service.RepositoryUrl = metadata.Url;
            db.Update(service);

            run = CreateSyncRun(service.Id, link.Id, startedAt, metadata, true, null);
            db.Add(run);

            await db.SaveChangesAsync(cancellationToken);
            await audit.LogAsync(
                AuditAction.Update,
                "ServiceRepositorySync",
                service.Id.ToString(),
                currentUser.UserId,
                $"Synced GitHub repository {metadata.Owner}/{metadata.Name}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            run = CreateSyncRun(service.Id, link.Id, startedAt, ToMetadata(link), false, ex.Message);
            db.Add(run);

            await db.SaveChangesAsync(cancellationToken);
            await audit.LogAsync(
                AuditAction.Update,
                "ServiceRepositorySync",
                service.Id.ToString(),
                currentUser.UserId,
                $"GitHub sync failed: {ex.Message}",
                cancellationToken);
        }

        return MapSyncRun(run);
    }

    public async Task<GitHubAccountSyncResultDto> Handle(SyncGitHubAccountCommand request, CancellationToken cancellationToken)
    {
        var accessToken = await GetCurrentUserAccessTokenAsync(cancellationToken)
            ?? throw new InvalidOperationException("Add an active GitHub token before syncing your GitHub account.");

        var repositories = await gitHubClient.ListRepositoriesAsync(accessToken, cancellationToken);
        await RemovePreviouslyImportedGitHubInfrastructureAsync(cancellationToken);
        var servicesCreated = 0;
        var servicesUpdated = 0;
        var environmentsImported = 0;
        var deploymentsImported = 0;
        var issuesImported = 0;

        foreach (var repository in repositories)
        {
            var service = await FindServiceForRepositoryAsync(repository, cancellationToken);
            if (service is null)
            {
                service = new Service
                {
                    Name = $"{repository.Owner}/{repository.Name}",
                    Description = repository.Description,
                    Criticality = ServiceCriticality.Medium,
                    RepositoryUrl = repository.Url
                };
                db.Add(service);
                servicesCreated++;
            }
            else
            {
                service.Description = repository.Description;
                service.RepositoryUrl = repository.Url;
                db.Update(service);
                servicesUpdated++;
            }

            var link = await db.ServiceRepositoryLinks.FirstOrDefaultAsync(x => x.ServiceId == service.Id, cancellationToken);
            if (link is null)
            {
                link = new ServiceRepositoryLink
                {
                    ServiceId = service.Id,
                    RepositoryOwner = repository.Owner,
                    RepositoryName = repository.Name,
                    RepositoryUrl = repository.Url
                };
                db.Add(link);
            }

            ApplyMetadata(link, repository);
            db.Add(CreateSyncRun(service.Id, link.Id, DateTime.UtcNow, repository, true, null));

            var branchNames = await gitHubClient.ListRepositoryBranchesAsync(repository.Owner, repository.Name, accessToken, cancellationToken);
            var environmentNames = branchNames
                .Select(branch => new EnvironmentImport(
                    branch,
                    null))
                .ToList();
            var deployments = await gitHubClient.ListRepositoryDeploymentsAsync(repository.Owner, repository.Name, accessToken, cancellationToken);
            var workflowRuns = await gitHubClient.ListRepositoryWorkflowRunsAsync(repository.Owner, repository.Name, repository.DefaultBranch ?? "main", accessToken, cancellationToken);
            var ciCdRuns = deployments.Concat(workflowRuns).ToList();
            environmentNames.AddRange(ciCdRuns.Select(x => new EnvironmentImport(x.Environment, null)));
            environmentNames.AddRange((await gitHubClient.ListRepositoryEnvironmentsAsync(repository.Owner, repository.Name, accessToken, cancellationToken))
                .Select(name => new EnvironmentImport(name, null)));

            foreach (var environment in environmentNames
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                var created = await EnsureEnvironmentAsync(service.Id, environment.Name, environment.Url, cancellationToken);
                if (created)
                {
                    environmentsImported++;
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            foreach (var deployment in ciCdRuns)
            {
                var environment = await EnsureEnvironmentEntityAsync(service.Id, deployment.Environment, repository.Url, cancellationToken);
                var exists = await db.Deployments.AnyAsync(
                    x => x.ServiceId == service.Id
                        && x.EnvironmentId == environment.Id
                        && x.CommitHash == deployment.Sha
                        && x.Version == deployment.Ref,
                    cancellationToken);

                if (exists)
                {
                    continue;
                }

                db.Add(new Deployment
                {
                    ServiceId = service.Id,
                    EnvironmentId = environment.Id,
                    Version = deployment.Ref,
                    CommitHash = deployment.Sha,
                    ReleaseNotes = deployment.Description ?? $"GitHub deployment by {deployment.CreatorLogin ?? "unknown"}",
                    DeploymentDateUtc = deployment.CreatedAtUtc,
                    DeployedByUserId = currentUser.UserId ?? Guid.Empty
                });
                deploymentsImported++;
            }

            var issues = await gitHubClient.ListRepositoryIssuesAsync(repository.Owner, repository.Name, accessToken, cancellationToken);
            foreach (var issue in issues)
            {
                await UpsertGitHubIssueAsync(service.Id, issue, cancellationToken);
                issuesImported++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(
            AuditAction.Update,
            "GitHubAccountSync",
            currentUser.UserId?.ToString() ?? "unknown",
            currentUser.UserId,
            $"Imported {repositories.Count} GitHub repositories",
            cancellationToken);

        return new GitHubAccountSyncResultDto(repositories.Count, servicesCreated, servicesUpdated, environmentsImported, deploymentsImported, issuesImported);
    }

    private async Task<Service> GetAllowedServiceAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        var service = await db.Services.FirstOrDefaultAsync(x => x.Id == serviceId, cancellationToken)
            ?? throw new KeyNotFoundException("Service not found.");

        var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isAdmin)
        {
            return service;
        }

        if (!service.OwnerTeamId.HasValue)
        {
            return service;
        }

        if (!currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Current user cannot access this service.");
        }

        var hasMembership = await db.TeamMembers.AnyAsync(
            x => x.UserId == currentUser.UserId.Value && x.TeamId == service.OwnerTeamId.Value,
            cancellationToken);

        if (!hasMembership)
        {
            throw new UnauthorizedAccessException("Current user cannot access this service.");
        }

        return service;
    }

    private async Task<string?> GetCurrentUserAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return null;
        }

        var token = await db.UserGitHubTokens
            .Where(x => x.UserId == currentUser.UserId.Value && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null)
        {
            return null;
        }

        token.LastUsedAtUtc = DateTime.UtcNow;
        db.Update(token);
        return secretProtector.Unprotect(token.EncryptedToken);
    }

    private async Task<Service?> FindServiceForRepositoryAsync(GitHubRepositoryMetadata repository, CancellationToken cancellationToken)
    {
        var existingLink = await db.ServiceRepositoryLinks
            .FirstOrDefaultAsync(x => x.RepositoryOwner == repository.Owner && x.RepositoryName == repository.Name, cancellationToken);

        if (existingLink is not null)
        {
            return await db.Services.FirstOrDefaultAsync(x => x.Id == existingLink.ServiceId, cancellationToken);
        }

        return await db.Services.FirstOrDefaultAsync(x => x.RepositoryUrl == repository.Url || x.Name == $"{repository.Owner}/{repository.Name}", cancellationToken);
    }

    private async Task RemovePreviouslyImportedGitHubInfrastructureAsync(CancellationToken cancellationToken)
    {
        var assets = await db.InfrastructureAssets
            .Where(x => x.Provider == "GitHub")
            .ToListAsync(cancellationToken);

        if (assets.Count == 0)
        {
            return;
        }

        var assetIds = assets.Select(x => x.Id).ToList();
        var links = await db.ServiceInfrastructureLinks
            .Where(x => assetIds.Contains(x.InfrastructureAssetId))
            .ToListAsync(cancellationToken);

        foreach (var link in links)
        {
            db.Remove(link);
        }

        foreach (var asset in assets)
        {
            asset.IsDeleted = true;
            db.Update(asset);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> EnsureEnvironmentAsync(Guid serviceId, string name, string? url, CancellationToken cancellationToken)
    {
        var existing = await db.ServiceEnvironments.FirstOrDefaultAsync(
            x => x.ServiceId == serviceId && x.Name == name.Trim(),
            cancellationToken);

        if (existing is not null)
        {
            return false;
        }

        db.Add(new ServiceEnvironment
        {
            ServiceId = serviceId,
            Name = name.Trim(),
            Kind = GuessEnvironmentKind(name),
            Url = url
        });

        return true;
    }

    private async Task<ServiceEnvironment> EnsureEnvironmentEntityAsync(Guid serviceId, string name, string? url, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();
        var existing = await db.ServiceEnvironments.FirstOrDefaultAsync(
            x => x.ServiceId == serviceId && x.Name == normalizedName,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var entity = new ServiceEnvironment
        {
            ServiceId = serviceId,
            Name = normalizedName,
            Kind = GuessEnvironmentKind(normalizedName),
            Url = url
        };

        db.Add(entity);
        return entity;
    }

    private async Task UpsertGitHubIssueAsync(Guid serviceId, GitHubRepositoryIssue importedIssue, CancellationToken cancellationToken)
    {
        var issue = await db.Issues.FirstOrDefaultAsync(
            x => x.ServiceId == serviceId
                && x.Source == IssueSource.GitHub
                && x.ExternalNumber == importedIssue.Number,
            cancellationToken);

        if (issue is null)
        {
            issue = new Issue
            {
                ServiceId = serviceId,
                Title = importedIssue.Title,
                Source = IssueSource.GitHub,
                ExternalNumber = importedIssue.Number,
                CreatedByUserId = currentUser.UserId
            };
            db.Add(issue);
        }

        issue.Title = importedIssue.Title;
        issue.Description = importedIssue.Body;
        issue.Status = string.Equals(importedIssue.State, "closed", StringComparison.OrdinalIgnoreCase)
            ? IssueStatus.Closed
            : IssueStatus.Open;
        issue.ExternalUrl = importedIssue.Url;
        issue.ExternalState = importedIssue.State;
        issue.ExternalCreatedAtUtc = importedIssue.CreatedAtUtc;
        issue.ExternalUpdatedAtUtc = importedIssue.UpdatedAtUtc;
    }

    private static EnvironmentKind GuessEnvironmentKind(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        if (normalized.Contains("prod"))
        {
            return EnvironmentKind.Production;
        }

        if (normalized.Contains("uat") || normalized.Contains("stage") || normalized.Contains("staging"))
        {
            return EnvironmentKind.Uat;
        }

        if (normalized.Contains("test") || normalized.Contains("qa"))
        {
            return EnvironmentKind.Test;
        }

        return EnvironmentKind.Development;
    }

    private static void ApplyMetadata(ServiceRepositoryLink link, GitHubRepositoryMetadata metadata)
    {
        link.RepositoryOwner = metadata.Owner;
        link.RepositoryName = metadata.Name;
        link.RepositoryUrl = metadata.Url;
        link.DefaultBranch = metadata.DefaultBranch;
        link.Description = metadata.Description;
        link.Visibility = metadata.Visibility;
        link.PrimaryLanguage = metadata.PrimaryLanguage;
        link.LatestCommitSha = metadata.LatestCommitSha;
        link.LatestCommitDateUtc = metadata.LatestCommitDateUtc;
        link.LatestCommitMessage = metadata.LatestCommitMessage;
        link.LastSyncedAtUtc = DateTime.UtcNow;
    }

    private static RepositorySyncRun CreateSyncRun(
        Guid serviceId,
        Guid linkId,
        DateTime startedAt,
        GitHubRepositoryMetadata metadata,
        bool isSuccess,
        string? errorMessage) =>
        new()
        {
            ServiceId = serviceId,
            ServiceRepositoryLinkId = linkId,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTime.UtcNow,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            RepositoryOwner = metadata.Owner,
            RepositoryName = metadata.Name,
            RepositoryUrl = metadata.Url,
            DefaultBranch = metadata.DefaultBranch,
            Description = metadata.Description,
            Visibility = metadata.Visibility,
            PrimaryLanguage = metadata.PrimaryLanguage,
            LatestCommitSha = metadata.LatestCommitSha,
            LatestCommitDateUtc = metadata.LatestCommitDateUtc,
            LatestCommitMessage = metadata.LatestCommitMessage
        };

    private static GitHubRepositoryMetadata ToMetadata(ServiceRepositoryLink link) =>
        new(
            link.RepositoryOwner,
            link.RepositoryName,
            link.RepositoryUrl,
            link.DefaultBranch,
            link.Description,
            link.Visibility,
            link.PrimaryLanguage,
            link.LatestCommitSha,
            link.LatestCommitDateUtc,
            link.LatestCommitMessage);

    private static GitHubRepositoryMetadataDto MapMetadata(GitHubRepositoryMetadata metadata) =>
        new(
            metadata.Owner,
            metadata.Name,
            metadata.Url,
            metadata.DefaultBranch,
            metadata.Description,
            metadata.Visibility,
            metadata.PrimaryLanguage,
            metadata.LatestCommitSha,
            metadata.LatestCommitDateUtc,
            metadata.LatestCommitMessage);

    private static RepositorySyncRunDto MapSyncRun(RepositorySyncRun run) =>
        new(
            run.Id,
            run.ServiceId,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.IsSuccess,
            run.ErrorMessage,
            run.RepositoryOwner,
            run.RepositoryName,
            run.RepositoryUrl,
            run.DefaultBranch,
            run.Description,
            run.Visibility,
            run.PrimaryLanguage,
            run.LatestCommitSha,
            run.LatestCommitDateUtc,
            run.LatestCommitMessage);

    private sealed record EnvironmentImport(string Name, string? Url);
}
