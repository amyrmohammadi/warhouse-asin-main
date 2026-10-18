using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptOptions;

internal sealed class GetGoodsReceiptOptionsQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsReceiptOptionsQuery, Result<GoodsReceiptOptions>>
{
    public async Task<Result<GoodsReceiptOptions>> Handle(
        GetGoodsReceiptOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var peopleRepository = unitOfWork.Repository<Person>();
        var warehouseRepository = unitOfWork.Repository<StorageWarehouse>();
        var productRepository = unitOfWork.Repository<Product>();
        var supplierRepository = unitOfWork.Repository<Supplier>();
        var unitRepository = unitOfWork.Repository<UnitOfMeasure>();

        var people = await peopleRepository.ToListAsync(
            peopleRepository.Entities
                .OrderBy(person => person.FullName)
                .Select(person => new PersonSummary
                {
                    Id = person.Id,
                    FullName = person.FullName,
                    PhoneNumber = person.PhoneNumber
                }),
            cancellationToken);
        var warehouses = await warehouseRepository.ToListAsync(
            warehouseRepository.Entities
                .OrderBy(item => item.Title)
                .Select(item => new TitledMasterDataSummary
                {
                    Id = item.Id,
                    Title = item.Title,
                    Description = item.Description
                }),
            cancellationToken);
        var products = await productRepository.ToListAsync(
            productRepository.Entities
                .OrderBy(product => product.Title)
                .Select(product => new ProductSummary
                {
                    Id = product.Id,
                    Title = product.Title,
                    Code = product.Code,
                    UnitOfMeasureId = product.UnitOfMeasureId,
                    UnitOfMeasureTitle = product.UnitOfMeasure.Title,
                    Description = product.Description
                }),
            cancellationToken);
        var suppliers = await supplierRepository.ToListAsync(
            supplierRepository.Entities
                .OrderBy(item => item.Title)
                .Select(item => new TitledMasterDataSummary
                {
                    Id = item.Id,
                    Title = item.Title,
                    Description = item.Description
                }),
            cancellationToken);
        var units = await unitRepository.ToListAsync(
            unitRepository.Entities
                .OrderBy(item => item.Title)
                .Select(item => new TitledMasterDataSummary
                {
                    Id = item.Id,
                    Title = item.Title,
                    Description = item.Description
                }),
            cancellationToken);

        return Result<GoodsReceiptOptions>.Success(new GoodsReceiptOptions
        {
            People = people,
            Warehouses = warehouses,
            Products = products,
            Suppliers = suppliers,
            UnitsOfMeasure = units
        });
    }
}
