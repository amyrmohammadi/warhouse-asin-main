using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Server.Contracts.Common;

namespace Warehouse.Server.Contracts.UserManagement;

public sealed class GetRolesRequest : DataTableRequest
{
    public string? RoleName { get; init; }

    public NumericFilter<int>? UserCount { get; init; }

    public NumericFilter<int>? PermissionCount { get; init; }

}
