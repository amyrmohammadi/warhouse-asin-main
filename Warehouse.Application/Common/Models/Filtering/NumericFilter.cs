using System.Text.Json.Serialization;

namespace Warehouse.Application.Common.Models.Filtering;

public enum NumericOperator
{
    [JsonStringEnumMemberName("eq")]
    Eq,

    [JsonStringEnumMemberName("neq")]
    Neq,

    [JsonStringEnumMemberName("gt")]
    Gt,

    [JsonStringEnumMemberName("gte")]
    Gte,

    [JsonStringEnumMemberName("lt")]
    Lt,

    [JsonStringEnumMemberName("lte")]
    Lte,

    [JsonStringEnumMemberName("between")]
    Between
}

public sealed record NumericFilter<T>
    where T : struct, IComparable<T>
{
    public NumericOperator Operator { get; init; }

    public T? Value { get; init; }

    public T? ValueTo { get; init; }
}
