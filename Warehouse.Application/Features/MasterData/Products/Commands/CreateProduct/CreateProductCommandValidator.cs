using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.Products.Commands.CreateProduct;

internal sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator() =>
        MasterDataValidationRules.AddProductRules(
            this,
            command => command.Title,
            command => command.Code,
            command => command.UnitOfMeasureTitle,
            command => command.Description);
}
