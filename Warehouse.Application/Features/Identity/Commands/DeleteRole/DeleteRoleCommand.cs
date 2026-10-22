using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.DeleteRole;

public sealed record DeleteRoleCommand(Guid RoleId) : IRequest<Result>;

internal sealed class DeleteRoleCommandHandler(IIdentityService identityService)
    : IRequestHandler<DeleteRoleCommand, Result>
{
    public async Task<Result> Handle(
        DeleteRoleCommand request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.DeleteRoleAsync(
            new DeleteRoleRequest(request.RoleId),
            cancellationToken);

        return result.Succeeded ? Result.Success() : Result.Error(result.Errors);
    }
}
