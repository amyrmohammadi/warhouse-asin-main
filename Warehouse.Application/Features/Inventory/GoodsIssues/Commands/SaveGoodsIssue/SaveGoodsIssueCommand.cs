using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Commands.SaveGoodsIssue;

public sealed record SaveGoodsIssueItem(
    Guid? ProductId,
    string ProductCode,
    decimal Quantity,
    string? AssetNumber,
    Guid? DestinationId,
    string? DestinationTitle,
    string ExitReason,
    string? Description,
    string DeliveringUnit,
    DateTimeOffset? ReturnAtUtc);

public sealed record SaveGoodsIssueCommand(
    Guid? Id,
    string IssueNumber,
    DateTimeOffset IssuedAtUtc,
    Guid? IssuerId,
    string? IssuerFullName,
    IReadOnlyCollection<SaveGoodsIssueItem> Items) : IRequest<Result<GoodsIssueSummary>>;

internal sealed class SaveGoodsIssueCommandValidator : AbstractValidator<SaveGoodsIssueCommand>
{
    public SaveGoodsIssueCommandValidator()
    {
        RuleFor(command => command.IssueNumber)
            .NotEmpty().WithMessage("وارد کردن کد یا شماره خروج الزامی است.")
            .MaximumLength(100).WithMessage("کد یا شماره خروج نباید بیشتر از ۱۰۰ نویسه باشد.");
        RuleFor(command => command.IssuedAtUtc)
            .NotEmpty().WithMessage("وارد کردن تاریخ خروج الزامی است.");
        RuleFor(command => command)
            .Must(command => command.IssuerId.HasValue || !string.IsNullOrWhiteSpace(command.IssuerFullName))
            .WithMessage("انتخاب یا وارد کردن خارج‌کننده الزامی است.");
        RuleFor(command => command.Items)
            .NotEmpty().WithMessage("حداقل یک ردیف کالا باید ثبت شود.")
            .Must(items => items.Count <= 100).WithMessage("در هر سند حداکثر ۱۰۰ ردیف قابل ثبت است.");
        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(value => value.ProductCode)
                .NotEmpty().WithMessage("وارد کردن یا انتخاب کد کالا الزامی است.")
                .MaximumLength(100).WithMessage("کد کالا نباید بیشتر از ۱۰۰ نویسه باشد.");
            item.RuleFor(value => value.Quantity)
                .GreaterThan(0).WithMessage("تعداد کالا باید بزرگ‌تر از صفر باشد.");
            item.RuleFor(value => value.AssetNumber).MaximumLength(100);
            item.RuleFor(value => value)
                .Must(value => value.DestinationId.HasValue || !string.IsNullOrWhiteSpace(value.DestinationTitle))
                .WithMessage("انتخاب یا وارد کردن مقصد الزامی است.");
            item.RuleFor(value => value.DestinationTitle).MaximumLength(200);
            item.RuleFor(value => value.ExitReason)
                .NotEmpty().WithMessage("وارد کردن علت خروج الزامی است.")
                .MaximumLength(1000);
            item.RuleFor(value => value.Description).MaximumLength(1000);
            item.RuleFor(value => value.DeliveringUnit)
                .NotEmpty().WithMessage("وارد کردن واحد تحویل‌دهنده الزامی است.")
                .MaximumLength(200);
        });
        RuleForEach(command => command.Items)
            .Must((command, item) => !item.ReturnAtUtc.HasValue || item.ReturnAtUtc >= command.IssuedAtUtc)
            .WithMessage("تاریخ عودت نمی‌تواند قبل از تاریخ خروج باشد.");
    }
}

internal sealed class SaveGoodsIssueCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<SaveGoodsIssueCommand, Result<GoodsIssueSummary>>
{
    public async Task<Result<GoodsIssueSummary>> Handle(
        SaveGoodsIssueCommand request,
        CancellationToken cancellationToken)
    {
        var issueRepository = unitOfWork.Repository<GoodsIssue>();
        var duplicate = await issueRepository.CountAsync(
            issueRepository.Entities.Where(issue =>
                issue.IssueNumber == request.IssueNumber.Trim() && issue.Id != request.Id),
            cancellationToken);
        if (duplicate > 0)
            return Result<GoodsIssueSummary>.Error("این کد یا شماره خروج قبلاً ثبت شده است.");

        var issuerResult = await ResolveIssuerAsync(request, cancellationToken);
        if (issuerResult.Issuer is null)
            return Result<GoodsIssueSummary>.Error(issuerResult.Error!);

        var preparedResult = await PrepareItemsAsync(request.Items, cancellationToken);
        if (!preparedResult.IsSuccess || preparedResult.Data is null)
            return Result<GoodsIssueSummary>.Error(preparedResult.Messages);

        await unitOfWork.BeginTransaction(cancellationToken);
        try
        {
            GoodsIssue issue;
            if (request.Id.HasValue)
            {
                issue = await issueRepository.GetByIdAsync(request.Id.Value, cancellationToken)
                    ?? throw new InvalidOperationException("سند خروج کالا پیدا نشد.");
                issue.Update(request.IssueNumber, request.IssuedAtUtc, issuerResult.Issuer.Id);
                var itemRepository = unitOfWork.Repository<GoodsIssueItem>();
                var oldItems = await itemRepository.ToListAsync(
                    itemRepository.Entities.Where(item => item.GoodsIssueId == issue.Id),
                    cancellationToken);
                foreach (var oldItem in oldItems)
                    await itemRepository.DeleteAsync(oldItem, cancellationToken);

                for (var index = 0; index < preparedResult.Data.Count; index++)
                {
                    var item = preparedResult.Data[index];
                    var entity = issue.AddItem(item.ProductId, item.ProductCode, item.Quantity, item.AssetNumber,
                        item.DestinationId, item.ExitReason, item.Description, item.DeliveringUnit,
                        item.ReturnAtUtc, index);
                    await itemRepository.AddAsync(entity, cancellationToken);
                }
                await issueRepository.UpdateAsync(issue, cancellationToken);
            }
            else
            {
                issue = new GoodsIssue(request.IssueNumber, request.IssuedAtUtc, issuerResult.Issuer.Id);
                for (var index = 0; index < preparedResult.Data.Count; index++)
                {
                    var item = preparedResult.Data[index];
                    issue.AddItem(item.ProductId, item.ProductCode, item.Quantity, item.AssetNumber,
                        item.DestinationId, item.ExitReason, item.Description, item.DeliveringUnit,
                        item.ReturnAtUtc, index);
                }
                await issueRepository.AddAsync(issue, cancellationToken);
            }

            await unitOfWork.SaveAsync(cancellationToken);
            await unitOfWork.CommitTransaction(cancellationToken);
            var summary = (await issueRepository.ToListAsync(
                issueRepository.Entities.Where(value => value.Id == issue.Id).ToSummary(),
                cancellationToken))[0];
            return Result<GoodsIssueSummary>.Success(summary);
        }
        catch (InvalidOperationException exception)
        {
            await unitOfWork.RollBackTransaction(cancellationToken);
            return Result<GoodsIssueSummary>.Error(exception.Message);
        }
        catch
        {
            await unitOfWork.RollBackTransaction(cancellationToken);
            throw;
        }
    }

    private async Task<(Person? Issuer, string? Error)> ResolveIssuerAsync(
        SaveGoodsIssueCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Person>();
        if (request.IssuerId.HasValue)
        {
            var issuer = await repository.GetByIdAsync(request.IssuerId.Value, cancellationToken);
            return issuer is null ? (null, "خارج‌کننده انتخاب‌شده پیدا نشد.") : (issuer, null);
        }

        var name = request.IssuerFullName!.Trim();
        var normalized = TextComparison.CreateCompactKey(name);
        var existing = (await repository.ToListAsync(
            repository.Entities.Where(person => person.NormalizedFullName == normalized).Take(1),
            cancellationToken)).FirstOrDefault();
        if (existing is not null) return (existing, null);

        var created = new Person(name, string.Empty);
        await repository.AddAsync(created, cancellationToken);
        return (created, null);
    }

    private async Task<Result<IReadOnlyList<PreparedItem>>> PrepareItemsAsync(
        IReadOnlyCollection<SaveGoodsIssueItem> items,
        CancellationToken cancellationToken)
    {
        var productRepository = unitOfWork.Repository<Product>();
        var productIds = items.Where(item => item.ProductId.HasValue)
            .Select(item => item.ProductId!.Value).Distinct().ToArray();
        var products = await productRepository.ToListAsync(
            productRepository.Entities.Where(product => productIds.Contains(product.Id)),
            cancellationToken);
        if (products.Count != productIds.Length)
            return Result<IReadOnlyList<PreparedItem>>.Error("یک یا چند کالای انتخاب‌شده پیدا نشد.");
        var productsById = products.ToDictionary(product => product.Id);

        var destinationRepository = unitOfWork.Repository<GoodsIssueDestination>();
        var destinationIds = items.Where(item => item.DestinationId.HasValue)
            .Select(item => item.DestinationId!.Value).Distinct().ToArray();
        var destinations = await destinationRepository.ToListAsync(
            destinationRepository.Entities.Where(destination => destinationIds.Contains(destination.Id)),
            cancellationToken);
        if (destinations.Count != destinationIds.Length)
            return Result<IReadOnlyList<PreparedItem>>.Error("یک یا چند مقصد انتخاب‌شده پیدا نشد.");
        var destinationsById = destinations.ToDictionary(destination => destination.Id);
        var allDestinations = await destinationRepository.GetAllAsync(cancellationToken);
        var destinationsByTitle = allDestinations
            .GroupBy(destination => destination.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var result = new List<PreparedItem>(items.Count);
        foreach (var item in items)
        {
            var productCode = item.ProductId.HasValue
                ? productsById[item.ProductId.Value].Code
                : item.ProductCode.Trim();
            GoodsIssueDestination destination;
            if (item.DestinationId.HasValue)
            {
                destination = destinationsById[item.DestinationId.Value];
            }
            else
            {
                var title = item.DestinationTitle!.Trim();
                if (!destinationsByTitle.TryGetValue(title, out destination!))
                {
                    destination = new GoodsIssueDestination(title, null);
                    await destinationRepository.AddAsync(destination, cancellationToken);
                    destinationsByTitle[title] = destination;
                }
            }
            result.Add(new PreparedItem(item.ProductId, productCode, item.Quantity, item.AssetNumber,
                destination.Id, item.ExitReason, item.Description, item.DeliveringUnit,
                item.ReturnAtUtc));
        }
        return Result<IReadOnlyList<PreparedItem>>.Success(result);
    }

    private sealed record PreparedItem(
        Guid? ProductId,
        string ProductCode,
        decimal Quantity,
        string? AssetNumber,
        Guid DestinationId,
        string ExitReason,
        string? Description,
        string DeliveringUnit,
        DateTimeOffset? ReturnAtUtc);
}
