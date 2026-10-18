using FluentValidation;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;

internal sealed class GetGoodsReceiptItemRowsQueryValidator
    : AbstractValidator<GetGoodsReceiptItemRowsQuery>
{
    private static readonly string[] SortFields =
    [
        "productTitle",
        "productCode",
        "quantity",
        "unitOfMeasureTitle",
        "requestNumber",
        "enteredAtUtc",
        "pageNumber",
        "delivererFullName",
        "warehouseTitle",
        "invoiceStatus"
    ];

    public GetGoodsReceiptItemRowsQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(256);
        RuleFor(query => query.SortBy)
            .Must(value => SortFields.Contains(value!, StringComparer.OrdinalIgnoreCase))
            .When(query => !string.IsNullOrWhiteSpace(query.SortBy));
    }
}
