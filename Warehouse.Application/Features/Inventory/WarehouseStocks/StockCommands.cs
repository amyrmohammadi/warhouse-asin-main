using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.WarehouseStocks;

public sealed record RegisterOpeningBalancesCommand(Guid WarehouseId, IReadOnlyCollection<OpeningStockItem> Items) : IRequest<Result>;
public sealed record AdjustWarehouseStockCommand(Guid WarehouseId, Guid ProductId, decimal NewQuantity,
    decimal ExpectedQuantity, byte[]? RowVersion, string Reason) : IRequest<Result>;

internal sealed class RegisterOpeningBalancesValidator : AbstractValidator<RegisterOpeningBalancesCommand>
{
    public RegisterOpeningBalancesValidator()
    {
        RuleFor(command => command.WarehouseId).NotEmpty().WithMessage("انبار را انتخاب کنید.");
        RuleFor(command => command.Items).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("حداقل یک کالا انتخاب کنید.").Must(items => items.Count <= 100)
            .WithMessage("در هر درخواست حداکثر ۱۰۰ کالا ثبت کنید.")
            .Must(items => items.All(item => item is not null)).WithMessage("ردیف کالا معتبر نیست.")
            .Must(items => items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .WithMessage("هر کالا را فقط یک‌بار انتخاب کنید.");
        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(value => value.ProductId).NotEmpty().WithMessage("کالا را انتخاب کنید.");
            item.RuleFor(value => value.Quantity).InclusiveBetween(0, 999999999999999.999m)
                .WithMessage("مقدار باید غیرمنفی و کمتر از ۱۰۰۰ تریلیون باشد.").PrecisionScale(18, 3, true)
                .WithMessage("مقدار باید غیرمنفی و حداکثر دارای ۳ رقم اعشار باشد.");
        });
    }
}

internal sealed class AdjustWarehouseStockValidator : AbstractValidator<AdjustWarehouseStockCommand>
{
    public AdjustWarehouseStockValidator()
    {
        RuleFor(command => command.WarehouseId).NotEmpty().WithMessage("انبار را انتخاب کنید.");
        RuleFor(command => command.ProductId).NotEmpty().WithMessage("کالا را انتخاب کنید.");
        RuleFor(command => command.NewQuantity).InclusiveBetween(0, 999999999999999.999m)
            .WithMessage("موجودی جدید باید غیرمنفی و کمتر از ۱۰۰۰ تریلیون باشد.").PrecisionScale(18, 3, true)
            .WithMessage("موجودی جدید باید غیرمنفی و حداکثر دارای ۳ رقم اعشار باشد.");
        RuleFor(command => command.ExpectedQuantity).InclusiveBetween(0, 999999999999999.999m)
            .WithMessage("موجودی مورد انتظار معتبر نیست.").PrecisionScale(18, 3, true).WithMessage("موجودی مورد انتظار معتبر نیست.");
        RuleFor(command => command.Reason).NotEmpty().WithMessage("علت اصلاح الزامی است.")
            .MaximumLength(1000).WithMessage("علت اصلاح را تا ۱۰۰۰ نویسه وارد کنید.");
        RuleFor(command => command.RowVersion).Must(version => version is null || version.Length == 8)
            .WithMessage("نسخه موجودی معتبر نیست؛ اطلاعات را تازه‌سازی کنید.");
    }
}

internal sealed class StockCommandHandler(IUnitOfWork<Guid> unitOfWork, IInventoryMovementService inventory)
    : IRequestHandler<RegisterOpeningBalancesCommand, Result>, IRequestHandler<AdjustWarehouseStockCommand, Result>
{
    public Task<Result> Handle(RegisterOpeningBalancesCommand request, CancellationToken token) =>
        Execute(request.WarehouseId, () => inventory.RegisterOpeningBalancesAsync(request.WarehouseId, request.Items, token), token);

    public Task<Result> Handle(AdjustWarehouseStockCommand request, CancellationToken token) =>
        Execute(request.WarehouseId, () => inventory.AdjustStockAsync(request.WarehouseId, request.ProductId,
            request.NewQuantity, request.ExpectedQuantity, request.RowVersion, request.Reason, token), token);

    private async Task<Result> Execute(Guid warehouseId, Func<Task<Result>> action, CancellationToken token)
    {
        await unitOfWork.BeginSerializableTransaction(token);
        try
        {
            var warehouses = unitOfWork.Repository<StorageWarehouse>();
            var result = await warehouses.CountAsync(warehouses.Entities.Where(warehouse => warehouse.Id == warehouseId), token) != 1
                ? Result.Error("انبار انتخاب‌شده پیدا نشد.") : await action();
            if (!result.IsSuccess)
            {
                await unitOfWork.RollBackTransaction(CancellationToken.None);
                return result;
            }
            await unitOfWork.SaveAsync(token);
            await unitOfWork.CommitTransaction(token);
            return result;
        }
        catch
        {
            await unitOfWork.RollBackTransaction(CancellationToken.None);
            throw;
        }
    }
}
