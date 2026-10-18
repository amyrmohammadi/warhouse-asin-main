namespace Warehouse.Application.Features.MasterData.Products.Commands.ImportProducts;

public sealed class ProductImportSummary
{
    public int ProcessedRowCount { get; init; }

    public int ImportedCount { get; init; }

    public int DuplicateCount { get; init; }

    public int EmptyRowCount { get; init; }

    public IReadOnlyList<ProductRowError> InvalidRows { get; init; } = [];
}

public sealed record ProductRowError(
    int RowNumber,
    string Message);
