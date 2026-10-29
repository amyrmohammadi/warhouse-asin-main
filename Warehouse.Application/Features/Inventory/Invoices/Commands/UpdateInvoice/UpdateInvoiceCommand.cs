using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoice;

public sealed record UpdateInvoiceReceiptItem(
    Guid Id,
    string? ReceiptNumber,
    Guid GoodsReceiptConcurrencyStamp,
    decimal? Price = null);

public sealed record UpdateInvoiceCommand(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid? SupplierId,
    string? Description,
    DateOnly? InvoiceDate,
    InvoiceProcessingStatus ProcessingStatus,
    IReadOnlyCollection<UpdateInvoiceReceiptItem> Items,
    bool CanEditInvoice = true,
    bool CanEditReceipt = true,
    decimal? Price = null,
    DateOnly? FinanceDeliveryDate = null) : IRequest<Result<InvoiceDetails>>;

internal sealed class UpdateInvoiceCommandValidator : AbstractValidator<UpdateInvoiceCommand>
{
    public UpdateInvoiceCommandValidator()
    {
        RuleFor(command => command.InvoiceId).NotEmpty();
        When(command => command.CanEditInvoice, () =>
        {
            RuleFor(command => command.InvoiceNumber).NotEmpty().WithMessage("شماره فاکتور الزامی است.")
                .MaximumLength(100).WithMessage("شماره فاکتور نباید بیشتر از ۱۰۰ نویسه باشد.");
            RuleFor(command => command.Description).MaximumLength(1000);
            RuleFor(command => command.ProcessingStatus).IsInEnum();
            RuleFor(command => command.Price).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
            RuleForEach(command => command.Items).ChildRules(item =>
                item.RuleFor(value => value.Price).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true));
        });
        When(command => command.CanEditReceipt || command.CanEditInvoice, () =>
        {
            RuleFor(command => command.Items).NotNull();
            RuleForEach(command => command.Items).ChildRules(item =>
            {
                item.RuleFor(value => value.Id).NotEmpty();
                item.RuleFor(value => value.ReceiptNumber).MaximumLength(100);
                item.RuleFor(value => value.GoodsReceiptConcurrencyStamp).NotEmpty();
            });
            RuleFor(command => command.Items)
                .Must(items => items is not null && items.Select(item => item.Id).Distinct().Count() == items.Count)
                .WithMessage("ردیف تکراری در فاکتور وجود دارد.");
        });
    }
}

internal sealed class UpdateInvoiceCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdateInvoiceCommand, Result<InvoiceDetails>>
{
    public async Task<Result<InvoiceDetails>> Handle(
        UpdateInvoiceCommand request, CancellationToken cancellationToken)
    {
        if (!request.CanEditInvoice && !request.CanEditReceipt)
            return Result<InvoiceDetails>.Error("مجوز ویرایش فاکتور یا رسید را ندارید.");

        await unitOfWork.BeginSerializableTransaction(cancellationToken);
        try
        {
            var invoiceRepository = unitOfWork.Repository<Invoice>();
            var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
            if (invoice is null)
            {
                await unitOfWork.RollBackTransaction(cancellationToken);
                return Result<InvoiceDetails>.Error("فاکتور پیدا نشد.");
            }

            string? normalizedNumber = null;
            if (request.CanEditInvoice)
            {
                normalizedNumber = Invoice.NormalizeNumber(request.InvoiceNumber);
                if (await invoiceRepository.CountAsync(invoiceRepository.Entities.Where(value =>
                        value.Id != request.InvoiceId &&
                        value.InvoiceNumber == normalizedNumber &&
                        value.SupplierId == request.SupplierId &&
                        value.ReceiptDate == invoice.ReceiptDate), cancellationToken) > 0)
                {
                    await unitOfWork.RollBackTransaction(cancellationToken);
                    return Result<InvoiceDetails>.Error(
                        "این شماره فاکتور برای طرف حساب و روز ورود انتخاب‌شده قبلاً ثبت شده است.");
                }
                if (request.SupplierId.HasValue &&
                    await unitOfWork.Repository<Supplier>().GetByIdAsync(request.SupplierId.Value, cancellationToken) is null)
                {
                    await unitOfWork.RollBackTransaction(cancellationToken);
                    return Result<InvoiceDetails>.Error("طرف حساب انتخاب‌شده پیدا نشد.");
                }
            }

            decimal? calculatedPrice = null;
            if (request.CanEditReceipt || request.CanEditInvoice)
            {
                var itemRepository = unitOfWork.Repository<GoodsReceiptItem>();
                var storedItems = await itemRepository.ToListAsync(itemRepository.Entities
                    .Where(item => item.InvoiceId == request.InvoiceId && item.InvoiceStatus == InvoiceStatus.HasInvoice),
                    cancellationToken);
                if (storedItems.Count != request.Items.Count ||
                    storedItems.Any(item => !request.Items.Any(value => value.Id == item.Id)))
                {
                    await unitOfWork.RollBackTransaction(cancellationToken);
                    return Result<InvoiceDetails>.Error("ردیف‌های فاکتور تغییر کرده‌اند؛ صفحه را دوباره باز کنید.");
                }

                var receiptRepository = unitOfWork.Repository<GoodsReceipt>();
                var receipts = new Dictionary<Guid, GoodsReceipt>();
                foreach (var item in storedItems)
                {
                    if (receipts.ContainsKey(item.GoodsReceiptId)) continue;
                    var receipt = await receiptRepository.GetByIdAsync(item.GoodsReceiptId, cancellationToken);
                    if (receipt is null)
                    {
                        await unitOfWork.RollBackTransaction(cancellationToken);
                        return Result<InvoiceDetails>.Error("سند ورود مرتبط با فاکتور پیدا نشد.");
                    }
                    var stamps = request.Items.Where(value =>
                            storedItems.Any(stored => stored.Id == value.Id && stored.GoodsReceiptId == receipt.Id))
                        .Select(value => value.GoodsReceiptConcurrencyStamp).Distinct().ToArray();
                    if (stamps.Length != 1 || !receipt.TryAdvanceConcurrencyStamp(stamps[0]))
                    {
                        await unitOfWork.RollBackTransaction(cancellationToken);
                        return Result<InvoiceDetails>.Error(ConcurrencyMessages.Conflict);
                    }
                    receipts.Add(receipt.Id, receipt);
                }

                foreach (var value in request.Items)
                {
                    var item = await itemRepository.GetByIdAsync(value.Id, cancellationToken);
                    if (request.CanEditReceipt) item!.UpdateReceiptNumber(value.ReceiptNumber);
                    if (request.CanEditInvoice) item!.UpdatePrice(value.Price);
                }
                if (request.CanEditInvoice && request.Items.Any(item => item.Price.HasValue))
                    calculatedPrice = request.Items.Sum(item => item.Price ?? 0);
            }

            if (request.CanEditInvoice)
            {
                invoice.UpdateHeader(normalizedNumber!, request.SupplierId);
                invoice.UpdateDetails(request.Description, request.InvoiceDate, request.ProcessingStatus);
                invoice.UpdateFinanceDeliveryDate(request.FinanceDeliveryDate);
                var price = request.Price ?? calculatedPrice;
                if (price >= 10000000000000000m)
                {
                    await unitOfWork.RollBackTransaction(cancellationToken);
                    return Result<InvoiceDetails>.Error("جمع قیمت فاکتور بیش از حد مجاز است.");
                }
                invoice.UpdatePrice(price);
            }
            await unitOfWork.SaveAsync(cancellationToken);
            await unitOfWork.CommitTransaction(cancellationToken);

            var details = (await invoiceRepository.ToListAsync(invoiceRepository.Entities
                .Where(item => item.Id == request.InvoiceId).ToDetails(), cancellationToken)).Single();
            return Result<InvoiceDetails>.Success(details);
        }
        catch
        {
            await unitOfWork.RollBackTransaction(cancellationToken);
            throw;
        }
    }
}
