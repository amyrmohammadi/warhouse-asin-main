using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoiceDetails;

public sealed record UpdateInvoiceDetailsCommand(
    Guid InvoiceId,
    string? Description,
    DateOnly? InvoiceDate,
    InvoiceProcessingStatus ProcessingStatus) : IRequest<Result>;

internal sealed class UpdateInvoiceDetailsCommandValidator : AbstractValidator<UpdateInvoiceDetailsCommand>
{
    public UpdateInvoiceDetailsCommandValidator()
    {
        RuleFor(command => command.InvoiceId).NotEmpty();
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.ProcessingStatus).IsInEnum();
    }
}

internal sealed class UpdateInvoiceDetailsCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdateInvoiceDetailsCommand, Result>
{
    public async Task<Result> Handle(
        UpdateInvoiceDetailsCommand request,
        CancellationToken cancellationToken)
    {
        var invoice = await unitOfWork.Repository<Invoice>()
            .GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return Result.Error("فاکتور پیدا نشد.");

        invoice.UpdateDetails(request.Description, request.InvoiceDate, request.ProcessingStatus);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
