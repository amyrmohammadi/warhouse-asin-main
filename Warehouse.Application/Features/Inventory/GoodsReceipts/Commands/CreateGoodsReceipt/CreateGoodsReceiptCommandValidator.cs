using FluentValidation;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;

internal sealed class CreateGoodsReceiptCommandValidator
    : AbstractValidator<CreateGoodsReceiptCommand>
{
    public CreateGoodsReceiptCommandValidator()
    {
        RuleFor(command => command.EnteredAtUtc)
            .NotEmpty().WithMessage("تاریخ و ساعت ورود کالا الزامی است.");
        RuleFor(command => command.DelivererFullName)
            .MaximumLength(200)
            .Must(fullName =>
                string.IsNullOrWhiteSpace(fullName) ||
                TextComparison.CreateCompactKey(fullName).Length > 0)
            .WithMessage("نام تحویل‌دهنده باید حداقل یک نویسه غیر از فاصله و خط تیره داشته باشد.");
        RuleFor(command => command)
            .Must(command =>
                command.DelivererId.HasValue ||
                !string.IsNullOrWhiteSpace(command.DelivererFullName))
            .WithMessage("تحویل‌دهنده را انتخاب کنید یا نام جدید وارد کنید.");
        RuleFor(command => command)
            .Must(command =>
                !command.DelivererId.HasValue ||
                string.IsNullOrWhiteSpace(command.DelivererFullName))
            .WithMessage("برای تحویل‌دهنده، فرد را انتخاب کنید یا نام جدید وارد کنید.");
        RuleFor(command => command.WarehouseId)
            .NotEmpty().WithMessage("انتخاب انبار الزامی است.");
        RuleFor(command => command.PageNumber)
            .NotEmpty().WithMessage("شماره صفحه الزامی است.")
            .MaximumLength(100).WithMessage("شماره صفحه نباید بیشتر از ۱۰۰ نویسه باشد.");
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.InvoiceNumber).MaximumLength(100);
        RuleFor(command => command.SupplierTitle)
            .MaximumLength(200)
            .When(command => !command.SupplierId.HasValue);
        RuleFor(command => command)
            .Must(command =>
                !command.SupplierId.HasValue ||
                string.IsNullOrWhiteSpace(command.SupplierTitle))
            .WithMessage("برای طرف حساب، تأمین‌کننده را انتخاب کنید یا عنوان جدید وارد کنید.");
        RuleFor(command => command.ReceiptNumber).MaximumLength(100);
        RuleFor(command => command.InvoiceStatus).IsInEnum();
        RuleFor(command => command.InvoiceNumber)
            .NotEmpty()
            .WithMessage("برای وضعیت دارای فاکتور، شماره فاکتور الزامی است.")
            .When(command => command.InvoiceStatus == InvoiceStatus.HasInvoice);
        RuleFor(command => command.Items)
            .NotEmpty().WithMessage("حداقل یک کالا وارد کنید.");
        RuleForEach(command => command.Items)
            .SetValidator(new CreateGoodsReceiptItemValidator());
        RuleFor(command => command.Items)
            .Must(HaveUniqueExistingProducts)
            .WithMessage("هر کالا فقط یک بار می‌تواند در سند ورود ثبت شود.");
        RuleFor(command => command.Items)
            .Must(HaveUniqueNewProducts)
            .WithMessage("عنوان و کد کالاهای جدید در فرم نباید تکراری باشند.");
    }

    private static bool HaveUniqueExistingProducts(
        IReadOnlyCollection<CreateGoodsReceiptItem> items) =>
        items.Where(item => item.ProductId.HasValue)
            .Select(item => item.ProductId!.Value)
            .Distinct()
            .Count() == items.Count(item => item.ProductId.HasValue);

    private static bool HaveUniqueNewProducts(
        IReadOnlyCollection<CreateGoodsReceiptItem> items)
    {
        var titles = items
            .Where(item => !item.ProductId.HasValue)
            .Select(item => Product.CreateComparisonKey(item.ProductTitle ?? string.Empty))
            .Where(title => title.Length > 0)
            .ToArray();
        var codes = items
            .Where(item => !item.ProductId.HasValue)
            .Select(item => Product.CreateComparisonKey(item.ProductCode ?? string.Empty))
            .Where(code => code.Length > 0)
            .ToArray();
        return titles.Distinct(StringComparer.Ordinal).Count() == titles.Length &&
               codes.Distinct(StringComparer.Ordinal).Count() == codes.Length;
    }
}

internal sealed class CreateGoodsReceiptItemValidator
    : AbstractValidator<CreateGoodsReceiptItem>
{
    public CreateGoodsReceiptItemValidator()
    {
        RuleFor(item => item.Quantity)
            .GreaterThan(0).WithMessage("مقدار کالا باید بزرگ‌تر از صفر باشد.");
        RuleFor(item => item.ProductTitle)
            .NotEmpty().WithMessage("عنوان کالای جدید الزامی است.")
            .MaximumLength(200)
            .Must(title => Product.CreateComparisonKey(title ?? string.Empty).Length > 0)
            .WithMessage("عنوان کالای جدید باید حداقل یک نویسه غیر از فاصله و خط تیره داشته باشد.")
            .When(item => !item.ProductId.HasValue);
        RuleFor(item => item.ProductCode)
            .NotEmpty().WithMessage("کد کالای جدید الزامی است.")
            .MaximumLength(100)
            .Must(code => Product.CreateComparisonKey(code ?? string.Empty).Length > 0)
            .WithMessage("کد کالای جدید باید حداقل یک نویسه غیر از فاصله و خط تیره داشته باشد.")
            .When(item => !item.ProductId.HasValue);
        RuleFor(item => item.UnitOfMeasureId)
            .NotEmpty().WithMessage("واحد اندازه‌گیری کالای جدید را از فهرست انتخاب کنید.")
            .When(item => !item.ProductId.HasValue);
        RuleFor(item => item.RequestNumber).MaximumLength(100);
        RuleFor(item => item.Description).MaximumLength(1000);
    }
}
