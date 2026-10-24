using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

public sealed class GoodsIssue : ConcurrencyEntity<Guid>
{
    private GoodsIssue() { }

    public GoodsIssue(string issueNumber, DateTimeOffset issuedAtUtc, string? description, Guid issuerId)
    {
        Id = Guid.NewGuid();
        Update(issueNumber, issuedAtUtc, description, issuerId);
    }

    public string IssueNumber { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public string? Description { get; private set; }
    public Guid IssuerId { get; private set; }
    public Person Issuer { get; private set; } = null!;
    public ICollection<GoodsIssueItem> Items { get; private set; } = [];

    public void Update(string issueNumber, DateTimeOffset issuedAtUtc, string? description, Guid issuerId)
    {
        IssueNumber = issueNumber.Trim();
        IssuedAtUtc = issuedAtUtc.ToUniversalTime();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IssuerId = issuerId;
    }

    public GoodsIssueItem AddItem(
        Guid? productId,
        string productCode,
        decimal quantity,
        string? assetNumber,
        Guid destinationId,
        string exitReason,
        string? description,
        string deliveringUnit,
        DateTimeOffset? returnAtUtc,
        int sortOrder)
    {
        var item = new GoodsIssueItem(
            Id, productId, productCode, quantity, assetNumber, destinationId, exitReason,
            description, deliveringUnit, returnAtUtc, sortOrder);
        Items.Add(item);
        return item;
    }
}
