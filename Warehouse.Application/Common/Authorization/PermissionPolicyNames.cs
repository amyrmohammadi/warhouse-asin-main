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
    }

    public static class Inventory
    {
        public const string GoodsReceiptsRead = nameof(AppPermission.GoodsReceiptsRead);
        public const string GoodsReceiptsInitialEntry = nameof(AppPermission.GoodsReceiptsInitialEntry);
        public const string GoodsReceiptsReceiptInvoiceOperations = nameof(AppPermission.GoodsReceiptsReceiptInvoiceOperations);
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
            "inventory",
            "عملیات انبار",
            [
                new(Inventory.GoodsReceiptsRead, "مشاهده تاریخچه ورود کالا"),
                new(Inventory.GoodsReceiptsInitialEntry, "ورود اولیه کالا"),
                new(Inventory.GoodsReceiptsReceiptInvoiceOperations, "عملیات رسید و فاکتور")
            ]),
        new(
            "master-data",
            "داده‌های پایه",
            [
                new(MasterData.Read, "مشاهده داده‌های پایه"),
                new(MasterData.Manage, "مدیریت داده‌های پایه")
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
