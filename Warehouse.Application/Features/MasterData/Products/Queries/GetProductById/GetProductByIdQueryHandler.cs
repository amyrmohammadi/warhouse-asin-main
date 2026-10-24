using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Products.Queries.GetProductById;

internal sealed class GetProductByIdQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetProductByIdQuery, Result<ProductSummary>>
{
    public async Task<Result<ProductSummary>> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Product>();
        var items = await repository.ToListAsync(
            repository.Entities
                .Where(product => product.Id == request.Id)
                .Select(product => new ProductSummary
                {
                    Id = product.Id,
                    ConcurrencyStamp = product.ConcurrencyStamp,
                    Title = product.Title,
                    Code = product.Code,
                    UnitOfMeasureId = product.UnitOfMeasureId,
                    UnitOfMeasureTitle = product.UnitOfMeasure.Title,
                    Description = product.Description
                })
                .Take(1),
            cancellationToken);

        return items.Count == 0
            ? Result<ProductSummary>.Error("کالای مورد نظر پیدا نشد.")
            : Result<ProductSummary>.Success(items[0]);
    }
}
