using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.WarehouseStocks;

public sealed record WarehouseStockRow
{
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = "";
    public string ProductTitle { get; init; } = "";
    public string UnitOfMeasure { get; init; } = "";
    public Guid WarehouseId { get; init; }
    public decimal? OpeningQuantity { get; init; }
    public decimal CurrentQuantity { get; init; }
    public byte[]? RowVersion { get; init; }
}
public sealed record WarehouseStockList(IReadOnlyList<WarehouseStockRow> Items, int TotalCount, int FilteredCount);
public sealed record WarehouseStockOption(Guid Id, string Title);
public sealed record GetStockWarehouseOptionsQuery : IRequest<Result<IReadOnlyList<WarehouseStockOption>>>;
public sealed record GetWarehouseStocksQuery : DataTableParameters, IRequest<Result<WarehouseStockList>>
{
    public Guid WarehouseId { get; init; }
    public Guid? ProductId { get; init; }
    public bool? HasStock { get; init; }
}

internal sealed class GetWarehouseStocksValidator : AbstractValidator<GetWarehouseStocksQuery>
{
    public GetWarehouseStocksValidator()
    {
        RuleFor(query => query.WarehouseId).NotEmpty().WithMessage("انبار را انتخاب کنید.");
        RuleFor(query => query.ProductId).NotEmpty().WithMessage("شناسه کالا معتبر نیست.").When(query => query.ProductId.HasValue);
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1000000).WithMessage("شماره صفحه معتبر نیست.");
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100).WithMessage("اندازه صفحه باید بین ۱ تا ۱۰۰ باشد.");
        RuleFor(query => query.Search).MaximumLength(200).WithMessage("عبارت جستجو حداکثر ۲۰۰ نویسه باشد.");
    }
}

internal sealed class GetWarehouseStocksHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetWarehouseStocksQuery, Result<WarehouseStockList>>,
      IRequestHandler<GetStockWarehouseOptionsQuery, Result<IReadOnlyList<WarehouseStockOption>>>
{
    public async Task<Result<IReadOnlyList<WarehouseStockOption>>> Handle(GetStockWarehouseOptionsQuery request, CancellationToken token)
    {
        var warehouses = unitOfWork.Repository<StorageWarehouse>();
        return Result<IReadOnlyList<WarehouseStockOption>>.Success(await warehouses.ToListAsync(
            warehouses.Entities.OrderBy(warehouse => warehouse.Title).ThenBy(warehouse => warehouse.Id)
                .Select(warehouse => new WarehouseStockOption(warehouse.Id, warehouse.Title)), token));
    }

    public async Task<Result<WarehouseStockList>> Handle(GetWarehouseStocksQuery request, CancellationToken token)
    {
        var warehouses = unitOfWork.Repository<StorageWarehouse>();
        if (await warehouses.CountAsync(warehouses.Entities.Where(warehouse => warehouse.Id == request.WarehouseId), token) == 0)
            return Result<WarehouseStockList>.Error("انبار انتخاب‌شده پیدا نشد.");
        var products = unitOfWork.Repository<Product>();
        var balances = unitOfWork.Repository<WarehouseStockBalance>().Entities.Where(stock => stock.WarehouseId == request.WarehouseId);
        var openings = unitOfWork.Repository<StockMovement>().Entities
            .Where(movement => movement.WarehouseId == request.WarehouseId && movement.MovementType == InventoryMovementType.OpeningBalance)
            .GroupBy(movement => movement.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(movement => movement.QuantityDelta) });
        var total = await products.CountAsync(products.Entities, token);
        var query = from product in products.Entities
                    join balance in balances on product.Id equals balance.ProductId into stockGroup
                    from balance in stockGroup.DefaultIfEmpty()
                    join opening in openings on product.Id equals opening.ProductId into openingGroup
                    from opening in openingGroup.DefaultIfEmpty()
                    select new WarehouseStockRow
                    {
                        ProductId = product.Id, ProductCode = product.Code, ProductTitle = product.Title,
                        UnitOfMeasure = product.UnitOfMeasure.Title, WarehouseId = request.WarehouseId,
                        CurrentQuantity = balance == null ? 0 : balance.Quantity,
                        OpeningQuantity = opening == null ? null : opening.Quantity,
                        RowVersion = balance == null ? null : balance.RowVersion
                    };
        if (request.ProductId.HasValue) query = query.Where(row => row.ProductId == request.ProductId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(row => row.ProductCode.Contains(search) || row.ProductTitle.Contains(search));
        }
        if (request.HasStock.HasValue)
            query = request.HasStock.Value ? query.Where(row => row.CurrentQuantity > 0) : query.Where(row => row.CurrentQuantity == 0);
        var filteredIds = query.Select(row => row.ProductId);
        var filtered = await products.CountAsync(products.Entities.Where(product => filteredIds.Contains(product.Id)), token);
        query = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("productcode", false) => query.OrderBy(row => row.ProductCode).ThenBy(row => row.ProductId),
            ("productcode", true) => query.OrderByDescending(row => row.ProductCode).ThenBy(row => row.ProductId),
            ("currentquantity", false) => query.OrderBy(row => row.CurrentQuantity).ThenBy(row => row.ProductId),
            ("currentquantity", true) => query.OrderByDescending(row => row.CurrentQuantity).ThenBy(row => row.ProductId),
            ("openingquantity", false) => query.OrderBy(row => row.OpeningQuantity).ThenBy(row => row.ProductId),
            ("openingquantity", true) => query.OrderByDescending(row => row.OpeningQuantity).ThenBy(row => row.ProductId),
            (_, true) => query.OrderByDescending(row => row.ProductTitle).ThenBy(row => row.ProductId),
            _ => query.OrderBy(row => row.ProductTitle).ThenBy(row => row.ProductId)
        };
        return Result<WarehouseStockList>.Success(new(await products.ToListAsync(
            query.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize), token), total, filtered));
    }
}
