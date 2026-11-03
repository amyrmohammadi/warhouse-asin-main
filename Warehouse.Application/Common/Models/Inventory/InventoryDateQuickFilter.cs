namespace Warehouse.Application.Common.Models.Inventory;

public enum InventoryDateQuickFilter
{
    Today,
    CreatedToday,
    ItemsToday,
    ItemsCreatedToday
}

public static class InventoryTodayRange
{
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) For(DateTimeOffset utcNow)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var today = TimeZoneInfo.ConvertTime(utcNow, zone).Date;
        return (new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(today, zone), TimeSpan.Zero),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(today.AddDays(1), zone), TimeSpan.Zero));
    }
}
