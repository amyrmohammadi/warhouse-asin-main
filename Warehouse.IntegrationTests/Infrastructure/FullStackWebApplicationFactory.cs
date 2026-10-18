using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Infrastructure.Identity;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Server;

namespace Warehouse.IntegrationTests.Infrastructure;

internal sealed class FullStackWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin.integration@example.com";
    public const string AdminPassword = "Admin123!";
    public const string UserEmail = "user.integration@example.com";
    public const string UserPassword = "User123!";

    private readonly string _connectionString;
    private readonly DbContextOptions<ApplicationDbContext> _databaseOptions;

    public FullStackWebApplicationFactory()
    {
        var databaseName = $"Warehouse_IntegrationTests_{Guid.NewGuid():N}";
        _connectionString =
            $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};" +
            "Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True";
        _databaseOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Warehouse.IntegrationTests",
                ["Jwt:Audience"] = "Warehouse.IntegrationTests.Client",
                ["Jwt:SecretKey"] =
                    "integration-tests-only-secret-key-with-at-least-32-characters",
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["IdentitySeed:AdminEmail"] = AdminEmail,
                ["IdentitySeed:AdminPassword"] = AdminPassword,
                ["IdentitySeed:UserEmail"] = UserEmail,
                ["IdentitySeed:UserPassword"] = UserPassword,
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            });
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        await using (var migrationContext = new ApplicationDbContext(_databaseOptions))
        {
            await migrationContext.Database.MigrateAsync();
        }

        // Starting the host runs the real IdentitySeeder from Program.Main.
        await using var scope = Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await database.Database.CanConnectAsync());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            using var cleanupContext = new ApplicationDbContext(_databaseOptions);
            cleanupContext.Database.EnsureDeleted();
        }
    }
}
