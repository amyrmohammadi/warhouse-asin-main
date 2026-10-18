using Warehouse.Application.Common.Authorization;

namespace Warehouse.IntegrationTests.Identity;

public sealed class ApiEnvelope<T>
{
    public bool IsSuccess { get; init; }

    public List<string> Messages { get; init; } = [];

    public T? Data { get; init; }
}

public sealed class CreateUserData
{
    public Guid UserId { get; init; }
}

public sealed class TokenData
{
    public Guid UserId { get; init; }

    public string AccessToken { get; init; } = string.Empty;

    public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    public string RefreshToken { get; init; } = string.Empty;

    public DateTimeOffset RefreshTokenExpiresAtUtc { get; init; }

    public List<AppPermission> Permissions { get; init; } = [];
}

public sealed class GetUsersData
{
    public List<UserListItemData> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int FilteredCount { get; init; }

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public bool PaginationEnabled { get; init; }
}

public sealed class GetRolesData
{
    public List<RoleListItemData> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int FilteredCount { get; init; }

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public bool PaginationEnabled { get; init; }
}

public sealed class RoleListItemData
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int UserCount { get; init; }

    public int PermissionCount { get; init; }
}

public sealed class UserListItemData
{
    public Guid Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public bool EmailConfirmed { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public List<string> Roles { get; init; } = [];

    public string? ProfileImageContentType { get; init; }

    public byte[]? ProfileImage { get; init; }
}

public sealed class CurrentUserProfileData
{
    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string? ProfileImageContentType { get; init; }

    public byte[]? ProfileImage { get; init; }
}

public sealed class GetAuditLogsData
{
    public List<AuditLogListItemData> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int FilteredCount { get; init; }

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public bool PaginationEnabled { get; init; }
}

public sealed class AuditLogListItemData
{
    public long Id { get; init; }

    public Guid? UserId { get; init; }

    public string UserFullName { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string TableName { get; init; } = string.Empty;

    public string PrimaryKey { get; init; } = string.Empty;

    public string? OldValues { get; init; }

    public string? NewValues { get; init; }

    public string? ChangedColumns { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }
}
