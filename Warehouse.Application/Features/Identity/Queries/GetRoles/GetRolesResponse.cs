namespace Warehouse.Application.Features.Identity.Queries.GetRoles;

public sealed record GetRolesResponse(
    IReadOnlyCollection<RoleListItemResponse> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize,
    bool PaginationEnabled);

public sealed record RoleListItemResponse(
    Guid Id,
    string Name,
    int UserCount,
    int PermissionCount);
