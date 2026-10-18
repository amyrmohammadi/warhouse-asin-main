using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;
using Warehouse.Application.Features.Identity.Commands.CreateUser;

namespace Warehouse.Application.Features.Identity.Commands.CreateManagedUser;

public sealed record CreateManagedUserCommand(
    string Email,
    string Password,
    string FullName,
    string PhoneNumber,
    string? UserName,
    bool IsActive,
    IReadOnlyCollection<string> RoleNames)
    : IRequest<Result<CreateUserResponse>>;

internal sealed class CreateManagedUserCommandHandler(
    IIdentityService identityService)
    : IRequestHandler<CreateManagedUserCommand, Result<CreateUserResponse>>
{
    public async Task<Result<CreateUserResponse>> Handle(
        CreateManagedUserCommand request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.CreateManagedUserAsync(
            new CreateManagedUserRequest(
                request.Email,
                request.Password,
                request.FullName,
                request.PhoneNumber,
                request.UserName,
                request.IsActive,
                request.RoleNames),
            cancellationToken);

        if (!result.Succeeded || result.UserId is null)
        {
            return Result<CreateUserResponse>.Error(
                result.Errors.Count > 0
                    ? result.Errors
                    : ["The user was created without a valid identifier."]);
        }

        return Result<CreateUserResponse>.Success(
            new CreateUserResponse(result.UserId.Value));
    }
}
