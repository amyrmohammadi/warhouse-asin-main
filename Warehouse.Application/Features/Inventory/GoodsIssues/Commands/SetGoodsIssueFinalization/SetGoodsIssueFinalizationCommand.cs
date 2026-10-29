using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Commands.SetGoodsIssueFinalization;

public sealed record SetGoodsIssueFinalizationCommand(
    bool IsFinalized, Guid? Id = null, DateOnly? Day = null, Guid? ConcurrencyStamp = null)
    : IRequest<Result<int>>;

internal sealed class SetGoodsIssueFinalizationCommandValidator : AbstractValidator<SetGoodsIssueFinalizationCommand>
{
    public SetGoodsIssueFinalizationCommandValidator()
    {
        RuleFor(request => request).Must(request => request.Id.HasValue != request.Day.HasValue)
            .WithMessage("یک سند یا یک روز را انتخاب کنید.");
        When(request => request.Id.HasValue, () =>
        {
            RuleFor(request => request.Id).NotEmpty();
            RuleFor(request => request.ConcurrencyStamp).NotEmpty();
        });
    }
}

internal sealed class SetGoodsIssueFinalizationCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<SetGoodsIssueFinalizationCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SetGoodsIssueFinalizationCommand request, CancellationToken token)
    {
        await unitOfWork.BeginSerializableTransaction(token);
        try
        {
            var repository = unitOfWork.Repository<GoodsIssue>();
            var query = repository.Entities;
            if (request.Id.HasValue) query = query.Where(issue => issue.Id == request.Id.Value);
            else
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
                var localDay = request.Day!.Value.ToDateTime(TimeOnly.MinValue);
                var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDay, zone), TimeSpan.Zero);
                var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDay.AddDays(1), zone), TimeSpan.Zero);
                query = query.Where(issue => issue.IssuedAtUtc >= start && issue.IssuedAtUtc < end);
            }
            var snapshots = await repository.ToListAsync(query.OrderBy(issue => issue.Id), token);
            if (request.Id.HasValue && snapshots.Count == 0)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<int>.Error("سند خروج کالا پیدا نشد.");
            }
            var count = 0;
            foreach (var snapshot in snapshots)
            {
                var issue = (await repository.GetByIdAsync(snapshot.Id, token))!;
                var expectedStamp = request.Id.HasValue ? request.ConcurrencyStamp!.Value : snapshot.ConcurrencyStamp;
                if (issue.ConcurrencyStamp != expectedStamp)
                {
                    await unitOfWork.RollBackTransaction(token);
                    return Result<int>.Error(ConcurrencyMessages.Conflict);
                }
                if (issue.IsFinalized == request.IsFinalized) continue;
                issue.TryAdvanceConcurrencyStamp(expectedStamp);
                issue.SetFinalized(request.IsFinalized);
                count++;
            }
            await unitOfWork.SaveAsync(token);
            await unitOfWork.CommitTransaction(token);
            return Result<int>.Success(count);
        }
        catch
        {
            await unitOfWork.RollBackTransaction(token);
            throw;
        }
    }
}
