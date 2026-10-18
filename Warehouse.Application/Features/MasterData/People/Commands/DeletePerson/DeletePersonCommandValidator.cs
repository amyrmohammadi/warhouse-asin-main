using FluentValidation;

namespace Warehouse.Application.Features.MasterData.People.Commands.DeletePerson;

internal sealed class DeletePersonCommandValidator : AbstractValidator<DeletePersonCommand>
{
    public DeletePersonCommandValidator() =>
        RuleFor(command => command.Id).NotEmpty();
}
