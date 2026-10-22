namespace Warehouse.Server.Contracts.Inventory;

public sealed record SaveGoodsIssueRequest(
    string IssueNumber,
    DateTimeOffset IssuedAtUtc,
    Guid? IssuerId,
    string? IssuerFullName,
    IReadOnlyCollection<SaveGoodsIssueItemRequest> Items);

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
