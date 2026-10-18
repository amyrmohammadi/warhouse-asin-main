namespace Warehouse.Application.Common.Interfaces;

public interface ICurrentUserContext
{
    Guid? UserId { get; }

    string UserName { get; }

    void SetCurrentUser(Guid userId, string? fullName);

    void SetSystem();
}
