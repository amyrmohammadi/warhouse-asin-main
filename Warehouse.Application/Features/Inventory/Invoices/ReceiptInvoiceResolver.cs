using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.Invoices;

internal static class ReceiptInvoiceResolver
{
    private static string Key(string number, Guid? supplierId, DateOnly? receiptDate) =>
        $"{number.ToUpperInvariant()}\u001F{supplierId?.ToString("D") ?? "-"}\u001F{receiptDate:yyyy-MM-dd}";

    public static async Task<(Invoice? Invoice, string? Error)> ResolveAsync(
        IUnitOfWork<Guid> unitOfWork, string number, Guid? supplierId, DateOnly receiptDate,
        IDictionary<string, Invoice> cache, CancellationToken token)
    {
        var key = Key(number, supplierId, receiptDate);
        if (cache.TryGetValue(key, out var cached)) return (cached, null);
        var repository = unitOfWork.Repository<Invoice>();
        var matches = await repository.ToListAsync(repository.Entities.Where(invoice =>
                invoice.InvoiceNumber == number && invoice.SupplierId == supplierId &&
                (invoice.ReceiptDate == receiptDate ||
                 (invoice.ReceiptDate == null && !invoice.GoodsReceiptItems.Any())))
            .OrderByDescending(invoice => invoice.ReceiptDate.HasValue).Take(1), token);
        Invoice invoice;
        if (matches.Count > 0)
        {
            var tracked = await repository.GetByIdAsync(matches[0].Id, token);
            if (tracked is null) return (null, "فاکتور انتخاب‌شده پیدا نشد.");
            if (tracked.IsLocked) return (null, Invoice.LockedMessage);
            invoice = tracked;
            if (invoice.ReceiptDate is null) invoice.AssignReceiptDate(receiptDate);
        }
        else
        {
            invoice = new Invoice(number, supplierId, receiptDate);
            await repository.AddAsync(invoice, token);
        }
        cache[key] = invoice;
        return (invoice, null);
    }

    // Only invoice shells detached by this edit can be removed. Standalone invoices and
    // invoices with accounting data remain available for an explicit review.
    public static async Task RemoveUnusedShellsAsync(IUnitOfWork<Guid> unitOfWork, Guid[] previousIds, CancellationToken token)
    {
        if (previousIds.Length == 0) return;
        var items = unitOfWork.Repository<GoodsReceiptItem>();
        var usedIds = await items.ToListAsync(items.Entities
            .Where(item => item.InvoiceId.HasValue && previousIds.Contains(item.InvoiceId.Value))
            .Select(item => item.InvoiceId!.Value).Distinct(), token);
        var repository = unitOfWork.Repository<Invoice>();
        var shells = await repository.ToListAsync(repository.Entities.Where(invoice =>
            previousIds.Contains(invoice.Id) && !usedIds.Contains(invoice.Id) && invoice.ReceiptDate != null &&
            invoice.ProcessingStatus == Domain.Enums.InvoiceProcessingStatus.UnderReview &&
            invoice.Price == null && invoice.InvoiceDate == null && invoice.FinanceDeliveryDate == null &&
            (invoice.Description == null || invoice.Description == "")), token);
        foreach (var shell in shells)
        {
            var tracked = await repository.GetByIdAsync(shell.Id, token);
            if (tracked is not null) await repository.DeleteAsync(tracked, token);
        }
        if (shells.Count > 0) await unitOfWork.SaveAsync(token);
    }
}
