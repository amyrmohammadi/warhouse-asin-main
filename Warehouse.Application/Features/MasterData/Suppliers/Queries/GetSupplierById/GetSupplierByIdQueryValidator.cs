using FluentValidation;

namespace Warehouse.Application.Features.MasterData.Suppliers.Queries.GetSupplierById;

internal sealed class GetSupplierByIdQueryValidator : AbstractValidator<GetSupplierByIdQuery>
{
    public GetSupplierByIdQueryValidator() =>
        RuleFor(query => query.Id).NotEmpty();
}
