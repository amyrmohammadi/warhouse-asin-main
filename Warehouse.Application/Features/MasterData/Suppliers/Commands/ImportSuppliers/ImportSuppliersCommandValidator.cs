using FluentValidation;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.ImportSuppliers;

internal sealed class ImportSuppliersCommandValidator
    : AbstractValidator<ImportSuppliersCommand>
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx",
            ".xlsm"
        };

    public ImportSuppliersCommandValidator()
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
            .Must(content => content.Length <= ImportSuppliersCommand.MaxFileSizeBytes)
            .WithMessage("حجم فایل Excel نباید بیشتر از ۱۰ مگابایت باشد.");
    }

    private static bool HaveSupportedExtension(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        AllowedExtensions.Contains(Path.GetExtension(fileName));
}
