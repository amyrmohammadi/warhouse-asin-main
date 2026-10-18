using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.SetUserActiveStatus;

public sealed record SetUserActiveStatusCommand(
    Guid UserId,
    bool IsActive) : IRequest<Result>;

internal sealed class SetUserActiveStatusCommandHandler(IIdentityService identityService)
    : IRequestHandler<SetUserActiveStatusCommand, Result>
{
    public async Task<Result> Handle(
        SetUserActiveStatusCommand request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.SetUserActiveStatusAsync(
            new SetUserActiveStatusRequest(request.UserId, request.IsActive),
            cancellationToken);

        return result.Succeeded
            ? Result.Success(request.IsActive ? "User activated." : "User deactivated.")
            : Result.Error(result.Errors);
    }
}
