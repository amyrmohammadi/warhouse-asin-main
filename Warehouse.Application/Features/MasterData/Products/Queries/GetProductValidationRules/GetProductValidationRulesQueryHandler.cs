using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Validation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.Products.Queries.GetProductValidationRules;

internal sealed class GetProductValidationRulesQueryHandler
    : IRequestHandler<GetProductValidationRulesQuery, Result<IReadOnlyDictionary<string, FieldValidationRules>>>
{
    public Task<Result<IReadOnlyDictionary<string, FieldValidationRules>>> Handle(
        GetProductValidationRulesQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, FieldValidationRules> rules =
            new Dictionary<string, FieldValidationRules>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = new(true, MasterDataValidationRules.TitleMaxLength),
                ["code"] = new(true, MasterDataValidationRules.ProductCodeMaxLength),
                ["unitOfMeasureTitle"] = new(true, MasterDataValidationRules.TitleMaxLength),
                ["description"] = new(false, MasterDataValidationRules.DescriptionMaxLength)
            };

        return Task.FromResult(
            Result<IReadOnlyDictionary<string, FieldValidationRules>>.Success(rules));
    }
}
