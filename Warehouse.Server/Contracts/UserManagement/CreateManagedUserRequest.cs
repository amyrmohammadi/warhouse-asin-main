namespace Warehouse.Server.Contracts.UserManagement;

public sealed record CreateManagedUserRequest(
    string Email,
    string Password,
    string FullName,
    string PhoneNumber,
    string? UserName,
    bool IsActive,
    IReadOnlyCollection<string> RoleNames);
