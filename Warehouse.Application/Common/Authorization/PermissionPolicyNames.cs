namespace Warehouse.Application.Common.Authorization;

public static class PermissionPolicyNames
{
    public static class UserManagement
    {
        public const string UsersRead = nameof(AppPermission.UsersRead);
        public const string UsersManage = nameof(AppPermission.UsersManage);
        public const string UserRolesManage = nameof(AppPermission.UserRolesManage);
    }

    public static class RoleManagement
    {
        public const string RolesRead = nameof(AppPermission.RolesRead);
        public const string RolesManage = nameof(AppPermission.RolesManage);
        public const string RolePoliciesManage = nameof(AppPermission.RolePoliciesManage);
    }

    public static class Auditing
    {
        public const string AuditLogsRead = nameof(AppPermission.AuditLogsRead);
    }

    public static class MasterData
    {
        public const string Read = nameof(AppPermission.MasterDataRead);
        public const string Manage = nameof(AppPermission.MasterDataManage);
        public const string ProductsCreate = nameof(AppPermission.ProductsCreate);
    }

    public static class Inventory
    {
        public const string GoodsReceiptsRead = nameof(AppPermission.GoodsReceiptsRead);
        public const string GoodsReceiptHistoryRead = nameof(AppPermission.GoodsReceiptHistoryRead);
        public const string GoodsReceiptsInitialEntry = nameof(AppPermission.GoodsReceiptsInitialEntry);
        public const string GoodsReceiptsReceiptInvoiceOperations = nameof(AppPermission.GoodsReceiptsReceiptInvoiceOperations);
        public const string GoodsReceiptsReceiptRegistration = nameof(AppPermission.GoodsReceiptsReceiptRegistration);
        public const string GoodsReceiptsImport = nameof(AppPermission.GoodsReceiptsImport);
        public const string GoodsReceiptsExport = nameof(AppPermission.GoodsReceiptsExport);
        public const string GoodsReceiptsMissingReceiptCountRead = nameof(AppPermission.GoodsReceiptsMissingReceiptCountRead);
        public const string GoodsReceiptsMissingInvoiceCountRead = nameof(AppPermission.GoodsReceiptsMissingInvoiceCountRead);
        public const string GoodsReceiptsTodayItemCountRead = nameof(AppPermission.GoodsReceiptsTodayItemCountRead);
        public const string GoodsReceiptsTodayDocumentCountRead = nameof(AppPermission.GoodsReceiptsTodayDocumentCountRead);
        public const string GoodsIssuesRead = nameof(AppPermission.GoodsIssuesRead);
        public const string GoodsIssuesCreate = nameof(AppPermission.GoodsIssuesCreate);
        public const string GoodsIssuesUpdate = nameof(AppPermission.GoodsIssuesUpdate);
        public const string GoodsIssuesDelete = nameof(AppPermission.GoodsIssuesDelete);
        public const string GoodsIssuesImport = nameof(AppPermission.GoodsIssuesImport);
        public const string GoodsIssuesExport = nameof(AppPermission.GoodsIssuesExport);
        public const string GoodsIssuesFinalize = nameof(AppPermission.GoodsIssuesFinalize);
        public const string GoodsIssuesReopen = nameof(AppPermission.GoodsIssuesReopen);
        public const string InvoicesRead = nameof(AppPermission.InvoicesRead);
        public const string InvoicesMissingReceiptCountRead = nameof(AppPermission.InvoicesMissingReceiptCountRead);
    }

    public static IReadOnlyCollection<PermissionPolicyGroup> Groups { get; } =
    [
        new(
            "users",
            "مدیریت کاربران",
            [
                new(UserManagement.UsersRead, "مشاهده کاربران"),
                new(UserManagement.UsersManage, "مدیریت اطلاعات کاربران"),
                new(UserManagement.UserRolesManage, "مدیریت نقش‌های کاربران")
            ]),
        new(
            "roles",
            "مدیریت نقش‌ها",
            [
                new(RoleManagement.RolesRead, "مشاهده نقش‌ها"),
                new(RoleManagement.RolesManage, "ایجاد و مدیریت نقش‌ها"),
                new(RoleManagement.RolePoliciesManage, "مدیریت دسترسی‌های نقش‌ها")
            ]),
        new(
            "auditing",
            "گزارش‌ها و نظارت",
            [
                new(Auditing.AuditLogsRead, "مشاهده گزارش فعالیت کاربران")
            ]),
        new(
            "goods-receipts",
            "ورود کالا",
            [
                new(Inventory.GoodsReceiptsRead, "مشاهده تاریخچه ورود کالا"),
                new(Inventory.GoodsReceiptHistoryRead, "مشاهده تاریخچه تغییرات هر کالای ورودی"),
                new(Inventory.GoodsReceiptsInitialEntry, "ورود اولیه کالا"),
                new(Inventory.GoodsReceiptsReceiptInvoiceOperations, "ثبت و ویرایش فاکتور"),
                new(Inventory.GoodsReceiptsReceiptRegistration, "ثبت و ویرایش رسید"),
                new(Inventory.GoodsReceiptsImport, "ایمپورت ورودی کالا"),
                new(Inventory.GoodsReceiptsExport, "اکسپورت ورودی کالا"),
                new(Inventory.GoodsReceiptsMissingReceiptCountRead, "مشاهده تعداد کالاهای بدون رسید"),
                new(Inventory.GoodsReceiptsMissingInvoiceCountRead, "مشاهده تعداد کالاهای بدون فاکتور"),
                new(Inventory.GoodsReceiptsTodayItemCountRead, "مشاهده تعداد اقلام ورودی امروز"),
                new(Inventory.GoodsReceiptsTodayDocumentCountRead, "مشاهده تعداد اسناد ورودی امروز")
            ]),
        new(
            "goods-issues",
            "خروج کالا",
            [
                new(Inventory.GoodsIssuesRead, "مشاهده تاریخچه خروج کالا"),
                new(Inventory.GoodsIssuesCreate, "ثبت خروج کالا"),
                new(Inventory.GoodsIssuesUpdate, "ویرایش خروج کالا"),
                new(Inventory.GoodsIssuesDelete, "حذف خروج کالا"),
                new(Inventory.GoodsIssuesFinalize, "تأیید نهایی اسناد خروج"),
                new(Inventory.GoodsIssuesReopen, "برگشت اسناد خروج از تأیید نهایی"),
                new(Inventory.GoodsIssuesImport, "ایمپورت خروج کالا"),
                new(Inventory.GoodsIssuesExport, "اکسپورت خروج کالا")
            ]),
        new(
            "invoices",
            "فاکتورها",
            [
                new(Inventory.InvoicesRead, "مشاهده فاکتورها و کالاهای آن‌ها"),
                new(Inventory.InvoicesMissingReceiptCountRead, "مشاهده تعداد اقلام بدون رسید فاکتورها")
            ]),
        new(
            "master-data",
            "داده‌های پایه",
            [
                new(MasterData.Read, "مشاهده داده‌های پایه"),
                new(MasterData.Manage, "مدیریت داده‌های پایه"),
                new(MasterData.ProductsCreate, "ثبت کالای جدید")
            ])
    ];

    public static IReadOnlyCollection<string> All { get; } =
        Groups
            .SelectMany(group => group.Policies)
            .Select(policy => policy.Name)
            .ToArray();

    public static bool IsDefined(string policyName) =>
        All.Contains(policyName, StringComparer.Ordinal);
}

public sealed record PermissionPolicyGroup(
    string Key,
    string DisplayName,
    IReadOnlyCollection<PermissionPolicyDefinition> Policies);

public sealed record PermissionPolicyDefinition(
    string Name,
    string DisplayName);
