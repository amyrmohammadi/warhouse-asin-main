using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Extensions;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Common.Validation;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueItems;

public sealed record GetGoodsIssueItemsQuery
    : DataTableParameters, IRequest<Result<GoodsIssueItemRowListResult>>
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

internal sealed class GetGoodsIssueItemsQueryValidator : AbstractValidator<GetGoodsIssueItemsQuery>
{
    public GetGoodsIssueItemsQueryValidator()
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

internal sealed class GetGoodsIssueItemsQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsIssueItemsQuery, Result<GoodsIssueItemRowListResult>>
{
    public async Task<Result<GoodsIssueItemRowListResult>> Handle(
        GetGoodsIssueItemsQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsIssueItem>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);
        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(item =>
                item.GoodsIssue.IssueNumber.Contains(search) ||
                (item.GoodsIssue.Description != null && item.GoodsIssue.Description.Contains(search)) ||
                item.GoodsIssue.Issuer.FullName.Contains(search) ||
                item.ProductCode.Contains(search) ||
                (item.AssetNumber != null && item.AssetNumber.Contains(search)) ||
                item.Destination.Title.Contains(search) ||
                item.ExitReason.Contains(search) ||
                item.DeliveringUnit.Contains(search));
        }
        query = query
            .ApplyDateFilter(request.IssuedAt, item => item.GoodsIssue.IssuedAtUtc)
            .ApplyTextFilter(request.IssueNumberFilter, item => item.GoodsIssue.IssueNumber)
            .ApplyTextFilter(request.ProductTitle, item => item.Product == null ? null : item.Product.Title)
            .ApplyTextFilter(request.ProductCode, item => item.ProductCode)
            .ApplyNumericFilter(request.Quantity, item => item.Quantity)
            .ApplyTextFilter(request.AssetNumber, item => item.AssetNumber)
            .ApplyTextFilter(request.DeliveringUnit, item => item.DeliveringUnit);
        if (request.DestinationId.HasValue)
            query = query.Where(item => item.DestinationId == request.DestinationId.Value);
        if (request.IssuerId.HasValue)
            query = query.Where(item => item.GoodsIssue.IssuerId == request.IssuerId.Value);
        var filteredCount = await repository.CountAsync(query, cancellationToken);
        query = ApplySorting(query, request.SortBy, request.SortDescending)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize);
        var data = await repository.ToListAsync(query.Select(item => new GoodsIssueItemRowSummary
        {
            Id = item.Id,
            GoodsIssueId = item.GoodsIssueId,
            IssueNumber = item.GoodsIssue.IssueNumber,
            IssuedAtUtc = item.GoodsIssue.IssuedAtUtc,
            IssueDescription = item.GoodsIssue.Description,
            IssuerFullName = item.GoodsIssue.Issuer.FullName,
            ProductId = item.ProductId,
            ProductTitle = item.Product == null ? null : item.Product.Title,
            ProductUnitOfMeasureTitle = item.Product == null ? null : item.Product.UnitOfMeasure.Title,
            ProductCode = item.ProductCode,
            Quantity = item.Quantity,
            AssetNumber = item.AssetNumber,
            DestinationId = item.DestinationId,
            DestinationTitle = item.Destination.Title,
            ExitReason = item.ExitReason,
            Description = item.Description,
            DeliveringUnit = item.DeliveringUnit,
            ReturnAtUtc = item.ReturnAtUtc
        }), cancellationToken);
        return Result<GoodsIssueItemRowListResult>.Success(new(
            data, totalCount, filteredCount, request.PageNumber, request.PageSize));
    }

    private static IQueryable<GoodsIssueItem> ApplySorting(
        IQueryable<GoodsIssueItem> query,
        string? sortBy,
        bool descending) =>
        (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("issuenumber", false) => query.OrderBy(item => item.GoodsIssue.IssueNumber).ThenBy(item => item.Id),
            ("issuenumber", true) => query.OrderByDescending(item => item.GoodsIssue.IssueNumber).ThenByDescending(item => item.Id),
            ("productcode", false) => query.OrderBy(item => item.ProductCode).ThenBy(item => item.Id),
            ("productcode", true) => query.OrderByDescending(item => item.ProductCode).ThenByDescending(item => item.Id),
            ("quantity", false) => query.OrderBy(item => item.Quantity).ThenBy(item => item.Id),
            ("quantity", true) => query.OrderByDescending(item => item.Quantity).ThenByDescending(item => item.Id),
            ("destinationtitle", false) => query.OrderBy(item => item.Destination.Title).ThenBy(item => item.Id),
            ("destinationtitle", true) => query.OrderByDescending(item => item.Destination.Title).ThenByDescending(item => item.Id),
            ("issuedatutc", false) => query.OrderBy(item => item.GoodsIssue.IssuedAtUtc).ThenBy(item => item.Id),
            _ => query.OrderByDescending(item => item.GoodsIssue.IssuedAtUtc).ThenByDescending(item => item.Id)
        };
}
