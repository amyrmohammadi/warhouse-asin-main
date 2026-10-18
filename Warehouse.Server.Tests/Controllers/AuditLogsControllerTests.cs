using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Auditing.Queries.GetAuditTrails;
using Warehouse.Server.Contracts.Auditing;
using Warehouse.Server.Controllers;

namespace Warehouse.Server.Tests.Controllers;

public sealed class AuditLogsControllerTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    [Fact]
    public async Task GetAuditLogs_MapsQueryParametersAndReturnsOk()
    {
        var expected = Result<GetAuditTrailsResponse>.Success(
            new GetAuditTrailsResponse([], 100, 12, 2, 25, true));
        _sender
            .Send(
                Arg.Is<GetAuditTrailsQuery>(query =>
                    query.PageNumber == 2 &&
                    query.PageSize == 25 &&
                    query.PaginationEnabled &&
                    query.Search == "Users" &&
                    query.UserFullName == "مدیر" &&
                    query.Action == "Update" &&
                    query.TableName == "Users" &&
                    query.PrimaryKey == "user-id" &&
                    query.ChangedColumns == "FullName" &&
                    query.SortBy == "occurredAtUtc" &&
                    query.SortDescending),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new AuditLogsController(_sender);

        var response = await sut.GetAuditLogs(
            new GetAuditLogsRequest
            {
                PaginationEnabled = true,
                PageNumber = 2,
                PageSize = 25,
                Search = "Users",
                UserFullName = "مدیر",
                Action = "Update",
                TableName = "Users",
                PrimaryKey = "user-id",
                ChangedColumns = "FullName",
                SortBy = "occurredAtUtc",
                SortDescending = true
            },
            CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }
}
