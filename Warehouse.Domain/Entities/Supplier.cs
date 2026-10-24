using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

public sealed class Supplier : ConcurrencyEntity<Guid>
{
    private Supplier()
    {
    }

    public Supplier(string title, string? description)
    {
        Id = Guid.NewGuid();
        Update(title, description);
    }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ICollection<Invoice> Invoices { get; private set; } = [];

    public void Update(string title, string? description)
    {
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
