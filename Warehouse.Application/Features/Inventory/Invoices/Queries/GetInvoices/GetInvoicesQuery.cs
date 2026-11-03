using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Extensions;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Common.Validation;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoices;

public sealed record GetInvoicesQuery
    : DataTableParameters, IRequest<Result<InvoiceListResult>>
{
    public TextFilter? InvoiceNumber { get; init; }

    public TextFilter? Counterparty { get; init; }
    public TextFilter? ReceiptNumber { get; init; }
    public TextFilter? ProductTitle { get; init; }
    public InvoiceProcessingStatus? ProcessingStatus { get; init; }
    public DateFilter? FinanceDeliveryDate { get; init; }
    public DateFilter? InvoiceDate { get; init; }
    public InventoryDateQuickFilter? QuickFilter { get; init; }
}

internal sealed class GetInvoicesQueryValidator : AbstractValidator<GetInvoicesQuery>
{
    public GetInvoicesQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(256);
        RuleFor(query => query.QuickFilter).IsInEnum().When(query => query.QuickFilter.HasValue);
        RuleFor(query => query.ProcessingStatus).IsInEnum().When(query => query.ProcessingStatus.HasValue);
        RuleFor(query => query.FinanceDeliveryDate!).SetValidator(new DateFilterValidator())
            .When(query => query.FinanceDeliveryDate is not null);
        RuleFor(query => query.InvoiceDate!).SetValidator(new DateFilterValidator())
            .When(query => query.InvoiceDate is not null);
        RuleFor(query => query.ReceiptNumber!).SetValidator(new TextFilterValidator(100))
            .When(query => query.ReceiptNumber is not null);
        RuleFor(query => query.ProductTitle!).SetValidator(new TextFilterValidator(256))
            .When(query => query.ProductTitle is not null);
        RuleFor(query => query.InvoiceNumber!)
            .SetValidator(new TextFilterValidator(100))
            .When(query => query.InvoiceNumber is not null);
        RuleFor(query => query.Counterparty!)
            .SetValidator(new TextFilterValidator(256))
            .When(query => query.Counterparty is not null);
    }
}

internal sealed class GetInvoicesQueryHandler(IUnitOfWork<Guid> unitOfWork, TimeProvider clock)
    : IRequestHandler<GetInvoicesQuery, Result<InvoiceListResult>>
{
    public async Task<Result<InvoiceListResult>> Handle(
        GetInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Invoice>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);
        var now = clock.GetUtcNow();
        var today = Invoice.GetReceiptDate(now);
        var (start, end) = InventoryTodayRange.For(now);
        query = request.QuickFilter switch
        {
            InventoryDateQuickFilter.Today => query.Where(invoice => invoice.InvoiceDate == today),
            InventoryDateQuickFilter.ItemsToday => query.Where(invoice => invoice.InvoiceDate == today && invoice.GoodsReceiptItems.Any()),
            InventoryDateQuickFilter.CreatedToday => query.Where(invoice => invoice.CreatedOn >= start && invoice.CreatedOn < end),
            InventoryDateQuickFilter.ItemsCreatedToday => query.Where(invoice => invoice.GoodsReceiptItems.Any(item =>
                item.CreatedOn >= start && item.CreatedOn < end)),
            _ => query
        };

        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(invoice =>
                invoice.InvoiceNumber.Contains(search) ||
                (invoice.Supplier != null && invoice.Supplier.Title.Contains(search)) ||
                invoice.GoodsReceiptItems.Any(item =>
                    (item.ReceiptNumber != null && item.ReceiptNumber.Contains(search)) ||
                    (item.Product != null && (item.Product.Title.Contains(search) || item.Product.Code.Contains(search)))));
        }

        query = query
            .ApplyAnyTextFilter(request.ReceiptNumber, invoice => invoice.GoodsReceiptItems, item => item.ReceiptNumber)
            .ApplyAnyTextFilter(request.ProductTitle, invoice => invoice.GoodsReceiptItems,
                item => item.Product == null ? null : item.Product.Title)
            .ApplyTextFilter(request.InvoiceNumber, invoice => invoice.InvoiceNumber)
            .ApplyTextFilter(
                request.Counterparty,
                invoice => invoice.Supplier == null ? null : invoice.Supplier.Title);

        if (request.ProcessingStatus.HasValue)
            query = query.Where(invoice => invoice.ProcessingStatus == request.ProcessingStatus.Value);

        if (request.FinanceDeliveryDate is { } date)
        {
            // Delivery dates are calendar dates; timezone offsets must not shift them.
            query = date.Operator switch
            {
                DateOperator.Before => query.Where(invoice => invoice.FinanceDeliveryDate <= date.Value),
                DateOperator.After => query.Where(invoice => invoice.FinanceDeliveryDate >= date.Value),
                DateOperator.Eq => query.Where(invoice => invoice.FinanceDeliveryDate == date.Value),
                DateOperator.Between => query.Where(invoice =>
                    invoice.FinanceDeliveryDate >= date.Value && invoice.FinanceDeliveryDate <= date.ValueTo),
                _ => query
            };
        }

        if (request.InvoiceDate is { } invoiceDate)
        {
            query = invoiceDate.Operator switch
            {
                DateOperator.Before => query.Where(invoice => invoice.InvoiceDate <= invoiceDate.Value),
                DateOperator.After => query.Where(invoice => invoice.InvoiceDate >= invoiceDate.Value),
                DateOperator.Eq => query.Where(invoice => invoice.InvoiceDate == invoiceDate.Value),
                DateOperator.Between => query.Where(invoice =>
                    invoice.InvoiceDate >= invoiceDate.Value && invoice.InvoiceDate <= invoiceDate.ValueTo),
                _ => query
            };
        }

        var filteredCount = await repository.CountAsync(query, cancellationToken);
        query = ApplySorting(query, request.SortBy, request.SortDescending)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize);
        var projectedData = await repository.ToListAsync(query.ToSummary(), cancellationToken);
        var data = projectedData.Select(summary => new InvoiceSummary
        {
            Id = summary.Id,
            Price = summary.Price,
            InvoiceNumber = summary.InvoiceNumber,
            Description = summary.Description,
            InvoiceDate = summary.InvoiceDate,
            FinanceDeliveryDate = summary.FinanceDeliveryDate,
            ProcessingStatus = summary.ProcessingStatus,
            Counterparty = summary.Counterparty,
            ReceiptNumbers = summary.ReceiptNumbers
                .Distinct(StringComparer.Ordinal)
                .OrderBy(receiptNumber => receiptNumber, StringComparer.Ordinal)
                .ToArray(),
            ItemsWithoutReceiptCount = summary.ItemsWithoutReceiptCount
        }).ToArray();

        return Result<InvoiceListResult>.Success(new(
            data,
            totalCount,
            filteredCount,
            request.PageNumber,
            request.PageSize));
    }

    private static IQueryable<Invoice> ApplySorting(
        IQueryable<Invoice> query,
        string? sortBy,
        bool descending) =>
        (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("id", false) => query.OrderBy(item => item.Id),
            ("id", true) => query.OrderByDescending(item => item.Id),
            ("createdatutc", false) => query.OrderBy(invoice => invoice.CreatedAtUtc).ThenBy(invoice => invoice.Id),
            ("createdatutc", true) => query.OrderByDescending(invoice => invoice.CreatedAtUtc).ThenByDescending(invoice => invoice.Id),
            ("financedeliverydate", false) => query.OrderBy(invoice => invoice.FinanceDeliveryDate).ThenBy(invoice => invoice.Id),
            ("financedeliverydate", true) => query.OrderByDescending(invoice => invoice.FinanceDeliveryDate).ThenBy(invoice => invoice.Id),
            ("price", false) => query.OrderBy(invoice => invoice.Price).ThenBy(invoice => invoice.Id),
            ("price", true) => query.OrderByDescending(invoice => invoice.Price).ThenByDescending(invoice => invoice.Id),
            ("invoicenumber", false) => query.OrderBy(invoice => invoice.InvoiceNumber).ThenBy(invoice => invoice.Id),
            ("invoicenumber", true) => query.OrderByDescending(invoice => invoice.InvoiceNumber).ThenByDescending(invoice => invoice.Id),
            ("counterparty", false) => query.OrderBy(invoice => invoice.Supplier == null ? null : invoice.Supplier.Title)
                .ThenBy(invoice => invoice.InvoiceNumber),
            ("counterparty", true) => query.OrderByDescending(invoice => invoice.Supplier == null ? null : invoice.Supplier.Title)
                .ThenByDescending(invoice => invoice.InvoiceNumber),
            ("invoicedate", false) => query.OrderBy(invoice => invoice.InvoiceDate)
                .ThenBy(invoice => invoice.InvoiceNumber),
            ("invoicedate", true) => query.OrderByDescending(invoice => invoice.InvoiceDate)
                .ThenByDescending(invoice => invoice.InvoiceNumber),
            ("processingstatus", false) => query.OrderBy(invoice => invoice.ProcessingStatus)
                .ThenBy(invoice => invoice.InvoiceNumber),
            ("processingstatus", true) => query.OrderByDescending(invoice => invoice.ProcessingStatus)
                .ThenByDescending(invoice => invoice.InvoiceNumber),
            _ => query.OrderByDescending(invoice => invoice.CreatedAtUtc).ThenByDescending(invoice => invoice.Id)
        };
}
