using Warehouse.Application.Common.Extensions;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Tests.Common.Extensions;

public sealed class DateFilterExtensionsTests
{
    private const int TehranOffsetMinutes = 210;

    [Fact]
    public void ApplyDateFilter_BeforeIncludesTheSelectedLocalDay()
    {
        var result = Items()
            .ApplyDateFilter(
                new DateFilter
                {
                    Operator = DateOperator.Before,
                    Value = new DateOnly(2026, 1, 10),
                    UtcOffsetMinutes = TehranOffsetMinutes
                },
                item => item.Value)
            .Select(item => item.Name)
            .ToArray();

        Assert.Equal(["previous", "selected-start", "selected-end"], result);
    }

    [Fact]
    public void ApplyDateFilter_AfterIncludesTheSelectedLocalDay()
    {
        var result = Items()
            .ApplyDateFilter(
                new DateFilter
                {
                    Operator = DateOperator.After,
                    Value = new DateOnly(2026, 1, 10),
                    UtcOffsetMinutes = TehranOffsetMinutes
                },
                item => item.Value)
            .Select(item => item.Name)
            .ToArray();

        Assert.Equal(["selected-start", "selected-end", "next"], result);
    }

    [Fact]
    public void ApplyDateFilter_EqualityMatchesTheWholeSelectedLocalDay()
    {
        var result = Items()
            .ApplyDateFilter(
                new DateFilter
                {
                    Operator = DateOperator.Eq,
                    Value = new DateOnly(2026, 1, 10),
                    UtcOffsetMinutes = TehranOffsetMinutes
                },
                item => item.Value)
            .Select(item => item.Name)
            .ToArray();

        Assert.Equal(["selected-start", "selected-end"], result);
    }

    [Fact]
    public void ApplyDateFilter_BetweenIncludesBothBoundaryDays()
    {
        var result = Items()
            .ApplyDateFilter(
                new DateFilter
                {
                    Operator = DateOperator.Between,
                    Value = new DateOnly(2026, 1, 9),
                    ValueTo = new DateOnly(2026, 1, 10),
                    UtcOffsetMinutes = TehranOffsetMinutes
                },
                item => item.Value)
            .Select(item => item.Name)
            .ToArray();

        Assert.Equal(["previous", "selected-start", "selected-end"], result);
    }

    private static IQueryable<DateItem> Items() =>
        new[]
        {
            new DateItem("previous", Utc(2026, 1, 9, 12, 0)),
            new DateItem("selected-start", Utc(2026, 1, 10, 0, 0)),
            new DateItem("selected-end", Utc(2026, 1, 10, 23, 59)),
            new DateItem("next", Utc(2026, 1, 11, 0, 0))
        }.AsQueryable();

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromMinutes(TehranOffsetMinutes))
            .ToUniversalTime();

    private sealed record DateItem(string Name, DateTimeOffset Value);
}
