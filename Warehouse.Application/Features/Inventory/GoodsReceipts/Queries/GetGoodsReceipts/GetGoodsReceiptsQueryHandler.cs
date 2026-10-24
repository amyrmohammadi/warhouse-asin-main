using MediatR;
using Warehouse.Application.Common.Extensions;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceipts;

internal sealed class GetGoodsReceiptsQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsReceiptsQuery, Result<GoodsReceiptListResult>>
{
    public async Task<Result<GoodsReceiptListResult>> Handle(
        GetGoodsReceiptsQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsReceipt>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);
        var search = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(receipt =>
                receipt.PageNumber.Contains(search) || receipt.Deliverer.FullName.Contains(search)||
                receipt.Items.Any(item =>
                    (item.Invoice != null && item.Invoice.InvoiceNumber.Contains(search)) ||
                    (item.ReceiptNumber != null && item.ReceiptNumber.Contains(search)) ||
                    item.Product.Title.Contains(search) ||                  
                    item.Product.Code.Contains(search)));
        }

        query = query
            .ApplyDateFilter(request.EnteredAt, receipt => receipt.EnteredAtUtc)
            .ApplyTextFilter(request.PageNumberFilter, receipt => receipt.PageNumber);

        if (request.InvoiceStatus.HasValue)
        {
            query = request.InvoiceStatus.Value switch
            {
                InvoiceStatusFilter.NotEntered =>
                    query.Where(receipt => receipt.Items.Any(item => item.InvoiceStatus == null)),
                InvoiceStatusFilter.DoesNotHave =>
                    query.Where(receipt => receipt.Items.Any(item =>
                        item.InvoiceStatus == InvoiceStatus.DoesNotHave)),
                InvoiceStatusFilter.HasInvoice =>
                    query.Where(receipt => receipt.Items.Any(item =>
                        item.InvoiceStatus == InvoiceStatus.HasInvoice)),
                _ => query
            };
        }

        query = query
            .ApplyAnyTextFilter(
                request.ProductTitle,
                receipt => receipt.Items,
                item => item.Product.Title)
            .ApplyAnyTextFilter(
                request.ProductCode,
                receipt => receipt.Items,
                item => item.Product.Code)
            .ApplyAnyNumericFilter(
                request.Quantity,
                receipt => receipt.Items,
                item => item.Quantity)
            .ApplyAnyTextFilter(
                request.InvoiceNumber,
                receipt => receipt.Items,
                item => item.Invoice == null ? null : item.Invoice.InvoiceNumber)
            .ApplyAnyTextFilter(
                request.ReceiptNumber,
                receipt => receipt.Items,
                item => item.ReceiptNumber);

        if (request.WarehouseId.HasValue)
        {
            query = query.Where(receipt =>
                receipt.Items.Any(item => item.WarehouseId == request.WarehouseId.Value));
        }

        if (request.DelivererId.HasValue)
        {
            query = query.Where(receipt => receipt.DelivererId == request.DelivererId.Value);
        }

        if (request.SupplierId.HasValue)
        {
            query = query.Where(receipt =>
                receipt.Items.Any(item => item.Invoice != null &&
                    item.Invoice.SupplierId == request.SupplierId.Value));
        }

        var filteredCount = await repository.CountAsync(query, cancellationToken);
        query = ApplySorting(query, request.SortBy, request.SortDescending)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize);
        var items = await repository.ToListAsync(query.ToSummary(), cancellationToken);

        return Result<GoodsReceiptListResult>.Success(new GoodsReceiptListResult(
            items,
            totalCount,
            filteredCount,
            request.PageNumber,
            request.PageSize));
    }

    private static IQueryable<GoodsReceipt> ApplySorting(
        IQueryable<GoodsReceipt> query,
        string? sortBy,
        bool descending) =>
        (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("pagenumber", false) => query.OrderBy(item => item.PageNumber).ThenBy(item => item.Id),
            ("pagenumber", true) => query.OrderByDescending(item => item.PageNumber).ThenByDescending(item => item.Id),
            ("delivererfullname", false) => query.OrderBy(item => item.Deliverer.FullName).ThenBy(item => item.Id),
            ("delivererfullname", true) => query.OrderByDescending(item => item.Deliverer.FullName).ThenByDescending(item => item.Id),
            ("invoicestatus", false) => query.OrderBy(item => item.Items.Min(row => row.InvoiceStatus)).ThenBy(item => item.Id),
            ("invoicestatus", true) => query.OrderByDescending(item => item.Items.Max(row => row.InvoiceStatus)).ThenByDescending(item => item.Id),
            ("enteredatutc", false) => query.OrderBy(item => item.EnteredAtUtc).ThenBy(item => item.Id),
            _ => query.OrderByDescending(item => item.EnteredAtUtc).ThenByDescending(item => item.Id)
        };
}
