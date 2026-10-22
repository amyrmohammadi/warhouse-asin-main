using System.Text.Json.Serialization;

namespace Warehouse.Application.Common.Models.Filtering;

public enum TextOperator
{
    [JsonStringEnumMemberName("contains")]
    Contains,

    [JsonStringEnumMemberName("eq")]
    Eq
}

public sealed record TextFilter
{
    public TextOperator Operator { get; init; }

    public string? Value { get; init; }

    public string[] GetTerms() =>
        Value?
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? [];
}
