using FluentValidation;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Common.Validation;

public sealed class DateFilterValidator : AbstractValidator<DateFilter>
{
    public DateFilterValidator()
    {
        RuleFor(filter => filter.Operator)
            .IsInEnum()
            .WithMessage("The date filter operator is not supported.");
        RuleFor(filter => filter.Value)
            .NotNull()
            .WithMessage("A date is required for an active date filter.");
        RuleFor(filter => filter.ValueTo)
            .NotNull()
            .WithMessage("The end date is required for a between filter.")
            .When(filter => filter.Operator == DateOperator.Between);
        RuleFor(filter => filter)
            .Must(filter =>
                filter.Operator != DateOperator.Between ||
                filter.Value is null ||
                filter.ValueTo is null ||
                filter.Value <= filter.ValueTo)
            .WithMessage("The date filter start cannot be after its end.");
        RuleFor(filter => filter.UtcOffsetMinutes)
            .InclusiveBetween(-14 * 60, 14 * 60)
            .WithMessage("The date filter UTC offset is invalid.");
    }
}
