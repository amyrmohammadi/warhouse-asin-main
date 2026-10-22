
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
                request.CanManageReceiptInvoice,
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

        await unitOfWork.BeginTransaction(token);

        try
        {
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

                // فقط یک آیتم برای هر سند
                receipt.AddItem(
                    row.ProductId,
                    row.WarehouseId,
                    row.InvoiceStatus,
                    row.Source.InvoiceNumber,
                    row.SupplierId,
                    row.Source.ReceiptNumber,
                    row.Source.Quantity!.Value,
                    null,
                    row.Source.Description,
                    0
                );

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
