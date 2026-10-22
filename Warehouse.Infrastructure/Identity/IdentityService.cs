using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Extensions;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models.Identity;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IJwtTokenGenerator jwtTokenGenerator,
    ApplicationDbContext dbContext,
    TimeProvider timeProvider,
    ICurrentUserContext currentUserContext) : IIdentityService
{
    private static readonly string[] InvalidCredentialsError =
        ["The email or password is invalid."];

    private static readonly string[] InactiveAccountError =
        ["The account is awaiting administrator approval."];

    private static readonly string[] InvalidRefreshTokenError =
        ["The refresh token is invalid or expired."];

    private const int MaximumProfileImageSize = 2 * 1024 * 1024;

    public async Task<CreateUserResult> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var email = request.Email.Trim();
        var userName = string.IsNullOrWhiteSpace(request.UserName)
            ? email
            : request.UserName.Trim();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = userName,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            IsActive = false,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        currentUserContext.SetCurrentUser(user.Id, user.FullName);
        var result = await userManager.CreateAsync(user, request.Password);

        return result.Succeeded
            ? new CreateUserResult(true, user.Id, [])
            : new CreateUserResult(false, null, GetErrors(result));
    }

    public async Task<CreateUserResult> CreateManagedUserAsync(
        CreateManagedUserRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roleNames = NormalizeNames(request.RoleNames);
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                return new CreateUserResult(
                    false,
                    null,
                    [$"The '{roleName}' role was not found."]);
            }
        }

        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = string.IsNullOrWhiteSpace(request.UserName)
                ? email
                : request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            IsActive = request.IsActive,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new CreateUserResult(false, null, GetErrors(createResult));
        }

        if (roleNames.Length > 0)
        {
            var roleResult = await userManager.AddToRolesAsync(user, roleNames);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new CreateUserResult(false, null, GetErrors(roleResult));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new CreateUserResult(true, user.Id, []);
    }

    public async Task<IdentityOperationResult> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roleName = request.Name.Trim();
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return FailedOperation("A role with this name already exists.");
        }

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        return result.Succeeded
            ? SuccessfulOperation()
            : new IdentityOperationResult(false, GetErrors(result));
    }

    public async Task<IdentityOperationResult> UpdateRoleAsync(
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var role = await roleManager.FindByIdAsync(request.RoleId.ToString());
        if (role is null)
        {
            return FailedOperation("نقش موردنظر پیدا نشد.");
        }

        if (string.Equals(role.Name, DefaultRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return FailedOperation("ویرایش نقش پیش‌فرض مدیر سیستم مجاز نیست.");
        }

        var roleName = request.Name.Trim();
        var duplicate = await roleManager.FindByNameAsync(roleName);
        if (duplicate is not null && duplicate.Id != role.Id)
        {
            return FailedOperation("نقشی با این نام قبلاً ثبت شده است.");
        }

        role.Name = roleName;
        var result = await roleManager.UpdateAsync(role);
        return result.Succeeded
            ? SuccessfulOperation()
            : new IdentityOperationResult(false, GetErrors(result));
    }

    public async Task<IdentityOperationResult> DeleteRoleAsync(
        DeleteRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var role = await roleManager.FindByIdAsync(request.RoleId.ToString());
        if (role is null)
        {
            return FailedOperation("نقش موردنظر پیدا نشد.");
        }

        if (string.Equals(role.Name, DefaultRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return FailedOperation("حذف نقش پیش‌فرض مدیر سیستم مجاز نیست.");
        }

        var result = await roleManager.DeleteAsync(role);
        return result.Succeeded
            ? SuccessfulOperation()
            : new IdentityOperationResult(false, GetErrors(result));
    }

    public async Task<TokenResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return FailedToken(InvalidCredentialsError);
        }

        currentUserContext.SetCurrentUser(
            user.Id,
            user.FullName);

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (!signInResult.Succeeded)
        {
            return FailedToken(InvalidCredentialsError);
        }

        if (!user.IsActive)
        {
            return FailedToken(InactiveAccountError);
        }

        var token = await jwtTokenGenerator.GenerateAsync(user, cancellationToken);
        return SuccessfulToken(user.Id, token);
    }

    public async Task<TokenResult> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = await jwtTokenGenerator.RefreshAsync(
            request.RefreshToken,
            cancellationToken);

        return token is null
            ? FailedToken(InvalidRefreshTokenError)
            : SuccessfulToken(GetUserId(token.AccessToken), token);
    }

    public async Task<GetUsersResult> GetUsersAsync(
        GetUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = userManager.Users.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var search = request.Search?.Trim();
        var fullName = request.FullName?.Trim();
        var userName = request.UserName?.Trim();
        var email = request.Email?.Trim();
        var roleName = request.RoleName?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.ToUpperInvariant();
            query = query.Where(user =>
                (user.NormalizedUserName != null &&
                 user.NormalizedUserName.Contains(normalizedSearch)) ||
                (user.NormalizedEmail != null &&
                 user.NormalizedEmail.Contains(normalizedSearch)) ||
                (user.FullName != null && user.FullName.Contains(search!)) ||
                (user.PhoneNumber != null && user.PhoneNumber.Contains(search!)));
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            query = query.Where(user =>
                user.FullName != null && user.FullName.Contains(fullName));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.ToUpperInvariant();
            query = query.Where(user =>
                user.NormalizedEmail != null &&
                user.NormalizedEmail.Contains(normalizedEmail));
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            var normalizedUserName = userName.ToUpperInvariant();
            query = query.Where(user =>
                user.NormalizedUserName != null &&
                user.NormalizedUserName.Contains(normalizedUserName));
        }

        if (request.IsActive is { } isActive)
        {
            query = query.Where(user => user.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var normalizedRoleName = roleName.ToUpperInvariant();
            query = query.Where(user =>
                dbContext.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Join(
                        dbContext.Roles,
                        userRole => userRole.RoleId,
                        role => role.Id,
                        (_, role) => role)
                    .Any(role => role.NormalizedName == normalizedRoleName));
        }

        var filteredCount = await query.CountAsync(cancellationToken);

        var orderedQuery = (request.SortBy?.Trim().ToLowerInvariant(), request.SortDescending) switch
        {
            ("email", false) => query.OrderBy(user => user.Email).ThenBy(user => user.Id),
            ("email", true) => query.OrderByDescending(user => user.Email).ThenBy(user => user.Id),
            ("emailconfirmed", false) => query.OrderBy(user => user.EmailConfirmed).ThenBy(user => user.Id),
            ("emailconfirmed", true) => query.OrderByDescending(user => user.EmailConfirmed).ThenBy(user => user.Id),
            ("fullname", false) => query.OrderBy(user => user.FullName).ThenBy(user => user.Id),
            ("fullname", true) => query.OrderByDescending(user => user.FullName).ThenBy(user => user.Id),
            ("phonenumber", false) => query.OrderBy(user => user.PhoneNumber).ThenBy(user => user.Id),
            ("phonenumber", true) => query.OrderByDescending(user => user.PhoneNumber).ThenBy(user => user.Id),
            ("isactive", false) => query.OrderBy(user => user.IsActive).ThenBy(user => user.Id),
            ("isactive", true) => query.OrderByDescending(user => user.IsActive).ThenBy(user => user.Id),
            ("createdatutc", false) => query.OrderBy(user => user.CreatedAtUtc).ThenBy(user => user.Id),
            ("createdatutc", true) => query.OrderByDescending(user => user.CreatedAtUtc).ThenBy(user => user.Id),
            (_, true) => query.OrderByDescending(user => user.UserName).ThenBy(user => user.Id),
            _ => query.OrderBy(user => user.UserName).ThenBy(user => user.Id)
        };

        var pagedQuery = request.PaginationEnabled
            ? orderedQuery
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
            : orderedQuery;
        var users = await pagedQuery
            .Select(user => new
            {
                user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                user.IsActive,
                user.EmailConfirmed,
                user.CreatedAtUtc,
                user.ProfileImage,
                Roles = (
                    from userRole in dbContext.UserRoles
                    join role in dbContext.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    orderby role.Name
                    select role.Name ?? string.Empty)
                    .ToArray()
            })
            .ToArrayAsync(cancellationToken);

        var items = users
            .Select(user => new UserSummary(
                user.Id,
                user.UserName,
                user.Email,
                user.FullName,
                user.PhoneNumber,
                user.IsActive,
                user.EmailConfirmed,
                user.CreatedAtUtc,
                user.Roles,
                DetectImageContentType(user.ProfileImage),
                user.ProfileImage))
            .ToArray();

        return new GetUsersResult(
            items,
            totalCount,
            filteredCount,
            request.PaginationEnabled ? request.PageNumber : 1,
            request.PaginationEnabled ? request.PageSize : items.Length,
            request.PaginationEnabled);
    }

    public async Task<GetRolesResult> GetRolesAsync(
        GetRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        var roles = roleManager.Roles
            .AsNoTracking()
            .Select(role => new
            {
                role.Id,
                Name = role.Name ?? string.Empty,
                UserCount = dbContext.UserRoles.Count(userRole => userRole.RoleId == role.Id),
                PermissionCount = dbContext.RoleClaims.Count(claim =>
                    claim.RoleId == role.Id &&
                    claim.ClaimType == ApplicationClaimTypes.Permission &&
                    claim.ClaimValue != null)
            });

        var totalCount = await roles.CountAsync(cancellationToken);
        var filteredRoles = roles;
        var search = request.Search?.Trim();
        var roleName = request.RoleName?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filteredRoles = filteredRoles.Where(role => role.Name.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            filteredRoles = filteredRoles.Where(role => role.Name.Contains(roleName));
        }

        filteredRoles = filteredRoles
            .ApplyNumericFilter(request.UserCount, role => role.UserCount)
            .ApplyNumericFilter(request.PermissionCount, role => role.PermissionCount);

        var filteredCount = await filteredRoles.CountAsync(cancellationToken);
        var orderedRoles = (request.SortBy?.Trim().ToLowerInvariant(), request.SortDescending) switch
        {
            ("usercount", false) => filteredRoles
                .OrderBy(role => role.UserCount)
                .ThenBy(role => role.Id),
            ("usercount", true) => filteredRoles
                .OrderByDescending(role => role.UserCount)
                .ThenBy(role => role.Id),
            ("permissioncount", false) => filteredRoles
                .OrderBy(role => role.PermissionCount)
                .ThenBy(role => role.Id),
            ("permissioncount", true) => filteredRoles
                .OrderByDescending(role => role.PermissionCount)
                .ThenBy(role => role.Id),
            (_, true) => filteredRoles
                .OrderByDescending(role => role.Name)
                .ThenBy(role => role.Id),
            _ => filteredRoles
                .OrderBy(role => role.Name)
                .ThenBy(role => role.Id)
        };

        var rows = request.PaginationEnabled
            ? await orderedRoles
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToArrayAsync(cancellationToken)
            : await orderedRoles.ToArrayAsync(cancellationToken);
        var items = rows
            .Select(role => new RoleSummary(
                role.Id,
                role.Name,
                role.UserCount,
                role.PermissionCount))
            .ToArray();
        var resultPageNumber = request.PaginationEnabled ? request.PageNumber : 1;
        var resultPageSize = request.PaginationEnabled ? request.PageSize : items.Length;

        return new GetRolesResult(
            items,
            totalCount,
            filteredCount,
            resultPageNumber,
            resultPageSize,
            request.PaginationEnabled);
    }

    public async Task<GetRolePoliciesResult> GetRolePoliciesAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRoleName = roleName.Trim();
        var role = await roleManager.FindByNameAsync(normalizedRoleName);
        if (role is null)
        {
            return new GetRolePoliciesResult(
                false,
                null,
                ["The role was not found."]);
        }

        var assignedPolicies = (await roleManager.GetClaimsAsync(role))
            .Where(claim =>
                claim.Type == ApplicationClaimTypes.Permission &&
                claim.Value is not null)
            .Select(claim => claim.Value!)
            .ToHashSet(StringComparer.Ordinal);

        var groups = PermissionPolicyNames.Groups
            .Select(group => new RolePermissionGroupSummary(
                group.Key,
                group.DisplayName,
                group.Policies
                    .Select(policy => new RolePermissionSummary(
                        policy.Name,
                        policy.DisplayName,
                        assignedPolicies.Contains(policy.Name)))
                    .ToArray()))
            .ToArray();

        return new GetRolePoliciesResult(
            true,
            new RolePoliciesSummary(role.Name!, groups),
            []);
    }

    public async Task<IdentityOperationResult> UpdateUserAsync(
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return FailedOperation("The user was not found.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.Email = request.Email.Trim();
        user.UserName = request.UserName.Trim();

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IdentityOperationResult(false, GetErrors(updateResult));
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IdentityOperationResult(false, GetErrors(stampResult));
        }

        await RevokeActiveRefreshTokensAsync([user.Id], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SuccessfulOperation();
    }

    public async Task<IdentityOperationResult> SetUserActiveStatusAsync(
        SetUserActiveStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return FailedOperation("The user was not found.");
        }

        if (user.IsActive == request.IsActive)
        {
            return SuccessfulOperation();
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        user.IsActive = request.IsActive;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IdentityOperationResult(false, GetErrors(updateResult));
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IdentityOperationResult(false, GetErrors(stampResult));
        }

        await RevokeActiveRefreshTokensAsync([user.Id], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SuccessfulOperation();
    }

    public async Task<IdentityOperationResult> ChangeUserPasswordAsync(
        ChangeUserPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return FailedOperation("The user was not found.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(
            user,
            resetToken,
            request.NewPassword);
        if (!resetResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IdentityOperationResult(false, GetErrors(resetResult));
        }

        await RevokeActiveRefreshTokensAsync([user.Id], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SuccessfulOperation();
    }

    public async Task<GetCurrentUserProfileResult> GetCurrentUserProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var profile = await userManager.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                user.ProfileImage
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return new GetCurrentUserProfileResult(
                false,
                null,
                ["The user was not found."]);
        }

        return new GetCurrentUserProfileResult(
            true,
            new CurrentUserProfile(
                profile.Id,
                profile.UserName,
                profile.Email,
                profile.FullName,
                profile.PhoneNumber,
                DetectImageContentType(profile.ProfileImage),
                profile.ProfileImage),
            []);
    }

    public async Task<IdentityOperationResult> UpdateCurrentUserProfileAsync(
        UpdateCurrentUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return FailedOperation("The user was not found.");
        }

        if (request.ProfileImage is { Length: > MaximumProfileImageSize })
        {
            return FailedOperation("Profile image cannot be larger than 2 MB.");
        }

        if (request.ProfileImage is not null)
        {
            var detectedContentType = DetectImageContentType(request.ProfileImage);
            if (detectedContentType is null ||
                !string.Equals(
                    detectedContentType,
                    request.ProfileImageContentType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return FailedOperation("The profile image content is invalid.");
            }
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();

        if (request.RemoveProfileImage)
        {
            user.ProfileImage = null;
        }
        else if (request.ProfileImage is not null)
        {
            user.ProfileImage = request.ProfileImage;
        }

        var updateResult = await userManager.UpdateAsync(user);
        return updateResult.Succeeded
            ? SuccessfulOperation()
            : new IdentityOperationResult(false, GetErrors(updateResult));
    }

    public async Task<IdentityOperationResult> ChangeCurrentUserPasswordAsync(
        ChangeCurrentUserPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return FailedOperation("The user was not found.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var changeResult = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);
        if (!changeResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IdentityOperationResult(false, GetErrors(changeResult));
        }

        await RevokeActiveRefreshTokensAsync([user.Id], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SuccessfulOperation();
    }

    public async Task<IdentityOperationResult> AddPoliciesToRoleAsync(
        AddPoliciesToRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedPolicies = NormalizeNames(request.Policies);
        if (string.IsNullOrWhiteSpace(request.RoleName) || normalizedPolicies.Length == 0)
        {
            return FailedOperation("A role and at least one policy are required.");
        }

        var unsupportedPolicies = normalizedPolicies
            .Where(policy => !PermissionPolicyNames.IsDefined(policy))
            .ToArray();
        if (unsupportedPolicies.Length > 0)
        {
            return FailedOperation(
                $"Unsupported policies: {string.Join(", ", unsupportedPolicies)}.");
        }

        var role = await roleManager.FindByNameAsync(request.RoleName.Trim());
        if (role is null)
        {
            return FailedOperation("The role was not found.");
        }

        var existingClaims = await roleManager.GetClaimsAsync(role);
        var policiesToAdd = normalizedPolicies
            .Where(policy => !existingClaims.Any(claim =>
                claim.Type == ApplicationClaimTypes.Permission &&
                string.Equals(claim.Value, policy, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (policiesToAdd.Length == 0)
        {
            return SuccessfulOperation();
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var policy in policiesToAdd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claimResult = await roleManager.AddClaimAsync(
                role,
                new Claim(ApplicationClaimTypes.Permission, policy));

            if (!claimResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IdentityOperationResult(false, GetErrors(claimResult));
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return SuccessfulOperation();
    }

    public async Task<IdentityOperationResult> SetPoliciesForRoleAsync(
        SetPoliciesForRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            return FailedOperation("A role is required.");
        }

        var normalizedPolicies = NormalizeNames(request.Policies);
        var unsupportedPolicies = normalizedPolicies
            .Where(policy => !PermissionPolicyNames.IsDefined(policy))
            .ToArray();
        if (unsupportedPolicies.Length > 0)
        {
            return FailedOperation(
                $"Unsupported policies: {string.Join(", ", unsupportedPolicies)}.");
        }

        var role = await roleManager.FindByNameAsync(request.RoleName.Trim());
        if (role is null)
        {
            return FailedOperation("The role was not found.");
        }

        var existingClaims = await roleManager.GetClaimsAsync(role);
        var existingPolicyClaims = existingClaims
            .Where(claim =>
                claim.Type == ApplicationClaimTypes.Permission &&
                claim.Value is not null &&
                PermissionPolicyNames.IsDefined(claim.Value))
            .ToArray();
        var requestedPolicies = normalizedPolicies.ToHashSet(StringComparer.Ordinal);
        var policiesToAdd = requestedPolicies
            .Except(
                existingPolicyClaims.Select(claim => claim.Value!),
                StringComparer.Ordinal)
            .ToArray();
        var claimsToRemove = existingPolicyClaims
            .Where(claim => !requestedPolicies.Contains(claim.Value!))
            .ToArray();

        if (policiesToAdd.Length == 0 && claimsToRemove.Length == 0)
        {
            return SuccessfulOperation();
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var claim in claimsToRemove)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var removeResult = await roleManager.RemoveClaimAsync(role, claim);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IdentityOperationResult(false, GetErrors(removeResult));
            }
        }

        foreach (var policy in policiesToAdd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var addResult = await roleManager.AddClaimAsync(
                role,
                new Claim(ApplicationClaimTypes.Permission, policy));
            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IdentityOperationResult(false, GetErrors(addResult));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return SuccessfulOperation();
    }

    public async Task<IdentityOperationResult> AddRolesToUserAsync(
        AddRolesToUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoleNames = NormalizeNames(request.RoleNames);
        if (normalizedRoleNames.Length == 0)
        {
            return FailedOperation("At least one role is required.");
        }

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return FailedOperation("The user was not found.");
        }

        foreach (var roleName in normalizedRoleNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                return FailedOperation($"The '{roleName}' role was not found.");
            }
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToAdd = normalizedRoleNames
            .Except(currentRoles, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rolesToAdd.Length == 0)
        {
            return SuccessfulOperation();
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var roleResult = await userManager.AddToRolesAsync(user, rolesToAdd);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IdentityOperationResult(false, GetErrors(roleResult));
        }

        await transaction.CommitAsync(cancellationToken);

        return SuccessfulOperation();
    }

    public async Task<IdentityOperationResult> SetRolesForUserAsync(
        SetRolesForUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoleNames = NormalizeNames(request.RoleNames);
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return FailedOperation("The user was not found.");
        }

        foreach (var roleName in normalizedRoleNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                return FailedOperation($"The '{roleName}' role was not found.");
            }
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToAdd = normalizedRoleNames
            .Except(currentRoles, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rolesToRemove = currentRoles
            .Except(normalizedRoleNames, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rolesToAdd.Length == 0 && rolesToRemove.Length == 0)
        {
            return SuccessfulOperation();
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IdentityOperationResult(false, GetErrors(removeResult));
            }
        }

        if (rolesToAdd.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IdentityOperationResult(false, GetErrors(addResult));
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return SuccessfulOperation();
    }

    private async Task RevokeActiveRefreshTokensAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var revokedAtUtc = timeProvider.GetUtcNow();
        await dbContext.RefreshTokens
            .Where(token => ids.Contains(token.UserId) && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.RevokedAtUtc,
                    revokedAtUtc),
                cancellationToken);
    }

    private static Guid GetUserId(string accessToken)
    {
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken);

        return Guid.Parse(token.Subject);
    }

    private static string[] NormalizeNames(IEnumerable<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string? DetectImageContentType(byte[]? image)
    {
        if (image is null || image.Length < 3)
        {
            return null;
        }

        if (image.Length >= 8 &&
            image[0] == 0x89 && image[1] == 0x50 && image[2] == 0x4E && image[3] == 0x47 &&
            image[4] == 0x0D && image[5] == 0x0A && image[6] == 0x1A && image[7] == 0x0A)
        {
            return "image/png";
        }

        if (image[0] == 0xFF && image[1] == 0xD8 && image[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (image.Length >= 12 &&
            image[0] == (byte)'R' && image[1] == (byte)'I' &&
            image[2] == (byte)'F' && image[3] == (byte)'F' &&
            image[8] == (byte)'W' && image[9] == (byte)'E' &&
            image[10] == (byte)'B' && image[11] == (byte)'P')
        {
            return "image/webp";
        }

        return null;
    }

    private static TokenResult SuccessfulToken(Guid userId, JwtToken token) =>
        new(
            true,
            userId,
            token.AccessToken,
            token.AccessTokenExpiresAtUtc,
            token.RefreshToken,
            token.RefreshTokenExpiresAtUtc,
            token.Permissions,
            []);

    private static TokenResult FailedToken(IReadOnlyCollection<string> errors) =>
        new(false, null, null, null, null, null, [], errors);

    private static IdentityOperationResult SuccessfulOperation() => new(true, []);

    private static IdentityOperationResult FailedOperation(string error) =>
        new(false, [error]);

    private static string[] GetErrors(IdentityResult result) =>
        result.Errors.Select(error => error.Description).ToArray();
}
