using MediatR;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.Identity.Queries.GetCurrentAccess;

public sealed record GetCurrentAccessQuery(Guid UserId)
    : IRequest<Result<GetCurrentAccessResponse>>;

internal sealed class GetCurrentAccessQueryHandler(
    IUserAccessService userAccessService)
    : IRequestHandler<GetCurrentAccessQuery, Result<GetCurrentAccessResponse>>
{
    public async Task<Result<GetCurrentAccessResponse>> Handle(
        GetCurrentAccessQuery request,
        CancellationToken cancellationToken)
    {
        var access = await userAccessService.GetAccessAsync(
            request.UserId,
            cancellationToken);
        if (!access.IsActive)
        {
            return Result<GetCurrentAccessResponse>.Error(
                "The user account is inactive or does not exist.");
        }

        var permissions = access.Permissions
            .Select(name => Enum.TryParse<AppPermission>(name, false, out var permission)
                ? permission
                : (AppPermission?)null)
            .Where(permission => permission.HasValue)
            .Select(permission => permission!.Value)
            .Distinct()
            .Order()
            .ToArray();

        return Result<GetCurrentAccessResponse>.Success(
            new GetCurrentAccessResponse(permissions));
    }
}
