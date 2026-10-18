using FluentValidation;

namespace Warehouse.Application.Features.MasterData.Products.Commands.DeleteProduct;

internal sealed class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator() => RuleFor(command => command.Id).NotEmpty();
}
