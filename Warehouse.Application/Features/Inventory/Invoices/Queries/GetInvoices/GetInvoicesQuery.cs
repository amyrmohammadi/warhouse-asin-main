using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Extensions;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Common.Validation;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoices;

public sealed record GetInvoicesQuery
    : DataTableParameters, IRequest<Result<InvoiceListResult>>
{
    public TextFilter? InvoiceNumber { get; init; }

    public TextFilter? Counterparty { get; init; }
}

internal sealed class GetInvoicesQueryValidator : AbstractValidator<GetInvoicesQuery>
{
    public GetInvoicesQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(256);
        RuleFor(query => query.InvoiceNumber!)
            .SetValidator(new TextFilterValidator(100))
            .When(query => query.InvoiceNumber is not null);
        RuleFor(query => query.Counterparty!)
            .SetValidator(new TextFilterValidator(256))
            .When(query => query.Counterparty is not null);
    }
}

internal sealed class GetInvoicesQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetInvoicesQuery, Result<InvoiceListResult>>
{
    public async Task<Result<InvoiceListResult>> Handle(
        GetInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Invoice>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);

        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(invoice =>
                invoice.InvoiceNumber.Contains(search) ||
                (invoice.Supplier != null && invoice.Supplier.Title.Contains(search)));
        }

        query = query
            .ApplyTextFilter(request.InvoiceNumber, invoice => invoice.InvoiceNumber)
            .ApplyTextFilter(
                request.Counterparty,
                invoice => invoice.Supplier == null ? null : invoice.Supplier.Title);

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
            ("price", false) => query.OrderBy(invoice => invoice.Price).ThenBy(invoice => invoice.Id),
            ("price", true) => query.OrderByDescending(invoice => invoice.Price).ThenByDescending(invoice => invoice.Id),
            ("invoicenumber", false) => query.OrderBy(invoice => invoice.InvoiceNumber).ThenBy(invoice => invoice.Id),
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
            _ => query.OrderByDescending(invoice => invoice.InvoiceNumber).ThenByDescending(invoice => invoice.Id)
        };
}
