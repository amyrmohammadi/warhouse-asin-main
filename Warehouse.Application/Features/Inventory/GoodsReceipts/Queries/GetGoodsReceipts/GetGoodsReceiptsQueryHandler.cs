using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;

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
                receipt.PageNumber.Contains(search) ||
                (receipt.InvoiceNumber != null && receipt.InvoiceNumber.Contains(search)) ||
                (receipt.ReceiptNumber != null && receipt.ReceiptNumber.Contains(search)) ||
                receipt.Deliverer.FullName.Contains(search) ||
                receipt.Warehouse.Title.Contains(search) ||
                (receipt.Supplier != null && receipt.Supplier.Title.Contains(search)) ||
                receipt.Items.Any(item =>
                    item.Product.Title.Contains(search) ||
                    item.Product.Code.Contains(search) ||
                    (item.RequestNumber != null && item.RequestNumber.Contains(search)) ||
                    (item.Description != null && item.Description.Contains(search))));
        }

        if (request.EnteredFromUtc.HasValue)
        {
            query = query.Where(receipt => receipt.EnteredAtUtc >= request.EnteredFromUtc.Value);
        }

        if (request.EnteredToUtc.HasValue)
        {
            query = query.Where(receipt => receipt.EnteredAtUtc <= request.EnteredToUtc.Value);
        }

        var pageNumber = request.PageNumberFilter?.Trim();
        if (!string.IsNullOrWhiteSpace(pageNumber))
        {
            query = query.Where(receipt => receipt.PageNumber.Contains(pageNumber));
        }

        if (request.InvoiceStatus.HasValue)
        {
            query = query.Where(receipt => receipt.InvoiceStatus == request.InvoiceStatus.Value);
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
            ("invoicestatus", false) => query.OrderBy(item => item.InvoiceStatus).ThenBy(item => item.Id),
            ("invoicestatus", true) => query.OrderByDescending(item => item.InvoiceStatus).ThenByDescending(item => item.Id),
            ("enteredatutc", false) => query.OrderBy(item => item.EnteredAtUtc).ThenBy(item => item.Id),
            _ => query.OrderByDescending(item => item.EnteredAtUtc).ThenByDescending(item => item.Id)
        };
}
