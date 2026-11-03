using Warehouse.Application.Common.Models.Inventory;

namespace Warehouse.Application.Common.Interfaces;

public interface IGoodsReceiptListPdfService
{
    Task<byte[]> GenerateItemsAsync(IReadOnlyList<GoodsReceiptItemRowSummary> items,
        IReadOnlyList<string> columns, CancellationToken cancellationToken = default);

    Task<byte[]> GenerateReceiptsAsync(IReadOnlyList<GoodsReceiptSummary> receipts,
        IReadOnlyList<string> columns, CancellationToken cancellationToken = default);
}
