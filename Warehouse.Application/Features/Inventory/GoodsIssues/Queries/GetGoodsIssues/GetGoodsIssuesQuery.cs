using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Extensions;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Common.Validation;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssues;

public sealed record GetGoodsIssuesQuery
    : DataTableParameters, IRequest<Result<GoodsIssueListResult>>
{
    public DateFilter? IssuedAt { get; init; }
    public TextFilter? IssueNumberFilter { get; init; }
    public TextFilter? ProductTitle { get; init; }
    public TextFilter? ProductCode { get; init; }
    public NumericFilter<decimal>? Quantity { get; init; }
    public TextFilter? AssetNumber { get; init; }
    public Guid? DestinationId { get; init; }
    public TextFilter? DeliveringUnit { get; init; }
    public Guid? IssuerId { get; init; }
}

internal sealed class GetGoodsIssuesQueryValidator : AbstractValidator<GetGoodsIssuesQuery>
{
    public GetGoodsIssuesQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(256);
        RuleFor(query => query.IssuedAt!).SetValidator(new DateFilterValidator()).When(query => query.IssuedAt is not null);
        RuleFor(query => query.IssueNumberFilter!).SetValidator(new TextFilterValidator(100)).When(query => query.IssueNumberFilter is not null);
        RuleFor(query => query.ProductTitle!).SetValidator(new TextFilterValidator(256)).When(query => query.ProductTitle is not null);
        RuleFor(query => query.ProductCode!).SetValidator(new TextFilterValidator(100)).When(query => query.ProductCode is not null);
        RuleFor(query => query.Quantity!).SetValidator(new NumericFilterValidator<decimal>(minimum: 0)).When(query => query.Quantity is not null);
        RuleFor(query => query.AssetNumber!).SetValidator(new TextFilterValidator(100)).When(query => query.AssetNumber is not null);
        RuleFor(query => query.DeliveringUnit!).SetValidator(new TextFilterValidator(200)).When(query => query.DeliveringUnit is not null);
    }
}

internal sealed class GetGoodsIssuesQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsIssuesQuery, Result<GoodsIssueListResult>>
{
    public async Task<Result<GoodsIssueListResult>> Handle(
        GetGoodsIssuesQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsIssue>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);
        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(issue =>
                issue.IssueNumber.Contains(search) ||
                issue.Issuer.FullName.Contains(search) ||
                issue.Items.Any(item =>
                    item.ProductCode.Contains(search) ||
                    (item.AssetNumber != null && item.AssetNumber.Contains(search)) ||
                    item.Destination.Title.Contains(search) ||
                    item.ExitReason.Contains(search) ||
                    item.DeliveringUnit.Contains(search)));
        }
        query = query
            .ApplyDateFilter(request.IssuedAt, issue => issue.IssuedAtUtc)
            .ApplyTextFilter(request.IssueNumberFilter, issue => issue.IssueNumber)
            .ApplyAnyTextFilter(request.ProductTitle, issue => issue.Items, item => item.Product == null ? null : item.Product.Title)
            .ApplyAnyTextFilter(request.ProductCode, issue => issue.Items, item => item.ProductCode)
            .ApplyAnyNumericFilter(request.Quantity, issue => issue.Items, item => item.Quantity)
            .ApplyAnyTextFilter(request.AssetNumber, issue => issue.Items, item => item.AssetNumber)
            .ApplyAnyTextFilter(request.DeliveringUnit, issue => issue.Items, item => item.DeliveringUnit);
        if (request.DestinationId.HasValue)
            query = query.Where(issue => issue.Items.Any(item => item.DestinationId == request.DestinationId.Value));
        if (request.IssuerId.HasValue)
            query = query.Where(issue => issue.IssuerId == request.IssuerId.Value);
        var filteredCount = await repository.CountAsync(query, cancellationToken);
        query = ApplySorting(query, request.SortBy, request.SortDescending)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize);
        var data = await repository.ToListAsync(query.ToSummary(), cancellationToken);
        return Result<GoodsIssueListResult>.Success(new(
            data, totalCount, filteredCount, request.PageNumber, request.PageSize));
    }

    private static IQueryable<GoodsIssue> ApplySorting(
        IQueryable<GoodsIssue> query,
        string? sortBy,
        bool descending) =>
        (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("issuenumber", false) => query.OrderBy(issue => issue.IssueNumber).ThenBy(issue => issue.Id),
            ("issuenumber", true) => query.OrderByDescending(issue => issue.IssueNumber).ThenByDescending(issue => issue.Id),
            ("issuerfullname", false) => query.OrderBy(issue => issue.Issuer.FullName).ThenBy(issue => issue.Id),
            ("issuerfullname", true) => query.OrderByDescending(issue => issue.Issuer.FullName).ThenByDescending(issue => issue.Id),
            ("issuedatutc", false) => query.OrderBy(issue => issue.IssuedAtUtc).ThenBy(issue => issue.Id),
            _ => query.OrderByDescending(issue => issue.IssuedAtUtc).ThenByDescending(issue => issue.Id)
        };
}
