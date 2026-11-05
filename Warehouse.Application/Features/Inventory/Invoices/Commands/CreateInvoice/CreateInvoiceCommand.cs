using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.Invoices.Commands.CreateInvoice;

public sealed record CreateInvoiceCommand(string InvoiceNumber, Guid? SupplierId, string? Description,
    DateOnly? InvoiceDate, InvoiceProcessingStatus ProcessingStatus = InvoiceProcessingStatus.UnderReview,
    decimal? Price = null, DateOnly? FinanceDeliveryDate = null) : IRequest<Result<InvoiceDetails>>;

internal sealed class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(command => command.InvoiceNumber).NotEmpty().WithMessage("شماره فاکتور الزامی است.")
            .MaximumLength(100).WithMessage("شماره فاکتور نباید بیشتر از ۱۰۰ نویسه باشد.");
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.SupplierId).NotEqual(Guid.Empty).When(command => command.SupplierId.HasValue);
        RuleFor(command => command.ProcessingStatus).IsInEnum();
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
    }
}

internal sealed class CreateInvoiceCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<CreateInvoiceCommand, Result<InvoiceDetails>>
{
    public async Task<Result<InvoiceDetails>> Handle(CreateInvoiceCommand request, CancellationToken token)
    {
        await unitOfWork.BeginSerializableTransaction(token);
        try
        {
            var repository = unitOfWork.Repository<Invoice>();
            var number = Invoice.NormalizeNumber(request.InvoiceNumber);
            if (await repository.CountAsync(repository.Entities.Where(invoice =>
                invoice.InvoiceNumber == number && invoice.SupplierId == request.SupplierId), token) > 0)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<InvoiceDetails>.Error("فاکتوری با این شماره و طرف حساب قبلاً ثبت شده است؛ همان فاکتور را ویرایش کنید.");
            }
            if (request.SupplierId.HasValue &&
                await unitOfWork.Repository<Supplier>().GetByIdAsync(request.SupplierId.Value, token) is null)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<InvoiceDetails>.Error("طرف حساب انتخاب‌شده پیدا نشد.");
            }
            var invoice = new Invoice(number, request.SupplierId);
            invoice.UpdateDetails(request.Description, request.InvoiceDate, request.ProcessingStatus);
            invoice.UpdatePrice(request.Price);
            invoice.UpdateFinanceDeliveryDate(request.FinanceDeliveryDate);
            await repository.AddAsync(invoice, token);
            await unitOfWork.SaveAsync(token);
            var details = (await repository.ToListAsync(repository.Entities
                .Where(value => value.Id == invoice.Id).ToDetails(), token)).Single();
            await unitOfWork.CommitTransaction(token);
            return Result<InvoiceDetails>.Success(details);
        }
        catch
        {
            await unitOfWork.RollBackTransaction(token);
            throw;
        }
    }
}
