using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.Invoices.Commands.PrintInvoices;

public sealed record PrintInvoicesCommand(IReadOnlyCollection<Guid> InvoiceIds, bool SendToFinance)
    : IRequest<Result<byte[]>>;

internal sealed class PrintInvoicesCommandValidator : AbstractValidator<PrintInvoicesCommand>
{
    public PrintInvoicesCommandValidator()
    {
        RuleFor(request => request.InvoiceIds).NotEmpty().WithMessage("حداقل یک فاکتور برای چاپ انتخاب کنید.")
            .Must(ids => ids is not null && ids.Count <= 500).WithMessage("در هر نوبت حداکثر ۵۰۰ فاکتور انتخاب کنید.")
            .Must(ids => ids is not null && ids.Distinct().Count() == ids.Count).WithMessage("فاکتور تکراری انتخاب شده است.");
        RuleForEach(request => request.InvoiceIds).NotEmpty();
    }
}

internal sealed class PrintInvoicesCommandHandler(
    IUnitOfWork<Guid> unitOfWork, IInvoicePdfService pdfService, TimeProvider timeProvider)
    : IRequestHandler<PrintInvoicesCommand, Result<byte[]>>
{
    public async Task<Result<byte[]>> Handle(PrintInvoicesCommand request, CancellationToken token)
    {
        await unitOfWork.BeginSerializableTransaction(token);
        try
        {
            var repository = unitOfWork.Repository<Invoice>();
            var invoices = await repository.ToListAsync(repository.Entities
                .Where(invoice => request.InvoiceIds.Contains(invoice.Id)).ToSummary(), token);
            var errors = new List<string>();
            if (invoices.Count != request.InvoiceIds.Count)
                errors.Add("یک یا چند فاکتور انتخاب‌شده پیدا نشد؛ فهرست را تازه‌سازی کنید.");
            foreach (var invoice in invoices)
            {
                if (!invoice.Price.HasValue)
                    errors.Add($"مبلغ فاکتور «{invoice.InvoiceNumber}» وارد نشده است؛ ابتدا مبلغ را ثبت کنید.");
                if (string.IsNullOrWhiteSpace(invoice.Counterparty))
                    errors.Add($"طرف حساب فاکتور «{invoice.InvoiceNumber}» مشخص نیست؛ ابتدا طرف حساب را ثبت کنید.");
                if (request.SendToFinance && invoice.ProcessingStatus != InvoiceProcessingStatus.UnderReview)
                    errors.Add($"فاکتور «{invoice.InvoiceNumber}» قبلاً به مالی ارسال یا تأیید شده است؛ گزینه چاپ بدون ارسال را انتخاب کنید.");
            }
            if (errors.Count > 0)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<byte[]>.Error(errors);
            }
            var today = Invoice.GetReceiptDate(timeProvider.GetUtcNow());
            var byId = invoices.ToDictionary(invoice => invoice.Id);
            var entries = request.InvoiceIds.Select(id => byId[id]).Select(invoice => new InvoicePrintEntry(
                invoice.InvoiceNumber, invoice.Counterparty!, request.SendToFinance ? today : invoice.FinanceDeliveryDate)).ToArray();
            // Generate successfully before changing any delivery dates or statuses.
            var pdf = await pdfService.GenerateAsync(entries, token);
            if (request.SendToFinance)
            {
                foreach (var id in request.InvoiceIds.Order())
                {
                    var invoice = await repository.GetByIdAsync(id, token)
                        ?? throw new InvalidOperationException("The selected invoice no longer exists.");
                    invoice.UpdateDetails(invoice.Description, invoice.InvoiceDate, InvoiceProcessingStatus.SentToFinance);
                    invoice.UpdateFinanceDeliveryDate(today);
                }
                await unitOfWork.SaveAsync(token);
            }
            await unitOfWork.CommitTransaction(token);
            return Result<byte[]>.Success(pdf);
        }
        catch
        {
            await unitOfWork.RollBackTransaction(token);
            throw;
        }
    }
}
