namespace Warehouse.Application.Common.Interfaces;

public sealed record InvoicePrintEntry(string InvoiceNumber, string Counterparty, DateOnly? FinanceDeliveryDate);

public interface IInvoicePdfService
{
    Task<byte[]> GenerateAsync(IReadOnlyList<InvoicePrintEntry> invoices, CancellationToken cancellationToken = default);
}
