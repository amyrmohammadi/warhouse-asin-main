using FluentValidation;

namespace Warehouse.Application.Features.MasterData.Products.Queries.GetProductById;

internal sealed class GetProductByIdQueryValidator : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdQueryValidator() => RuleFor(query => query.Id).NotEmpty();
}
