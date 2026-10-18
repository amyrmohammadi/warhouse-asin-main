using AutoMapper;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.AddRolesToUser;

public sealed record AddRolesToUserCommand(
    Guid UserId,
    IReadOnlyCollection<string> RoleNames) : IRequest<Result>;

internal sealed class AddRolesToUserCommandHandler(
    IIdentityService identityService,
    IMapper mapper) : IRequestHandler<AddRolesToUserCommand, Result>
{
    public async Task<Result> Handle(
        AddRolesToUserCommand request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = mapper.Map<AddRolesToUserRequest>(request);
        var result = await identityService.AddRolesToUserAsync(
            serviceRequest,
            cancellationToken);

        return result.Succeeded
            ? Result.Success()
            : Result.Error(result.Errors);
    }
}
