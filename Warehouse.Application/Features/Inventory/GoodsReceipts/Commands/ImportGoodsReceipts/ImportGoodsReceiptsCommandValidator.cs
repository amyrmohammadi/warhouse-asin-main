using FluentValidation;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;

internal sealed class ImportGoodsReceiptsCommandValidator : AbstractValidator<ImportGoodsReceiptsCommand>
{
    public ImportGoodsReceiptsCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().Must(name =>
            !string.IsNullOrWhiteSpace(name) && new[] { ".xlsx", ".xlsm" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            .WithMessage("فایل Excel با پسوند xlsx یا xlsm انتخاب کنید.");
        RuleFor(x => x.FileContent).NotEmpty().WithMessage("فایل خالی است.")
            .Must(bytes => bytes is null || bytes.Length <= ImportGoodsReceiptsCommand.MaxFileSizeBytes)
            .WithMessage("حجم فایل نباید بیشتر از ۵۰ مگابایت باشد.");
    }
}


