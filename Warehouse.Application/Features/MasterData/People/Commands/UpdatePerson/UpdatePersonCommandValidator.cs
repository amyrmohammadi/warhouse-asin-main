using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.People.Commands.UpdatePerson;

internal sealed class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        MasterDataValidationRules.AddPersonRules(
            this,
            command => command.FullName,
            command => command.PhoneNumber);
    }
}
