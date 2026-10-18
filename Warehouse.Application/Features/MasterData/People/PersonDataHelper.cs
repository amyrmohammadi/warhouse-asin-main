using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.People;

internal static class PersonDataHelper
{
    public static async Task<string?> FindDuplicateMessageAsync(
        IUnitOfWork<Guid> unitOfWork,
        string fullName,
        Guid? excludedPersonId,
        CancellationToken cancellationToken)
    {
        var normalizedFullName = TextComparison.CreateCompactKey(fullName);
        var repository = unitOfWork.Repository<Person>();
        var query = repository.Entities.AsQueryable();

        if (excludedPersonId.HasValue)
        {
            query = query.Where(person => person.Id != excludedPersonId.Value);
        }

        return await repository.CountAsync(
                query.Where(person => person.NormalizedFullName == normalizedFullName),
                cancellationToken) > 0
            ? "نام تحویل‌دهنده قبلاً ثبت شده است."
            : null;
    }
}
