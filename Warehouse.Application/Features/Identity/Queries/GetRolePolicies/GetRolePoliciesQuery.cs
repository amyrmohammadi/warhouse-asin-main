using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.Identity.Queries.GetRolePolicies;

public sealed record GetRolePoliciesQuery(string RoleName)
    : IRequest<Result<GetRolePoliciesResponse>>;

internal sealed class GetRolePoliciesQueryHandler(
    IIdentityService identityService)
    : IRequestHandler<GetRolePoliciesQuery, Result<GetRolePoliciesResponse>>
{
    public async Task<Result<GetRolePoliciesResponse>> Handle(
        GetRolePoliciesQuery request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.GetRolePoliciesAsync(
            request.RoleName,
            cancellationToken);

        if (!result.Succeeded || result.RolePolicies is null)
        {
            return Result<GetRolePoliciesResponse>.Error(result.Errors);
        }

        var response = new GetRolePoliciesResponse(
            result.RolePolicies.RoleName,
            result.RolePolicies.Groups
                .Select(group => new RolePermissionGroupResponse(
                    group.Key,
                    group.DisplayName,
                    group.Permissions
                        .Select(permission => new RolePermissionResponse(
                            permission.Name,
                            permission.DisplayName,
                            permission.IsAssigned))
                        .ToArray()))
                .ToArray());

        return Result<GetRolePoliciesResponse>.Success(response);
    }
}
