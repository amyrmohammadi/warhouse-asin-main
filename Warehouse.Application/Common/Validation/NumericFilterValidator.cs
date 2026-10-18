using FluentValidation;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Common.Validation;

public sealed class NumericFilterValidator<T> : AbstractValidator<NumericFilter<T>>
    where T : struct, IComparable<T>
{
    public NumericFilterValidator(T? minimum = null, T? maximum = null)
    {
        RuleFor(filter => filter.Operator)
            .IsInEnum()
            .WithMessage("The numeric filter operator is not supported.");
        RuleFor(filter => filter.Value)
            .NotNull()
            .WithMessage("A value is required for an active numeric filter.");
        RuleFor(filter => filter.ValueTo)
            .NotNull()
            .WithMessage("The upper value is required for a between filter.")
            .When(filter => filter.Operator == NumericOperator.Between);
        RuleFor(filter => filter)
            .Must(filter =>
                filter.Operator != NumericOperator.Between ||
                filter.Value is null ||
                filter.ValueTo is null ||
                filter.Value.Value.CompareTo(filter.ValueTo.Value) <= 0)
            .WithMessage("The numeric filter start value cannot exceed its end value.");

        if (minimum is { } minimumValue)
        {
            RuleFor(filter => filter)
                .Must(filter =>
                    IsWithinLowerBound(filter.Value, minimumValue) &&
                    IsWithinLowerBound(filter.ValueTo, minimumValue))
                .WithMessage($"Numeric filter values must be greater than or equal to {minimumValue}.");
        }

        if (maximum is { } maximumValue)
        {
            RuleFor(filter => filter)
                .Must(filter =>
                    IsWithinUpperBound(filter.Value, maximumValue) &&
                    IsWithinUpperBound(filter.ValueTo, maximumValue))
                .WithMessage($"Numeric filter values must be less than or equal to {maximumValue}.");
        }
    }

    private static bool IsWithinLowerBound(T? value, T minimum) =>
        value is null || value.Value.CompareTo(minimum) >= 0;

    private static bool IsWithinUpperBound(T? value, T maximum) =>
        value is null || value.Value.CompareTo(maximum) <= 0;
}
