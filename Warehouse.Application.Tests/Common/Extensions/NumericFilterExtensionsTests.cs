using Warehouse.Application.Common.Extensions;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Tests.Common.Extensions;

public sealed class NumericFilterExtensionsTests
{
    public static TheoryData<NumericOperator, int, int?, int[]> IntegerCases => new()
    {
        { NumericOperator.Eq, 0, null, [0] },
        { NumericOperator.Neq, 0, null, [-2, 1, 2, 3] },
        { NumericOperator.Gt, 1, null, [2, 3] },
        { NumericOperator.Gte, 1, null, [1, 2, 3] },
        { NumericOperator.Lt, 1, null, [-2, 0] },
        { NumericOperator.Lte, 1, null, [-2, 0, 1] },
        { NumericOperator.Between, 0, 2, [0, 1, 2] }
    };

    [Theory]
    [MemberData(nameof(IntegerCases))]
    public void ApplyNumericFilter_AppliesEveryOperator(
        NumericOperator numericOperator,
        int value,
        int? valueTo,
        int[] expected)
    {
        var items = new[] { -2, 0, 1, 2, 3 }
            .Select(number => new NumericItem(number, number, number))
            .AsQueryable();
        var filter = new NumericFilter<int>
        {
            Operator = numericOperator,
            Value = value,
            ValueTo = valueTo
        };

        var result = items
            .ApplyNumericFilter(filter, item => item.Integer)
            .Select(item => item.Integer)
            .ToArray();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApplyNumericFilter_SupportsDecimalsAndNegativeValues()
    {
        var items = new[]
        {
            new NumericItem(1, -1.25m, 1),
            new NumericItem(2, 0m, 2),
            new NumericItem(3, 2.75m, 3)
        }.AsQueryable();

        var result = items.ApplyNumericFilter(
            new NumericFilter<decimal>
            {
                Operator = NumericOperator.Gte,
                Value = -1.25m
            },
            item => item.Decimal);

        Assert.Equal(3, result.Count());
    }

    [Fact]
    public void ApplyNumericFilter_SupportsNullableSelectors()
    {
        var items = new[]
        {
            new NumericItem(1, 1m, null),
            new NumericItem(2, 2m, 0),
            new NumericItem(3, 3m, 2)
        }.AsQueryable();

        var result = items
            .ApplyNumericFilter(
                new NumericFilter<int>
                {
                    Operator = NumericOperator.Between,
                    Value = 0,
                    ValueTo = 2
                },
                item => item.NullableInteger)
            .Select(item => item.Integer)
            .ToArray();

        Assert.Equal([2, 3], result);
    }

    [Fact]
    public void ApplyNumericFilter_ComposesMultipleNumericAndTextFilters()
    {
        var items = new[]
        {
            new NamedNumericItem("Admin", 2, 8),
            new NamedNumericItem("Assistant", 1, 4),
            new NamedNumericItem("Operator", 5, 2)
        }.AsQueryable();

        var result = items
            .Where(item => item.Name.Contains("A"))
            .ApplyNumericFilter(
                new NumericFilter<int> { Operator = NumericOperator.Gte, Value = 2 },
                item => item.First)
            .ApplyNumericFilter(
                new NumericFilter<int> { Operator = NumericOperator.Gt, Value = 5 },
                item => item.Second)
            .ToArray();

        Assert.Equal("Admin", Assert.Single(result).Name);
    }

    [Fact]
    public void ApplyAnyNumericFilter_FiltersParentsByTheirChildren()
    {
        var groups = new[]
        {
            new NumericGroup("first", [new NumericItem(1, 5m, null)]),
            new NumericGroup("second", [new NumericItem(2, 12m, null)])
        }.AsQueryable();

        var result = groups.ApplyAnyNumericFilter(
            new NumericFilter<decimal>
            {
                Operator = NumericOperator.Gte,
                Value = 10m
            },
            group => group.Items,
            item => item.Decimal);

        Assert.Equal("second", Assert.Single(result).Name);
    }

    private sealed record NumericItem(
        int Integer,
        decimal Decimal,
        int? NullableInteger);

    private sealed record NamedNumericItem(
        string Name,
        int First,
        int Second);

    private sealed record NumericGroup(
        string Name,
        IReadOnlyCollection<NumericItem> Items);
}
