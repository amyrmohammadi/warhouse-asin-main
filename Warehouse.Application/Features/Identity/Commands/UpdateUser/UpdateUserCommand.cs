using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string Email,
    string UserName) : IRequest<Result>;

internal sealed class UpdateUserCommandHandler(IIdentityService identityService)
    : IRequestHandler<UpdateUserCommand, Result>
{
    public async Task<Result> Handle(
        UpdateUserCommand request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.UpdateUserAsync(
            new UpdateUserRequest(
                request.UserId,
                request.FullName,
                request.PhoneNumber,
                request.Email,
                request.UserName),
            cancellationToken);

        return result.Succeeded
            ? Result.Success("User information updated.")
            : Result.Error(result.Errors);
    }
}
