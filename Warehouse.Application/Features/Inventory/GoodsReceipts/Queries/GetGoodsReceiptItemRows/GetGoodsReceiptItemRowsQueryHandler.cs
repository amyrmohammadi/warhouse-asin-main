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
        var access = request.ColumnAccess;
        request = request with
        {
            WarehouseId = access.Warehouse ? request.WarehouseId : null,
            SupplierId = access.Counterparty ? request.SupplierId : null,
            InvoiceStatus = access.InvoiceStatus ? request.InvoiceStatus : null,
            InvoiceNumber = access.InvoiceNumber ? request.InvoiceNumber : null,
            ReceiptNumber = access.ReceiptNumber ? request.ReceiptNumber : null,
            ReceiptNumberIsEmpty = access.ReceiptNumber && request.ReceiptNumberIsEmpty,
            SortBy = access.CanRead(request.SortBy) ? request.SortBy : null,
            QuickFilter = request.QuickFilter switch
            {
                GoodsReceiptQuickFilter.MissingReceipt when !access.ReceiptNumber => null,
                GoodsReceiptQuickFilter.MissingInvoice when !access.InvoiceStatus => null,
                _ => request.QuickFilter
            }
        };
        var repository = unitOfWork.Repository<GoodsReceiptItem>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);
        if (request.QuickFilter.HasValue)
        {
            var (startUtc, endUtc) = GoodsReceiptTodayRange.For(DateTimeOffset.UtcNow);
            query = request.QuickFilter.Value switch
            {
                GoodsReceiptQuickFilter.MissingReceipt => query.Where(item =>
                    item.ReceiptNumber == null || item.ReceiptNumber == ""),
                GoodsReceiptQuickFilter.MissingInvoice => query.Where(item => item.InvoiceStatus == null),
                GoodsReceiptQuickFilter.Today => query.Where(item =>
                    item.GoodsReceipt.EnteredAtUtc >= startUtc && item.GoodsReceipt.EnteredAtUtc < endUtc),
                _ => query
            };
        }
        if (request.ReceiptNumberIsEmpty)
            query = query.Where(item => item.ReceiptNumber == null || item.ReceiptNumber == "");
        var search = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(item =>
                item.GoodsReceipt.Deliverer.FullName.Contains(search) ||
                item.GoodsReceipt.PageNumber.Contains(search) ||
                (item.GoodsReceipt.Description != null &&
                    item.GoodsReceipt.Description.Contains(search)) ||
                item.Product.Title.Contains(search) ||
                item.Product.Code.Contains(search) ||
                (access.InvoiceNumber && item.Invoice != null && item.Invoice.InvoiceNumber.Contains(search)) ||
                (access.Counterparty && item.Invoice != null && item.Invoice.Supplier != null &&
                    item.Invoice.Supplier.Title.Contains(search)) ||
                (access.Counterparty && item.Invoice == null && item.Supplier != null && item.Supplier.Title.Contains(search)) ||
                (item.Description != null && item.Description.Contains(search)) ||
                (access.ReceiptNumber && item.ReceiptNumber != null && item.ReceiptNumber.Contains(search)));
        }

        query = query
            .ApplyDateFilter(request.EnteredAt, item => item.GoodsReceipt.EnteredAtUtc)
            .ApplyTextFilter(request.ProductTitle, item => item.Product.Title)
            .ApplyTextFilter(request.ProductCode, item => item.Product.Code)
            .ApplyTextFilter(request.PageNumberFilter, item => item.GoodsReceipt.PageNumber)
            .ApplyNumericFilter(request.Quantity, item => item.Quantity)
            .ApplyTextFilter(request.InvoiceNumber, item => item.Invoice == null ? null : item.Invoice.InvoiceNumber)
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
            query = query.Where(item => (item.Invoice == null ? item.SupplierId : item.Invoice.SupplierId)
                == request.SupplierId.Value);
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
                IsLocked = item.GoodsReceipt.Items.Any(row => row.Invoice != null &&
                    row.Invoice.ProcessingStatus != InvoiceProcessingStatus.UnderReview),
                GoodsIssueItemId = item.GoodsIssueItemId,
                IssueNumber = item.GoodsIssueItem == null ? null : item.GoodsIssueItem.GoodsIssue.IssueNumber,
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
                InvoiceNumber = item.Invoice == null ? null : item.Invoice.InvoiceNumber,
                SupplierId = item.Invoice == null ? item.SupplierId : item.Invoice.SupplierId,
                Counterparty = item.Invoice == null
                    ? item.Supplier == null ? null : item.Supplier.Title
                    : item.Invoice.Supplier == null ? null : item.Invoice.Supplier.Title,
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
            ("pagenumber", false) => OrderByPage(query, false),
            ("pagenumber", true) => OrderByPage(query, true),
            ("delivererfullname", false) => query.OrderBy(item => item.GoodsReceipt.Deliverer.FullName).ThenBy(item => item.Id),
            ("delivererfullname", true) => query.OrderByDescending(item => item.GoodsReceipt.Deliverer.FullName).ThenByDescending(item => item.Id),
            ("warehousetitle", false) => query.OrderBy(item => item.Warehouse.Title).ThenBy(item => item.Id),
            ("warehousetitle", true) => query.OrderByDescending(item => item.Warehouse.Title).ThenByDescending(item => item.Id),
            ("invoicestatus", false) => query.OrderBy(item => item.InvoiceStatus).ThenBy(item => item.Id),
            ("invoicestatus", true) => query.OrderByDescending(item => item.InvoiceStatus).ThenByDescending(item => item.Id),
            ("enteredatutc", false) => query.OrderBy(item => item.GoodsReceipt.EnteredAtUtc).ThenBy(item => item.Id),
            ("enteredatutc", true) => query.OrderByDescending(item => item.GoodsReceipt.EnteredAtUtc)
                .ThenBy(item => item.GoodsReceiptId).ThenBy(item => item.SortOrder).ThenBy(item => item.Id),
            _ => OrderByPage(query, true)
        };

    private static IOrderedQueryable<GoodsReceiptItem> OrderByPage(
        IQueryable<GoodsReceiptItem> query,
        bool descending)
    {
        // Compare digit counts first so page 10 follows page 9 in ascending order.
        var ordered = descending
            ? query.OrderByDescending(item => item.GoodsReceipt.PageNumber.Length)
                .ThenByDescending(item => item.GoodsReceipt.PageNumber)
            : query.OrderBy(item => item.GoodsReceipt.PageNumber.Length)
                .ThenBy(item => item.GoodsReceipt.PageNumber);

        return ordered.ThenBy(item => item.GoodsReceipt.EnteredAtUtc)
            .ThenBy(item => item.GoodsReceiptId)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.Id);
    }
}
