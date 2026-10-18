namespace Warehouse.Server.Contracts.UserManagement;

public sealed record UpdateUserRequest(
    string FullName,
    string PhoneNumber,
    string Email,
    string UserName);
