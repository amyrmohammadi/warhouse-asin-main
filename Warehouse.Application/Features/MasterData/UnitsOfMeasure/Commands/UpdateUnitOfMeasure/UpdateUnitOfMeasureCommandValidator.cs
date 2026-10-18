using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.UpdateUnitOfMeasure;

internal sealed class UpdateUnitOfMeasureCommandValidator
    : AbstractValidator<UpdateUnitOfMeasureCommand>
{
    public UpdateUnitOfMeasureCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        MasterDataValidationRules.AddTitleRules(
            this,
            command => command.Title,
            command => command.Description);
    }
}
