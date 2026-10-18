using Warehouse.Server.Contracts.Common;

namespace Warehouse.Server.Contracts.UserManagement;

public sealed class GetUsersRequest : DataTableRequest
{
    public string? FullName { get; init; }

    public string? UserName { get; init; }

    public string? Email { get; init; }

    public bool? IsActive { get; init; }

    public string? RoleName { get; init; }
}
