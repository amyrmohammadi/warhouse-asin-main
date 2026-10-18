namespace Warehouse.Application.Common.Models.MasterData;

public sealed record MasterDataListResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize,
    bool PaginationEnabled);

public sealed class TitledMasterDataSummary
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed class PersonSummary
{
    public Guid Id { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;
}

public sealed class ProductSummary
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public Guid UnitOfMeasureId { get; init; }

    public string UnitOfMeasureTitle { get; init; } = string.Empty;

    public string? Description { get; init; }
}
