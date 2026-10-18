using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.ChangeUserPassword;

public sealed record ChangeUserPasswordCommand(
    Guid UserId,
    string NewPassword) : IRequest<Result>;

internal sealed class ChangeUserPasswordCommandHandler(IIdentityService identityService)
    : IRequestHandler<ChangeUserPasswordCommand, Result>
{
    public async Task<Result> Handle(
        ChangeUserPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.ChangeUserPasswordAsync(
            new ChangeUserPasswordRequest(request.UserId, request.NewPassword),
            cancellationToken);

        return result.Succeeded
            ? Result.Success("User password changed.")
            : Result.Error(result.Errors);
    }
}
