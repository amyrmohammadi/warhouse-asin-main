using AutoMapper;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.SetRolesForUser;

public sealed record SetRolesForUserCommand(
    Guid UserId,
    IReadOnlyCollection<string> RoleNames) : IRequest<Result>;

internal sealed class SetRolesForUserCommandHandler(
    IIdentityService identityService,
    IMapper mapper) : IRequestHandler<SetRolesForUserCommand, Result>
{
    public async Task<Result> Handle(
        SetRolesForUserCommand request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = mapper.Map<SetRolesForUserRequest>(request);
        var result = await identityService.SetRolesForUserAsync(
            serviceRequest,
            cancellationToken);

        return result.Succeeded
            ? Result.Success()
            : Result.Error(result.Errors);
    }
}
