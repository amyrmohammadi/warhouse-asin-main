using MediatR;
using Warehouse.Application.Common.Extensions;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

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
            item.GoodsReceipt.Deliverer.FullName.Contains(search) || 
            item.GoodsReceipt.PageNumber.Contains(search) ||
                item.Product.Title.Contains(search) ||
                item.Product.Code.Contains(search) ||
                item.GoodsReceipt.PageNumber.Contains(search) ||
                (item.InvoiceNumber != null && item.InvoiceNumber.Contains(search)) ||
                (item.ReceiptNumber != null && item.ReceiptNumber.Contains(search)));
        }

        query = query
            .ApplyDateFilter(request.EnteredAt, item => item.GoodsReceipt.EnteredAtUtc)
            .ApplyTextFilter(request.ProductTitle, item => item.Product.Title)
            .ApplyTextFilter(request.ProductCode, item => item.Product.Code)
            .ApplyTextFilter(request.PageNumberFilter, item => item.GoodsReceipt.PageNumber)
            .ApplyNumericFilter(request.Quantity, item => item.Quantity)
            .ApplyTextFilter(request.InvoiceNumber, item => item.InvoiceNumber)
            .ApplyTextFilter(request.ReceiptNumber, item => item.ReceiptNumber);

        if (request.WarehouseId.HasValue)
        {
            query = query.Where(item => item.WarehouseId == request.WarehouseId.Value);
        }

        if (request.DelivererId.HasValue)
        {
            query = query.Where(item => item.GoodsReceipt.DelivererId == request.DelivererId.Value);
        }

        if (request.SupplierId.HasValue)
        {
            query = query.Where(item => item.SupplierId == request.SupplierId.Value);
        }

        if (request.InvoiceStatus.HasValue)
        {
            query = request.InvoiceStatus.Value switch
            {
                InvoiceStatusFilter.NotEntered =>
                    query.Where(item => item.InvoiceStatus == null),
                InvoiceStatusFilter.DoesNotHave =>
                    query.Where(item => item.InvoiceStatus == InvoiceStatus.DoesNotHave),
                InvoiceStatusFilter.HasInvoice =>
                    query.Where(item => item.InvoiceStatus == InvoiceStatus.HasInvoice),
                _ => query
            };
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
                WarehouseTitle = item.Warehouse.Title,
                InvoiceStatus = item.InvoiceStatus,
                InvoiceNumber = item.InvoiceNumber,
                SupplierId = item.SupplierId,
                Counterparty = item.Supplier == null
                    ? null
                    : item.Supplier.Title,
                ReceiptNumber = item.ReceiptNumber,
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
            ("warehousetitle", false) => query.OrderBy(item => item.Warehouse.Title).ThenBy(item => item.Id),
            ("warehousetitle", true) => query.OrderByDescending(item => item.Warehouse.Title).ThenByDescending(item => item.Id),
            ("invoicestatus", false) => query.OrderBy(item => item.InvoiceStatus).ThenBy(item => item.Id),
            ("invoicestatus", true) => query.OrderByDescending(item => item.InvoiceStatus).ThenByDescending(item => item.Id),
            ("enteredatutc", false) => query.OrderBy(item => item.GoodsReceipt.EnteredAtUtc).ThenBy(item => item.Id),
            _ => query.OrderByDescending(item => item.GoodsReceipt.EnteredAtUtc).ThenByDescending(item => item.Id)
        };
}
