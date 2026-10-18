namespace Warehouse.Application.Features.Identity.Queries.GetUsers;

public sealed record GetUsersResponse(
    IReadOnlyCollection<UserListItemResponse> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize,
    bool PaginationEnabled);

public sealed record UserListItemResponse(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    string PhoneNumber,
    bool IsActive,
    bool EmailConfirmed,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyCollection<string> Roles,
    string? ProfileImageContentType,
    byte[]? ProfileImage);
