using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.Warehouses.Commands.UpdateWarehouse;

internal sealed class UpdateWarehouseCommandValidator
    : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        MasterDataValidationRules.AddTitleRules(
            this,
            command => command.Title,
            command => command.Description);
    }
}
