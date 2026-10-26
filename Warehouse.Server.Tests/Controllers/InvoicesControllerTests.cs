using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoiceItem;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoiceById;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoices;
using Warehouse.Server.Controllers;
using Warehouse.Server.Contracts.Inventory;

namespace Warehouse.Server.Tests.Controllers;

public sealed class InvoicesControllerTests
{
    [Fact]
    public void Controller_UsesDedicatedInvoiceReadPolicy()
    {
        var authorize = typeof(InvoicesController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(PermissionPolicyNames.Inventory.InvoicesRead, authorize.Policy);
    }

    [Fact]
    public void UpdateItem_UsesReceiptAndInvoiceOperationsPolicy()
    {
        var action = typeof(InvoicesController).GetMethod(nameof(InvoicesController.UpdateItem))!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(
            PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations,
            authorize.Policy);
    }

    [Fact]
    public async Task GetById_ForwardsInvoiceIdAndReturnsResult()
    {
        var sender = Substitute.For<ISender>();
        var id = Guid.NewGuid();
        var expected = Result<InvoiceDetails>.Success(new InvoiceDetails
        {
            Id = id,
            InvoiceNumber = "INV-1"
        });
        sender.Send(
                Arg.Is<GetInvoiceByIdQuery>(query => query.Id == id),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var controller = new InvoicesController(sender);

        var response = await controller.GetById(id, default);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task GetAll_ForwardsPagingAndFilters()
    {
        var sender = Substitute.For<ISender>();
        var expected = Result<InvoiceListResult>.Success(new InvoiceListResult([], 0, 0, 2, 25));
        sender.Send(Arg.Any<GetInvoicesQuery>(), Arg.Any<CancellationToken>()).Returns(expected);
        var controller = new InvoicesController(sender);
        var query = new GetInvoicesQuery { PageNumber = 2, PageSize = 25, Search = "INV" };

        var response = await controller.GetAll(query, default);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, ok.Value);
        await sender.Received(1).Send(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateItem_WithConcurrencyConflict_ReturnsHttp409()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<UpdateInvoiceItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UpdateInvoiceItemResult>.Error(ConcurrencyMessages.Conflict));
        var controller = new InvoicesController(sender);

        var response = await controller.UpdateItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpdateInvoiceItemRequest("REC-1", Guid.NewGuid()),
            default);

        Assert.IsType<ConflictObjectResult>(response.Result);
    }
}
