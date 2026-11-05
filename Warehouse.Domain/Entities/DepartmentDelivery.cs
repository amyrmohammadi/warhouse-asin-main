using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

public sealed class DepartmentDelivery : ConcurrencyEntity<Guid>
{
    private readonly List<DepartmentDeliveryItem> _items = [];

    private DepartmentDelivery() { }

    public DepartmentDelivery(string documentNumber, DateTimeOffset date, Guid warehouseId,
        Guid departmentId, Guid? receiverPersonId = null, string? description = null)
    {
        Id = Guid.NewGuid();
        Update(documentNumber, date, warehouseId, departmentId, receiverPersonId, description);
    }

    public string DocumentNumber { get; private set; } = string.Empty;
    public DateTimeOffset Date { get; private set; }
    public Guid WarehouseId { get; private set; }
    public StorageWarehouse Warehouse { get; private set; } = null!;
    public Guid DepartmentId { get; private set; }
    public Department Department { get; private set; } = null!;
    public Guid? ReceiverPersonId { get; private set; }
    public Person? ReceiverPerson { get; private set; }
    public string? Description { get; private set; }
    public IReadOnlyCollection<DepartmentDeliveryItem> Items => _items.AsReadOnly();

    public void Update(string documentNumber, DateTimeOffset date, Guid warehouseId,
        Guid departmentId, Guid? receiverPersonId, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        if (warehouseId == Guid.Empty) throw new ArgumentException("Warehouse is required.", nameof(warehouseId));
        if (departmentId == Guid.Empty) throw new ArgumentException("Department is required.", nameof(departmentId));
        if (receiverPersonId == Guid.Empty) throw new ArgumentException("Receiver must be specified or null.", nameof(receiverPersonId));

        DocumentNumber = documentNumber.Trim();
        Date = date.ToUniversalTime();
        WarehouseId = warehouseId;
        DepartmentId = departmentId;
        ReceiverPersonId = receiverPersonId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public DepartmentDeliveryItem AddItem(Guid productId, decimal quantity,
        string? assetNumber = null, string? description = null)
    {
        var item = new DepartmentDeliveryItem(this, productId, quantity, assetNumber, description);
        _items.Add(item);
        return item;
    }

    public void UpdateItem(Guid itemId, Guid productId, decimal quantity,
        string? assetNumber = null, string? description = null) =>
        FindItem(itemId).Update(productId, quantity, assetNumber, description);

    public void RemoveItem(Guid itemId) => _items.Remove(FindItem(itemId));

    private DepartmentDeliveryItem FindItem(Guid itemId) =>
        _items.SingleOrDefault(item => item.Id == itemId)
        ?? throw new InvalidOperationException("The item does not belong to this department delivery.");
}
