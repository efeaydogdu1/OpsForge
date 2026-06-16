using System.Text;
using System.Net;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpsForge.Api;
using OpsForge.Application;
using OpsForge.Domain;
using OpsForge.Infrastructure.Persistence;
using OpsForge.Infrastructure.Services;
using OpsForge.Modules.GitHub;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddGitHubModule(builder.Configuration);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000", "http://127.0.0.1:3000"];

var jwtOptions = new JwtOptions(
    builder.Configuration["Jwt:Issuer"] ?? "OpsForge",
    builder.Configuration["Jwt:Audience"] ?? "OpsForge",
    builder.Configuration["Jwt:SecretKey"] ?? "change-this-secret-key-at-least-32-chars",
    int.TryParse(builder.Configuration["Jwt:AccessTokenMinutes"], out var accessTokenMinutes) ? accessTokenMinutes : 30);

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<IdentityHandlers>());
builder.Services.AddHttpClient();
builder.Services.AddDbContext<OpsForgeDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Port=5432;Database=opsforge;Username=postgres;Password=postgres";

    options.UseNpgsql(connectionString);
});
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<OpsForgeDbContext>());
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRole.Admin.ToString()));
    options.AddPolicy("TeamLeadOrAdmin", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.TeamLead.ToString()));
});

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OpsForgeDbContext>();
    await db.Database.MigrateAsync();
}

if (builder.Configuration.GetValue<bool>("Seed:CreateDemoUser"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OpsForgeDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    var email = (builder.Configuration["Seed:DemoUser:Email"] ?? "demo@opsforge.local").Trim().ToLowerInvariant();
    var displayName = (builder.Configuration["Seed:DemoUser:DisplayName"] ?? "Demo Admin").Trim();
    var password = builder.Configuration["Seed:DemoUser:Password"] ?? "Demo123!";
    var roleRaw = builder.Configuration["Seed:DemoUser:Role"];
    var role = Enum.TryParse<UserRole>(roleRaw, ignoreCase: true, out var parsedRole) ? parsedRole : UserRole.Admin;

    var exists = await db.UsersSet.AnyAsync(x => x.Email == email);
    if (!exists)
    {
        db.UsersSet.Add(new AppUser
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = passwordHasher.Hash(password),
            Role = role
        });

        await db.SaveChangesAsync();
    }
}

if (builder.Configuration.GetValue<bool>("Seed:CreateDemoData"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OpsForgeDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    await DemoDataSeeder.SeedAsync(db, passwordHasher, tokenService, builder.Configuration);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api/v1");
api.MapGet("/health", () => Results.Ok(new { status = "ok" }));
api.MapGet("/meta", () => Results.Ok(new { modules = new[] { "Identity", "Teams", "Services", "Environments", "InfrastructureInventory", "Deployments", "AuditLogging" } }));

var auth = api.MapGroup("/auth");
auth.MapPost("/register", async (HttpContext context, RegisterCommand command, ISender sender) =>
{
    var response = await sender.Send(command);
    context.Response.Cookies.Append("opsforge.refresh_token", response.RefreshToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddDays(14)
    });

    return Results.Ok(response);
});
auth.MapPost("/login", async (HttpContext context, LoginCommand command, ISender sender) =>
{
    var response = await sender.Send(command);
    context.Response.Cookies.Append("opsforge.refresh_token", response.RefreshToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddDays(14)
    });

    return Results.Ok(response);
});
auth.MapPost("/refresh", async (HttpContext context, RefreshTokenCommand command, ISender sender) =>
{
    var response = await sender.Send(command);
    context.Response.Cookies.Append("opsforge.refresh_token", response.RefreshToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddDays(14)
    });

    return Results.Ok(response);
});

var teams = api.MapGroup("/teams").RequireAuthorization();
teams.MapGet("", async (IAppDbContext db, ICurrentUserContext currentUser) =>
{
    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    var query = db.Teams;

    if (!isAdmin && currentUser.UserId.HasValue)
    {
        var allowedTeamIds = await db.TeamMembers
            .Where(m => m.UserId == currentUser.UserId.Value)
            .Select(m => m.TeamId)
            .Distinct()
            .ToListAsync();

        query = query.Where(team => allowedTeamIds.Contains(team.Id));
    }

    return Results.Ok(await query
        .Select(team => new TeamDto(team.Id, team.Name, team.Description, team.IsDeleted, Array.Empty<TeamMemberDto>()))
        .ToListAsync());
});
teams.MapGet("/{id:guid}", async (Guid id, IAppDbContext db, ICurrentUserContext currentUser) =>
{
    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    if (!isAdmin && currentUser.UserId.HasValue)
    {
        var hasMembership = await db.TeamMembers.AnyAsync(x => x.TeamId == id && x.UserId == currentUser.UserId.Value);
        if (!hasMembership)
        {
            return Results.Forbid();
        }
    }

    var team = await db.Teams.FirstOrDefaultAsync(x => x.Id == id);
    return team is null ? Results.NotFound() : Results.Ok(new TeamDto(team.Id, team.Name, team.Description, team.IsDeleted, Array.Empty<TeamMemberDto>()));
});
teams.MapPost("", async (CreateTeamCommand command, ISender sender) => Results.Ok(await sender.Send(command)));
teams.MapPut("/{id:guid}", async (Guid id, UpdateTeamCommand command, ISender sender) => Results.Ok(await sender.Send(command with { Id = id })));
teams.MapDelete("/{id:guid}", async (Guid id, ISender sender) => { await sender.Send(new DeleteTeamCommand(id)); return Results.NoContent(); });
teams.MapPost("/{id:guid}/members", async (Guid id, AddTeamMemberCommand command, ISender sender) => { await sender.Send(command with { TeamId = id }); return Results.NoContent(); });
teams.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, ISender sender) => { await sender.Send(new RemoveTeamMemberCommand(id, userId)); return Results.NoContent(); });

var services = api.MapGroup("/services").RequireAuthorization();
services.MapGet("", async (IAppDbContext db, ICurrentUserContext currentUser) =>
{
    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    var query = db.Services;

    if (!isAdmin && currentUser.UserId.HasValue)
    {
        var allowedTeamIds = await db.TeamMembers
            .Where(m => m.UserId == currentUser.UserId.Value)
            .Select(m => m.TeamId)
            .Distinct()
            .ToListAsync();

        query = query.Where(s => s.OwnerTeamId.HasValue && allowedTeamIds.Contains(s.OwnerTeamId.Value));
    }

    return Results.Ok(await query
        .Select(s => new ServiceDto(s.Id, s.Name, s.Description, s.OwnerTeamId, s.Criticality.ToString(), s.RepositoryUrl, s.IsDeleted))
        .ToListAsync());
});
services.MapGet("/{id:guid}", async (Guid id, IAppDbContext db, ICurrentUserContext currentUser) =>
{
    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    var s = await db.Services.FirstOrDefaultAsync(x => x.Id == id);
    if (s is not null && !isAdmin && currentUser.UserId.HasValue)
    {
        if (!s.OwnerTeamId.HasValue)
        {
            return Results.Forbid();
        }

        var hasMembership = await db.TeamMembers.AnyAsync(x => x.TeamId == s.OwnerTeamId.Value && x.UserId == currentUser.UserId.Value);
        if (!hasMembership)
        {
            return Results.Forbid();
        }
    }

    return s is null ? Results.NotFound() : Results.Ok(new ServiceDto(s.Id, s.Name, s.Description, s.OwnerTeamId, s.Criticality.ToString(), s.RepositoryUrl, s.IsDeleted));
});
services.MapPost("", async (CreateServiceCommand command, ISender sender) => Results.Ok(await sender.Send(command)));
services.MapPut("/{id:guid}", async (Guid id, UpdateServiceCommand command, ISender sender) => Results.Ok(await sender.Send(command with { Id = id })));
services.MapDelete("/{id:guid}", async (Guid id, ISender sender) => { await sender.Send(new DeleteServiceCommand(id)); return Results.NoContent(); });
services.MapPost("/{id:guid}/health-check", async (Guid id, IAppDbContext db, ICurrentUserContext currentUser, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    var service = await db.Services.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (service is null)
    {
        return Results.NotFound(new { message = "Service not found." });
    }

    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    if (!isAdmin && currentUser.UserId.HasValue)
    {
        if (!service.OwnerTeamId.HasValue)
        {
            return Results.Forbid();
        }

        var hasMembership = await db.TeamMembers.AnyAsync(
            x => x.UserId == currentUser.UserId.Value && x.TeamId == service.OwnerTeamId.Value,
            cancellationToken);

        if (!hasMembership)
        {
            return Results.Forbid();
        }
    }

    if (string.IsNullOrWhiteSpace(service.RepositoryUrl) || !Uri.TryCreate(service.RepositoryUrl, UriKind.Absolute, out var targetUri))
    {
        return Results.BadRequest(new
        {
            status = "unknown",
            message = "Service URL is missing or invalid.",
            checkedAtUtc = DateTime.UtcNow
        });
    }

    var httpClient = httpClientFactory.CreateClient();
    httpClient.Timeout = TimeSpan.FromSeconds(8);

    try
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var isHealthy = response.IsSuccessStatusCode || response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

        return Results.Ok(new
        {
            status = isHealthy ? "healthy" : "unhealthy",
            statusCode = (int)response.StatusCode,
            checkedAtUtc = DateTime.UtcNow,
            url = targetUri.ToString()
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            status = "unreachable",
            message = ex.Message,
            checkedAtUtc = DateTime.UtcNow,
            url = targetUri.ToString()
        });
    }
});

var environments = api.MapGroup("/environments").RequireAuthorization();
environments.MapGet("", async (Guid? serviceId, IAppDbContext db, ICurrentUserContext currentUser) =>
{
    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    var query = db.ServiceEnvironments.AsQueryable();

    if (!isAdmin && currentUser.UserId.HasValue)
    {
        var allowedTeamIds = await db.TeamMembers
            .Where(m => m.UserId == currentUser.UserId.Value)
            .Select(m => m.TeamId)
            .Distinct()
            .ToListAsync();

        query = query.Where(e => db.Services.Any(s => s.Id == e.ServiceId && s.OwnerTeamId.HasValue && allowedTeamIds.Contains(s.OwnerTeamId.Value)));
    }

    if (serviceId.HasValue) query = query.Where(e => e.ServiceId == serviceId.Value);
    return Results.Ok(await query.Select(e => new EnvironmentDto(e.Id, e.ServiceId, e.Name, e.Kind.ToString(), e.Url, e.IsDeleted)).ToListAsync());
});
environments.MapPost("", async (CreateEnvironmentCommand command, ISender sender) => Results.Ok(await sender.Send(command)));
environments.MapPut("/{id:guid}", async (Guid id, UpdateEnvironmentCommand command, ISender sender) => Results.Ok(await sender.Send(command with { Id = id })));
environments.MapDelete("/{id:guid}", async (Guid id, ISender sender) => { await sender.Send(new DeleteEnvironmentCommand(id)); return Results.NoContent(); });

var infra = api.MapGroup("/infrastructure").RequireAuthorization();
infra.MapGet("", async (IAppDbContext db, ICurrentUserContext currentUser) =>
{
    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    List<Guid> allowedServiceIds = [];

    if (!isAdmin && currentUser.UserId.HasValue)
    {
        var allowedTeamIds = await db.TeamMembers
            .Where(m => m.UserId == currentUser.UserId.Value)
            .Select(m => m.TeamId)
            .Distinct()
            .ToListAsync();

        allowedServiceIds = await db.Services
            .Where(s => s.OwnerTeamId.HasValue && allowedTeamIds.Contains(s.OwnerTeamId.Value))
            .Select(s => s.Id)
            .ToListAsync();
    }

    var assetQuery = db.InfrastructureAssets.AsQueryable();
    if (!isAdmin)
    {
        assetQuery = assetQuery.Where(a => db.ServiceInfrastructureLinks.Any(l => l.InfrastructureAssetId == a.Id && allowedServiceIds.Contains(l.ServiceId)));
    }

    var assets = await assetQuery
        .Select(a => new
        {
            a.Id,
            a.Name,
            AssetType = a.AssetType.ToString(),
            a.Provider,
            a.ResourceIdentifier,
            a.IsDeleted
        })
        .ToListAsync();

    var linkQuery = db.ServiceInfrastructureLinks.AsQueryable();
    if (!isAdmin)
    {
        linkQuery = linkQuery.Where(x => allowedServiceIds.Contains(x.ServiceId));
    }

    var links = await linkQuery
        .GroupBy(x => x.InfrastructureAssetId)
        .Select(g => new { AssetId = g.Key, ServiceIds = g.Select(x => x.ServiceId).ToList() })
        .ToListAsync();

    var linkLookup = links.ToDictionary(x => x.AssetId, x => (IReadOnlyCollection<Guid>)x.ServiceIds);

    var result = assets.Select(a => new InfrastructureAssetDto(
        a.Id,
        a.Name,
        a.AssetType,
        a.Provider,
        a.ResourceIdentifier,
        a.IsDeleted,
        linkLookup.TryGetValue(a.Id, out var linkedIds) ? linkedIds : Array.Empty<Guid>()));

    return Results.Ok(result);
});
infra.MapPost("", async (CreateInfrastructureAssetCommand command, ISender sender) => Results.Ok(await sender.Send(command)));
infra.MapPut("/{id:guid}", async (Guid id, UpdateInfrastructureAssetCommand command, ISender sender) => Results.Ok(await sender.Send(command with { Id = id })));
infra.MapDelete("/{id:guid}", async (Guid id, ISender sender) => { await sender.Send(new DeleteInfrastructureAssetCommand(id)); return Results.NoContent(); });
infra.MapPost("/{id:guid}/link/{serviceId:guid}", async (Guid id, Guid serviceId, ISender sender) => { await sender.Send(new LinkAssetToServiceCommand(id, serviceId)); return Results.NoContent(); });
infra.MapDelete("/{id:guid}/link/{serviceId:guid}", async (Guid id, Guid serviceId, ISender sender) => { await sender.Send(new UnlinkAssetFromServiceCommand(id, serviceId)); return Results.NoContent(); });

var deployments = api.MapGroup("/deployments").RequireAuthorization();
deployments.MapGet("", async (Guid? serviceId, IAppDbContext db, ICurrentUserContext currentUser) =>
{
    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    var query = db.Deployments.AsQueryable();

    if (!isAdmin && currentUser.UserId.HasValue)
    {
        var allowedTeamIds = await db.TeamMembers
            .Where(m => m.UserId == currentUser.UserId.Value)
            .Select(m => m.TeamId)
            .Distinct()
            .ToListAsync();

        query = query.Where(d => db.Services.Any(s => s.Id == d.ServiceId && s.OwnerTeamId.HasValue && allowedTeamIds.Contains(s.OwnerTeamId.Value)));
    }

    if (serviceId.HasValue) query = query.Where(d => d.ServiceId == serviceId.Value);
    return Results.Ok(await query
        .OrderByDescending(d => d.DeploymentDateUtc)
        .Select(d => new DeploymentDto(d.Id, d.ServiceId, d.EnvironmentId, d.Version, d.CommitHash, d.ReleaseNotes, d.DeploymentDateUtc, d.DeployedByUserId, d.IsDeleted))
        .ToListAsync());
});
deployments.MapPost("", async (CreateDeploymentCommand command, ISender sender) => Results.Ok(await sender.Send(command)));

var auditLogs = api.MapGroup("/audit").RequireAuthorization();
auditLogs.MapGet("", async (string? entityType, Guid? userId, IAppDbContext db) =>
{
    var query = db.AuditLogs.AsQueryable();
    if (entityType is not null) query = query.Where(a => a.EntityType == entityType);
    if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
    return Results.Ok(await query
        .OrderByDescending(a => a.CreatedAtUtc)
        .Select(a => new AuditLogDto(a.Id, a.UserId, a.CreatedAtUtc, a.Action.ToString(), a.EntityType, a.EntityId, a.DetailsJson))
        .ToListAsync());
});

    api.MapGet("/github-ping", () => Results.Ok(new { status = "ok" }));

var github = api.MapGroup("/github").RequireAuthorization();
github.MapPost("/preview", async (PreviewRepositoryRequest request, [FromServices] IGitHubRepositoryUrlParser parser, [FromServices] IGitHubApiClient gitHubClient, CancellationToken cancellationToken) =>
{
    if (!parser.TryParse(request.RepositoryUrl, out var owner, out var repositoryName, out var normalizedUrl))
    {
        return Results.BadRequest(new { message = "Invalid GitHub repository URL." });
    }

    var metadata = await gitHubClient.GetRepositoryMetadataAsync(owner, repositoryName, normalizedUrl, cancellationToken);
    var preview = new GitHubRepositoryMetadataDto(
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

    return Results.Ok(preview);
});

github.MapPost("/services/{serviceId:guid}/link", async (Guid serviceId, LinkRepositoryRequest request, [FromServices] OpsForgeDbContext db, [FromServices] ICurrentUserContext currentUser, [FromServices] IGitHubRepositoryUrlParser parser, [FromServices] IGitHubApiClient gitHubClient, [FromServices] IAuditService auditService, CancellationToken cancellationToken) =>
{
    var service = await db.ServicesSet.FirstOrDefaultAsync(x => x.Id == serviceId, cancellationToken);
    if (service is null)
    {
        return Results.NotFound();
    }

    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    if (!isAdmin && currentUser.UserId.HasValue)
    {
        if (!service.OwnerTeamId.HasValue)
        {
            return Results.Forbid();
        }

        var hasMembership = await db.TeamMembersSet.AnyAsync(m => m.UserId == currentUser.UserId.Value && m.TeamId == service.OwnerTeamId.Value, cancellationToken);
        if (!hasMembership)
        {
            return Results.Forbid();
        }
    }

    if (!parser.TryParse(request.RepositoryUrl, out var owner, out var repositoryName, out var normalizedUrl))
    {
        return Results.BadRequest(new { message = "Invalid GitHub repository URL." });
    }

    var metadata = await gitHubClient.GetRepositoryMetadataAsync(owner, repositoryName, normalizedUrl, cancellationToken);

    var existingLink = await db.ServiceRepositoryLinksSet.FirstOrDefaultAsync(x => x.ServiceId == serviceId, cancellationToken);
    if (existingLink is null)
    {
        existingLink = new ServiceRepositoryLink
        {
            ServiceId = serviceId,
            RepositoryOwner = metadata.Owner,
            RepositoryName = metadata.Name,
            RepositoryUrl = metadata.Url,
            DefaultBranch = metadata.DefaultBranch,
            Description = metadata.Description,
            Visibility = metadata.Visibility,
            PrimaryLanguage = metadata.PrimaryLanguage,
            LatestCommitSha = metadata.LatestCommitSha,
            LatestCommitDateUtc = metadata.LatestCommitDateUtc,
            LatestCommitMessage = metadata.LatestCommitMessage,
            LastSyncedAtUtc = DateTime.UtcNow
        };
        db.ServiceRepositoryLinksSet.Add(existingLink);
    }
    else
    {
        existingLink.RepositoryOwner = metadata.Owner;
        existingLink.RepositoryName = metadata.Name;
        existingLink.RepositoryUrl = metadata.Url;
        existingLink.DefaultBranch = metadata.DefaultBranch;
        existingLink.Description = metadata.Description;
        existingLink.Visibility = metadata.Visibility;
        existingLink.PrimaryLanguage = metadata.PrimaryLanguage;
        existingLink.LatestCommitSha = metadata.LatestCommitSha;
        existingLink.LatestCommitDateUtc = metadata.LatestCommitDateUtc;
        existingLink.LatestCommitMessage = metadata.LatestCommitMessage;
        existingLink.LastSyncedAtUtc = DateTime.UtcNow;
        db.ServiceRepositoryLinksSet.Update(existingLink);
    }

    service.RepositoryUrl = metadata.Url;

    var syncRun = new RepositorySyncRun
    {
        ServiceId = serviceId,
        ServiceRepositoryLinkId = existingLink.Id,
        StartedAtUtc = DateTime.UtcNow,
        CompletedAtUtc = DateTime.UtcNow,
        IsSuccess = true,
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
    db.RepositorySyncRunsSet.Add(syncRun);

    await db.SaveChangesAsync(cancellationToken);
    await auditService.LogAsync(AuditAction.Update, "ServiceRepositoryLink", serviceId.ToString(), currentUser.UserId, $"Linked GitHub repository {metadata.Owner}/{metadata.Name}", cancellationToken);

    var result = new GitHubRepositoryMetadataDto(
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

    return Results.Ok(result);
});

github.MapPost("/services/{serviceId:guid}/sync", async (Guid serviceId, [FromServices] OpsForgeDbContext db, [FromServices] ICurrentUserContext currentUser, [FromServices] IGitHubApiClient gitHubClient, [FromServices] IAuditService auditService, CancellationToken cancellationToken) =>
{
    var service = await db.ServicesSet.FirstOrDefaultAsync(x => x.Id == serviceId, cancellationToken);
    if (service is null)
    {
        return Results.NotFound();
    }

    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    if (!isAdmin && currentUser.UserId.HasValue)
    {
        if (!service.OwnerTeamId.HasValue)
        {
            return Results.Forbid();
        }

        var hasMembership = await db.TeamMembersSet.AnyAsync(m => m.UserId == currentUser.UserId.Value && m.TeamId == service.OwnerTeamId.Value, cancellationToken);
        if (!hasMembership)
        {
            return Results.Forbid();
        }
    }

    var link = await db.ServiceRepositoryLinksSet.FirstOrDefaultAsync(x => x.ServiceId == serviceId, cancellationToken);
    if (link is null)
    {
        return Results.BadRequest(new { message = "Service does not have a linked GitHub repository." });
    }

    var startedAt = DateTime.UtcNow;
    RepositorySyncRun run;

    try
    {
        var metadata = await gitHubClient.GetRepositoryMetadataAsync(link.RepositoryOwner, link.RepositoryName, link.RepositoryUrl, cancellationToken);

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

        run = new RepositorySyncRun
        {
            ServiceId = serviceId,
            ServiceRepositoryLinkId = link.Id,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTime.UtcNow,
            IsSuccess = true,
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

        db.RepositorySyncRunsSet.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(AuditAction.Update, "ServiceRepositorySync", serviceId.ToString(), currentUser.UserId, $"Synced GitHub repository {metadata.Owner}/{metadata.Name}", cancellationToken);
    }
    catch (Exception ex)
    {
        run = new RepositorySyncRun
        {
            ServiceId = serviceId,
            ServiceRepositoryLinkId = link.Id,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTime.UtcNow,
            IsSuccess = false,
            ErrorMessage = ex.Message,
            RepositoryOwner = link.RepositoryOwner,
            RepositoryName = link.RepositoryName,
            RepositoryUrl = link.RepositoryUrl,
            DefaultBranch = link.DefaultBranch,
            Description = link.Description,
            Visibility = link.Visibility,
            PrimaryLanguage = link.PrimaryLanguage,
            LatestCommitSha = link.LatestCommitSha,
            LatestCommitDateUtc = link.LatestCommitDateUtc,
            LatestCommitMessage = link.LatestCommitMessage
        };

        db.RepositorySyncRunsSet.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(AuditAction.Update, "ServiceRepositorySync", serviceId.ToString(), currentUser.UserId, $"GitHub sync failed: {ex.Message}", cancellationToken);
    }

    var result = new RepositorySyncRunDto(
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

    return Results.Ok(result);
});

github.MapGet("/services/{serviceId:guid}/sync-runs", async (Guid serviceId, [FromServices] OpsForgeDbContext db, [FromServices] ICurrentUserContext currentUser, CancellationToken cancellationToken) =>
{
    var service = await db.ServicesSet.FirstOrDefaultAsync(x => x.Id == serviceId, cancellationToken);
    if (service is null)
    {
        return Results.NotFound();
    }

    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    if (!isAdmin && currentUser.UserId.HasValue)
    {
        if (!service.OwnerTeamId.HasValue)
        {
            return Results.Forbid();
        }

        var hasMembership = await db.TeamMembersSet.AnyAsync(m => m.UserId == currentUser.UserId.Value && m.TeamId == service.OwnerTeamId.Value, cancellationToken);
        if (!hasMembership)
        {
            return Results.Forbid();
        }
    }

    var runs = await db.RepositorySyncRunsSet
        .Where(x => x.ServiceId == serviceId)
        .OrderByDescending(x => x.StartedAtUtc)
        .Select(x => new RepositorySyncRunDto(
            x.Id,
            x.ServiceId,
            x.StartedAtUtc,
            x.CompletedAtUtc,
            x.IsSuccess,
            x.ErrorMessage,
            x.RepositoryOwner,
            x.RepositoryName,
            x.RepositoryUrl,
            x.DefaultBranch,
            x.Description,
            x.Visibility,
            x.PrimaryLanguage,
            x.LatestCommitSha,
            x.LatestCommitDateUtc,
            x.LatestCommitMessage))
        .ToListAsync(cancellationToken);

    return Results.Ok(runs);
});

app.Run();

public sealed record LinkRepositoryRequest(string RepositoryUrl);
public sealed record PreviewRepositoryRequest(string RepositoryUrl);
public partial class Program;
