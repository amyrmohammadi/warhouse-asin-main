using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Products;

internal static class ProductDataHelper
{
    public static async Task<UnitOfMeasure> ResolveUnitOfMeasureAsync(
        IUnitOfWork<Guid> unitOfWork,
        string title,
        CancellationToken cancellationToken)
    {
        var normalizedTitle = title.Trim();
        var repository = unitOfWork.Repository<UnitOfMeasure>();
        var matches = await repository.ToListAsync(
            repository.Entities
                .Where(unit => unit.Title == normalizedTitle)
                .OrderBy(unit => unit.Id)
                .Take(1),
            cancellationToken);

        if (matches.Count > 0)
        {
            return matches[0];
        }

        var unit = new UnitOfMeasure(normalizedTitle, null);
        await repository.AddAsync(unit, cancellationToken);
        return unit;
    }

    public static async Task<string?> FindDuplicateMessageAsync(
        IUnitOfWork<Guid> unitOfWork,
        string title,
        string code,
        Guid? excludedProductId,
        CancellationToken cancellationToken)
    {
        var normalizedTitle = Product.CreateComparisonKey(title);
        var normalizedCode = Product.CreateComparisonKey(code);
        var repository = unitOfWork.Repository<Product>();
        var query = repository.Entities.AsQueryable();

        if (excludedProductId.HasValue)
        {
            query = query.Where(product => product.Id != excludedProductId.Value);
        }

        if (await repository.CountAsync(
                query.Where(product => product.NormalizedTitle == normalizedTitle),
                cancellationToken) > 0)
        {
            return "عنوان کالا قبلاً ثبت شده است.";
        }

        if (Product.IsPlaceholderCode(code)) return null;

        return await repository.CountAsync(
                query.Where(product => product.NormalizedCode == normalizedCode),
                cancellationToken) > 0
            ? "کد کالا قبلاً ثبت شده است."
            : null;
    }

    public static ProductSummary ToSummary(Product product, string unitTitle) => new()
    {
        Id = product.Id,
        ConcurrencyStamp = product.ConcurrencyStamp,
        Title = product.Title,
        Code = product.Code,
        UnitOfMeasureId = product.UnitOfMeasureId,
        UnitOfMeasureTitle = unitTitle,
        Description = product.Description
    };
}
