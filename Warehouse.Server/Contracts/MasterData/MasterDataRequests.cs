namespace Warehouse.Server.Contracts.MasterData;

public sealed record TitledMasterDataRequest(
    string Title,
    string? Description);

public sealed record PersonRequest(
    string FullName,
    string PhoneNumber);

public sealed record ProductRequest(
    string Title,
    string Code,
    string UnitOfMeasureTitle,
    string? Description);

public sealed class ImportSuppliersRequest
{
    public IFormFile? File { get; init; }
}
