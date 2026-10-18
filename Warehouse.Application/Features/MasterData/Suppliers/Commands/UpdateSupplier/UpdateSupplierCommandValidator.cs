using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.UpdateSupplier;

internal sealed class UpdateSupplierCommandValidator
    : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        MasterDataValidationRules.AddTitleRules(
            this,
            command => command.Title,
            command => command.Description);
    }
}
