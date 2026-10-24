using MediatR;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Products.Commands.UpdateProduct;

internal sealed class UpdateProductCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdateProductCommand, Result<ProductSummary>>
{
    public async Task<Result<ProductSummary>> Handle(
        UpdateProductCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Product>();
        var product = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
        {
            return Result<ProductSummary>.Error("کالای مورد نظر پیدا نشد.");
        }

        if (!product.TryAdvanceConcurrencyStamp(request.ConcurrencyStamp))
        {
            return Result<ProductSummary>.Error(ConcurrencyMessages.Conflict);
        }

        var duplicateMessage = await ProductDataHelper.FindDuplicateMessageAsync(
                unitOfWork,
                request.Title,
                request.Code,
                request.Id,
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
            product.Update(request.Title, request.Code, unit.Id, request.Description);

            await repository.UpdateAsync(product, cancellationToken);
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
