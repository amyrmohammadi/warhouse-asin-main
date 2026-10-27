using Warehouse.Domain.Enums;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Common.Models.Inventory;

public sealed class GoodsReceiptSummary
{
    public Guid Id { get; init; }

    public Guid ConcurrencyStamp { get; init; }

    public DateTimeOffset EnteredAtUtc { get; init; }

    public string? Description { get; init; }

    public Guid DelivererId { get; init; }

    public string DelivererFullName { get; init; } = string.Empty;

    public string PageNumber { get; init; } = string.Empty;

    public IReadOnlyCollection<GoodsReceiptItemSummary> Items { get; init; } = [];
}

public sealed class GoodsReceiptItemSummary
{
    public Guid Id { get; init; }

    public Guid? GoodsIssueItemId { get; init; }

    public string? IssueNumber { get; init; }

    public Guid ProductId { get; init; }

    public string ProductTitle { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public Guid UnitOfMeasureId { get; init; }

    public string UnitOfMeasureTitle { get; init; } = string.Empty;

    public Guid WarehouseId { get; init; }

    public string WarehouseTitle { get; init; } = string.Empty;

    public InvoiceStatus? InvoiceStatus { get; init; }

    public string? InvoiceNumber { get; init; }

    public Guid? SupplierId { get; init; }

    public string? Counterparty { get; init; }

    public string? ReceiptNumber { get; init; }

    public decimal Quantity { get; init; }

    public string? RequestNumber { get; init; }

    public string? Description { get; init; }
}

public sealed record GoodsReceiptListResult(
    IReadOnlyCollection<GoodsReceiptSummary> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize);

public sealed class GoodsReceiptItemRowSummary
{
    public Guid Id { get; init; }

    public Guid? GoodsIssueItemId { get; init; }

    public string? IssueNumber { get; init; }

    public Guid GoodsReceiptId { get; init; }

    public Guid ProductId { get; init; }

    public string ProductTitle { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public string UnitOfMeasureTitle { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public string? RequestNumber { get; init; }

    public string? ItemDescription { get; init; }

    public DateTimeOffset EnteredAtUtc { get; init; }

    public string? ReceiptDescription { get; init; }

    public string DelivererFullName { get; init; } = string.Empty;

    public string WarehouseTitle { get; init; } = string.Empty;

    public InvoiceStatus? InvoiceStatus { get; init; }

    public string? InvoiceNumber { get; init; }

    public Guid? SupplierId { get; init; }

    public string? Counterparty { get; init; }

    public string? ReceiptNumber { get; init; }

    public string PageNumber { get; init; } = string.Empty;
}

public sealed record GoodsReceiptItemRowListResult(
    IReadOnlyCollection<GoodsReceiptItemRowSummary> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize);

public sealed class GoodsReceiptOptions
{
    public IReadOnlyCollection<PersonSummary> People { get; init; } = [];

    public IReadOnlyCollection<TitledMasterDataSummary> Warehouses { get; init; } = [];

    public IReadOnlyCollection<TitledMasterDataSummary> UnitsOfMeasure { get; init; } = [];
}
