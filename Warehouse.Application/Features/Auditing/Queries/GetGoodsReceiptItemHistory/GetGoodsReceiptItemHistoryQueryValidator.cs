using FluentValidation;

namespace Warehouse.Application.Features.Auditing.Queries.GetGoodsReceiptItemHistory;

internal sealed class GetGoodsReceiptItemHistoryQueryValidator
    : AbstractValidator<GetGoodsReceiptItemHistoryQuery>
{
    public GetGoodsReceiptItemHistoryQueryValidator()
    {
        RuleFor(query => query.ItemId).NotEmpty();
    }
}
