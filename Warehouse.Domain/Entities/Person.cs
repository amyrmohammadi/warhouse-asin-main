using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

public sealed class Person : ConcurrencyEntity<Guid>
{
    private Person()
    {
    }

    public Person(string fullName, string phoneNumber)
    {
        Id = Guid.NewGuid();
        Update(fullName, phoneNumber);
    }

    public string FullName { get; private set; } = string.Empty;

    public string NormalizedFullName { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public ICollection<GoodsReceipt> GoodsReceipts { get; private set; } = [];

    public ICollection<GoodsIssue> GoodsIssues { get; private set; } = [];

    public void Update(string fullName, string phoneNumber)
    {
        FullName = fullName.Trim();
        NormalizedFullName = TextComparison.CreateCompactKey(FullName);
        PhoneNumber = phoneNumber.Trim();
    }
}
