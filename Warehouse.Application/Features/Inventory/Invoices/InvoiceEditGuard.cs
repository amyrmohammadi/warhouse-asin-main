using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.Invoices;

internal static class InvoiceEditGuard
{
    public static async Task<bool> IsReceiptLockedAsync(IUnitOfWork<Guid> unitOfWork, Guid receiptId, CancellationToken token)
    {
        var items = unitOfWork.Repository<GoodsReceiptItem>();
        return await items.CountAsync(items.Entities.Where(item => item.GoodsReceiptId == receiptId &&
            item.Invoice != null && item.Invoice.ProcessingStatus != InvoiceProcessingStatus.UnderReview), token) > 0;
    }
}
