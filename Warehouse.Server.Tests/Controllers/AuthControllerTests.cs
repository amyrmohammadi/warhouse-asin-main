using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Identity.Commands.CreateUser;
using Warehouse.Application.Features.Identity.Commands.Login;
using Warehouse.Application.Features.Identity.Commands.RefreshToken;
using Warehouse.Application.Features.Identity.Queries.GetCurrentAccess;
using Warehouse.Server.Controllers;

namespace Warehouse.Server.Tests.Controllers;

public sealed class AuthControllerTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    [Fact]
    public async Task Register_WhenCommandSucceeds_ReturnsCreatedResult()
    {
        var command = new CreateUserCommand(
            "user@example.com",
            "Password1!",
            "Test User",
            "+989121234567",
            "user");
        var expected = Result<CreateUserResponse>.Success(new CreateUserResponse(Guid.NewGuid()));
        var cancellationToken = new CancellationTokenSource().Token;
        _sender.Send(command, cancellationToken).Returns(expected);
        var sut = new AuthController(_sender);

        var response = await sut.Register(command, cancellationToken);

        var result = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Same(expected, result.Value);
        await _sender.Received(1).Send(command, cancellationToken);
    }

    [Fact]
    public async Task Register_WhenCommandFails_ReturnsBadRequest()
    {
        var command = new CreateUserCommand(
            "invalid",
            "weak",
            "Test User",
            "+989121234567");
        var expected = Result<CreateUserResponse>.Error("Registration failed.");
        _sender.Send(command, Arg.Any<CancellationToken>()).Returns(expected);
        var sut = new AuthController(_sender);

        var response = await sut.Register(command, CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsOk()
    {
        var command = new LoginCommand("user@example.com", "Password1!");
        var expected = Result<LoginResponse>.Success(CreateLoginResponse());
        _sender.Send(command, Arg.Any<CancellationToken>()).Returns(expected);
        var sut = new AuthController(_sender);

        var response = await sut.Login(command, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorized()
    {
        var command = new LoginCommand("user@example.com", "wrong-password");
        var expected = Result<LoginResponse>.Error("Invalid credentials.");
        _sender.Send(command, Arg.Any<CancellationToken>()).Returns(expected);
        var sut = new AuthController(_sender);

        var response = await sut.Login(command, CancellationToken.None);

        var result = Assert.IsType<UnauthorizedObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task RefreshToken_WhenTokenIsValid_ReturnsOk()
    {
        var command = new RefreshTokenCommand("valid-refresh-token");
        var expected = Result<RefreshTokenResponse>.Success(CreateRefreshTokenResponse());
        _sender.Send(command, Arg.Any<CancellationToken>()).Returns(expected);
        var sut = new AuthController(_sender);

        var response = await sut.RefreshToken(command, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task RefreshToken_WhenTokenIsInvalid_ReturnsUnauthorized()
    {
        var command = new RefreshTokenCommand("invalid-refresh-token");
        var expected = Result<RefreshTokenResponse>.Error("Invalid refresh token.");
        _sender.Send(command, Arg.Any<CancellationToken>()).Returns(expected);
        var sut = new AuthController(_sender);

        var response = await sut.RefreshToken(command, CancellationToken.None);

        var result = Assert.IsType<UnauthorizedObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task GetCurrentAccess_WhenUserIsAuthenticated_ReturnsCurrentPermissions()
    {
        var userId = Guid.NewGuid();
        var query = new GetCurrentAccessQuery(userId);
        var expected = Result<GetCurrentAccessResponse>.Success(
            new GetCurrentAccessResponse([AppPermission.UsersRead]));
        _sender.Send(query, Arg.Any<CancellationToken>()).Returns(expected);
        var sut = CreateController(userId.ToString());

        var response = await sut.GetCurrentAccess(CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
        await _sender.Received(1).Send(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCurrentAccess_WhenUserIdentifierIsInvalid_ReturnsUnauthorized()
    {
        var sut = CreateController("invalid-user-id");

        var response = await sut.GetCurrentAccess(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response.Result);
        await _sender.DidNotReceive().Send(
            Arg.Any<GetCurrentAccessQuery>(),
            Arg.Any<CancellationToken>());
    }

    private static LoginResponse CreateLoginResponse()
    {
        var now = DateTimeOffset.UtcNow;
        return new LoginResponse(
            Guid.NewGuid(),
            "access-token",
            now.AddMinutes(15),
            "refresh-token",
            now.AddDays(7),
            [AppPermission.UsersRead]);
    }

    private static RefreshTokenResponse CreateRefreshTokenResponse()
    {
        var now = DateTimeOffset.UtcNow;
        return new RefreshTokenResponse(
            Guid.NewGuid(),
            "new-access-token",
            now.AddMinutes(15),
            "new-refresh-token",
            now.AddDays(7),
            [AppPermission.UsersRead]);
    }

    private AuthController CreateController(string userId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)],
            "Test"));

        return new AuthController(_sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }
}
