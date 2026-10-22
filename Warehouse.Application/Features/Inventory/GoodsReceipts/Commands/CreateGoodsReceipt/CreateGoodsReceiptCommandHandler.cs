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
        return await SaveAsync(request, cancellationToken);
    }

    internal async Task<Result<GoodsReceiptSummary>> SaveAsync(
        CreateGoodsReceiptCommand request, CancellationToken cancellationToken,
        GoodsReceipt? existing = null, bool updateInitial = true, bool updateInvoice = true)
    {
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

            var resolvedItems = await ResolveProductsAsync(request.Items, cancellationToken);
            if (!resolvedItems.IsSuccess)
            {
                await unitOfWork.RollBackTransaction(cancellationToken);
                return Result<GoodsReceiptSummary>.Error(resolvedItems.Messages);
            }

            var warehouseRepository = unitOfWork.Repository<StorageWarehouse>();
            var warehouseIds = request.Items.Select(item => item.WarehouseId).Distinct().ToArray();
            var warehouses = await warehouseRepository.ToListAsync(
                warehouseRepository.Entities.Where(item => warehouseIds.Contains(item.Id)),
                cancellationToken);
            if (warehouses.Count != warehouseIds.Length)
            {
                await unitOfWork.RollBackTransaction(cancellationToken);
                return Result<GoodsReceiptSummary>.Error("یک یا چند انبار انتخاب‌شده پیدا نشد.");
            }
            var warehousesById = warehouses.ToDictionary(item => item.Id);

            var preparedItems = new List<PreparedItem>(request.Items.Count);
            var supplierCache = new Dictionary<string, Supplier?>(StringComparer.Ordinal);
            foreach (var pair in resolvedItems.Data!.Zip(request.Items))
            {
                var source = pair.Second;
                var supplierKey = source.SupplierId?.ToString() ??
                                  NormalizeSupplierTitle(source.SupplierTitle ?? string.Empty);
                if (!supplierCache.TryGetValue(supplierKey, out var supplier))
                {
                    var resolvedSupplier = await ResolveSupplierAsync(
                        source.SupplierId, source.SupplierTitle, cancellationToken);
                    if (resolvedSupplier.Error is not null)
                    {
                        await unitOfWork.RollBackTransaction(cancellationToken);
                        return Result<GoodsReceiptSummary>.Error(resolvedSupplier.Error);
                    }
                    supplier = resolvedSupplier.Supplier;
                    supplierCache[supplierKey] = supplier;
                }
                preparedItems.Add(new PreparedItem(
                    pair.First,
                    warehousesById[source.WarehouseId],
                    source.InvoiceStatus,
                    source.InvoiceNumber,
                    supplier,
                    source.ReceiptNumber));
            }

            var receipt = existing ?? new GoodsReceipt(
                request.EnteredAtUtc,
                request.Description,
                deliverer!.Id,
                request.PageNumber);

            if (existing is not null && updateInitial)
                receipt.UpdateInitialEntry(request.EnteredAtUtc, request.Description,
                    deliverer!.Id, request.PageNumber);

            var itemRepository = unitOfWork.Repository<GoodsReceiptItem>();
            var oldItems = existing is null ? [] : await itemRepository.ToListAsync(
                itemRepository.Entities
                    .Where(item => item.GoodsReceiptId == receipt.Id)
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Id),
                cancellationToken);
            var oldItemsByProduct = oldItems
                .GroupBy(item => item.ProductId)
                .ToDictionary(group => group.Key, group => new Queue<GoodsReceiptItem>(group));
            var matchedOldItemIds = new HashSet<Guid>();

            for (var sortOrder = 0; sortOrder < preparedItems.Count; sortOrder++)
            {
                var item = preparedItems[sortOrder];
                GoodsReceiptItem? old = null;
                if (oldItemsByProduct.TryGetValue(item.Product.ProductId, out var candidates) &&
                    candidates.Count > 0)
                {
                    old = candidates.Dequeue();
                    matchedOldItemIds.Add(old.Id);
                }
                if (old is null)
                {
                    if (!updateInitial && existing is not null) continue;
                    receipt.AddItem(
                        item.Product.ProductId, item.Warehouse.Id, item.InvoiceStatus,
                        item.InvoiceNumber, item.Supplier?.Id, item.ReceiptNumber,
                        item.Product.Quantity, item.Product.RequestNumber, item.Product.Description,
                        sortOrder);
                    if (existing is not null)
                        await itemRepository.AddAsync(receipt.Items.Last(), cancellationToken);
                    continue;
                }

                var tracked = await itemRepository.GetByIdAsync(old.Id, cancellationToken);
                tracked!.Update(
                    updateInitial ? item.Warehouse.Id : old.WarehouseId,
                    updateInvoice ? item.InvoiceStatus : old.InvoiceStatus,
                    updateInvoice ? item.InvoiceNumber : old.InvoiceNumber,
                    updateInvoice ? item.Supplier?.Id : old.SupplierId,
                    updateInvoice ? item.ReceiptNumber : old.ReceiptNumber,
                    updateInitial ? item.Product.Quantity : old.Quantity,
                    updateInitial ? item.Product.RequestNumber : old.RequestNumber,
                    updateInitial ? item.Product.Description : old.Description,
                    updateInitial ? sortOrder : old.SortOrder);
            }

            if (existing is not null && updateInitial)
            {
                foreach (var old in oldItems.Where(item => !matchedOldItemIds.Contains(item.Id)))
                {
                    var tracked = await itemRepository.GetByIdAsync(old.Id, cancellationToken);
                    await itemRepository.DeleteAsync(tracked!, cancellationToken);
                }
            }

            var receiptRepository = unitOfWork.Repository<GoodsReceipt>();
            if (existing is null)
                await receiptRepository.AddAsync(receipt, cancellationToken);
            await unitOfWork.SaveAsync(cancellationToken);
            await unitOfWork.CommitTransaction(cancellationToken);

            var summaries = await receiptRepository.ToListAsync(
                receiptRepository.Entities.Where(item => item.Id == receipt.Id).ToSummary(),
                cancellationToken);
            return Result<GoodsReceiptSummary>.Success(summaries[0]);
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

            if (duplicateCode is not null && duplicateCode.Code != "-")
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
        var createdProducts = new Dictionary<string, (Product Product, string UnitTitle)>(
            StringComparer.Ordinal);
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
            var codeKey = Product.CreateComparisonKey(item.ProductCode!);
            var productKey = Product.IsPlaceholderCode(item.ProductCode!)
                ? $"{codeKey}:{Product.CreateComparisonKey(item.ProductTitle!)}:{unit.Id}"
                : codeKey;
            if (!createdProducts.TryGetValue(productKey, out var created))
            {
                var product = new Product(
                    item.ProductTitle!,
                    item.ProductCode!,
                    unit.Id,
                    null);
                await productRepository.AddAsync(product, cancellationToken);
                created = (product, unit.Title);
                createdProducts[productKey] = created;
            }
            result.Add(new ResolvedItem(
                created.Product.Id,
                created.Product.Title,
                created.Product.Code,
                created.UnitTitle,
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

    private sealed record PreparedItem(
        ResolvedItem Product,
        StorageWarehouse Warehouse,
        Domain.Enums.InvoiceStatus? InvoiceStatus,
        string? InvoiceNumber,
        Supplier? Supplier,
        string? ReceiptNumber);
}
