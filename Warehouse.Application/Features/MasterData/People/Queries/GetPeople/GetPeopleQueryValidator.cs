using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.People.Queries.GetPeople;

internal sealed class GetPeopleQueryValidator : AbstractValidator<GetPeopleQuery>
{
    public GetPeopleQueryValidator()
    {
        MasterDataValidationRules.AddPersonListRules(this);

        RuleFor(query => query.FullName)
            .MaximumLength(MasterDataValidationRules.FullNameMaxLength)
            .When(query => !string.IsNullOrWhiteSpace(query.FullName));
        RuleFor(query => query.PhoneNumber)
            .MaximumLength(MasterDataValidationRules.PhoneNumberMaxLength)
            .When(query => !string.IsNullOrWhiteSpace(query.PhoneNumber));
    }
}
