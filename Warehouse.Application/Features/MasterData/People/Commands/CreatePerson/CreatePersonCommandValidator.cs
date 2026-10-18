using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.People.Commands.CreatePerson;

internal sealed class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator() =>
        MasterDataValidationRules.AddPersonRules(
            this,
            command => command.FullName,
            command => command.PhoneNumber);
}
