using FluentValidation;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Commands.ImportGoodsIssues;

internal sealed class ImportGoodsIssuesCommandValidator : AbstractValidator<ImportGoodsIssuesCommand>
{
    public ImportGoodsIssuesCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().Must(name =>
                new[] { ".xlsx", ".xlsm" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            .WithMessage("فایل Excel با پسوند xlsx یا xlsm انتخاب کنید.");
        RuleFor(x => x.FileContent).NotEmpty().WithMessage("فایل خالی است.")
            .Must(bytes => bytes is null || bytes.Length <= ImportGoodsIssuesCommand.MaxFileSizeBytes)
            .WithMessage("حجم فایل نباید بیشتر از ۵۰ مگابایت باشد.");
    }
}
