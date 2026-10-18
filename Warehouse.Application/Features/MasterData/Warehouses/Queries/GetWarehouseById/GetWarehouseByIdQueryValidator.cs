using FluentValidation;

namespace Warehouse.Application.Features.MasterData.Warehouses.Queries.GetWarehouseById;

internal sealed class GetWarehouseByIdQueryValidator : AbstractValidator<GetWarehouseByIdQuery>
{
    public GetWarehouseByIdQueryValidator() =>
        RuleFor(query => query.Id).NotEmpty();
}
