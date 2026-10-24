using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;

internal static class GoodsReceiptImportValidation
{
    internal sealed record ValidRow(GoodsReceiptExcelReader.Row Source, Guid ProductId, Guid WarehouseId,
        Guid? SupplierId, InvoiceStatus? InvoiceStatus, string DelivererKey);
    internal sealed record ValidationResult(List<ValidRow> Rows, List<GoodsReceiptImportRowError> Errors,
        IReadOnlyList<Person> People);

    public static async Task<ValidationResult> ValidateAsync(List<GoodsReceiptExcelReader.Row> rows,
        IUnitOfWork<Guid> unitOfWork, bool canManageInvoice, CancellationToken token)
    {
        var products = await unitOfWork.Repository<Product>().GetAllAsync(token);
        var units = await unitOfWork.Repository<UnitOfMeasure>().GetAllAsync(token);
        var warehouses = await unitOfWork.Repository<StorageWarehouse>().GetAllAsync(token);
        var suppliers = await unitOfWork.Repository<Supplier>().GetAllAsync(token);
        var people = await unitOfWork.Repository<Person>().GetAllAsync(token);
        var valid = new List<ValidRow>();
        var errors = new List<GoodsReceiptImportRowError>();
        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested();
            var messages = new List<string>();
            if (row.Date is null) messages.Add("تاریخ معتبر نیست؛ تاریخ شمسی یا میلادی با قالب سال/ماه/روز وارد کنید.");
            if (row.Quantity is null or <= 0) messages.Add("تعداد باید عددی بزرگ‌تر از صفر باشد.");
            else if (row.Quantity >= 1000000000000000m || decimal.Round(row.Quantity.Value, 3) != row.Quantity.Value)
                messages.Add("تعداد باید حداکثر ۱۵ رقم صحیح و ۳ رقم اعشار داشته باشد.");
            if (row.Code.Length == 0) messages.Add("کد کالا خالی است.");
            if (row.Deliverer.Length > 200 || TextComparison.CreateCompactKey(row.Deliverer).Length == 0)
                messages.Add("نام تحویل‌دهنده الزامی است و نباید بیشتر از ۲۰۰ نویسه باشد.");
            if (row.PageNumber.Length is 0 or > 100) messages.Add("شماره صفحه الزامی است و نباید بیشتر از ۱۰۰ نویسه باشد.");
            if (row.Description.Length > 1000) messages.Add("توضیحات نباید بیشتر از ۱۰۰۰ نویسه باشد.");
            if (row.InvoiceNumber.Length > 100 || row.ReceiptNumber.Length > 100)
                messages.Add("شماره رسید و شماره فاکتور نباید بیشتر از ۱۰۰ نویسه باشند.");

            var matchingUnits = units.Where(u => Key(u.Title) == Key(row.Unit)).ToArray();
            if (matchingUnits.Length != 1)
                messages.Add($"واحد «{row.Unit}» باید از قبل ثبت شده و قابل تشخیص باشد.");
            var matchingWarehouses = warehouses.Where(w => Key(w.Title) == Key(row.Warehouse)).ToArray();
            if (matchingWarehouses.Length != 1)
                messages.Add($"انبار «{row.Warehouse}» باید از قبل ثبت شده و قابل تشخیص باشد.");
            var matchingSuppliers = suppliers.Where(s => Key(s.Title) == Key(row.Supplier)).ToArray();
            if (row.Supplier.Length > 0 && matchingSuppliers.Length != 1)
                messages.Add($"طرف حساب «{row.Supplier}» باید از قبل ثبت شده و قابل تشخیص باشد.");

            var candidates = products.Where(p => Product.IsPlaceholderCode(row.Code)
                ? Product.IsPlaceholderCode(p.Code) && Key(p.Title) == Key(row.Title)
                : Product.CreateComparisonKey(p.Code) == Product.CreateComparisonKey(row.Code)).ToArray();
            if (Product.IsPlaceholderCode(row.Code) && matchingUnits.Length == 1)
                candidates = candidates.Where(p => p.UnitOfMeasureId == matchingUnits[0].Id).ToArray();
            if (candidates.Length != 1)
                messages.Add($"کالای «{row.Title}» با کد «{row.Code}» در مفروضات پیدا نشد یا بیش از یک تطبیق دارد.");
            else
            {
                if (Key(candidates[0].Title) != Key(row.Title)) messages.Add("عنوان کالا با کالای ثبت‌شده برای این کد مطابقت ندارد.");
                if (matchingUnits.Length == 1 && candidates[0].UnitOfMeasureId != matchingUnits[0].Id)
                    messages.Add("واحد واردشده با واحد ثبت‌شدهٔ کالا مطابقت ندارد.");
            }
            InvoiceStatus? invoice = row.Invoice.Trim().ToLowerInvariant() switch
            {
                "دارد" or "hasinvoice" or "1" or "۱"
                    or "✓" or "✔" or "☑" or "✅" or "P"
                    => InvoiceStatus.HasInvoice,

                "ندارد" or "doesnothave" or "0" or "۰"
                    or "✗" or "✘" or "×" or "❌" or "✕" or "O"
                    => InvoiceStatus.DoesNotHave,

                _ => null
            };
            if (invoice is null && !string.IsNullOrWhiteSpace(row.Invoice)) messages.Add("وضعیت فاکتور باید «دارد» یا «ندارد» باشد.");
            if (invoice == InvoiceStatus.HasInvoice && row.InvoiceNumber.Length == 0)
                messages.Add("برای وضعیت دارای فاکتور، شماره فاکتور الزامی است.");
            //if (invoice == InvoiceStatus.DoesNotHave && row.InvoiceNumber.Length > 0)
            //    messages.Add("وضعیت فاکتور «ندارد» است اما شماره فاکتور وارد شده است.");
            if (!canManageInvoice && (invoice == InvoiceStatus.HasInvoice || row.InvoiceNumber.Length > 0 || row.Supplier.Length > 0 || row.ReceiptNumber.Length > 0))
                messages.Add("مجوز ثبت اطلاعات رسید و فاکتور را ندارید.");
            if (messages.Count > 0) errors.Add(new GoodsReceiptImportRowError(row.Number, string.Join(" ", messages)));
            else valid.Add(new ValidRow(row, candidates[0].Id, matchingWarehouses[0].Id,
                row.Supplier.Length > 0 ? matchingSuppliers[0].Id : null, invoice,
                TextComparison.CreateCompactKey(row.Deliverer)));
        }
        return new ValidationResult(valid, errors, people);
    }

    private static string Key(string value) => string.Join(' ', value.Trim().Replace('ي', 'ی').Replace('ك', 'ک')
        .Replace('\u200C', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
