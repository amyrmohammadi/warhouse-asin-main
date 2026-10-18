using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.SetPoliciesForRole;

public sealed record SetPoliciesForRoleCommand(
    string RoleName,
    IReadOnlyCollection<string> Policies) : IRequest<Result>;

internal sealed class SetPoliciesForRoleCommandHandler(
    IIdentityService identityService)
    : IRequestHandler<SetPoliciesForRoleCommand, Result>
{
    public async Task<Result> Handle(
        SetPoliciesForRoleCommand request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.SetPoliciesForRoleAsync(
            new SetPoliciesForRoleRequest(request.RoleName, request.Policies),
            cancellationToken);

        return result.Succeeded
            ? Result.Success()
            : Result.Error(result.Errors);
    }
}
