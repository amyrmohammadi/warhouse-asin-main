using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;

internal sealed class GetGoodsReceiptItemRowsQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsReceiptItemRowsQuery, Result<GoodsReceiptItemRowListResult>>
{
    public async Task<Result<GoodsReceiptItemRowListResult>> Handle(
        GetGoodsReceiptItemRowsQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsReceiptItem>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);
        var search = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(item =>
                item.Product.Title.Contains(search) ||
                item.Product.Code.Contains(search) ||
                item.Product.UnitOfMeasure.Title.Contains(search) ||
                (item.RequestNumber != null && item.RequestNumber.Contains(search)) ||
                (item.Description != null && item.Description.Contains(search)) ||
                item.GoodsReceipt.PageNumber.Contains(search) ||
                item.GoodsReceipt.Deliverer.FullName.Contains(search) ||
                item.GoodsReceipt.Warehouse.Title.Contains(search) ||
                (item.GoodsReceipt.InvoiceNumber != null && item.GoodsReceipt.InvoiceNumber.Contains(search)) ||
                (item.GoodsReceipt.ReceiptNumber != null && item.GoodsReceipt.ReceiptNumber.Contains(search)) ||
                (item.GoodsReceipt.Supplier != null &&
                    item.GoodsReceipt.Supplier.Title.Contains(search)));
        }

        var filteredCount = await repository.CountAsync(query, cancellationToken);
        query = ApplySorting(query, request.SortBy, request.SortDescending)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize);

        var items = await repository.ToListAsync(
            query.Select(item => new GoodsReceiptItemRowSummary
            {
                Id = item.Id,
                GoodsReceiptId = item.GoodsReceiptId,
                ProductId = item.ProductId,
                ProductTitle = item.Product.Title,
                ProductCode = item.Product.Code,
                UnitOfMeasureTitle = item.Product.UnitOfMeasure.Title,
                Quantity = item.Quantity,
                RequestNumber = item.RequestNumber,
                ItemDescription = item.Description,
                EnteredAtUtc = item.GoodsReceipt.EnteredAtUtc,
                ReceiptDescription = item.GoodsReceipt.Description,
                DelivererFullName = item.GoodsReceipt.Deliverer.FullName,
                WarehouseTitle = item.GoodsReceipt.Warehouse.Title,
                InvoiceStatus = item.GoodsReceipt.InvoiceStatus,
                InvoiceNumber = item.GoodsReceipt.InvoiceNumber,
                SupplierId = item.GoodsReceipt.SupplierId,
                Counterparty = item.GoodsReceipt.Supplier == null
                    ? null
                    : item.GoodsReceipt.Supplier.Title,
                ReceiptNumber = item.GoodsReceipt.ReceiptNumber,
                PageNumber = item.GoodsReceipt.PageNumber
            }),
            cancellationToken);

        return Result<GoodsReceiptItemRowListResult>.Success(new GoodsReceiptItemRowListResult(
            items,
            totalCount,
            filteredCount,
            request.PageNumber,
            request.PageSize));
    }

    private static IQueryable<GoodsReceiptItem> ApplySorting(
        IQueryable<GoodsReceiptItem> query,
        string? sortBy,
        bool descending) =>
        (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("producttitle", false) => query.OrderBy(item => item.Product.Title).ThenBy(item => item.Id),
            ("producttitle", true) => query.OrderByDescending(item => item.Product.Title).ThenByDescending(item => item.Id),
            ("productcode", false) => query.OrderBy(item => item.Product.Code).ThenBy(item => item.Id),
            ("productcode", true) => query.OrderByDescending(item => item.Product.Code).ThenByDescending(item => item.Id),
            ("quantity", false) => query.OrderBy(item => item.Quantity).ThenBy(item => item.Id),
            ("quantity", true) => query.OrderByDescending(item => item.Quantity).ThenByDescending(item => item.Id),
            ("unitofmeasuretitle", false) => query.OrderBy(item => item.Product.UnitOfMeasure.Title).ThenBy(item => item.Id),
            ("unitofmeasuretitle", true) => query.OrderByDescending(item => item.Product.UnitOfMeasure.Title).ThenByDescending(item => item.Id),
            ("requestnumber", false) => query.OrderBy(item => item.RequestNumber).ThenBy(item => item.Id),
            ("requestnumber", true) => query.OrderByDescending(item => item.RequestNumber).ThenByDescending(item => item.Id),
            ("pagenumber", false) => query.OrderBy(item => item.GoodsReceipt.PageNumber).ThenBy(item => item.Id),
            ("pagenumber", true) => query.OrderByDescending(item => item.GoodsReceipt.PageNumber).ThenByDescending(item => item.Id),
            ("delivererfullname", false) => query.OrderBy(item => item.GoodsReceipt.Deliverer.FullName).ThenBy(item => item.Id),
            ("delivererfullname", true) => query.OrderByDescending(item => item.GoodsReceipt.Deliverer.FullName).ThenByDescending(item => item.Id),
            ("warehousetitle", false) => query.OrderBy(item => item.GoodsReceipt.Warehouse.Title).ThenBy(item => item.Id),
            ("warehousetitle", true) => query.OrderByDescending(item => item.GoodsReceipt.Warehouse.Title).ThenByDescending(item => item.Id),
            ("invoicestatus", false) => query.OrderBy(item => item.GoodsReceipt.InvoiceStatus).ThenBy(item => item.Id),
            ("invoicestatus", true) => query.OrderByDescending(item => item.GoodsReceipt.InvoiceStatus).ThenByDescending(item => item.Id),
            ("enteredatutc", false) => query.OrderBy(item => item.GoodsReceipt.EnteredAtUtc).ThenBy(item => item.Id),
            _ => query.OrderByDescending(item => item.GoodsReceipt.EnteredAtUtc).ThenByDescending(item => item.Id)
        };
}
