using System.Text.Json.Serialization;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Common.Models.Inventory;

public sealed class InvoiceSummary
{
    public decimal? Price { get; init; }

    public Guid Id { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateOnly? InvoiceDate { get; init; }
    public DateOnly? FinanceDeliveryDate { get; init; }

    public InvoiceProcessingStatus ProcessingStatus { get; init; }

    public string? Counterparty { get; init; }

    public IReadOnlyCollection<string> ReceiptNumbers { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ItemsWithoutReceiptCount { get; init; }
}

public sealed class InvoiceItemSummary
{
    public decimal? Price { get; init; }

    public Guid Id { get; init; }

    public Guid GoodsReceiptId { get; init; }

    public Guid? GoodsIssueItemId { get; init; }

    public Guid GoodsReceiptConcurrencyStamp { get; init; }

    public string ProductTitle { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public string UnitOfMeasureTitle { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public string WarehouseTitle { get; init; } = string.Empty;

    public DateTimeOffset EnteredAtUtc { get; init; }

    public string PageNumber { get; init; } = string.Empty;

    public string? ReceiptNumber { get; init; }

    public string? Description { get; init; }
}

public sealed class InvoiceDetails
{
    public decimal? Price { get; init; }

    public Guid Id { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateOnly? InvoiceDate { get; init; }
    public DateOnly? FinanceDeliveryDate { get; init; }

    public InvoiceProcessingStatus ProcessingStatus { get; init; }

    public string? Counterparty { get; init; }

    public Guid? SupplierId { get; init; }

    public IReadOnlyCollection<InvoiceItemSummary> Items { get; init; } = [];
}

public sealed record InvoiceListResult(
    IReadOnlyCollection<InvoiceSummary> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize);
