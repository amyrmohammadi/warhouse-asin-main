namespace Warehouse.Server.Contracts.UserManagement;

public sealed record AssignRolesRequest(IReadOnlyCollection<string> RoleNames);
