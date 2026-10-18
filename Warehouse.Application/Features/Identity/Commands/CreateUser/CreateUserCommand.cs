using AutoMapper;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    string Password,
    string FullName,
    string PhoneNumber,
    string? UserName = null) : IRequest<Result<CreateUserResponse>>;

internal sealed class CreateUserCommandHandler(
    IIdentityService identityService,
    IMapper mapper) : IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>
{
    public async Task<Result<CreateUserResponse>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = mapper.Map<CreateUserRequest>(request);
        var result = await identityService.CreateUserAsync(serviceRequest, cancellationToken);

        if (!result.Succeeded)
        {
            return Result<CreateUserResponse>.Error(result.Errors);
        }

        if (result.UserId is null)
        {
            return Result<CreateUserResponse>.Error(
                "The user was created without a valid identifier.");
        }

        return Result<CreateUserResponse>.Success(
            mapper.Map<CreateUserResponse>(result));
    }
}
