using Warehouse.Domain.Common;
using Warehouse.Domain.Enums;

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
    public bool IsFinalized { get; private set; }
    public const string FinalizedMessage = "سند خروج تأیید نهایی شده است؛ ابتدا آن را از تأیید نهایی خارج کنید.";

    public void SetFinalized(bool finalized) => IsFinalized = finalized;

    private void EnsureEditable()
    {
        if (IsFinalized) throw new InvalidOperationException(FinalizedMessage);
    }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public string? Description { get; private set; }
    public Guid IssuerId { get; private set; }
    public Person Issuer { get; private set; } = null!;
    public ICollection<GoodsIssueItem> Items { get; private set; } = [];

    public void Update(string issueNumber, DateTimeOffset issuedAtUtc, string? description, Guid issuerId)
    {
        EnsureEditable();
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
        Guid? destinationId,
        string? exitReason,
        string? description,
        string? deliveringUnit,
        DateTimeOffset? returnAtUtc,
        int sortOrder)
    {
        return AddItem(productId, productCode, quantity, assetNumber, destinationId, null,
            GoodsIssueExitReasonParser.FromLegacyText(exitReason), true, description,
            deliveringUnit, returnAtUtc, sortOrder, exitReason);
    }

    public GoodsIssueItem AddItem(
        Guid? productId,
        string productCode,
        decimal quantity,
        string? assetNumber,
        Guid? destinationId,
        Guid? shippingLocationId,
        GoodsIssueExitReason exitReason,
        bool isReturnable,
        string? description,
        string? deliveringUnit,
        DateTimeOffset? returnAtUtc,
        int sortOrder,
        string? legacyExitReason = null)
    {
        EnsureEditable();
        var item = new GoodsIssueItem(
            Id, productId, productCode, quantity, assetNumber, destinationId,
            shippingLocationId, exitReason, isReturnable, legacyExitReason,
            description, deliveringUnit, isReturnable ? returnAtUtc : null, sortOrder);
        Items.Add(item);
        return item;
    }
}
