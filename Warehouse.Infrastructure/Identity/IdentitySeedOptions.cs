namespace Warehouse.Infrastructure.Identity;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    public string AdminEmail { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public string UserPassword { get; init; } = string.Empty;
}
