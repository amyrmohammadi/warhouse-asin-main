namespace Warehouse.Application.Features.Inventory.GoodsIssues;

internal static class GoodsIssueLinkRules
{
    public const string ProductChangeMessage =
        "این قلم کالا بین سند خروج و ورودی ارتباط دارد و تغییر کالا یا کد آن مجاز نیست. " +
        "ابتدا ارتباط شماره خروج را از کالای ورودی بردارید و ذخیره کنید؛ سپس کالا را در هر دو سند اصلاح و دوباره ارتباط را برقرار کنید.";

    public const string ProductMismatchMessage =
        "کالای ورودی و خروجی یکسان نیستند؛ ابتدا کالا را در هر دو سند یکسان کنید و سپس ارتباط را برقرار کنید.";
}
