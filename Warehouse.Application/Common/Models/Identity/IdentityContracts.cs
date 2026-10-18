using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Common.Models.Identity;

public sealed record CreateUserRequest(
    string Email,
    string Password,
    string FullName,
    string PhoneNumber,
    string? UserName = null);

public sealed record CreateUserResult(
    bool Succeeded,
    Guid? UserId,
    IReadOnlyCollection<string> Errors);

public sealed record CreateManagedUserRequest(
    string Email,
    string Password,
    string FullName,
    string PhoneNumber,
    string? UserName,
    bool IsActive,
    IReadOnlyCollection<string> RoleNames);

public sealed record CreateRoleRequest(string Name);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record GetUsersRequest : DataTableParameters
{
    public string? FullName { get; init; }

    public string? UserName { get; init; }

    public string? Email { get; init; }

    public bool? IsActive { get; init; }

    public string? RoleName { get; init; }
}

public sealed record GetUsersResult(
    IReadOnlyCollection<UserSummary> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize,
    bool PaginationEnabled);

public sealed record UserSummary(
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

public sealed record UpdateUserRequest(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string Email,
    string UserName);

public sealed record SetUserActiveStatusRequest(
    Guid UserId,
    bool IsActive);

public sealed record ChangeUserPasswordRequest(
    Guid UserId,
    string NewPassword);

public sealed record CurrentUserProfile(
    Guid UserId,
    string UserName,
    string Email,
    string FullName,
    string PhoneNumber,
    string? ProfileImageContentType,
    byte[]? ProfileImage);

public sealed record GetCurrentUserProfileResult(
    bool Succeeded,
    CurrentUserProfile? Profile,
    IReadOnlyCollection<string> Errors);

public sealed record UpdateCurrentUserProfileRequest(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    byte[]? ProfileImage,
    string? ProfileImageContentType,
    bool RemoveProfileImage);

public sealed record ChangeCurrentUserPasswordRequest(
    Guid UserId,
    string CurrentPassword,
    string NewPassword);

public sealed record GetRolesRequest : DataTableParameters
{
    public string? RoleName { get; init; }

    public NumericFilter<int>? UserCount { get; init; }

    public NumericFilter<int>? PermissionCount { get; init; }
}

public sealed record GetRolesResult(
    IReadOnlyCollection<RoleSummary> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize,
    bool PaginationEnabled);

public sealed record RoleSummary(
    Guid Id,
    string Name,
    int UserCount,
    int PermissionCount);

public sealed record AddPoliciesToRoleRequest(
    string RoleName,
    IReadOnlyCollection<string> Policies);

public sealed record SetPoliciesForRoleRequest(
    string RoleName,
    IReadOnlyCollection<string> Policies);

public sealed record GetRolePoliciesResult(
    bool Succeeded,
    RolePoliciesSummary? RolePolicies,
    IReadOnlyCollection<string> Errors);

public sealed record RolePoliciesSummary(
    string RoleName,
    IReadOnlyCollection<RolePermissionGroupSummary> Groups);

public sealed record RolePermissionGroupSummary(
    string Key,
    string DisplayName,
    IReadOnlyCollection<RolePermissionSummary> Permissions);

public sealed record RolePermissionSummary(
    string Name,
    string DisplayName,
    bool IsAssigned);

public sealed record AddRolesToUserRequest(
    Guid UserId,
    IReadOnlyCollection<string> RoleNames);

public sealed record SetRolesForUserRequest(
    Guid UserId,
    IReadOnlyCollection<string> RoleNames);

public sealed record TokenResult(
    bool Succeeded,
    Guid? UserId,
    string? AccessToken,
    DateTimeOffset? AccessTokenExpiresAtUtc,
    string? RefreshToken,
    DateTimeOffset? RefreshTokenExpiresAtUtc,
    IReadOnlyCollection<AppPermission> Permissions,
    IReadOnlyCollection<string> Errors);

public sealed record IdentityOperationResult(
    bool Succeeded,
    IReadOnlyCollection<string> Errors);
