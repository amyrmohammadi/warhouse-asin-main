namespace Warehouse.Infrastructure.Identity;

public interface IIdentitySeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
