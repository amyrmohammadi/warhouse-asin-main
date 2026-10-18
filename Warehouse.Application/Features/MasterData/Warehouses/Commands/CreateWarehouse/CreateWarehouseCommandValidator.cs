using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.Warehouses.Commands.CreateWarehouse;

internal sealed class CreateWarehouseCommandValidator
    : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator() =>
        MasterDataValidationRules.AddTitleRules(
            this,
            command => command.Title,
            command => command.Description);
}
