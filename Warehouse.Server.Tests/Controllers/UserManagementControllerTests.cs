using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Identity.Commands.AddPoliciesToRole;
using Warehouse.Application.Features.Identity.Commands.AddRolesToUser;
using Warehouse.Application.Features.Identity.Commands.ChangeUserPassword;
using Warehouse.Application.Features.Identity.Commands.CreateManagedUser;
using Warehouse.Application.Features.Identity.Commands.CreateRole;
using Warehouse.Application.Features.Identity.Commands.CreateUser;
using Warehouse.Application.Features.Identity.Commands.SetPoliciesForRole;
using Warehouse.Application.Features.Identity.Commands.SetUserActiveStatus;
using Warehouse.Application.Features.Identity.Commands.SetRolesForUser;
using Warehouse.Application.Features.Identity.Commands.UpdateUser;
using Warehouse.Application.Features.Identity.Queries.GetRolePolicies;
using Warehouse.Application.Features.Identity.Queries.GetRoles;
using Warehouse.Application.Features.Identity.Queries.GetUsers;
using Warehouse.Server.Contracts.UserManagement;
using Warehouse.Server.Controllers;

namespace Warehouse.Server.Tests.Controllers;

public sealed class UserManagementControllerTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    [Fact]
    public async Task GetUsers_MapsQueryParametersAndReturnsOk()
    {
        var expected = Result<GetUsersResponse>.Success(
            new GetUsersResponse([], 0, 0, 2, 25, true));
        _sender
            .Send(
                Arg.Is<GetUsersQuery>(query =>
                    query.PageNumber == 2 &&
                    query.PageSize == 25 &&
                    query.Search == "amir" &&
                    query.FullName == "Amir Ahmadi" &&
                    query.UserName == "amir" &&
                    query.Email == "amir@example.com" &&
                    query.IsActive == true &&
                    query.RoleName == "Admin" &&
                    query.SortBy == "email" &&
                    query.SortDescending),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.GetUsers(
            new Warehouse.Server.Contracts.UserManagement.GetUsersRequest
            {
                PaginationEnabled = true,
                PageNumber = 2,
                PageSize = 25,
                Search = "amir",
                FullName = "Amir Ahmadi",
                UserName = "amir",
                Email = "amir@example.com",
                IsActive = true,
                RoleName = "Admin",
                SortBy = "email",
                SortDescending = true
            },
            CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task GetRoles_ReturnsMediatorResult()
    {
        var expected = Result<GetRolesResponse>.Success(
            new GetRolesResponse([], 0, 0, 2, 25, true));
        _sender
            .Send(
                Arg.Is<GetRolesQuery>(query =>
                    query.PaginationEnabled &&
                    query.PageNumber == 2 &&
                    query.PageSize == 25 &&
                    query.Search == "admin" &&
                    query.SortBy == "userCount" &&
                    query.SortDescending),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.GetRoles(
            new Warehouse.Server.Contracts.UserManagement.GetRolesRequest
            {
                PaginationEnabled = true,
                PageNumber = 2,
                PageSize = 25,
                Search = "admin",
                SortBy = "userCount",
                SortDescending = true
            },
            CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task CreateRole_WhenCommandSucceeds_ReturnsCreated()
    {
        var expected = Result.Success("Created.");
        _sender
            .Send(
                Arg.Is<CreateRoleCommand>(command => command.Name == "WarehouseManager"),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.CreateRole(
            new Warehouse.Server.Contracts.UserManagement.CreateRoleRequest("WarehouseManager"),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task CreateUser_MapsActivationAndRolesAndReturnsCreated()
    {
        var userId = Guid.NewGuid();
        var request = new Warehouse.Server.Contracts.UserManagement.CreateManagedUserRequest(
            "user@example.com",
            "Password1!",
            "Managed User",
            "+989121234567",
            "managed-user",
            true,
            ["Admin"]);
        var expected = Result<CreateUserResponse>.Success(new CreateUserResponse(userId));
        _sender
            .Send(
                Arg.Is<CreateManagedUserCommand>(command =>
                    command.Email == request.Email &&
                    command.IsActive &&
                    command.RoleNames.SequenceEqual(request.RoleNames)),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.CreateUser(request, CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task GetRolePolicies_ReturnsMediatorResult()
    {
        var expected = Result<GetRolePoliciesResponse>.Success(
            new GetRolePoliciesResponse("Admin", []));
        _sender
            .Send(
                Arg.Is<GetRolePoliciesQuery>(query => query.RoleName == "Admin"),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.GetRolePolicies("Admin", CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task UpdateUser_WhenCommandSucceeds_MapsRouteAndBody()
    {
        var userId = Guid.NewGuid();
        var request = new UpdateUserRequest(
            "Amir Ahmadi",
            "+989121234567",
            "amir@example.com",
            "amir");
        var expected = Result.Success("Updated.");
        _sender
            .Send(
                Arg.Is<UpdateUserCommand>(command =>
                    command.UserId == userId &&
                    command.FullName == request.FullName &&
                    command.PhoneNumber == request.PhoneNumber &&
                    command.Email == request.Email &&
                    command.UserName == request.UserName),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.UpdateUser(userId, request, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task SetUserActiveStatus_WhenCommandSucceeds_MapsRequestedStatus()
    {
        var userId = Guid.NewGuid();
        var request = new SetUserActiveStatusRequest(true);
        var expected = Result.Success("Activated.");
        _sender
            .Send(
                Arg.Is<SetUserActiveStatusCommand>(command =>
                    command.UserId == userId && command.IsActive),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.SetUserActiveStatus(
            userId,
            request,
            CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task ChangeUserPassword_WhenCommandFails_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var request = new ChangeUserPasswordRequest("NewPassword1!");
        var expected = Result.Error("Password rejected.");
        _sender
            .Send(
                Arg.Is<ChangeUserPasswordCommand>(command =>
                    command.UserId == userId &&
                    command.NewPassword == request.NewPassword),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.ChangeUserPassword(
            userId,
            request,
            CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task AssignRoles_WhenCommandSucceeds_ReturnsOkAndMapsRouteUserId()
    {
        var userId = Guid.NewGuid();
        var request = new AssignRolesRequest(["Admin", "User"]);
        var expected = Result.Success("Roles assigned.");
        _sender
            .Send(
                Arg.Is<AddRolesToUserCommand>(command =>
                    command.UserId == userId &&
                    command.RoleNames.SequenceEqual(request.RoleNames)),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.AssignRoles(userId, request, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task AssignRoles_WhenCommandFails_ReturnsBadRequest()
    {
        var request = new AssignRolesRequest(["MissingRole"]);
        var expected = Result.Error("Role not found.");
        _sender
            .Send(Arg.Any<AddRolesToUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.AssignRoles(Guid.NewGuid(), request, CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task SetRoles_WhenCommandSucceeds_ReplacesSelectedRoles()
    {
        var userId = Guid.NewGuid();
        var request = new AssignRolesRequest(["Admin"]);
        var expected = Result.Success("Roles updated.");
        _sender
            .Send(
                Arg.Is<SetRolesForUserCommand>(command =>
                    command.UserId == userId &&
                    command.RoleNames.SequenceEqual(request.RoleNames)),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.SetRoles(userId, request, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task SetRoles_WhenCommandFails_ReturnsBadRequest()
    {
        var expected = Result.Error("User not found.");
        _sender
            .Send(Arg.Any<SetRolesForUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.SetRoles(
            Guid.NewGuid(),
            new AssignRolesRequest([]),
            CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task AssignPolicies_WhenCommandSucceeds_MapsEnumValuesToPolicyNames()
    {
        var request = new AssignPoliciesRequest(
            [AppPermission.UsersRead, AppPermission.RolePoliciesManage]);
        var expected = Result.Success("Policies assigned.");
        _sender
            .Send(
                Arg.Is<AddPoliciesToRoleCommand>(command =>
                    command.RoleName == "Admin" &&
                    command.Policies.SequenceEqual(
                        new[] { "UsersRead", "RolePoliciesManage" })),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.AssignPolicies("Admin", request, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task AssignPolicies_WhenCommandFails_ReturnsBadRequest()
    {
        var request = new AssignPoliciesRequest([AppPermission.UsersRead]);
        var expected = Result.Error("Role not found.");
        _sender
            .Send(Arg.Any<AddPoliciesToRoleCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.AssignPolicies("MissingRole", request, CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task SetPolicies_WhenCommandSucceeds_ReplacesRolePolicies()
    {
        var request = new AssignPoliciesRequest(
            [AppPermission.UsersRead, AppPermission.AuditLogsRead]);
        var expected = Result.Success("Policies updated.");
        _sender
            .Send(
                Arg.Is<SetPoliciesForRoleCommand>(command =>
                    command.RoleName == "Admin" &&
                    command.Policies.SequenceEqual(
                        new[] { "UsersRead", "AuditLogsRead" })),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new UserManagementController(_sender);

        var response = await sut.SetPolicies("Admin", request, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }
}
