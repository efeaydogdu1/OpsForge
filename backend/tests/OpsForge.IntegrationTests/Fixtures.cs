using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsForge.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace OpsForge.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("opsforge_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}

public sealed class OpsForgeWebFactory : WebApplicationFactory<OpsForge.Api.ApiMarker>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture = new();

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<OpsForgeDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<OpsForgeDbContext>(options =>
                options.UseNpgsql(_fixture.ConnectionString));

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OpsForgeDbContext>();
            db.Database.EnsureCreated();
        });
    }

    public new async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
        await base.DisposeAsync();
    }
}
