using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Application;
using OpsForge.Domain;
using OpsForge.Infrastructure.Persistence;
using OpsForge.Infrastructure.Services;
using OpsForge.Modules.GitHub;
using OpsForge.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGitHubModule(builder.Configuration);
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
builder.Services.AddScoped<WorkerCurrentUserContext>();
builder.Services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<WorkerCurrentUserContext>());
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<IdentityHandlers>());
builder.Services.AddDbContext<OpsForgeDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Port=5432;Database=opsforge;Username=postgres;Password=postgres";

    options.UseNpgsql(connectionString);
});
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<OpsForgeDbContext>());

var hangfireConnectionString = builder.Configuration.GetConnectionString("HangfireConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=opsforge;Username=postgres;Password=postgres";

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireConnectionString)));
builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"{Environment.MachineName}:github-sync";
    options.WorkerCount = 1;
});

builder.Services.AddTransient<GitHubAccountSyncJob>();
builder.Services.AddHostedService<GitHubSyncScheduler>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OpsForgeDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", worker = "github-sync" }));
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    DashboardTitle = "OpsForge Jobs",
    Authorization = [new LocalDashboardAuthorizationFilter()]
});

app.Run();

internal sealed class LocalDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.Request.Host.Host is "localhost" or "127.0.0.1"
            || httpContext.Request.Host.Host.StartsWith("opsforge-", StringComparison.OrdinalIgnoreCase);
    }
}
