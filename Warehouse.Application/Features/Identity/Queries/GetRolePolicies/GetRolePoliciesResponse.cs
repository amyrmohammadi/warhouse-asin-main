namespace Warehouse.Application.Features.Identity.Queries.GetRolePolicies;

public sealed record GetRolePoliciesResponse(
    string RoleName,
    IReadOnlyCollection<RolePermissionGroupResponse> Groups);

public sealed record RolePermissionGroupResponse(
    string Key,
    string DisplayName,
    IReadOnlyCollection<RolePermissionResponse> Permissions);

public sealed record RolePermissionResponse(
    string Name,
    string DisplayName,
    bool IsAssigned);
