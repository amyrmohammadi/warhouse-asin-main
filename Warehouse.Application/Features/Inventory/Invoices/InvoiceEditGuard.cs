using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;

namespace Warehouse.Application.Features.Inventory.Invoices;

internal static class InvoiceEditGuard
{
    // Prefer stable row IDs; retain product-order matching for older clients.
    public static GoodsReceiptItem?[] MatchItems(IReadOnlyCollection<CreateGoodsReceiptItem> requested,
        IReadOnlyList<GoodsReceiptItem> stored)
    {
        var explicitIds = requested.Where(item => item.Id.HasValue).Select(item => item.Id!.Value).ToHashSet();
        var available = stored.Where(item => !explicitIds.Contains(item.Id)).GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => new Queue<GoodsReceiptItem>(group));
        return requested.Select(item => item.Id is Guid id
            ? stored.FirstOrDefault(row => row.Id == id)
            : item.ProductId is Guid productId && available.TryGetValue(productId, out var rows) && rows.Count > 0
                ? rows.Dequeue() : null).ToArray();
    }

    public static async Task<bool> CanUpdateReceiptAsync(IUnitOfWork<Guid> unitOfWork, GoodsReceipt receipt,
        CreateGoodsReceiptCommand values, IReadOnlyList<GoodsReceiptItem> stored,
        bool updateInitial, bool updateInvoice, bool updateReceipt, bool updateWarehouse, CancellationToken token)
    {
        var invoiceIds = stored.Where(item => item.InvoiceId.HasValue).Select(item => item.InvoiceId!.Value).ToArray();
        var invoices = unitOfWork.Repository<Invoice>();
        var locked = await invoices.ToListAsync(invoices.Entities.Where(invoice => invoiceIds.Contains(invoice.Id) &&
            invoice.ProcessingStatus != InvoiceProcessingStatus.UnderReview), token);
        if (locked.Count == 0) return true;
        // A shared entry date also determines the invoice's receipt day.
        if (updateInitial && receipt.EnteredAtUtc != values.EnteredAtUtc) return false;
        var matches = MatchItems(values.Items, stored);
        var requested = values.Items.ToArray();
        var issueRepository = unitOfWork.Repository<GoodsIssueItem>();
        var issueIds = stored.Where(item => item.GoodsIssueItemId.HasValue).Select(item => item.GoodsIssueItemId!.Value).ToArray();
        var issues = await issueRepository.ToListAsync(issueRepository.Entities.Where(item => issueIds.Contains(item.Id))
            .Select(item => new { item.Id, item.GoodsIssue.IssueNumber }), token);
        foreach (var item in stored.Where(item => locked.Any(invoice => invoice.Id == item.InvoiceId)))
        {
            var index = Array.FindIndex(matches, match => match?.Id == item.Id);
            if (index < 0) return false;
            var source = requested[index];
            var invoice = locked.Single(invoice => invoice.Id == item.InvoiceId);
            if (source.ProductId != item.ProductId ||
                (updateWarehouse && source.WarehouseId != item.WarehouseId) ||
                (updateReceipt && Normalize(source.ReceiptNumber) != item.ReceiptNumber) ||
                (updateInvoice && (source.InvoiceStatus != item.InvoiceStatus ||
                    Normalize(source.InvoiceNumber) != invoice.InvoiceNumber || source.SupplierId != invoice.SupplierId ||
                    !string.IsNullOrWhiteSpace(source.SupplierTitle))) ||
                (updateInitial && (source.Quantity != item.Quantity || Normalize(source.RequestNumber) != item.RequestNumber ||
                    Normalize(source.Description) != item.Description ||
                    Normalize(source.IssueNumber) != issues.FirstOrDefault(issue => issue.Id == item.GoodsIssueItemId)?.IssueNumber)))
                return false;
        }
        return true;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static async Task<bool> IsItemLockedAsync(IUnitOfWork<Guid> unitOfWork, Guid itemId, CancellationToken token)
    {
        var items = unitOfWork.Repository<GoodsReceiptItem>();
        return await items.CountAsync(items.Entities.Where(item => item.Id == itemId &&
            item.Invoice != null && item.Invoice.ProcessingStatus != InvoiceProcessingStatus.UnderReview), token) > 0;
    }
    public static async Task<bool> IsReceiptLockedAsync(IUnitOfWork<Guid> unitOfWork, Guid receiptId, CancellationToken token)
    {
        var items = unitOfWork.Repository<GoodsReceiptItem>();
        return await items.CountAsync(items.Entities.Where(item => item.GoodsReceiptId == receiptId &&
            item.Invoice != null && item.Invoice.ProcessingStatus != InvoiceProcessingStatus.UnderReview), token) > 0;
    }
}
