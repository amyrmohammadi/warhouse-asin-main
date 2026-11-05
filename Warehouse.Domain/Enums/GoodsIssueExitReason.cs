namespace Warehouse.Domain.Enums;

public enum GoodsIssueExitReason
{
    Sample = 1,
    Purchase = 2,
    Sale = 3,
    Services = 4,
    Consignment = 5,
    Repair = 6,
    Other = 7,
    Charge = 8,
    NotApproved = 9
}

public static class GoodsIssueExitReasonParser
{
    public static bool TryFromText(string? value, out GoodsIssueExitReason reason)
    {
        var normalized = value?.Trim();
        reason = normalized switch
        {
            "تعمیرات" or "Maintenance" => GoodsIssueExitReason.Repair,
            "نمونه" => GoodsIssueExitReason.Sample,
            "خرید" => GoodsIssueExitReason.Purchase,
            "فروش" => GoodsIssueExitReason.Sale,
            "خدمات" => GoodsIssueExitReason.Services,
            "امانی" => GoodsIssueExitReason.Consignment,
            "تعمیر" => GoodsIssueExitReason.Repair,
            "سایر" => GoodsIssueExitReason.Other,
            "شارژ" => GoodsIssueExitReason.Charge,
            "عدم تایید" or "عدم تأیید" => GoodsIssueExitReason.NotApproved,
            _ when Enum.TryParse<GoodsIssueExitReason>(normalized, true, out var parsed) => parsed,
            _ => GoodsIssueExitReason.Other
        };
        return normalized is "تعمیرات" or "نمونه" or "خرید" or "فروش" or "خدمات" or
            "امانی" or "تعمیر" or "سایر" or "شارژ" or "عدم تایید" or "عدم تأیید" or "Maintenance" ||
            Enum.TryParse<GoodsIssueExitReason>(normalized, true, out _);
    }

    public static GoodsIssueExitReason FromLegacyText(string? value) =>
        TryFromText(value, out var reason) ? reason : GoodsIssueExitReason.Other;
}
