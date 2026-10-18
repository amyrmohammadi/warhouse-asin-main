using FluentValidation;

namespace Warehouse.Application.Features.MasterData.Products.Commands.ImportProducts;

internal sealed class ImportProductsCommandValidator
    : AbstractValidator<ImportProductsCommand>
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx",
            ".xlsm"
        };

    public ImportProductsCommandValidator()
    {
        RuleFor(command => command.FileName)
            .NotEmpty()
            .WithMessage("نام فایل الزامی است.")
            .Must(HaveSupportedExtension)
            .WithMessage("فقط فایل‌های Excel با پسوند xlsx یا xlsm قابل ایمپورت هستند.");

        RuleFor(command => command.FileContent)
            .NotNull()
            .NotEmpty()
            .WithMessage("فایل Excel خالی است.")
            .Must(content => content is null || content.Length <= ImportProductsCommand.MaxFileSizeBytes)
            .WithMessage("حجم فایل Excel نباید بیشتر از 50 مگابایت باشد.");
    }

    private static bool HaveSupportedExtension(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        AllowedExtensions.Contains(Path.GetExtension(fileName));
}
