using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueById;
using Warehouse.Server.Controllers;

namespace Warehouse.Server.Tests.Controllers;

public sealed class GoodsIssuePrintTests
{
    [Fact]
    public void Print_RequiresReadAndExportPermissions()
    {
        var policies = typeof(GoodsIssuesController).GetMethod(nameof(GoodsIssuesController.Print))!
            .GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy).ToArray();
        Assert.Contains(PermissionPolicyNames.Inventory.GoodsIssuesRead, policies);
        Assert.Contains(PermissionPolicyNames.Inventory.GoodsIssuesExport, policies);
    }

    [Fact]
    public async Task Print_ReturnsEntireDocumentPdfInline_AndDisablesCaching()
    {
        var sender = Substitute.For<ISender>();
        var service = Substitute.For<IGoodsIssuePdfService>();
        var issue = new GoodsIssueSummary { Id = Guid.NewGuid(), IssueNumber = "OUT-1" };
        var bytes = "%PDF-1.7\n%%EOF"u8.ToArray();
        sender.Send(Arg.Is<GetGoodsIssueByIdQuery>(query => query.Id == issue.Id), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsIssueSummary>.Success(issue));
        service.GenerateAsync(issue, Arg.Any<CancellationToken>()).Returns(bytes);
        var controller = Controller(sender);

        var response = Assert.IsType<FileContentResult>(await controller.Print(issue.Id, service, default));

        Assert.Equal("application/pdf", response.ContentType);
        Assert.Equal(bytes, response.FileContents);
        Assert.True(string.IsNullOrEmpty(response.FileDownloadName));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
        await service.Received(1).GenerateAsync(issue, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Print_MissingIssueReturnsNotFound_AndDoesNotConvert()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetGoodsIssueByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsIssueSummary>.Error("پیدا نشد"));
        var service = Substitute.For<IGoodsIssuePdfService>();
        Assert.IsType<NotFoundObjectResult>(await Controller(sender).Print(Guid.NewGuid(), service, default));
        await service.DidNotReceive().GenerateAsync(Arg.Any<GoodsIssueSummary>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, 503)]
    [InlineData(true, 504)]
    public async Task Print_ConverterFailureReturnsActionableError(bool timeout, int expectedStatus)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetGoodsIssueByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsIssueSummary>.Success(new GoodsIssueSummary()));
        var service = Substitute.For<IGoodsIssuePdfService>();
        service.GenerateAsync(Arg.Any<GoodsIssueSummary>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<byte[]>(timeout ? new TimeoutException() : new ExcelToPdfConversionException("missing executable")));
        var response = Assert.IsType<ObjectResult>(await Controller(sender).Print(Guid.NewGuid(), service, default));
        Assert.Equal(expectedStatus, response.StatusCode);
    }

    private static GoodsIssuesController Controller(ISender sender) => new(sender, Substitute.For<IAuthorizationService>())
    { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
}
