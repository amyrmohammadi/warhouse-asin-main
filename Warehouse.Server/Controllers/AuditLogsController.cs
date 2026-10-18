using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Auditing.Queries.GetAuditTrails;
using Warehouse.Server.Contracts.Auditing;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/admin/audit-logs")]
[Authorize(Policy = PermissionPolicyNames.Auditing.AuditLogsRead)]
public sealed class AuditLogsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(Result<GetAuditTrailsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<GetAuditTrailsResponse>>> GetAuditLogs(
        [FromQuery] GetAuditLogsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetAuditTrailsQuery
            {
                PaginationEnabled = request.PaginationEnabled,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                Search = request.Search,
                UserFullName = request.UserFullName,
                Action = request.Action,
                TableName = request.TableName,
                PrimaryKey = request.PrimaryKey,
                ChangedColumns = request.ChangedColumns,
                SortBy = request.SortBy,
                SortDescending = request.SortDescending
            },
            cancellationToken);

        return Ok(result);
    }
}
