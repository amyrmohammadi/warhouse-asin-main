using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

public sealed class GoodsIssueDestination : Entity<Guid>
{
    private GoodsIssueDestination() { }

    public GoodsIssueDestination(string title, string? description)
    {
        Id = Guid.NewGuid();
        Update(title, description);
    }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ICollection<GoodsIssueItem> GoodsIssueItems { get; private set; } = [];

    public void Update(string title, string? description)
    {
        Title = title.Trim();
        Description = Normalize(description);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
