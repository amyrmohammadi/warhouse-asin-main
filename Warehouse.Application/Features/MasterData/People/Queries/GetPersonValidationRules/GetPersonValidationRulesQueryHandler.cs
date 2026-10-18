using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Validation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.People.Queries.GetPersonValidationRules;

internal sealed class GetPersonValidationRulesQueryHandler
    : IRequestHandler<GetPersonValidationRulesQuery, Result<IReadOnlyDictionary<string, FieldValidationRules>>>
{
    public Task<Result<IReadOnlyDictionary<string, FieldValidationRules>>> Handle(
        GetPersonValidationRulesQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, FieldValidationRules> rules =
            new Dictionary<string, FieldValidationRules>(StringComparer.OrdinalIgnoreCase)
            {
                ["fullName"] = new(true, MasterDataValidationRules.FullNameMaxLength),
                ["phoneNumber"] = new(true, MasterDataValidationRules.PhoneNumberMaxLength)
            };

        return Task.FromResult(Result<IReadOnlyDictionary<string, FieldValidationRules>>.Success(rules));
    }
}
