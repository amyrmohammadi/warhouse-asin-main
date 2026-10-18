using FluentValidation;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.DeleteUnitOfMeasure;

internal sealed class DeleteUnitOfMeasureCommandValidator
    : AbstractValidator<DeleteUnitOfMeasureCommand>
{
    public DeleteUnitOfMeasureCommandValidator() =>
        RuleFor(command => command.Id).NotEmpty();
}
