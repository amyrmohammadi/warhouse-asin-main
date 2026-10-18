namespace Warehouse.Application.Common.Interfaces;

public interface IUserAccessService
{
    Task<UserAccessSnapshot> GetAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record UserAccessSnapshot(
    bool IsActive,
    IReadOnlyCollection<string> Permissions);
