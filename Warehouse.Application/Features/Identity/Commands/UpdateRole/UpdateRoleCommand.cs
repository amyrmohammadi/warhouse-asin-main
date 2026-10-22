using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.UpdateRole;

public sealed record UpdateRoleCommand(Guid RoleId, string Name) : IRequest<Result>;

internal sealed class UpdateRoleCommandHandler(IIdentityService identityService)
    : IRequestHandler<UpdateRoleCommand, Result>
{
    public async Task<Result> Handle(
        UpdateRoleCommand request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.UpdateRoleAsync(
            new UpdateRoleRequest(request.RoleId, request.Name),
            cancellationToken);

        return result.Succeeded ? Result.Success() : Result.Error(result.Errors);
    }
}
