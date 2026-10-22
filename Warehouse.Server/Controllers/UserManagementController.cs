using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Identity.Commands.AddPoliciesToRole;
using Warehouse.Application.Features.Identity.Commands.AddRolesToUser;
using Warehouse.Application.Features.Identity.Commands.ChangeUserPassword;
using Warehouse.Application.Features.Identity.Commands.CreateManagedUser;
using Warehouse.Application.Features.Identity.Commands.CreateRole;
using Warehouse.Application.Features.Identity.Commands.CreateUser;
using Warehouse.Application.Features.Identity.Commands.DeleteRole;
using Warehouse.Application.Features.Identity.Commands.SetPoliciesForRole;
using Warehouse.Application.Features.Identity.Commands.SetUserActiveStatus;
using Warehouse.Application.Features.Identity.Commands.SetRolesForUser;
using Warehouse.Application.Features.Identity.Commands.UpdateUser;
using Warehouse.Application.Features.Identity.Commands.UpdateRole;
using Warehouse.Application.Features.Identity.Queries.GetRolePolicies;
using Warehouse.Application.Features.Identity.Queries.GetRoles;
using Warehouse.Application.Features.Identity.Queries.GetUsers;
using Warehouse.Server.Contracts.UserManagement;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/admin")]
public sealed class UserManagementController(ISender sender) : ControllerBase
{
    [Authorize(Policy = PermissionPolicyNames.UserManagement.UsersRead)]
    [HttpGet("users")]
    [ProducesResponseType(typeof(Result<GetUsersResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<GetUsersResponse>>> GetUsers(
        [FromQuery] Warehouse.Server.Contracts.UserManagement.GetUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetUsersQuery
            {
                PaginationEnabled = request.PaginationEnabled,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                Search = request.Search,
                FullName = request.FullName,
                UserName = request.UserName,
                Email = request.Email,
                IsActive = request.IsActive,
                RoleName = request.RoleName,
                SortBy = request.SortBy,
                SortDescending = request.SortDescending
            },
            cancellationToken);

        return Ok(result);
    }

    [Authorize(Policy = PermissionPolicyNames.RoleManagement.RolesRead)]
    [HttpGet("roles")]
    [ProducesResponseType(typeof(Result<GetRolesResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<GetRolesResponse>>> GetRoles(
        [FromQuery] Warehouse.Server.Contracts.UserManagement.GetRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetRolesQuery
            {
                PaginationEnabled = request.PaginationEnabled,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                Search = request.Search,
                RoleName = request.RoleName,
                UserCount = request.UserCount,
                PermissionCount = request.PermissionCount,
                SortBy = request.SortBy,
                SortDescending = request.SortDescending
            },
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = PermissionPolicyNames.RoleManagement.RolesManage)]
    [HttpPost("roles")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result>> CreateRole(
        [FromBody] Warehouse.Server.Contracts.UserManagement.CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateRoleCommand(request.Name),
            cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result)
            : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.RoleManagement.RolesManage)]
    [HttpPut("roles/{roleId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result>> UpdateRole(
        Guid roleId,
        [FromBody] Warehouse.Server.Contracts.UserManagement.UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateRoleCommand(roleId, request.Name),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.RoleManagement.RolesManage)]
    [HttpDelete("roles/{roleId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result>> DeleteRole(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteRoleCommand(roleId),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.UserManagement.UsersManage)]
    [Authorize(Policy = PermissionPolicyNames.UserManagement.UserRolesManage)]
    [HttpPost("users")]
    [ProducesResponseType(typeof(Result<CreateUserResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result<CreateUserResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<CreateUserResponse>>> CreateUser(
        [FromBody] Warehouse.Server.Contracts.UserManagement.CreateManagedUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateManagedUserCommand(
                request.Email,
                request.Password,
                request.FullName,
                request.PhoneNumber,
                request.UserName,
                request.IsActive,
                request.RoleNames),
            cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result)
            : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.RoleManagement.RolePoliciesManage)]
    [HttpGet("roles/{roleName}/policies")]
    [ProducesResponseType(typeof(Result<GetRolePoliciesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<GetRolePoliciesResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<GetRolePoliciesResponse>>> GetRolePolicies(
        string roleName,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetRolePoliciesQuery(roleName),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.UserManagement.UsersManage)]
    [HttpPut("users/{userId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result>> UpdateUser(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateUserCommand(
                userId,
                request.FullName,
                request.PhoneNumber,
                request.Email,
                request.UserName),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.UserManagement.UsersManage)]
    [HttpPut("users/{userId:guid}/active-status")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result>> SetUserActiveStatus(
        Guid userId,
        [FromBody] SetUserActiveStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SetUserActiveStatusCommand(userId, request.IsActive),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.UserManagement.UsersManage)]
    [HttpPut("users/{userId:guid}/password")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result>> ChangeUserPassword(
        Guid userId,
        [FromBody] ChangeUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ChangeUserPasswordCommand(userId, request.NewPassword),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.UserManagement.UserRolesManage)]
    [HttpPost("users/{userId:guid}/roles")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result>> AssignRoles(
        Guid userId,
        [FromBody] AssignRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddRolesToUserCommand(userId, request.RoleNames),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.UserManagement.UserRolesManage)]
    [HttpPut("users/{userId:guid}/roles")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result>> SetRoles(
        Guid userId,
        [FromBody] AssignRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SetRolesForUserCommand(userId, request.RoleNames),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.RoleManagement.RolePoliciesManage)]
    [HttpPost("roles/{roleName}/policies")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result>> AssignPolicies(
        string roleName,
        [FromBody] AssignPoliciesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddPoliciesToRoleCommand(
                roleName,
                request.Policies.Select(permission => permission.ToString()).ToArray()),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.RoleManagement.RolePoliciesManage)]
    [HttpPut("roles/{roleName}/policies")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result>> SetPolicies(
        string roleName,
        [FromBody] AssignPoliciesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SetPoliciesForRoleCommand(
                roleName,
                request.Policies.Select(permission => permission.ToString()).ToArray()),
            cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
