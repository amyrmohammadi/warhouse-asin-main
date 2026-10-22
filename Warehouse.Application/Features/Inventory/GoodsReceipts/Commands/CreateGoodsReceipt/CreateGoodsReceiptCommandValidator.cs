using FluentValidation;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;

internal sealed class CreateGoodsReceiptCommandValidator : AbstractValidator<CreateGoodsReceiptCommand>
{
    public CreateGoodsReceiptCommandValidator()
    {
        RuleFor(command => command.EnteredAtUtc)
            .NotEmpty().WithMessage("تاریخ و ساعت ورود کالا الزامی است.");
        RuleFor(command => command.DelivererFullName)
            .MaximumLength(200)
            .Must(fullName => string.IsNullOrWhiteSpace(fullName) ||
                              TextComparison.CreateCompactKey(fullName).Length > 0)
            .WithMessage("نام تحویل‌دهنده معتبر نیست.");
        RuleFor(command => command)
            .Must(command => command.DelivererId.HasValue ||
                             !string.IsNullOrWhiteSpace(command.DelivererFullName))
            .WithMessage("تحویل‌دهنده را انتخاب کنید یا نام جدید وارد کنید.");
        RuleFor(command => command)
            .Must(command => !command.DelivererId.HasValue ||
                             string.IsNullOrWhiteSpace(command.DelivererFullName))
            .WithMessage("برای تحویل‌دهنده فقط یکی از شناسه یا نام جدید را وارد کنید.");
        RuleFor(command => command.PageNumber)
            .NotEmpty().WithMessage("شماره صفحه الزامی است.")
            .MaximumLength(100).WithMessage("شماره صفحه نباید بیشتر از ۱۰۰ نویسه باشد.");
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.Items)
            .NotEmpty().WithMessage("حداقل یک کالا وارد کنید.");
        RuleForEach(command => command.Items).SetValidator(new CreateGoodsReceiptItemValidator());
        RuleFor(command => command.Items)
            .Must(HaveNoExactDuplicateItems)
            .WithMessage("ردیف‌های کالا نباید کاملاً تکراری باشند؛ حداقل یکی از مشخصات هر ردیف را تغییر دهید.");
        RuleFor(command => command.Items)
            .Must(HaveConsistentNewProductDefinitions)
            .WithMessage("کالاهای جدید با عنوان یا کد یکسان باید عنوان، کد و واحد یکسانی داشته باشند.");
    }

    private static bool HaveNoExactDuplicateItems(
        IReadOnlyCollection<CreateGoodsReceiptItem> items)
    {
        var keys = items.Select(item => new ItemComparisonKey(
            item.ProductId?.ToString() ??
            $"new:{Product.CreateComparisonKey(item.ProductTitle ?? string.Empty)}:" +
            $"{Product.CreateComparisonKey(item.ProductCode ?? string.Empty)}:" +
            $"{item.UnitOfMeasureId}",
            item.WarehouseId,
            item.InvoiceStatus,
            NormalizeRowText(item.InvoiceNumber),
            item.SupplierId?.ToString() ??
            $"new:{NormalizeRowText(item.SupplierTitle)}",
            NormalizeRowText(item.ReceiptNumber),
            item.Quantity,
            NormalizeRowText(item.RequestNumber),
            NormalizeRowText(item.Description)));

        return keys.Distinct().Count() == items.Count;
    }

    private static bool HaveConsistentNewProductDefinitions(
        IReadOnlyCollection<CreateGoodsReceiptItem> items)
    {
        var definitions = items
            .Where(item => !item.ProductId.HasValue)
            .Select(item => new NewProductDefinition(
                Product.CreateComparisonKey(item.ProductTitle ?? string.Empty),
                Product.CreateComparisonKey(item.ProductCode ?? string.Empty),
                item.UnitOfMeasureId))
            .ToArray();

        return definitions
                   .Where(item => item.Code.Length > 0)
                   .GroupBy(item => item.Code)
                   .All(group => group
                       .Select(item => (item.Title, item.UnitOfMeasureId))
                       .Distinct()
                       .Count() == 1) &&
               definitions
                   .Where(item => item.Title.Length > 0)
                   .GroupBy(item => item.Title)
                   .All(group => group
                       .Select(item => (item.Code, item.UnitOfMeasureId))
                       .Distinct()
                       .Count() == 1);
    }

    private static string NormalizeRowText(string? value) =>
        string.Join(' ', (value ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    private sealed record ItemComparisonKey(
        string Product,
        Guid WarehouseId,
        InvoiceStatus? InvoiceStatus,
        string InvoiceNumber,
        string Supplier,
        string ReceiptNumber,
        decimal Quantity,
        string RequestNumber,
        string Description);

    private sealed record NewProductDefinition(
        string Title,
        string Code,
        Guid? UnitOfMeasureId);
}

internal sealed class CreateGoodsReceiptItemValidator : AbstractValidator<CreateGoodsReceiptItem>
{
    public CreateGoodsReceiptItemValidator()
    {
        RuleFor(item => item.WarehouseId)
            .NotEmpty().WithMessage("انتخاب انبار برای هر ردیف کالا الزامی است.");
        RuleFor(item => item.InvoiceNumber).MaximumLength(100);
        RuleFor(item => item.SupplierTitle)
            .MaximumLength(200)
            .When(item => !item.SupplierId.HasValue);
        RuleFor(item => item)
            .Must(item => !item.SupplierId.HasValue || string.IsNullOrWhiteSpace(item.SupplierTitle))
            .WithMessage("برای طرف حساب فقط یکی از شناسه یا عنوان جدید را وارد کنید.");
        RuleFor(item => item.ReceiptNumber).MaximumLength(100);
        RuleFor(item => item.InvoiceStatus).IsInEnum()
            .When(item => item.InvoiceStatus.HasValue);
        RuleFor(item => item.InvoiceNumber)
            .NotEmpty().WithMessage("برای وضعیت دارای فاکتور، شماره فاکتور الزامی است.")
            .When(item => item.InvoiceStatus == InvoiceStatus.HasInvoice);
        RuleFor(item => item.Quantity)
            .GreaterThan(0).WithMessage("مقدار کالا باید بزرگ‌تر از صفر باشد.");
        RuleFor(item => item.ProductTitle)
            .NotEmpty().WithMessage("عنوان کالای جدید الزامی است.")
            .MaximumLength(200)
            .Must(title => Product.CreateComparisonKey(title ?? string.Empty).Length > 0)
            .WithMessage("عنوان کالای جدید معتبر نیست.")
            .When(item => !item.ProductId.HasValue);

        RuleFor(item => item.ProductCode)
            .NotEmpty().WithMessage("کد کالای جدید الزامی است.")
            .MaximumLength(100)
            //.Must(code => Product.CreateComparisonKey(code ?? string.Empty).Length > 0)
            //.WithMessage("کد کالای جدید معتبر نیست.")
            .When(item => !item.ProductId.HasValue);

        RuleFor(item => item.UnitOfMeasureId)
            .NotEmpty().WithMessage("واحد اندازه‌گیری کالای جدید را انتخاب کنید.")
            .When(item => !item.ProductId.HasValue);
        RuleFor(item => item.RequestNumber).MaximumLength(100);
        RuleFor(item => item.Description).MaximumLength(1000);
    }
}
