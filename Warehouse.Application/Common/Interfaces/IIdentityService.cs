using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<CreateUserResult> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateUserResult> CreateManagedUserAsync(
        CreateManagedUserRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> UpdateRoleAsync(
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> DeleteRoleAsync(
        DeleteRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<TokenResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<TokenResult> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<GetUsersResult> GetUsersAsync(
        GetUsersRequest request,
        CancellationToken cancellationToken = default);

    Task<GetRolesResult> GetRolesAsync(
        GetRolesRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> UpdateUserAsync(
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> SetUserActiveStatusAsync(
        SetUserActiveStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> ChangeUserPasswordAsync(
        ChangeUserPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<GetCurrentUserProfileResult> GetCurrentUserProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> UpdateCurrentUserProfileAsync(
        UpdateCurrentUserProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> ChangeCurrentUserPasswordAsync(
        ChangeCurrentUserPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> AddPoliciesToRoleAsync(
        AddPoliciesToRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<GetRolePoliciesResult> GetRolePoliciesAsync(
        string roleName,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> SetPoliciesForRoleAsync(
        SetPoliciesForRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> AddRolesToUserAsync(
        AddRolesToUserRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> SetRolesForUserAsync(
        SetRolesForUserRequest request,
        CancellationToken cancellationToken = default);
}
