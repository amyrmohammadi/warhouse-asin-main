using FluentValidation;
using Warehouse.Application.Common.Validation;

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
        RuleFor(query => query.PageSize).InclusiveBetween(1, 500);
        RuleFor(query => query.Search).MaximumLength(256);
        RuleFor(query => query.EnteredAt!)
            .SetValidator(new DateFilterValidator())
            .When(query => query.EnteredAt is not null);
        RuleFor(query => query.ProductTitle!)
            .SetValidator(new TextFilterValidator(256))
            .When(query => query.ProductTitle is not null);
        RuleFor(query => query.ProductCode!)
            .SetValidator(new TextFilterValidator(100))
            .When(query => query.ProductCode is not null);
        RuleFor(query => query.PageNumberFilter!)
            .SetValidator(new TextFilterValidator(100))
            .When(query => query.PageNumberFilter is not null);
        RuleFor(query => query.Quantity!)
            .SetValidator(new NumericFilterValidator<decimal>(minimum: 0))
            .When(query => query.Quantity is not null);
        RuleFor(query => query.InvoiceNumber!)
            .SetValidator(new TextFilterValidator(100))
            .When(query => query.InvoiceNumber is not null);
        RuleFor(query => query.ReceiptNumber!)
            .SetValidator(new TextFilterValidator(100))
            .When(query => query.ReceiptNumber is not null);
        RuleFor(query => query.SortBy)
            .Must(value => SortFields.Contains(value!, StringComparer.OrdinalIgnoreCase))
            .When(query => !string.IsNullOrWhiteSpace(query.SortBy));
        RuleFor(query => query.InvoiceStatus)
            .IsInEnum()
            .When(query => query.InvoiceStatus.HasValue);
    }
}
