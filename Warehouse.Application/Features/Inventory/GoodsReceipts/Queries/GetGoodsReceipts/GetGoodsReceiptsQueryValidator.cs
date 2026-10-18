using FluentValidation;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceipts;

internal sealed class GetGoodsReceiptsQueryValidator : AbstractValidator<GetGoodsReceiptsQuery>
{
    private static readonly string[] SortFields =
        ["enteredAtUtc", "pageNumber", "delivererFullName", "invoiceStatus"];

    public GetGoodsReceiptsQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(256);
        RuleFor(query => query.PageNumberFilter).MaximumLength(100);
        RuleFor(query => query.SortBy)
            .Must(value => SortFields.Contains(value!, StringComparer.OrdinalIgnoreCase))
            .When(query => !string.IsNullOrWhiteSpace(query.SortBy));
        RuleFor(query => query.InvoiceStatus)
            .IsInEnum()
            .When(query => query.InvoiceStatus.HasValue);
        RuleFor(query => query.EnteredToUtc)
            .GreaterThanOrEqualTo(query => query.EnteredFromUtc)
            .When(query => query.EnteredFromUtc.HasValue && query.EnteredToUtc.HasValue);
    }
}
