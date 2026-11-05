-- Read-only production diagnostic. No records are changed or merged.
SET NOCOUNT ON;

WITH InvoiceUsage AS
(
    SELECT invoice.Id, invoice.InvoiceNumber, invoice.SupplierId, invoice.ReceiptDate,
           invoice.ProcessingStatus, invoice.CreatedAtUtc,
           COUNT(item.Id) AS ItemCount,
           CASE WHEN invoice.Price IS NOT NULL OR invoice.InvoiceDate IS NOT NULL
                     OR invoice.FinanceDeliveryDate IS NOT NULL
                     OR NULLIF(LTRIM(RTRIM(invoice.Description)), '') IS NOT NULL
                THEN 1 ELSE 0 END AS HasAccountingDetails
    FROM Business.Invoices AS invoice
    LEFT JOIN Business.GoodsReceiptItems AS item ON item.InvoiceId = invoice.Id
    GROUP BY invoice.Id, invoice.InvoiceNumber, invoice.SupplierId, invoice.ReceiptDate,
             invoice.ProcessingStatus, invoice.CreatedAtUtc, invoice.Price,
             invoice.InvoiceDate, invoice.FinanceDeliveryDate, invoice.Description
)
SELECT currentInvoice.*,
       CASE WHEN currentInvoice.ItemCount = 0 THEN 'Empty invoice' ELSE 'Invoice with items' END AS Usage,
       CASE WHEN currentInvoice.ItemCount = 0 AND currentInvoice.ReceiptDate IS NOT NULL
                 AND currentInvoice.ProcessingStatus = 'UnderReview'
                 AND currentInvoice.HasAccountingDetails = 0
            THEN 'Review unused receipt invoice shell'
            ELSE 'Preserve and review identity, receipt day and financial details' END AS ReviewAction
FROM InvoiceUsage AS currentInvoice
WHERE EXISTS
(
    SELECT 1 FROM InvoiceUsage AS otherInvoice
    WHERE otherInvoice.Id <> currentInvoice.Id
      AND otherInvoice.InvoiceNumber = currentInvoice.InvoiceNumber
      AND (otherInvoice.SupplierId = currentInvoice.SupplierId
           OR (otherInvoice.SupplierId IS NULL AND currentInvoice.SupplierId IS NULL))
)
ORDER BY currentInvoice.InvoiceNumber, currentInvoice.SupplierId,
         currentInvoice.ReceiptDate, currentInvoice.Id;

-- A receipt invoice must contain only entries from its designated Tehran calendar day.
SELECT invoice.Id AS InvoiceId, invoice.InvoiceNumber, invoice.ReceiptDate,
       item.GoodsReceiptId, receipt.EnteredAtUtc
FROM Business.Invoices AS invoice
JOIN Business.GoodsReceiptItems AS item ON item.InvoiceId = invoice.Id
JOIN Business.GoodsReceipts AS receipt ON receipt.Id = item.GoodsReceiptId
WHERE invoice.ReceiptDate IS NOT NULL
  AND invoice.ReceiptDate <> CONVERT(date, SWITCHOFFSET(receipt.EnteredAtUtc, '+03:30'))
ORDER BY invoice.InvoiceNumber, item.GoodsReceiptId;
