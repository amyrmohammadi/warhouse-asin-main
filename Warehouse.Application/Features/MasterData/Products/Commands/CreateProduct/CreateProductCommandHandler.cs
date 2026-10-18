using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Products.Commands.CreateProduct;

internal sealed class CreateProductCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<CreateProductCommand, Result<ProductSummary>>
{
    public async Task<Result<ProductSummary>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        var duplicateMessage = await ProductDataHelper.FindDuplicateMessageAsync(
                unitOfWork,
                request.Title,
                request.Code,
                null,
                cancellationToken);
        if (duplicateMessage is not null)
        {
            return Result<ProductSummary>.Error(duplicateMessage);
        }

        await unitOfWork.BeginTransaction(cancellationToken);
        try
        {
            var unit = await ProductDataHelper.ResolveUnitOfMeasureAsync(
                unitOfWork,
                request.UnitOfMeasureTitle,
                cancellationToken);
            var product = new Product(
                request.Title,
                request.Code,
                unit.Id,
                request.Description);

            await unitOfWork.Repository<Product>().AddAsync(product, cancellationToken);
            await unitOfWork.SaveAsync(cancellationToken);
            await unitOfWork.CommitTransaction(cancellationToken);

            return Result<ProductSummary>.Success(
                ProductDataHelper.ToSummary(product, unit.Title));
        }
        catch
        {
            await unitOfWork.RollBackTransaction(cancellationToken);
            throw;
        }
    }
}
