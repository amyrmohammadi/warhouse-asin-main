using Warehouse.Application.Common.Authorization;

namespace Warehouse.Application.Features.Identity.Queries.GetCurrentAccess;

public sealed record GetCurrentAccessResponse(
    IReadOnlyCollection<AppPermission> Permissions);
