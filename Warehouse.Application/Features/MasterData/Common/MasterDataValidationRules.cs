using System.Linq.Expressions;
using FluentValidation;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Common;

internal static class MasterDataValidationRules
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 1000;
    public const int FullNameMaxLength = 200;
    public const int PhoneNumberMaxLength = 32;
    public const int ProductCodeMaxLength = 100;

    private static readonly string[] TitledSortFields = ["id", "title"];
    private static readonly string[] PersonSortFields = ["id", "fullName", "phoneNumber"];
    private static readonly string[] ProductSortFields =
        ["id", "title", "code", "unitOfMeasureTitle"];

    public static void AddTitledListRules<T>(AbstractValidator<T> validator)
        where T : DataTableParameters
    {
        AddListRules(validator);
        AddSortRule(validator, TitledSortFields);
    }

    public static void AddPersonListRules<T>(AbstractValidator<T> validator)
        where T : DataTableParameters
    {
        AddListRules(validator);
        AddSortRule(validator, PersonSortFields);
    }

    public static void AddProductListRules<T>(AbstractValidator<T> validator)
        where T : DataTableParameters
    {
        AddListRules(validator);
        AddSortRule(validator, ProductSortFields);
    }

    public static void AddProductRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> title,
        Expression<Func<T, string>> code,
        Expression<Func<T, string>> unitOfMeasureTitle,
        Expression<Func<T, string?>> description)
    {
        AddTitleRules(validator, title, description);

        validator.RuleFor(title)
            .Must(value =>
                value is not null &&
                Product.CreateComparisonKey(value).Length > 0)
            .WithMessage("عنوان کالا باید حداقل یک نویسه غیر از فاصله و خط تیره داشته باشد.");

        validator.RuleFor(code)
            .NotEmpty()
            .WithMessage("وارد کردن کد کالا الزامی است.")
            .MaximumLength(ProductCodeMaxLength)
            .WithMessage($"کد کالا نباید بیشتر از {ProductCodeMaxLength} نویسه باشد.")
            .Must(value =>
                value is not null &&
                (Product.IsPlaceholderCode(value) || Product.CreateComparisonKey(value).Length > 0))
            .WithMessage("کد کالا باید یک کد معتبر یا علامت - باشد.");

        validator.RuleFor(unitOfMeasureTitle)
            .NotEmpty()
            .WithMessage("انتخاب یا وارد کردن واحد اندازه‌گیری الزامی است.")
            .MaximumLength(TitleMaxLength)
            .WithMessage($"عنوان واحد نباید بیشتر از {TitleMaxLength} نویسه باشد.");
    }

    public static void AddTitleRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> title,
        Expression<Func<T, string?>> description)
    {
        validator.RuleFor(title)
            .NotEmpty()
            .WithMessage("وارد کردن عنوان الزامی است.")
            .MaximumLength(TitleMaxLength)
            .WithMessage($"عنوان نباید بیشتر از {TitleMaxLength} نویسه باشد.");

        validator.RuleFor(description)
            .MaximumLength(DescriptionMaxLength)
            .WithMessage($"توضیحات نباید بیشتر از {DescriptionMaxLength} نویسه باشد.");
    }

    public static void AddPersonRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> fullName,
        Expression<Func<T, string>> phoneNumber)
    {
        validator.RuleFor(fullName)
            .NotEmpty()
            .WithMessage("وارد کردن نام کامل الزامی است.")
            .MaximumLength(FullNameMaxLength)
            .WithMessage($"نام کامل نباید بیشتر از {FullNameMaxLength} نویسه باشد.");
        //validator.RuleFor(phoneNumber)
        //    //.NotEmpty()
        //    //.WithMessage("وارد کردن شماره تلفن الزامی است.")
        //    .MaximumLength(PhoneNumberMaxLength)
        //    .WithMessage($"شماره تلفن نباید بیشتر از {PhoneNumberMaxLength} نویسه باشد.");
    }

    private static void AddListRules<T>(AbstractValidator<T> validator)
        where T : DataTableParameters
    {
        validator.RuleFor(request => request.PageNumber).GreaterThanOrEqualTo(1);
        validator.RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        validator.RuleFor(request => request.Search)
            .MaximumLength(256)
            .When(request => !string.IsNullOrWhiteSpace(request.Search));
    }

    private static void AddSortRule<T>(
        AbstractValidator<T> validator,
        IReadOnlyCollection<string> supportedFields)
        where T : DataTableParameters
    {
        validator.RuleFor(request => request.SortBy)
            .Must(sortBy => supportedFields.Contains(
                sortBy!,
                StringComparer.OrdinalIgnoreCase))
            .WithMessage("The selected sort field is not supported.")
            .When(request => !string.IsNullOrWhiteSpace(request.SortBy));
    }
}
