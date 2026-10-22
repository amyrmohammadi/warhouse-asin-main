using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Validation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Queries.GetGoodsIssueDestinationValidationRules;

public sealed record GetGoodsIssueDestinationValidationRulesQuery
    : IRequest<Result<IReadOnlyDictionary<string, FieldValidationRules>>>;

internal sealed class GetGoodsIssueDestinationValidationRulesQueryHandler
    : IRequestHandler<GetGoodsIssueDestinationValidationRulesQuery,
        Result<IReadOnlyDictionary<string, FieldValidationRules>>>
{
    public Task<Result<IReadOnlyDictionary<string, FieldValidationRules>>> Handle(
        GetGoodsIssueDestinationValidationRulesQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, FieldValidationRules> rules =
            new Dictionary<string, FieldValidationRules>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = new(true, MasterDataValidationRules.TitleMaxLength),
                ["description"] = new(false, MasterDataValidationRules.DescriptionMaxLength)
            };
        return Task.FromResult(Result<IReadOnlyDictionary<string, FieldValidationRules>>.Success(rules));
    }
}
