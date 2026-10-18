using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;

internal sealed class CreateGoodsReceiptCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<CreateGoodsReceiptCommand, Result<GoodsReceiptSummary>>
{
    public async Task<Result<GoodsReceiptSummary>> Handle(
        CreateGoodsReceiptCommand request,
        CancellationToken cancellationToken)
    {
        var warehouseRepository = unitOfWork.Repository<StorageWarehouse>();
        var warehouse = await warehouseRepository.GetByIdAsync(
            request.WarehouseId,
            cancellationToken);
        if (warehouse is null)
        {
            return Result<GoodsReceiptSummary>.Error("انبار انتخاب‌شده پیدا نشد.");
        }

        await unitOfWork.BeginTransaction(cancellationToken);
        try
        {
            var (deliverer, delivererError) = await ResolveDelivererAsync(
                request.DelivererId,
                request.DelivererFullName,
                cancellationToken);
            if (delivererError is not null)
            {
                await unitOfWork.RollBackTransaction(cancellationToken);
                return Result<GoodsReceiptSummary>.Error(delivererError);
            }

            var (supplier, supplierError) = await ResolveSupplierAsync(
                request.SupplierId,
                request.SupplierTitle,
                cancellationToken);
            if (supplierError is not null)
            {
                await unitOfWork.RollBackTransaction(cancellationToken);
                return Result<GoodsReceiptSummary>.Error(supplierError);
            }

            var resolvedItems = await ResolveProductsAsync(request.Items, cancellationToken);
            if (!resolvedItems.IsSuccess)
            {
                await unitOfWork.RollBackTransaction(cancellationToken);
                return Result<GoodsReceiptSummary>.Error(resolvedItems.Messages);
            }

            var receipt = new GoodsReceipt(
                request.EnteredAtUtc,
                request.Description,
                deliverer!.Id,
                request.WarehouseId,
                request.InvoiceStatus,
                request.InvoiceNumber,
                supplier?.Id,
                request.ReceiptNumber,
                request.PageNumber);

            foreach (var item in resolvedItems.Data!)
            {
                receipt.AddItem(
                    item.ProductId,
                    item.Quantity,
                    item.RequestNumber,
                    item.Description);
            }

            await unitOfWork.Repository<GoodsReceipt>().AddAsync(receipt, cancellationToken);
            await unitOfWork.SaveAsync(cancellationToken);
            await unitOfWork.CommitTransaction(cancellationToken);

            return Result<GoodsReceiptSummary>.Success(new GoodsReceiptSummary
            {
                Id = receipt.Id,
                EnteredAtUtc = receipt.EnteredAtUtc,
                Description = receipt.Description,
                DelivererId = deliverer.Id,
                DelivererFullName = deliverer.FullName,
                WarehouseId = warehouse.Id,
                WarehouseTitle = warehouse.Title,
                InvoiceStatus = receipt.InvoiceStatus,
                InvoiceNumber = receipt.InvoiceNumber,
                SupplierId = supplier?.Id,
                Counterparty = supplier?.Title,
                ReceiptNumber = receipt.ReceiptNumber,
                PageNumber = receipt.PageNumber,
                Items = resolvedItems.Data!.Select(item => new GoodsReceiptItemSummary
                {
                    ProductId = item.ProductId,
                    ProductTitle = item.ProductTitle,
                    ProductCode = item.ProductCode,
                    UnitOfMeasureTitle = item.UnitTitle,
                    Quantity = item.Quantity,
                    RequestNumber = item.RequestNumber,
                    Description = item.Description
                }).ToArray()
            });
        }
        catch
        {
            await unitOfWork.RollBackTransaction(cancellationToken);
            throw;
        }
    }

    private async Task<(Person? Deliverer, string? Error)> ResolveDelivererAsync(
        Guid? delivererId,
        string? delivererFullName,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Person>();
        if (delivererId.HasValue)
        {
            var deliverer = await repository.GetByIdAsync(delivererId.Value, cancellationToken);
            return deliverer is null
                ? (null, "تحویل‌دهنده انتخاب‌شده پیدا نشد.")
                : (deliverer, null);
        }

        if (string.IsNullOrWhiteSpace(delivererFullName))
        {
            return (null, "نام تحویل‌دهنده وارد نشده است.");
        }

        var normalizedFullName = TextComparison.CreateCompactKey(delivererFullName);
        var matches = await repository.ToListAsync(
            repository.Entities
                .Where(person => person.NormalizedFullName == normalizedFullName)
                .OrderBy(person => person.Id)
                .Take(1),
            cancellationToken);
        if (matches.Count > 0)
        {
            return (matches[0], null);
        }

        var newDeliverer = new Person(delivererFullName, string.Empty);
        await repository.AddAsync(newDeliverer, cancellationToken);
        return (newDeliverer, null);
    }

    private async Task<(Supplier? Supplier, string? Error)> ResolveSupplierAsync(
        Guid? supplierId,
        string? supplierTitle,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Supplier>();
        if (supplierId.HasValue)
        {
            var supplier = await repository.GetByIdAsync(supplierId.Value, cancellationToken);
            return supplier is null
                ? (null, "تأمین‌کننده انتخاب‌شده پیدا نشد.")
                : (supplier, null);
        }

        if (string.IsNullOrWhiteSpace(supplierTitle))
        {
            return (null, null);
        }

        var normalizedTitle = NormalizeSupplierTitle(supplierTitle);
        var suppliers = await repository.GetAllAsync(cancellationToken);
        var existingSupplier = suppliers.FirstOrDefault(
            supplier => NormalizeSupplierTitle(supplier.Title) == normalizedTitle);
        if (existingSupplier is not null)
        {
            return (existingSupplier, null);
        }

        var newSupplier = new Supplier(supplierTitle, null);
        await repository.AddAsync(newSupplier, cancellationToken);
        return (newSupplier, null);
    }

    private static string NormalizeSupplierTitle(string value) =>
        string.Join(
                ' ',
                value.Replace('‌', ' ')
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    private async Task<Result<IReadOnlyCollection<ResolvedItem>>> ResolveProductsAsync(
        IReadOnlyCollection<CreateGoodsReceiptItem> items,
        CancellationToken cancellationToken)
    {
        var productRepository = unitOfWork.Repository<Product>();
        var existingIds = items
            .Where(item => item.ProductId.HasValue)
            .Select(item => item.ProductId!.Value)
            .Distinct()
            .ToArray();
        var existingProducts = await productRepository.ToListAsync(
            productRepository.Entities
                .Where(product => existingIds.Contains(product.Id))
                .Select(product => new ProductLookup(
                    product.Id,
                    product.Title,
                    product.Code,
                    product.UnitOfMeasureId,
                    product.UnitOfMeasure.Title)),
            cancellationToken);
        if (existingProducts.Count != existingIds.Length)
        {
            return Result<IReadOnlyCollection<ResolvedItem>>.Error(
                "یک یا چند کالای انتخاب‌شده پیدا نشد.");
        }

        var newItems = items.Where(item => !item.ProductId.HasValue).ToArray();
        var newTitleKeys = newItems
            .Select(item => Product.CreateComparisonKey(item.ProductTitle!))
            .ToArray();
        var newCodeKeys = newItems
            .Select(item => Product.CreateComparisonKey(item.ProductCode!))
            .ToArray();
        if (newItems.Length > 0)
        {
            var duplicateProducts = await productRepository.ToListAsync(
                productRepository.Entities
                    .Where(product =>
                        newTitleKeys.Contains(product.NormalizedTitle) ||
                        newCodeKeys.Contains(product.NormalizedCode))
                    .Select(product => new
                    {
                        product.Title,
                        product.Code,
                        product.NormalizedTitle,
                        product.NormalizedCode
                    }),
                cancellationToken);
            var duplicateTitle = duplicateProducts.FirstOrDefault(
                product => newTitleKeys.Contains(product.NormalizedTitle));
            if (duplicateTitle is not null)
            {
                return Result<IReadOnlyCollection<ResolvedItem>>.Error(
                    $"عنوان کالا «{duplicateTitle.Title}» قبلاً ثبت شده است؛ کالا را از فهرست انتخاب کنید.");
            }

            var duplicateCode = duplicateProducts.FirstOrDefault(
                product => newCodeKeys.Contains(product.NormalizedCode));
            if (duplicateCode is not null)
            {
                return Result<IReadOnlyCollection<ResolvedItem>>.Error(
                    $"کد کالا «{duplicateCode.Code}» قبلاً ثبت شده است؛ کالا را از فهرست انتخاب کنید.");
            }
        }

        var unitRepository = unitOfWork.Repository<UnitOfMeasure>();
        var requestedUnitIds = newItems
            .Select(item => item.UnitOfMeasureId!.Value)
            .Distinct()
            .ToArray();
        var storedUnits = await unitRepository.ToListAsync(
            unitRepository.Entities.Where(unit => requestedUnitIds.Contains(unit.Id)),
            cancellationToken);
        if (storedUnits.Count != requestedUnitIds.Length)
        {
            return Result<IReadOnlyCollection<ResolvedItem>>.Error(
                "واحد اندازه‌گیری انتخاب‌شده پیدا نشد.");
        }
        var unitsById = storedUnits.ToDictionary(unit => unit.Id);

        var existingById = existingProducts.ToDictionary(product => product.Id);
        var result = new List<ResolvedItem>(items.Count);
        foreach (var item in items)
        {
            if (item.ProductId.HasValue)
            {
                var product = existingById[item.ProductId.Value];
                result.Add(new ResolvedItem(
                    product.Id,
                    product.Title,
                    product.Code,
                    product.UnitTitle,
                    item.Quantity,
                    item.RequestNumber,
                    item.Description));
                continue;
            }

            var unit = unitsById[item.UnitOfMeasureId!.Value];
            var newProduct = new Product(
                item.ProductTitle!,
                item.ProductCode!,
                unit.Id,
                null);
            await productRepository.AddAsync(newProduct, cancellationToken);
            result.Add(new ResolvedItem(
                newProduct.Id,
                newProduct.Title,
                newProduct.Code,
                unit.Title,
                item.Quantity,
                item.RequestNumber,
                item.Description));
        }

        return Result<IReadOnlyCollection<ResolvedItem>>.Success(result);
    }

    private sealed record ProductLookup(
        Guid Id,
        string Title,
        string Code,
        Guid UnitOfMeasureId,
        string UnitTitle);

    private sealed record ResolvedItem(
        Guid ProductId,
        string ProductTitle,
        string ProductCode,
        string UnitTitle,
        decimal Quantity,
        string? RequestNumber,
        string? Description);
}
