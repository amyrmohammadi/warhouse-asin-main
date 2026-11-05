using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

public sealed class DepartmentReceipt : ConcurrencyEntity<Guid>
{
    private readonly List<DepartmentReceiptItem> _items = [];

    private DepartmentReceipt() { }

    public DepartmentReceipt(string documentNumber, DateTimeOffset date, Guid warehouseId,
        Guid departmentId, Guid? delivererPersonId = null, string? description = null)
    {
        Id = Guid.NewGuid();
        Update(documentNumber, date, warehouseId, departmentId, delivererPersonId, description);
    }

    public string DocumentNumber { get; private set; } = string.Empty;
    public DateTimeOffset Date { get; private set; }
    public Guid WarehouseId { get; private set; }
    public StorageWarehouse Warehouse { get; private set; } = null!;
    public Guid DepartmentId { get; private set; }
    public Department Department { get; private set; } = null!;
    public Guid? DelivererPersonId { get; private set; }
    public Person? DelivererPerson { get; private set; }
    public string? Description { get; private set; }
    public IReadOnlyCollection<DepartmentReceiptItem> Items => _items.AsReadOnly();

    public void Update(string documentNumber, DateTimeOffset date, Guid warehouseId,
        Guid departmentId, Guid? delivererPersonId, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        if (warehouseId == Guid.Empty) throw new ArgumentException("Warehouse is required.", nameof(warehouseId));
        if (departmentId == Guid.Empty) throw new ArgumentException("Department is required.", nameof(departmentId));
        if (delivererPersonId == Guid.Empty) throw new ArgumentException("Deliverer must be specified or null.", nameof(delivererPersonId));

        DocumentNumber = documentNumber.Trim();
        Date = date.ToUniversalTime();
        WarehouseId = warehouseId;
        DepartmentId = departmentId;
        DelivererPersonId = delivererPersonId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public DepartmentReceiptItem AddItem(Guid productId, decimal quantity,
        string? assetNumber = null, string? description = null)
    {
        var item = new DepartmentReceiptItem(this, productId, quantity, assetNumber, description);
        _items.Add(item);
        return item;
    }

    public void UpdateItem(Guid itemId, Guid productId, decimal quantity,
        string? assetNumber = null, string? description = null) =>
        FindItem(itemId).Update(productId, quantity, assetNumber, description);

    public void RemoveItem(Guid itemId) => _items.Remove(FindItem(itemId));

    private DepartmentReceiptItem FindItem(Guid itemId) =>
        _items.SingleOrDefault(item => item.Id == itemId)
        ?? throw new InvalidOperationException("The item does not belong to this department receipt.");
}
