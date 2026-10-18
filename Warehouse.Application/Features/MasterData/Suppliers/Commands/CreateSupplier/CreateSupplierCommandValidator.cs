using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.CreateSupplier;

internal sealed class CreateSupplierCommandValidator
    : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator() =>
        MasterDataValidationRules.AddTitleRules(
            this,
            command => command.Title,
            command => command.Description);
}
