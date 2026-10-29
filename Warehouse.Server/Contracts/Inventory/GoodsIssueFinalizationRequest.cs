namespace Warehouse.Server.Contracts.Inventory;

public sealed record GoodsIssueFinalizationRequest(Guid? Id, DateOnly? Day, Guid? ConcurrencyStamp);
