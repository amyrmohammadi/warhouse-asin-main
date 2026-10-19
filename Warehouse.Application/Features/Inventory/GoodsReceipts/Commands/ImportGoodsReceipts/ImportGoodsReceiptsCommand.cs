using MediatR;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;

public sealed record ImportGoodsReceiptsCommand(string FileName, byte[] FileContent,
    bool CanManageReceiptInvoice) : IRequest<Result<GoodsReceiptImportSummary>>
{
    public const int MaxFileSizeBytes = 50 * 1024 * 1024;
}

public sealed record GoodsReceiptImportRowError(int RowNumber, string Message);
public sealed class GoodsReceiptImportSummary
{
    public int ProcessedRowCount { get; init; }
    public int ImportedReceiptCount { get; init; }
    public int ImportedItemCount { get; init; }
    public int CreatedDelivererCount { get; init; }
    public IReadOnlyList<GoodsReceiptImportRowError> InvalidRows { get; init; } = [];
}
