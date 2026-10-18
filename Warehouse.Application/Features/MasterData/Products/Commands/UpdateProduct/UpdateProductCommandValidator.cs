using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.Products.Commands.UpdateProduct;

internal sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        MasterDataValidationRules.AddProductRules(
            this,
            command => command.Title,
            command => command.Code,
            command => command.UnitOfMeasureTitle,
            command => command.Description);
    }
}
