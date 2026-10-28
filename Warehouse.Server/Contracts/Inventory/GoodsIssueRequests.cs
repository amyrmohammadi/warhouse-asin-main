namespace Warehouse.Server.Contracts.Inventory;

using Warehouse.Domain.Enums;

public sealed class ImportGoodsIssuesRequest
{
    public IFormFile? File { get; init; }
}

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
    GoodsIssueExitReason ExitReason,
    string? Description,
    string? DeliveringUnit,
    DateTimeOffset? ReturnAtUtc,
    string? ProductTitle = null,
    Guid? UnitOfMeasureId = null,
    Guid? ShippingLocationId = null,
    bool IsReturnable = true);
