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
builder.Services.AddScoped<ISecretProtector, AesSecretProtector>();
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
api.MapGet("/meta", () => Results.Ok(new { modules = new[] { "Identity", "Teams", "Services", "Environments", "InfrastructureInventory", "Deployments", "Incidents", "Issues", "AuditLogging" } }));

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

var me = api.MapGroup("/me").RequireAuthorization();
me.MapGet("/github-tokens", async (IAppDbContext db, ICurrentUserContext currentUser) =>
{
    if (!currentUser.UserId.HasValue)
    {
        return Results.Unauthorized();
    }

    var tokens = await db.UserGitHubTokens
        .Where(x => x.UserId == currentUser.UserId.Value)
        .OrderByDescending(x => x.IsDefault)
        .ThenBy(x => x.Name)
        .Select(x => new UserGitHubTokenDto(x.Id, x.Name, x.TokenLastFour, x.IsDefault, x.IsActive, x.CreatedAtUtc, x.LastUsedAtUtc))
        .ToListAsync();

    return Results.Ok(tokens);
});
me.MapPost("/github-tokens", async (CreateUserGitHubTokenCommand command, ISender sender, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await sender.Send(command, cancellationToken));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});
me.MapPut("/github-tokens/{id:guid}", async (Guid id, UpdateUserGitHubTokenRequest request, ISender sender, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await sender.Send(new UpdateUserGitHubTokenCommand(id, request.Name, request.IsDefault, request.IsActive), cancellationToken));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});
me.MapDelete("/github-tokens/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
{
    try
    {
        await sender.Send(new DeleteUserGitHubTokenCommand(id), cancellationToken);
        return Results.NoContent();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
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

        query = query.Where(s => !s.OwnerTeamId.HasValue || allowedTeamIds.Contains(s.OwnerTeamId.Value));
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
        if (s.OwnerTeamId.HasValue)
        {
            var hasMembership = await db.TeamMembers.AnyAsync(x => x.TeamId == s.OwnerTeamId.Value && x.UserId == currentUser.UserId.Value);
            if (!hasMembership)
            {
                return Results.Forbid();
            }
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
        if (service.OwnerTeamId.HasValue)
        {
            var hasMembership = await db.TeamMembers.AnyAsync(
                x => x.UserId == currentUser.UserId.Value && x.TeamId == service.OwnerTeamId.Value,
                cancellationToken);

            if (!hasMembership)
            {
                return Results.Forbid();
            }
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

        query = query.Where(e => db.Services.Any(s => s.Id == e.ServiceId && (!s.OwnerTeamId.HasValue || allowedTeamIds.Contains(s.OwnerTeamId.Value))));
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
            .Where(s => !s.OwnerTeamId.HasValue || allowedTeamIds.Contains(s.OwnerTeamId.Value))
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

        query = query.Where(d => db.Services.Any(s => s.Id == d.ServiceId && (!s.OwnerTeamId.HasValue || allowedTeamIds.Contains(s.OwnerTeamId.Value))));
    }

    if (serviceId.HasValue) query = query.Where(d => d.ServiceId == serviceId.Value);
    return Results.Ok(await query
        .OrderByDescending(d => d.DeploymentDateUtc)
        .Select(d => new DeploymentDto(d.Id, d.ServiceId, d.EnvironmentId, d.Version, d.CommitHash, d.ReleaseNotes, d.DeploymentDateUtc, d.DeployedByUserId, d.IsDeleted))
        .ToListAsync());
});
deployments.MapPost("", async (CreateDeploymentCommand command, ISender sender) => Results.Ok(await sender.Send(command)));
deployments.MapPut("/{id:guid}", async (Guid id, UpdateDeploymentCommand command, ISender sender) => Results.Ok(await sender.Send(command with { Id = id })));
deployments.MapDelete("/{id:guid}", async (Guid id, ISender sender) => { await sender.Send(new DeleteDeploymentCommand(id)); return Results.NoContent(); });

var incidents = api.MapGroup("/incidents").RequireAuthorization();
incidents.MapGet("", async (IAppDbContext db, ICurrentUserContext currentUser) =>
{
    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    var query = db.Incidents.AsQueryable();

    if (!isAdmin && currentUser.UserId.HasValue)
    {
        var allowedTeamIds = await db.TeamMembers
            .Where(m => m.UserId == currentUser.UserId.Value)
            .Select(m => m.TeamId)
            .Distinct()
            .ToListAsync();

        query = query.Where(i => db.Services.Any(s => s.Id == i.ServiceId && (!s.OwnerTeamId.HasValue || allowedTeamIds.Contains(s.OwnerTeamId.Value))));
    }

    var rows = await query
        .OrderByDescending(i => i.OccurredAtUtc)
        .Select(i => new
        {
            Incident = i,
            Service = db.Services.First(s => s.Id == i.ServiceId),
            Environment = i.EnvironmentId.HasValue ? db.ServiceEnvironments.FirstOrDefault(e => e.Id == i.EnvironmentId.Value) : null,
            Deployment = i.DeploymentId.HasValue ? db.Deployments.FirstOrDefault(d => d.Id == i.DeploymentId.Value) : null
        })
        .ToListAsync();

    var result = rows.Select(row =>
    {
        var repositoryUrl = row.Service.RepositoryUrl;
        var environmentGitHubUrl = repositoryUrl is not null && row.Environment is not null
            ? $"{repositoryUrl}/tree/{Uri.EscapeDataString(row.Environment.Name)}"
            : null;
        var deploymentGitHubUrl = repositoryUrl is not null && row.Deployment is not null
            ? $"{repositoryUrl}/commit/{row.Deployment.CommitHash}"
            : null;

        return new IncidentResponse(
            row.Incident.Id,
            row.Incident.Title,
            row.Incident.Description,
            row.Incident.Severity.ToString(),
            row.Incident.Status.ToString(),
            row.Incident.ServiceId,
            row.Service.Name,
            repositoryUrl,
            row.Incident.EnvironmentId,
            row.Environment?.Name,
            environmentGitHubUrl,
            row.Incident.DeploymentId,
            row.Deployment?.Version,
            deploymentGitHubUrl,
            row.Incident.ReportedByUserId,
            row.Incident.OccurredAtUtc,
            row.Incident.ResolvedAtUtc);
    });

    return Results.Ok(result);
});
incidents.MapPost("", async (CreateIncidentCommand command, ISender sender) => Results.Ok(await sender.Send(command)));
incidents.MapPut("/{id:guid}", async (Guid id, UpdateIncidentCommand command, ISender sender) => Results.Ok(await sender.Send(command with { Id = id })));
incidents.MapDelete("/{id:guid}", async (Guid id, ISender sender) => { await sender.Send(new DeleteIncidentCommand(id)); return Results.NoContent(); });

var issues = api.MapGroup("/issues").RequireAuthorization();
issues.MapGet("", async (IAppDbContext db, ICurrentUserContext currentUser) =>
{
    var isAdmin = string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    var query = db.Issues.AsQueryable();

    if (!isAdmin && currentUser.UserId.HasValue)
    {
        var allowedTeamIds = await db.TeamMembers
            .Where(m => m.UserId == currentUser.UserId.Value)
            .Select(m => m.TeamId)
            .Distinct()
            .ToListAsync();

        query = query.Where(i => db.Services.Any(s => s.Id == i.ServiceId && (!s.OwnerTeamId.HasValue || allowedTeamIds.Contains(s.OwnerTeamId.Value))));
    }

    var rows = await query
        .OrderByDescending(i => i.ExternalUpdatedAtUtc ?? i.UpdatedAtUtc)
        .Select(i => new
        {
            Issue = i,
            Service = db.Services.First(s => s.Id == i.ServiceId),
            Environment = i.EnvironmentId.HasValue ? db.ServiceEnvironments.FirstOrDefault(e => e.Id == i.EnvironmentId.Value) : null,
            Deployment = i.DeploymentId.HasValue ? db.Deployments.FirstOrDefault(d => d.Id == i.DeploymentId.Value) : null
        })
        .ToListAsync();

    var result = rows.Select(row =>
    {
        var repositoryUrl = row.Service.RepositoryUrl;
        var environmentGitHubUrl = repositoryUrl is not null && row.Environment is not null
            ? $"{repositoryUrl}/tree/{Uri.EscapeDataString(row.Environment.Name)}"
            : null;
        var deploymentGitHubUrl = repositoryUrl is not null && row.Deployment is not null
            ? $"{repositoryUrl}/commit/{row.Deployment.CommitHash}"
            : null;

        return new IssueResponse(
            row.Issue.Id,
            row.Issue.Title,
            row.Issue.Description,
            row.Issue.Status.ToString(),
            row.Issue.Source.ToString(),
            row.Issue.ServiceId,
            row.Service.Name,
            repositoryUrl,
            row.Issue.EnvironmentId,
            row.Environment?.Name,
            environmentGitHubUrl,
            row.Issue.DeploymentId,
            row.Deployment?.Version,
            deploymentGitHubUrl,
            row.Issue.ExternalUrl,
            row.Issue.ExternalNumber,
            row.Issue.ExternalState,
            row.Issue.ExternalCreatedAtUtc,
            row.Issue.ExternalUpdatedAtUtc);
    });

    return Results.Ok(result);
});
issues.MapPost("", async (CreateIssueCommand command, ISender sender) => Results.Ok(await sender.Send(command)));
issues.MapPut("/{id:guid}", async (Guid id, UpdateIssueCommand command, ISender sender) => Results.Ok(await sender.Send(command with { Id = id })));
issues.MapDelete("/{id:guid}", async (Guid id, ISender sender) => { await sender.Send(new DeleteIssueCommand(id)); return Results.NoContent(); });

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
github.MapPost("/sync-account", async (ISender sender, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await sender.Send(new SyncGitHubAccountCommand(), cancellationToken));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

github.MapPost("/preview", async (PreviewRepositoryRequest request, ISender sender, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await sender.Send(new PreviewRepositoryMetadataCommand(request.RepositoryUrl), cancellationToken));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

github.MapPost("/services/{serviceId:guid}/link", async (Guid serviceId, LinkRepositoryRequest request, ISender sender, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await sender.Send(new LinkServiceRepositoryCommand(serviceId, request.RepositoryUrl), cancellationToken));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
});

github.MapPost("/services/{serviceId:guid}/sync", async (Guid serviceId, ISender sender, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await sender.Send(new SyncLinkedRepositoryCommand(serviceId), cancellationToken));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
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
public sealed record UpdateUserGitHubTokenRequest(string Name, bool IsDefault, bool IsActive);
public sealed record IncidentResponse(
    Guid Id,
    string Title,
    string Description,
    string Severity,
    string Status,
    Guid ServiceId,
    string ServiceName,
    string? ServiceGitHubUrl,
    Guid? EnvironmentId,
    string? EnvironmentName,
    string? EnvironmentGitHubUrl,
    Guid? DeploymentId,
    string? DeploymentVersion,
    string? DeploymentGitHubUrl,
    Guid ReportedByUserId,
    DateTime OccurredAtUtc,
    DateTime? ResolvedAtUtc);
public sealed record IssueResponse(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    string Source,
    Guid ServiceId,
    string ServiceName,
    string? ServiceGitHubUrl,
    Guid? EnvironmentId,
    string? EnvironmentName,
    string? EnvironmentGitHubUrl,
    Guid? DeploymentId,
    string? DeploymentVersion,
    string? DeploymentGitHubUrl,
    string? ExternalUrl,
    int? ExternalNumber,
    string? ExternalState,
    DateTime? ExternalCreatedAtUtc,
    DateTime? ExternalUpdatedAtUtc);
public partial class Program;
