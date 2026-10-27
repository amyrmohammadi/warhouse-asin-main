using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoiceDetails;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class UpdateInvoiceDetailsTests
{
    [Fact]
    public async Task Update_SavesDescriptionDateAndProcessingStatus()
    {
        await using var context = new ApplicationTestContext();
        var invoice = new Invoice("INV-1", null);
        Assert.Equal(InvoiceProcessingStatus.UnderReview, invoice.ProcessingStatus);
        var repository = Substitute.For<IRepositoryAsync<Invoice, Guid>>();
        repository.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        context.UnitOfWork.Repository<Invoice>().Returns(repository);

        var result = await context.Sender.Send(new UpdateInvoiceDetailsCommand(
            invoice.Id, "  شرح فاکتور  ", new DateOnly(2026, 10, 20),
            InvoiceProcessingStatus.SentToFinance));

        Assert.True(result.IsSuccess);
        Assert.Equal("شرح فاکتور", invoice.Description);
        Assert.Equal(new DateOnly(2026, 10, 20), invoice.InvoiceDate);
        Assert.Equal(InvoiceProcessingStatus.SentToFinance, invoice.ProcessingStatus);
        await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithUndefinedStatus_IsRejected()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(
            new UpdateInvoiceDetailsCommand(Guid.NewGuid(), null, null,
                (InvoiceProcessingStatus)99)));
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }
}
