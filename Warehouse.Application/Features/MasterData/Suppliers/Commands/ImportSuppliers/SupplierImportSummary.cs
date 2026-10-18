namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.ImportSuppliers;

public sealed class SupplierImportSummary
{
    public int ProcessedRowCount { get; init; }

    public int ImportedCount { get; init; }

    public int DuplicateCount { get; init; }

    public int EmptyRowCount { get; init; }

    public IReadOnlyList<SupplierImportRowError> InvalidRows { get; init; } = [];
}

public sealed record SupplierImportRowError(
    int RowNumber,
    string Message);
