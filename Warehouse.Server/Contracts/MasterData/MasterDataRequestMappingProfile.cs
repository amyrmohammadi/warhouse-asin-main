using AutoMapper;
using Warehouse.Application.Features.MasterData.People.Commands.CreatePerson;
using Warehouse.Application.Features.MasterData.People.Commands.UpdatePerson;
using Warehouse.Application.Features.MasterData.Products.Commands.CreateProduct;
using Warehouse.Application.Features.MasterData.Products.Commands.UpdateProduct;
using Warehouse.Application.Features.MasterData.Suppliers.Commands.CreateSupplier;
using Warehouse.Application.Features.MasterData.Suppliers.Commands.UpdateSupplier;
using Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.CreateUnitOfMeasure;
using Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.UpdateUnitOfMeasure;
using Warehouse.Application.Features.MasterData.Warehouses.Commands.CreateWarehouse;
using Warehouse.Application.Features.MasterData.Warehouses.Commands.UpdateWarehouse;

namespace Warehouse.Server.Contracts.MasterData;

public sealed class MasterDataRequestMappingProfile : Profile
{
    public MasterDataRequestMappingProfile()
    {
        CreateMap<TitledMasterDataRequest, CreateUnitOfMeasureCommand>()
            .ConstructUsing(request => new CreateUnitOfMeasureCommand(
                request.Title,
                request.Description));
        CreateMap<TitledMasterDataRequest, UpdateUnitOfMeasureCommand>()
            .ConstructUsing(request => new UpdateUnitOfMeasureCommand(
                Guid.Empty,
                request.Title,
                request.Description,
                request.ConcurrencyStamp ?? Guid.Empty));
        CreateMap<TitledMasterDataRequest, CreateSupplierCommand>()
            .ConstructUsing(request => new CreateSupplierCommand(
                request.Title,
                request.Description));
        CreateMap<TitledMasterDataRequest, UpdateSupplierCommand>()
            .ConstructUsing(request => new UpdateSupplierCommand(
                Guid.Empty,
                request.Title,
                request.Description,
                request.ConcurrencyStamp ?? Guid.Empty));
        CreateMap<TitledMasterDataRequest, CreateWarehouseCommand>()
            .ConstructUsing(request => new CreateWarehouseCommand(
                request.Title,
                request.Description));
        CreateMap<TitledMasterDataRequest, UpdateWarehouseCommand>()
            .ConstructUsing(request => new UpdateWarehouseCommand(
                Guid.Empty,
                request.Title,
                request.Description,
                request.ConcurrencyStamp ?? Guid.Empty));

        CreateMap<PersonRequest, CreatePersonCommand>()
            .ConstructUsing(request => new CreatePersonCommand(
                request.FullName,
                request.PhoneNumber));

        CreateMap<PersonRequest, UpdatePersonCommand>()
            .ConstructUsing(request => new UpdatePersonCommand(
                Guid.Empty,
                request.FullName,
                request.PhoneNumber,
                request.ConcurrencyStamp ?? Guid.Empty));

        CreateMap<ProductRequest, CreateProductCommand>()
            .ConstructUsing(request => new CreateProductCommand(
                request.Title,
                request.Code,
                request.UnitOfMeasureTitle,
                request.Description));
        CreateMap<ProductRequest, UpdateProductCommand>()
            .ConstructUsing(request => new UpdateProductCommand(
                Guid.Empty,
                request.Title,
                request.Code,
                request.UnitOfMeasureTitle,
                request.Description,
                request.ConcurrencyStamp ?? Guid.Empty));
    }
}
