using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.CreateUnitOfMeasure;

internal sealed class CreateUnitOfMeasureCommandValidator
    : AbstractValidator<CreateUnitOfMeasureCommand>
{
    public CreateUnitOfMeasureCommandValidator() =>
        MasterDataValidationRules.AddTitleRules(
            this,
            command => command.Title,
            command => command.Description);
}
