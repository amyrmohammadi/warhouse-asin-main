using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.WarehouseStocks;

public sealed record StockHistoryRow(Guid Id, DateTimeOffset? Date,
    InventoryMovementType MovementType, decimal QuantityDelta, string? Description, bool IsReversal,
    string? PageNumber);
public sealed record StockHistoryPage(IReadOnlyList<StockHistoryRow> Items, int TotalCount);
public sealed record GetStockHistoryQuery(Guid WarehouseId, Guid ProductId, int PageNumber = 1, int PageSize = 20)
    : IRequest<Result<StockHistoryPage>>;

internal sealed class GetStockHistoryValidator : AbstractValidator<GetStockHistoryQuery>
{
    public GetStockHistoryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEmpty();
        RuleFor(query => query.ProductId).NotEmpty();
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1000000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetStockHistoryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetStockHistoryQuery, Result<StockHistoryPage>>
{
    public async Task<Result<StockHistoryPage>> Handle(GetStockHistoryQuery request, CancellationToken token)
    {
        var repository = unitOfWork.Repository<StockMovement>();
        var query = repository.Entities.Where(movement => movement.WarehouseId == request.WarehouseId
            && movement.ProductId == request.ProductId);
        var total = await repository.CountAsync(query, token);
        var receipts = unitOfWork.Repository<GoodsReceipt>().Entities;
        var items = unitOfWork.Repository<GoodsReceiptItem>().Entities;
        var history = from movement in query
                      join receipt in receipts on movement.SourceDocumentId equals (Guid?)receipt.Id into receiptGroup
                      from receipt in receiptGroup.DefaultIfEmpty()
                      join item in items on movement.SourceLineId equals (Guid?)item.Id into itemGroup
                      from item in itemGroup.DefaultIfEmpty()
                      select new
                      {
                          Movement = movement,
                          Date = movement.MovementType == InventoryMovementType.GoodsReceipt
                              ? (receipt == null ? (DateTimeOffset?)null : receipt.EnteredAtUtc) : movement.OccurredAtUtc,
                          Description = movement.MovementType == InventoryMovementType.GoodsReceipt && item != null
                              ? item.Description : movement.Description,
                          PageNumber = movement.MovementType == InventoryMovementType.GoodsReceipt && receipt != null
                              ? receipt.PageNumber : null
                      };
        var rows = await repository.ToListAsync(history.OrderByDescending(row => row.Date)
            .ThenByDescending(row => row.Movement.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(row => new StockHistoryRow(row.Movement.Id, row.Date, row.Movement.MovementType,
                row.Movement.QuantityDelta, row.Description, row.Movement.ReversedMovementId != null, row.PageNumber)), token);
        return Result<StockHistoryPage>.Success(new(rows, total));
    }
}
