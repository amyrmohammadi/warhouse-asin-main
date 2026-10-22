using Warehouse.Application.Common.Extensions;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Tests.Common.Extensions;

public sealed class TextFilterExtensionsTests
{
    [Fact]
    public void ApplyTextFilter_UsesPlusSeparatedTermsAsOrConditions()
    {
        var items = new[]
        {
            new TextItem("کیلوگرم"),
            new TextItem("متر مربع"),
            new TextItem("عدد")
        }.AsQueryable();

        var result = items
            .ApplyTextFilter(
                new TextFilter
                {
                    Operator = TextOperator.Contains,
                    Value = "کیلو + متر"
                },
                item => item.Value)
            .Select(item => item.Value)
            .ToArray();

        Assert.Equal(["کیلوگرم", "متر مربع"], result);
    }

    [Fact]
    public void ApplyTextFilter_EqualityMatchesAnyExactTerm()
    {
        var items = new[]
        {
            new TextItem("کیلو"),
            new TextItem("کیلوگرم"),
            new TextItem("متر")
        }.AsQueryable();

        var result = items
            .ApplyTextFilter(
                new TextFilter { Operator = TextOperator.Eq, Value = "کیلو + متر" },
                item => item.Value)
            .Select(item => item.Value)
            .ToArray();

        Assert.Equal(["کیلو", "متر"], result);
    }

    [Fact]
    public void ApplyAnyTextFilter_FiltersParentsByTheirChildren()
    {
        var groups = new[]
        {
            new TextGroup("first", [new TextItem("کیلو")]),
            new TextGroup("second", [new TextItem("عدد")])
        }.AsQueryable();

        var result = groups.ApplyAnyTextFilter(
            new TextFilter { Operator = TextOperator.Contains, Value = "متر + کیلو" },
            group => group.Items,
            item => item.Value);

        Assert.Equal("first", Assert.Single(result).Name);
    }

    private sealed record TextItem(string Value);

    private sealed record TextGroup(string Name, IReadOnlyCollection<TextItem> Items);
}
