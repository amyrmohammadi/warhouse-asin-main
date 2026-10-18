using System.Globalization;

namespace Warehouse.Domain.Common;

public static class TextComparison
{
    public static string CreateCompactKey(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return string.Concat(value.Where(character =>
                !char.IsWhiteSpace(character) &&
                character != '\u200C' &&
                character != '\u2212' &&
                CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.DashPunctuation))
            .ToUpperInvariant();
    }
}
