using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.CreateRole;

public sealed record CreateRoleCommand(string Name) : IRequest<Result>;

internal sealed class CreateRoleCommandHandler(
    IIdentityService identityService)
    : IRequestHandler<CreateRoleCommand, Result>
{
    public async Task<Result> Handle(
        CreateRoleCommand request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.CreateRoleAsync(
            new CreateRoleRequest(request.Name),
            cancellationToken);

        return result.Succeeded
            ? Result.Success()
            : Result.Error(result.Errors);
    }
}
