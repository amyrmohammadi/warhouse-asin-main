using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

public sealed class Product : Entity<Guid>
{
    private Product()
    {
    }

    public Product(
        string title,
        string code,
        Guid unitOfMeasureId,
        string? description)
    {
        Id = Guid.NewGuid();
        Update(title, code, unitOfMeasureId, description);
    }

    public string Title { get; private set; } = string.Empty;

    public string NormalizedTitle { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public Guid UnitOfMeasureId { get; private set; }

    public UnitOfMeasure UnitOfMeasure { get; private set; } = null!;

    public string? Description { get; private set; }

    public ICollection<GoodsReceiptItem> GoodsReceiptItems { get; private set; } = [];

    public ICollection<GoodsIssueItem> GoodsIssueItems { get; private set; } = [];

    public void Update(
        string title,
        string code,
        Guid unitOfMeasureId,
        string? description)
    {
        Title = title.Trim();
        NormalizedTitle = CreateComparisonKey(Title);
        Code = code.Trim();
        NormalizedCode = CreateComparisonKey(Code);
        UnitOfMeasureId = unitOfMeasureId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public static bool IsPlaceholderCode(string? value) => value?.Trim() == "-";

    public static string CreateComparisonKey(string value) =>
        TextComparison.CreateCompactKey(value);
}
