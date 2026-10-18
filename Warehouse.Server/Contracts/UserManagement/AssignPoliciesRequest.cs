using Warehouse.Application.Common.Authorization;

namespace Warehouse.Server.Contracts.UserManagement;

public sealed record AssignPoliciesRequest(IReadOnlyCollection<AppPermission> Policies);
