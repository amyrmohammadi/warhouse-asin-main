namespace Warehouse.Domain.Enums;

public enum InventoryMovementType
{
    GoodsReceipt = 1,
    GoodsIssue = 2,
    DepartmentDelivery = 3,
    DepartmentReceipt = 4,
    OpeningBalance = 5,
    Adjustment = 6
}
