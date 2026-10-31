using Warehouse.Domain.Common;
using Warehouse.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Warehouse.Domain.Entities;

public sealed class Invoice : Entity<Guid>
{
    public const string LockedMessage = "این فاکتور به واحد مالی ارسال یا تأیید شده است؛ برای ویرایش فاکتور و ورودی مرتبط، ابتدا با مجوز مربوطه آن را به «در حال رسیدگی» برگردانید.";

    [NotMapped]
    public bool IsLocked => ProcessingStatus != InvoiceProcessingStatus.UnderReview;

    private Invoice()
    {
    }

    public Invoice(string invoiceNumber, Guid? supplierId, DateOnly? receiptDate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        Id = Guid.NewGuid();
        CreatedAtUtc = DateTimeOffset.UtcNow;
        InvoiceNumber = NormalizeNumber(invoiceNumber);
        SupplierId = supplierId;
        ReceiptDate = receiptDate;
    }

    public string InvoiceNumber { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string? Description { get; private set; }

    public decimal? Price { get; private set; }

    public void UpdatePrice(decimal? price)
    {
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
        Price = price;
    }

    public DateOnly? InvoiceDate { get; private set; }
    public DateOnly? FinanceDeliveryDate { get; private set; }

    public void UpdateFinanceDeliveryDate(DateOnly? date) => FinanceDeliveryDate = date;

    public DateOnly? ReceiptDate { get; private set; }

    public static DateOnly GetReceiptDate(DateTimeOffset enteredAtUtc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(enteredAtUtc,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran")).DateTime);

    public InvoiceProcessingStatus ProcessingStatus { get; private set; } = InvoiceProcessingStatus.UnderReview;

    public Guid? SupplierId { get; private set; }

    public Supplier? Supplier { get; private set; }

    public ICollection<GoodsReceiptItem> GoodsReceiptItems { get; private set; } = [];

    public void UpdateDetails(string? description, DateOnly? invoiceDate, InvoiceProcessingStatus processingStatus)
    {
        if (!Enum.IsDefined(processingStatus))
            throw new ArgumentOutOfRangeException(nameof(processingStatus));

        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        InvoiceDate = invoiceDate;
        ProcessingStatus = processingStatus;
    }

    public void UpdateHeader(string invoiceNumber, Guid? supplierId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        InvoiceNumber = NormalizeNumber(invoiceNumber);
        SupplierId = supplierId;
    }

    public static string NormalizeNumber(string value) => value.Trim();
}
