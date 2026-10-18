using FluentValidation;

namespace Warehouse.Application.Features.MasterData.Warehouses.Commands.DeleteWarehouse;

internal sealed class DeleteWarehouseCommandValidator
    : AbstractValidator<DeleteWarehouseCommand>
{
    public DeleteWarehouseCommandValidator() =>
        RuleFor(command => command.Id).NotEmpty();
}
