namespace Warehouse.Domain.Enums;

public enum GoodsIssueExitReason
{
    Maintenance = 0,
    Sample = 1,
    Purchase = 2,
    Sale = 3,
    Services = 4,
    Consignment = 5,
    Repair = 6,
    Other = 7
}

public static class GoodsIssueExitReasonParser
{
    public static bool TryFromText(string? value, out GoodsIssueExitReason reason)
    {
        var normalized = value?.Trim();
        reason = normalized switch
        {
            "تعمیرات" => GoodsIssueExitReason.Maintenance,
            "نمونه" => GoodsIssueExitReason.Sample,
            "خرید" => GoodsIssueExitReason.Purchase,
            "فروش" => GoodsIssueExitReason.Sale,
            "خدمات" => GoodsIssueExitReason.Services,
            "امانی" => GoodsIssueExitReason.Consignment,
            "تعمیر" => GoodsIssueExitReason.Repair,
            "سایر" => GoodsIssueExitReason.Other,
            _ when Enum.TryParse<GoodsIssueExitReason>(normalized, true, out var parsed) => parsed,
            _ => GoodsIssueExitReason.Other
        };
        return normalized is "تعمیرات" or "نمونه" or "خرید" or "فروش" or "خدمات" or
            "امانی" or "تعمیر" or "سایر" ||
            Enum.TryParse<GoodsIssueExitReason>(normalized, true, out _);
    }

    public static GoodsIssueExitReason FromLegacyText(string? value) =>
        TryFromText(value, out var reason) ? reason : GoodsIssueExitReason.Other;
}
