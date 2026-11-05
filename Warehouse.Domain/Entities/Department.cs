using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

public sealed class Department : ConcurrencyEntity<Guid>
{
    private Department() { }

    public Department(string title, string? description)
    {
        Id = Guid.NewGuid();
        Update(title, description);
    }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public void Update(string title, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
