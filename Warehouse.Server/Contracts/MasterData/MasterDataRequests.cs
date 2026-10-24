namespace Warehouse.Server.Contracts.MasterData;

public sealed record TitledMasterDataRequest(
    string Title,
    string? Description,
    Guid? ConcurrencyStamp = null);

public sealed record PersonRequest(
    string FullName,
    string PhoneNumber,
    Guid? ConcurrencyStamp = null);

public sealed record ProductRequest(
    string Title,
    string Code,
    string UnitOfMeasureTitle,
    string? Description,
    Guid? ConcurrencyStamp = null);

public sealed class ImportSuppliersRequest
{
    public IFormFile? File { get; init; }
}

public sealed class ImportProductsRequest
{
    public IFormFile? File { get; init; }
    public bool CheckOnlyProductCode { get; init; }
}
