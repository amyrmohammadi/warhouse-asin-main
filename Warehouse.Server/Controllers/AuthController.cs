using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Identity.Commands.ChangeCurrentUserPassword;
using Warehouse.Application.Features.Identity.Commands.CreateUser;
using Warehouse.Application.Features.Identity.Commands.Login;
using Warehouse.Application.Features.Identity.Commands.RefreshToken;
using Warehouse.Application.Features.Identity.Commands.UpdateCurrentUserProfile;
using Warehouse.Application.Features.Identity.Queries.GetCurrentAccess;
using Warehouse.Application.Features.Identity.Queries.GetCurrentUserProfile;
using Warehouse.Server.Contracts.Account;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    private const int MaximumProfileImageSize = 2 * 1024 * 1024;

    [Authorize]
    [HttpGet("access")]
    [ProducesResponseType(typeof(Result<GetCurrentAccessResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<GetCurrentAccessResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Result<GetCurrentAccessResponse>>> GetCurrentAccess(
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized(Result<GetCurrentAccessResponse>.Error(
                "The authenticated user identifier is invalid."));
        }

        var result = await sender.Send(
            new GetCurrentAccessQuery(userId),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : Unauthorized(result);
    }

    [Authorize]
    [HttpGet("profile")]
    [ProducesResponseType(typeof(Result<GetCurrentUserProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<GetCurrentUserProfileResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Result<GetCurrentUserProfileResponse>>> GetProfile(
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized(Result<GetCurrentUserProfileResponse>.Error(
                "The authenticated user identifier is invalid."));
        }

        var result = await sender.Send(
            new GetCurrentUserProfileQuery(userId),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : Unauthorized(result);
    }

    [Authorize]
    [HttpPut("profile")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3 * 1024 * 1024)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Result>> UpdateProfile(
        [FromForm] UpdateCurrentUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized(Result.Error(
                "The authenticated user identifier is invalid."));
        }

        byte[]? image = null;
        string? imageContentType = null;
        if (request.ProfileImage is not null)
        {
            if (request.ProfileImage.Length == 0 ||
                request.ProfileImage.Length > MaximumProfileImageSize)
            {
                return BadRequest(Result.Error(
                    "Profile image must be a non-empty file no larger than 2 MB."));
            }

            await using var stream = new MemoryStream((int)request.ProfileImage.Length);
            await request.ProfileImage.CopyToAsync(stream, cancellationToken);
            image = stream.ToArray();
            imageContentType = request.ProfileImage.ContentType;
        }

        var result = await sender.Send(
            new UpdateCurrentUserProfileCommand(
                userId,
                request.FullName,
                request.PhoneNumber,
                image,
                imageContentType,
                request.RemoveProfileImage),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPut("profile/password")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Result>> ChangePassword(
        [FromBody] ChangeCurrentUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized(Result.Error(
                "The authenticated user identifier is invalid."));
        }

        var result = await sender.Send(
            new ChangeCurrentUserPasswordCommand(
                userId,
                request.CurrentPassword,
                request.NewPassword),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(Result<CreateUserResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result<CreateUserResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<CreateUserResponse>>> Register(
        [FromBody] CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result)
            : BadRequest(result);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(Result<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<LoginResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Result<LoginResponse>>> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Ok(result)
            : Unauthorized(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(Result<RefreshTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<RefreshTokenResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Result<RefreshTokenResponse>>> RefreshToken(
        [FromBody] RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Ok(result)
            : Unauthorized(result);
    }

    private bool TryGetAuthenticatedUserId(out Guid userId)
    {
        var userIdValue = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userIdValue, out userId);
    }
}
