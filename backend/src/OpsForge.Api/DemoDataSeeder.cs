using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpsForge.Application;
using OpsForge.Domain;
using OpsForge.Infrastructure.Persistence;

namespace OpsForge.Api;

internal static class DemoDataSeeder
{
    public static async Task SeedAsync(
        OpsForgeDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Seed:CreateDemoData"))
        {
            return;
        }

        var changed = false;

        var existingUsers = await db.UsersSet.ToListAsync(cancellationToken);
        var usersByEmail = existingUsers.ToDictionary(x => x.Email, StringComparer.OrdinalIgnoreCase);

        AppUser EnsureUser(string email, string displayName, UserRole role, string password)
        {
            if (usersByEmail.TryGetValue(email, out var existingUser))
            {
                return existingUser;
            }

            var user = new AppUser
            {
                Email = email,
                DisplayName = displayName,
                Role = role,
                PasswordHash = passwordHasher.Hash(password)
            };

            db.UsersSet.Add(user);
            usersByEmail[email] = user;
            changed = true;
            return user;
        }

        var userSeeds = new (string Email, string DisplayName, UserRole Role, string Password)[]
        {
            ("demo@opsforge.local", "Demo Admin", UserRole.Admin, "Demo123!"),
            ("platform.admin@opsforge.local", "Platform Admin", UserRole.Admin, "Demo123!"),
            ("sre.lead@opsforge.local", "SRE Lead", UserRole.TeamLead, "Demo123!"),
            ("backend.lead@opsforge.local", "Backend Lead", UserRole.TeamLead, "Demo123!"),
            ("frontend.lead@opsforge.local", "Frontend Lead", UserRole.TeamLead, "Demo123!"),
            ("data.lead@opsforge.local", "Data Lead", UserRole.TeamLead, "Demo123!"),
            ("alice@opsforge.local", "Alice Engineer", UserRole.Engineer, "Demo123!"),
            ("bob@opsforge.local", "Bob Engineer", UserRole.Engineer, "Demo123!"),
            ("carol@opsforge.local", "Carol Engineer", UserRole.Engineer, "Demo123!"),
            ("dave@opsforge.local", "Dave Engineer", UserRole.Engineer, "Demo123!"),
            ("erin@opsforge.local", "Erin Engineer", UserRole.Engineer, "Demo123!"),
            ("frank@opsforge.local", "Frank Engineer", UserRole.Engineer, "Demo123!")
        };

        foreach (var seed in userSeeds)
        {
            EnsureUser(seed.Email, seed.DisplayName, seed.Role, seed.Password);
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var users = await db.UsersSet.ToListAsync(cancellationToken);
        var usersByName = users.ToDictionary(x => x.DisplayName, StringComparer.OrdinalIgnoreCase);

        var refreshTokens = await db.RefreshTokensSet.ToListAsync(cancellationToken);
        var hasTokenByUser = refreshTokens
            .Where(x => x.RevokedAtUtc is null && x.ExpiresAtUtc > DateTime.UtcNow)
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => true);

        foreach (var user in users)
        {
            if (hasTokenByUser.ContainsKey(user.Id))
            {
                continue;
            }

            var refreshToken = tokenService.CreateRefreshToken();
            db.RefreshTokensSet.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = tokenService.HashRefreshToken(refreshToken),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
            });
            changed = true;
        }

        var teams = await db.TeamsSet.ToListAsync(cancellationToken);
        var teamsByName = teams.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        Team EnsureTeam(string name, string description)
        {
            if (teamsByName.TryGetValue(name, out var existingTeam))
            {
                return existingTeam;
            }

            var team = new Team
            {
                Name = name,
                Description = description
            };

            db.TeamsSet.Add(team);
            teamsByName[name] = team;
            changed = true;
            return team;
        }

        var teamPlatform = EnsureTeam("Platform", "Platform engineering and internal developer tooling.");
        var teamSre = EnsureTeam("SRE", "Site reliability operations and incident response.");
        var teamPayments = EnsureTeam("Payments", "Payments services and billing workflows.");
        var teamData = EnsureTeam("Data", "Data pipelines, analytics and platform reporting.");

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        teams = await db.TeamsSet.ToListAsync(cancellationToken);
        teamsByName = teams.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var members = await db.TeamMembersSet.ToListAsync(cancellationToken);

        void EnsureMember(string teamName, string userDisplayName, TeamMemberRole role)
        {
            if (!teamsByName.TryGetValue(teamName, out var team) || !usersByName.TryGetValue(userDisplayName, out var user))
            {
                return;
            }

            var exists = members.Any(m => m.TeamId == team.Id && m.UserId == user.Id);
            if (exists)
            {
                return;
            }

            var member = new TeamMember
            {
                TeamId = team.Id,
                UserId = user.Id,
                Role = role
            };

            db.TeamMembersSet.Add(member);
            members.Add(member);
            changed = true;
        }

        EnsureMember("Platform", "Platform Admin", TeamMemberRole.Lead);
        EnsureMember("Platform", "Alice Engineer", TeamMemberRole.Member);
        EnsureMember("Platform", "Bob Engineer", TeamMemberRole.Member);
        EnsureMember("SRE", "SRE Lead", TeamMemberRole.Lead);
        EnsureMember("SRE", "Carol Engineer", TeamMemberRole.Member);
        EnsureMember("SRE", "Dave Engineer", TeamMemberRole.Member);
        EnsureMember("Payments", "Backend Lead", TeamMemberRole.Lead);
        EnsureMember("Payments", "Erin Engineer", TeamMemberRole.Member);
        EnsureMember("Payments", "Frank Engineer", TeamMemberRole.Member);
        EnsureMember("Data", "Data Lead", TeamMemberRole.Lead);
        EnsureMember("Data", "Bob Engineer", TeamMemberRole.Member);
        EnsureMember("Data", "Carol Engineer", TeamMemberRole.Member);

        var services = await db.ServicesSet.ToListAsync(cancellationToken);
        var servicesByName = services.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        Service EnsureService(string name, string? description, string teamName, ServiceCriticality criticality, string? repositoryUrl)
        {
            if (servicesByName.TryGetValue(name, out var existingService))
            {
                return existingService;
            }

            var ownerTeamId = teamsByName.TryGetValue(teamName, out var team) ? team.Id : (Guid?)null;
            var service = new Service
            {
                Name = name,
                Description = description,
                OwnerTeamId = ownerTeamId,
                Criticality = criticality,
                RepositoryUrl = repositoryUrl
            };

            db.ServicesSet.Add(service);
            servicesByName[name] = service;
            changed = true;
            return service;
        }

        EnsureService("opsforge-api", "Main public API for OpsForge workflows.", "Platform", ServiceCriticality.Critical, "https://github.com/example/opsforge-api");
        EnsureService("opsforge-web", "Next.js frontend shell for IDP features.", "Platform", ServiceCriticality.High, "https://github.com/example/opsforge-web");
        EnsureService("deployment-orchestrator", "Release orchestration and deployment tracking.", "SRE", ServiceCriticality.Critical, "https://github.com/example/deployment-orchestrator");
        EnsureService("alerts-gateway", "Notification and on-call routing gateway.", "SRE", ServiceCriticality.High, "https://github.com/example/alerts-gateway");
        EnsureService("billing-core", "Core billing and pricing calculations.", "Payments", ServiceCriticality.Critical, "https://github.com/example/billing-core");
        EnsureService("invoice-worker", "Invoice generation background processor.", "Payments", ServiceCriticality.High, "https://github.com/example/invoice-worker");
        EnsureService("analytics-api", "Analytics query API and data products.", "Data", ServiceCriticality.Medium, "https://github.com/example/analytics-api");
        EnsureService("etl-pipeline", "Nightly ETL and warehouse ingestion.", "Data", ServiceCriticality.High, "https://github.com/example/etl-pipeline");

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        services = await db.ServicesSet.ToListAsync(cancellationToken);
        servicesByName = services.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var environments = await db.ServiceEnvironmentsSet.ToListAsync(cancellationToken);

        void EnsureEnvironment(string serviceName, string envName, EnvironmentKind kind, string url)
        {
            if (!servicesByName.TryGetValue(serviceName, out var service))
            {
                return;
            }

            var exists = environments.Any(e => e.ServiceId == service.Id && e.Name.Equals(envName, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                return;
            }

            var env = new ServiceEnvironment
            {
                ServiceId = service.Id,
                Name = envName,
                Kind = kind,
                Url = url
            };

            db.ServiceEnvironmentsSet.Add(env);
            environments.Add(env);
            changed = true;
        }

        foreach (var service in services)
        {
            EnsureEnvironment(service.Name, "dev", EnvironmentKind.Development, $"https://{service.Name}.dev.opsforge.local");
            EnsureEnvironment(service.Name, "test", EnvironmentKind.Test, $"https://{service.Name}.test.opsforge.local");
            EnsureEnvironment(service.Name, "uat", EnvironmentKind.Uat, $"https://{service.Name}.uat.opsforge.local");
            EnsureEnvironment(service.Name, "prod", EnvironmentKind.Production, $"https://{service.Name}.opsforge.local");
        }

        var assets = await db.InfrastructureAssetsSet.ToListAsync(cancellationToken);
        var assetsByName = assets.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        InfrastructureAsset EnsureAsset(string name, InfrastructureAssetType type, string provider, string identifier)
        {
            if (assetsByName.TryGetValue(name, out var existingAsset))
            {
                return existingAsset;
            }

            var asset = new InfrastructureAsset
            {
                Name = name,
                AssetType = type,
                Provider = provider,
                ResourceIdentifier = identifier
            };

            db.InfrastructureAssetsSet.Add(asset);
            assetsByName[name] = asset;
            changed = true;
            return asset;
        }

        EnsureAsset("pg-main", InfrastructureAssetType.SqlDatabase, "Azure", "/subscriptions/dev/resourceGroups/opsforge/providers/Microsoft.DBforPostgreSQL/flexibleServers/pg-main");
        EnsureAsset("redis-cache", InfrastructureAssetType.Redis, "Azure", "/subscriptions/dev/resourceGroups/opsforge/providers/Microsoft.Cache/Redis/redis-cache");
        EnsureAsset("kv-shared", InfrastructureAssetType.KeyVault, "Azure", "/subscriptions/dev/resourceGroups/opsforge/providers/Microsoft.KeyVault/vaults/kv-shared");
        EnsureAsset("storage-artifacts", InfrastructureAssetType.StorageAccount, "Azure", "/subscriptions/dev/resourceGroups/opsforge/providers/Microsoft.Storage/storageAccounts/stopsforgeartifacts");
        EnsureAsset("app-opsforge-api", InfrastructureAssetType.AppService, "Azure", "/subscriptions/dev/resourceGroups/opsforge/providers/Microsoft.Web/sites/app-opsforge-api");
        EnsureAsset("app-opsforge-web", InfrastructureAssetType.AppService, "Azure", "/subscriptions/dev/resourceGroups/opsforge/providers/Microsoft.Web/sites/app-opsforge-web");
        EnsureAsset("vm-runner-01", InfrastructureAssetType.VirtualMachine, "Azure", "/subscriptions/dev/resourceGroups/opsforge/providers/Microsoft.Compute/virtualMachines/vm-runner-01");
        EnsureAsset("vm-runner-02", InfrastructureAssetType.VirtualMachine, "Azure", "/subscriptions/dev/resourceGroups/opsforge/providers/Microsoft.Compute/virtualMachines/vm-runner-02");

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        assets = await db.InfrastructureAssetsSet.ToListAsync(cancellationToken);
        assetsByName = assets.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var links = await db.ServiceInfrastructureLinksSet.ToListAsync(cancellationToken);

        void EnsureLink(string serviceName, string assetName)
        {
            if (!servicesByName.TryGetValue(serviceName, out var service) || !assetsByName.TryGetValue(assetName, out var asset))
            {
                return;
            }

            var exists = links.Any(l => l.ServiceId == service.Id && l.InfrastructureAssetId == asset.Id);
            if (exists)
            {
                return;
            }

            var link = new ServiceInfrastructureLink
            {
                ServiceId = service.Id,
                InfrastructureAssetId = asset.Id
            };

            db.ServiceInfrastructureLinksSet.Add(link);
            links.Add(link);
            changed = true;
        }

        foreach (var service in services)
        {
            EnsureLink(service.Name, "pg-main");
            EnsureLink(service.Name, "kv-shared");
            EnsureLink(service.Name, "storage-artifacts");
        }

        EnsureLink("opsforge-api", "app-opsforge-api");
        EnsureLink("opsforge-web", "app-opsforge-web");
        EnsureLink("deployment-orchestrator", "vm-runner-01");
        EnsureLink("invoice-worker", "vm-runner-02");
        EnsureLink("alerts-gateway", "redis-cache");

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        environments = await db.ServiceEnvironmentsSet.ToListAsync(cancellationToken);
        var deployments = await db.DeploymentsSet.ToListAsync(cancellationToken);

        var deploymentUsers = users.Where(u => u.Role is UserRole.Admin or UserRole.TeamLead).ToList();
        if (deploymentUsers.Count == 0)
        {
            deploymentUsers = users;
        }

        foreach (var service in services)
        {
            var prodEnv = environments.FirstOrDefault(e => e.ServiceId == service.Id && e.Name == "prod");
            var uatEnv = environments.FirstOrDefault(e => e.ServiceId == service.Id && e.Name == "uat");

            if (prodEnv is null || uatEnv is null || deploymentUsers.Count == 0)
            {
                continue;
            }

            var versions = new[]
            {
                (Version: "1.0.0", Env: uatEnv),
                (Version: "1.1.0", Env: prodEnv),
                (Version: "1.2.0", Env: prodEnv)
            };

            for (var i = 0; i < versions.Length; i++)
            {
                var version = versions[i];
                var exists = deployments.Any(d => d.ServiceId == service.Id && d.EnvironmentId == version.Env.Id && d.Version == version.Version);
                if (exists)
                {
                    continue;
                }

                var deployer = deploymentUsers[(service.Name.Length + i) % deploymentUsers.Count];
                db.DeploymentsSet.Add(new Deployment
                {
                    ServiceId = service.Id,
                    EnvironmentId = version.Env.Id,
                    Version = version.Version,
                    CommitHash = Guid.NewGuid().ToString("N")[..8],
                    ReleaseNotes = $"Automated seeded deployment for {service.Name} {version.Version}.",
                    DeploymentDateUtc = DateTime.UtcNow.AddDays(-(15 - i * 3)),
                    DeployedByUserId = deployer.Id
                });
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        deployments = await db.DeploymentsSet.ToListAsync(cancellationToken);
        var auditLogs = await db.AuditLogsSet.ToListAsync(cancellationToken);

        void EnsureAudit(Guid? userId, AuditAction action, string entityType, string entityId, object details)
        {
            var detailsJson = JsonSerializer.Serialize(details);
            var exists = auditLogs.Any(a => a.Action == action && a.EntityType == entityType && a.EntityId == entityId && a.DetailsJson == detailsJson);
            if (exists)
            {
                return;
            }

            var entry = new AuditLogEntry
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                DetailsJson = detailsJson
            };

            db.AuditLogsSet.Add(entry);
            auditLogs.Add(entry);
            changed = true;
        }

        var admin = usersByEmail.TryGetValue("demo@opsforge.local", out var seededAdmin)
            ? seededAdmin
            : users.FirstOrDefault();

        foreach (var team in teams)
        {
            EnsureAudit(admin?.Id, AuditAction.Create, "Team", team.Id.ToString(), new { team.Name, team.Description });
        }

        foreach (var service in services)
        {
            EnsureAudit(admin?.Id, AuditAction.Create, "Service", service.Id.ToString(), new { service.Name, Criticality = service.Criticality.ToString() });
            EnsureAudit(admin?.Id, AuditAction.Update, "Service", service.Id.ToString(), new { Change = "RepositoryUpdated", service.RepositoryUrl });
        }

        foreach (var deployment in deployments)
        {
            EnsureAudit(deployment.DeployedByUserId, AuditAction.Deployment, "Deployment", deployment.Id.ToString(), new
            {
                deployment.ServiceId,
                deployment.EnvironmentId,
                deployment.Version,
                deployment.CommitHash
            });
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
