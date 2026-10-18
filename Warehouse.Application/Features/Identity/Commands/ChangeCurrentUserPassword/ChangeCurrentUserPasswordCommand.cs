using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.ChangeCurrentUserPassword;

public sealed record ChangeCurrentUserPasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;

internal sealed class ChangeCurrentUserPasswordCommandHandler(IIdentityService identityService)
    : IRequestHandler<ChangeCurrentUserPasswordCommand, Result>
{
    public async Task<Result> Handle(
        ChangeCurrentUserPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.ChangeCurrentUserPasswordAsync(
            new ChangeCurrentUserPasswordRequest(
                request.UserId,
                request.CurrentPassword,
                request.NewPassword),
            cancellationToken);

        return result.Succeeded
            ? Result.Success("Password changed.")
            : Result.Error(result.Errors);
    }
}
