namespace Warehouse.Server.Contracts.Inventory;

public sealed record SaveGoodsIssueRequest(
    string IssueNumber,
    DateTimeOffset IssuedAtUtc,
    string? Description,
    Guid? IssuerId,
    string? IssuerFullName,
    IReadOnlyCollection<SaveGoodsIssueItemRequest> Items,
    Guid? ConcurrencyStamp = null);

public sealed record SaveGoodsIssueItemRequest(
    Guid? ProductId,
    string ProductCode,
    decimal Quantity,
    string? AssetNumber,
    Guid? DestinationId,
    string? DestinationTitle,
    string ExitReason,
    string? Description,
    string DeliveringUnit,
    DateTimeOffset? ReturnAtUtc);
