namespace Warehouse.Application.Common.Authorization;

public enum AppPermission
{
    UsersRead,
    UsersManage,
    RolesRead,
    RolesManage,
    UserRolesManage,
    RolePoliciesManage,
    AuditLogsRead,
    MasterDataRead,
    MasterDataManage,
    ProductsCreate,
    GoodsReceiptsRead,
    GoodsReceiptHistoryRead,
    GoodsReceiptsInitialEntry,
    GoodsReceiptsReceiptInvoiceOperations,
    GoodsReceiptsImport,
    GoodsReceiptsExport,
    GoodsIssuesRead,
    GoodsIssuesCreate,
    GoodsIssuesUpdate,
    GoodsIssuesDelete,
    GoodsIssuesExport,
    InvoicesRead
}
