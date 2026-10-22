using FluentValidation;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Common.Validation;

public sealed class TextFilterValidator : AbstractValidator<TextFilter>
{
    public TextFilterValidator(int maximumLength = 256)
    {
        RuleFor(filter => filter.Operator)
            .IsInEnum()
            .WithMessage("The text filter operator is not supported.");
        RuleFor(filter => filter.Value)
            .NotEmpty()
            .MaximumLength(maximumLength)
            .WithMessage($"The text filter value is required and cannot exceed {maximumLength} characters.");
        RuleFor(filter => filter)
            .Must(filter => filter.GetTerms().Length is > 0 and <= 20)
            .WithMessage("A text filter must contain between 1 and 20 search terms.");
    }
}
