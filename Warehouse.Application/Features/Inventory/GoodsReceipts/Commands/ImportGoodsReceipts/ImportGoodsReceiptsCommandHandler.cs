
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;

internal sealed class ImportGoodsReceiptsCommandHandler(
    IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<ImportGoodsReceiptsCommand, Result<GoodsReceiptImportSummary>>
{
    public async Task<Result<GoodsReceiptImportSummary>> Handle(
        ImportGoodsReceiptsCommand request,
        CancellationToken token)
    {
        List<GoodsReceiptExcelReader.Row> rows;

        try
        {
            rows = GoodsReceiptExcelReader.Read(
                request.FileContent,
                token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<GoodsReceiptImportSummary>.Error(
                "فایل Excel معتبر نیست یا امکان خواندن آن وجود ندارد.");
        }

        if (rows.Count == 0)
        {
            return Result<GoodsReceiptImportSummary>.Error(
                "هیچ ردیف ورودی کالا در فایل پیدا نشد.");
        }

        // اعتبارسنجی تمام ردیف‌ها
        var validation =
            await GoodsReceiptImportValidation.ValidateAsync(
                rows,
                unitOfWork,
                request.CanManageInvoice,
                request.CanManageReceipt,
                token);

        // در صورت وجود خطا، هیچ اطلاعاتی ثبت نمی‌شود
        if (validation.Errors.Count > 0)
        {
            return Result<GoodsReceiptImportSummary>.Success(
                new GoodsReceiptImportSummary
                {
                    ProcessedRowCount = rows.Count,

                    InvalidRows = validation.Errors
                        .GroupBy(error => error.RowNumber)
                        .OrderBy(group => group.Key)
                        .Select(group =>
                            new GoodsReceiptImportRowError(
                                group.Key,
                                string.Join(
                                    " ",
                                    group.Select(e => e.Message))))
                        .ToArray()
                });
        }

        // اشخاص موجود بر اساس نام
        var peopleByName = validation.People
            .GroupBy(p =>
                TextComparison.CreateCompactKey(p.FullName))
            .ToDictionary(
                g => g.Key,
                g => g.First());

        var newPeople = 0;
        var importedReceipts = 0;
        var importedItems = 0;

        var invoicesByNumberAndSupplier = new Dictionary<string, Invoice>(StringComparer.Ordinal);

        await unitOfWork.BeginSerializableTransaction(token);

        try
        {
            var requestedPageNumbers = validation.Rows
                .Select(row => row.Source.PageNumber.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var receiptRepository = unitOfWork.Repository<GoodsReceipt>();
            if (await receiptRepository.CountAsync(
                    receiptRepository.Entities.Where(receipt =>
                        requestedPageNumbers.Contains(receipt.PageNumber)),
                    token) > 0)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<GoodsReceiptImportSummary>.Error(
                    "یک یا چند شماره صفحه هم‌زمان توسط کاربر دیگری ثبت شده است. " +
                    "شماره صفحه را در فهرست ورودی کالا پیدا کنید و کالا را به همان سند اضافه کنید.");
            }

            // هر ردیف یک سند مستقل
            foreach (var row in validation.Rows)
            {
                token.ThrowIfCancellationRequested();

                // یافتن یا ایجاد شخص تحویل‌دهنده
                if (!peopleByName.TryGetValue(
                        row.DelivererKey,
                        out var person))
                {
                    person = new Person(
                        row.Source.Deliverer,
                        string.Empty);

                    await unitOfWork
                        .Repository<Person>()
                        .AddAsync(person, token);

                    peopleByName.Add(
                        row.DelivererKey,
                        person);

                    newPeople++;
                }

                // شماره درخواست اختیاری
                var requestNumber =
                    row.Source.RequestNumber?.Trim();

                if (string.IsNullOrWhiteSpace(requestNumber))
                {
                    requestNumber = null;
                }

                // ایجاد سند مستقل برای همین ردیف
                var receipt = new GoodsReceipt(
                    row.Source.Date!.Value,
                    requestNumber,
                    person.Id,
                    row.Source.PageNumber
                );

                Invoice? invoice = null;
                if (row.InvoiceStatus == Domain.Enums.InvoiceStatus.HasInvoice)
                {
                    var invoiceNumber = Invoice.NormalizeNumber(row.Source.InvoiceNumber);
                    var receiptDate = Invoice.GetReceiptDate(row.Source.Date!.Value);
                    var resolved = await Invoices.ReceiptInvoiceResolver.ResolveAsync(unitOfWork,
                        invoiceNumber, row.SupplierId, receiptDate, invoicesByNumberAndSupplier, token);
                    if (resolved.Error is not null)
                    {
                        await unitOfWork.RollBackTransaction(token);
                        return Result<GoodsReceiptImportSummary>.Error($"فاکتور «{invoiceNumber}»: {resolved.Error}");
                    }
                    invoice = resolved.Invoice!;
                    if (invoice.IsLocked)
                    {
                        await unitOfWork.RollBackTransaction(token);
                        return Result<GoodsReceiptImportSummary>.Error($"فاکتور «{invoice.InvoiceNumber}»: {Invoice.LockedMessage}");
                    }
                }

                // فقط یک آیتم برای هر سند
                receipt.AddItem(
                    row.ProductId,
                    row.WarehouseId,
                    row.InvoiceStatus,
                    invoice,
                    request.CanManageReceipt ? row.Source.ReceiptNumber : null,
                    row.Source.Quantity!.Value,
                    null,
                    row.Source.Description,
                    0
                );
                if (row.InvoiceStatus != Domain.Enums.InvoiceStatus.HasInvoice && row.SupplierId.HasValue)
                    receipt.Items.Last().SetSupplier(await unitOfWork.Repository<Supplier>()
                        .GetByIdAsync(row.SupplierId.Value, token));

                await unitOfWork
                    .Repository<GoodsReceipt>()
                    .AddAsync(receipt, token);

                importedReceipts++;
                importedItems++;
            }

            await unitOfWork.SaveAsync(token);
            await unitOfWork.CommitTransaction(token);
        }
        catch
        {
            await unitOfWork.RollBackTransaction(
                CancellationToken.None);

            throw;
        }

        return Result<GoodsReceiptImportSummary>.Success(
            new GoodsReceiptImportSummary
            {
                ProcessedRowCount = rows.Count,
                ImportedReceiptCount = importedReceipts,
                ImportedItemCount = importedItems,
                CreatedDelivererCount = newPeople
            });
    }

}
