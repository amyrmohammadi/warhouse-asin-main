using FluentValidation;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.DeleteSupplier;

internal sealed class DeleteSupplierCommandValidator
    : AbstractValidator<DeleteSupplierCommand>
{
    public DeleteSupplierCommandValidator() =>
        RuleFor(command => command.Id).NotEmpty();
}
