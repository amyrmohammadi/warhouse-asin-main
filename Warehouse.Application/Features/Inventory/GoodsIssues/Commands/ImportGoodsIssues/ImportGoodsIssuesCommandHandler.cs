using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Commands.ImportGoodsIssues;

internal sealed class ImportGoodsIssuesCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<ImportGoodsIssuesCommand, Result<GoodsIssueImportSummary>>
{
    public async Task<Result<GoodsIssueImportSummary>> Handle(ImportGoodsIssuesCommand request, CancellationToken token)
    {
        List<GoodsIssueExcelReader.Row> rows;
        try { rows = GoodsIssueExcelReader.Read(request.FileContent, token); }
        catch (OperationCanceledException) { throw; }
        catch (FormatException exception) { return Result<GoodsIssueImportSummary>.Error(exception.Message); }
        catch (Exception) { return Result<GoodsIssueImportSummary>.Error("فایل Excel معتبر نیست یا امکان خواندن آن وجود ندارد."); }
        if (rows.Count == 0) return Result<GoodsIssueImportSummary>.Error("هیچ ردیف خروج کالا در فایل پیدا نشد.");

        var products = await unitOfWork.Repository<Product>().GetAllAsync(token);
        var units = await unitOfWork.Repository<UnitOfMeasure>().GetAllAsync(token);
        var people = await unitOfWork.Repository<Person>().GetAllAsync(token);
        var destinations = await unitOfWork.Repository<GoodsIssueDestination>().GetAllAsync(token);
        var existingIssues = await unitOfWork.Repository<GoodsIssue>().GetAllAsync(token);
        var productLookup = products.GroupBy(product => Key(product.Title))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var unitLookup = units.GroupBy(unit => Key(unit.Title))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var destinationLookup = destinations.GroupBy(destination => Key(destination.Title))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var existingNumbers = existingIssues.Select(issue => issue.IssueNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groups = rows.Where(row => row.IssueNumber.Length > 0)
            .GroupBy(row => row.IssueNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var errors = new List<GoodsIssueImportRowError>();
        var validRows = new List<PreparedRow>();

        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested();
            var messages = new List<string>();
            if (row.IssueNumber.Length is 0 or > 100) messages.Add("شماره خروج الزامی است و باید حداکثر ۱۰۰ نویسه باشد.");
            if (row.ProductTitle.Length == 0) messages.Add("عنوان کالا خالی است.");
            if (row.Quantity is null or <= 0 || row.Quantity >= 1000000000000000m ||
                (row.Quantity.HasValue && decimal.Round(row.Quantity.Value, 3) != row.Quantity.Value))
                messages.Add("تعداد باید عدد مثبت با حداکثر ۳ رقم اعشار باشد.");
            if (row.ExitReason.Length > 1000) messages.Add("علت خروج نباید بیشتر از ۱۰۰۰ نویسه باشد.");
            if (row.DeliveringUnit.Length > 200) messages.Add("واحد تحویل‌دهنده نباید بیشتر از ۲۰۰ نویسه باشد.");
            if (row.Description.Length > 1000) messages.Add("توضیحات نباید بیشتر از ۱۰۰۰ نویسه باشد.");
            var group = groups.GetValueOrDefault(row.IssueNumber) ?? [];
            var dates = group.Where(item => item.Date.HasValue).Select(item => item.Date!.Value).Distinct().ToArray();
            if (dates.Length != 1) messages.Add("تاریخ خروج این سند خالی یا بین ردیف‌ها متفاوت است.");
            var issuers = group.Select(item => item.Issuer.Trim()).Where(value => value.Length > 0)
                .GroupBy(Key).Select(values => values.First()).ToArray();
            if (issuers.Length != 1 || issuers[0].Length > 200)
                messages.Add("خارج‌کنندهٔ این شماره خروج خالی یا بین ردیف‌ها متفاوت است.");
            if (existingNumbers.Contains(row.IssueNumber)) messages.Add("این شماره خروج قبلاً ثبت شده است.");

            unitLookup.TryGetValue(Key(row.Unit), out var matchingUnits);
            if (matchingUnits?.Length != 1) messages.Add($"واحد «{row.Unit}» باید از قبل ثبت شده و یکتا باشد.");
            productLookup.TryGetValue(Key(row.ProductTitle), out var matchingProducts);
            var candidates = matchingUnits?.Length == 1
                ? (matchingProducts ?? []).Where(product => product.UnitOfMeasureId == matchingUnits[0].Id).ToArray()
                : [];
            if (candidates.Length != 1)
                messages.Add($"کالای «{row.ProductTitle}» با واحد «{row.Unit}» باید از قبل ثبت شده و یکتا باشد.");
            destinationLookup.TryGetValue(Key(row.Destination), out var matchingDestinations);
            if (row.Destination.Length > 0 && matchingDestinations?.Length != 1)
                messages.Add($"مقصد «{row.Destination}» باید از قبل ثبت شده و یکتا باشد.");

            if (messages.Count > 0)
                errors.Add(new GoodsIssueImportRowError(row.Sheet, row.Number, string.Join(" ", messages)));
            else
                validRows.Add(new PreparedRow(row, candidates[0].Id, candidates[0].Code,
                    row.Destination.Length == 0 ? null : matchingDestinations![0].Id,
                    issuers[0], Key(issuers[0]), dates[0]));
        }

        if (errors.Count > 0)
            return Result<GoodsIssueImportSummary>.Success(new GoodsIssueImportSummary
            {
                ProcessedRowCount = rows.Count,
                InvalidRows = errors
            });

        var peopleByName = people.GroupBy(person => Key(person.FullName))
            .ToDictionary(group => group.Key, group => group.First());
        var createdIssuers = 0;
        var importedIssues = 0;
        await unitOfWork.BeginTransaction(token);
        try
        {
            foreach (var group in validRows.GroupBy(row => row.Source.IssueNumber, StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                var first = group.First();
                if (!peopleByName.TryGetValue(first.IssuerKey, out var issuer))
                {
                    issuer = new Person(first.IssuerName, string.Empty);
                    await unitOfWork.Repository<Person>().AddAsync(issuer, token);
                    peopleByName.Add(first.IssuerKey, issuer);
                    createdIssuers++;
                }
                var issue = new GoodsIssue(first.Source.IssueNumber, first.IssuedAtUtc, null, issuer.Id);
                var sortOrder = 0;
                foreach (var row in group)
                    issue.AddItem(row.ProductId, row.ProductCode, row.Source.Quantity!.Value, null,
                        row.DestinationId, row.Source.ExitReason, row.Source.Description,
                        row.Source.DeliveringUnit, row.Source.ReturnAtUtc, sortOrder++);
                await unitOfWork.Repository<GoodsIssue>().AddAsync(issue, token);
                importedIssues++;
            }
            await unitOfWork.SaveAsync(token);
            await unitOfWork.CommitTransaction(token);
        }
        catch
        {
            await unitOfWork.RollBackTransaction(CancellationToken.None);
            throw;
        }
        return Result<GoodsIssueImportSummary>.Success(new GoodsIssueImportSummary
        {
            ProcessedRowCount = rows.Count,
            ImportedIssueCount = importedIssues,
            ImportedItemCount = validRows.Count,
            CreatedIssuerCount = createdIssuers
        });
    }

    private static string Key(string value) =>
        TextComparison.CreateCompactKey(value.Replace('ي', 'ی').Replace('ك', 'ک'));

    private sealed record PreparedRow(GoodsIssueExcelReader.Row Source, Guid ProductId, string ProductCode,
        Guid? DestinationId, string IssuerName, string IssuerKey, DateTimeOffset IssuedAtUtc);
}
