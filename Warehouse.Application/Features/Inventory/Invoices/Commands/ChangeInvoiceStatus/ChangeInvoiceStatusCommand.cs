using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.Invoices.Commands.ChangeInvoiceStatus;

public sealed record ChangeInvoiceStatusCommand(Guid InvoiceId, InvoiceProcessingStatus Status,
    InvoiceProcessingStatus ExpectedStatus, bool CanReopen = false, bool CanApprove = false)
    : IRequest<Result<InvoiceDetails>>;

internal sealed class ChangeInvoiceStatusCommandValidator : AbstractValidator<ChangeInvoiceStatusCommand>
{
    public ChangeInvoiceStatusCommandValidator()
    {
        RuleFor(request => request.InvoiceId).NotEmpty();
        RuleFor(request => request.Status).IsInEnum();
        RuleFor(request => request.ExpectedStatus).IsInEnum();
    }
}

internal sealed class ChangeInvoiceStatusCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<ChangeInvoiceStatusCommand, Result<InvoiceDetails>>
{
    public async Task<Result<InvoiceDetails>> Handle(ChangeInvoiceStatusCommand request, CancellationToken token)
    {
        if (request.Status == InvoiceProcessingStatus.UnderReview ? !request.CanReopen :
            request.Status != InvoiceProcessingStatus.FinanceApproved || !request.CanApprove)
            return Result<InvoiceDetails>.Error("مجوز تغییر وضعیت فاکتور را ندارید.");
        await unitOfWork.BeginSerializableTransaction(token);
        try
        {
            var repository = unitOfWork.Repository<Invoice>();
            var invoice = await repository.GetByIdAsync(request.InvoiceId, token);
            if (invoice is null)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<InvoiceDetails>.Error("فاکتور پیدا نشد.");
            }
            if (invoice.ProcessingStatus != request.ExpectedStatus)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<InvoiceDetails>.Error(ConcurrencyMessages.Conflict);
            }
            if (request.Status == InvoiceProcessingStatus.UnderReview ? !invoice.IsLocked :
                invoice.ProcessingStatus != InvoiceProcessingStatus.SentToFinance)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<InvoiceDetails>.Error("این تغییر وضعیت برای فاکتور مجاز نیست.");
            }
            // This endpoint changes only the status; it cannot carry document edits.
            invoice.UpdateDetails(invoice.Description, invoice.InvoiceDate, request.Status);
            await unitOfWork.SaveAsync(token);
            await unitOfWork.CommitTransaction(token);
            var details = (await repository.ToListAsync(repository.Entities
                .Where(value => value.Id == invoice.Id).ToDetails(), token)).Single();
            return Result<InvoiceDetails>.Success(details);
        }
        catch
        {
            await unitOfWork.RollBackTransaction(token);
            throw;
        }
    }
}
