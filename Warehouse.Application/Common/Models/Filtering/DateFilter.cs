using System.Text.Json.Serialization;

namespace Warehouse.Application.Common.Models.Filtering;

public enum DateOperator
{
    [JsonStringEnumMemberName("before")]
    Before,

    [JsonStringEnumMemberName("after")]
    After,

    [JsonStringEnumMemberName("eq")]
    Eq,

    [JsonStringEnumMemberName("between")]
    Between
}

public sealed record DateFilter
{
    public DateOperator Operator { get; init; }

    public DateOnly? Value { get; init; }

    public DateOnly? ValueTo { get; init; }

    public int UtcOffsetMinutes { get; init; }
}
