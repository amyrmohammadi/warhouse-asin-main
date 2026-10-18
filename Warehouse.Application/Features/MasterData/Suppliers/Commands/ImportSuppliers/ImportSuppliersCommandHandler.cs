using ClosedXML.Excel;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.MasterData.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.ImportSuppliers;

internal sealed class ImportSuppliersCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<ImportSuppliersCommand, Result<SupplierImportSummary>>
{
    private const int SupplierNameColumnNumber = 2;

    public async Task<Result<SupplierImportSummary>> Handle(
        ImportSuppliersCommand request,
        CancellationToken cancellationToken)
    {
        var sourceRows = new List<(int RowNumber, string Title)>();
        var emptyRowCount = 0;

        try
        {
            using var stream = new MemoryStream(request.FileContent, writable: false);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();

            if (worksheet is null)
            {
                return Result<SupplierImportSummary>.Error(
                    "فایل Excel هیچ شیتی ندارد.");
            }

            var lastUsedRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

            for (var rowNumber = 1; rowNumber <= lastUsedRow; rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawTitle = worksheet
                    .Cell(rowNumber, SupplierNameColumnNumber)
                    .GetFormattedString();
                var title = NormalizeWhitespace(rawTitle);

                if (string.IsNullOrWhiteSpace(title))
                {
                    emptyRowCount++;
                    continue;
                }

                sourceRows.Add((rowNumber, title));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<SupplierImportSummary>.Error(
                "فایل Excel معتبر نیست یا امکان خواندن آن وجود ندارد.");
        }

        if (sourceRows.Count > 0 && IsHeader(sourceRows[0].Title))
        {
            sourceRows.RemoveAt(0);
        }

        if (sourceRows.Count == 0)
        {
            return Result<SupplierImportSummary>.Error(
                "در ستون B هیچ نام تأمین‌کننده‌ای پیدا نشد.");
        }

        var repository = unitOfWork.Repository<Supplier>();
        var existingSuppliers = await repository.GetAllAsync(cancellationToken);
        var knownTitles = new HashSet<string>(
            existingSuppliers.Select(item => CreateComparisonKey(item.Title)),
            StringComparer.OrdinalIgnoreCase);
        var invalidRows = new List<SupplierImportRowError>();
        var importedCount = 0;
        var duplicateCount = 0;

        foreach (var sourceRow in sourceRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sourceRow.Title.Length > MasterDataValidationRules.TitleMaxLength)
            {
                invalidRows.Add(new SupplierImportRowError(
                    sourceRow.RowNumber,
                    $"نام تأمین‌کننده نباید بیشتر از {MasterDataValidationRules.TitleMaxLength} کاراکتر باشد."));
                continue;
            }

            if (!knownTitles.Add(CreateComparisonKey(sourceRow.Title)))
            {
                duplicateCount++;
                continue;
            }

            await repository.AddAsync(
                new Supplier(sourceRow.Title, null),
                cancellationToken);
            importedCount++;
        }

        if (importedCount > 0)
        {
            await unitOfWork.SaveAsync(cancellationToken);
        }

        return Result<SupplierImportSummary>.Success(new SupplierImportSummary
        {
            ProcessedRowCount = sourceRows.Count,
            ImportedCount = importedCount,
            DuplicateCount = duplicateCount,
            EmptyRowCount = emptyRowCount,
            InvalidRows = invalidRows
        });
    }

    private static bool IsHeader(string value)
    {
        var key = CreateComparisonKey(value);
        return key is "نام تامین کننده" or "تامین کننده" or "عنوان" or "supplier" or "supplier name";
    }

    private static string CreateComparisonKey(string value) =>
        NormalizeWhitespace(value.Replace('\u200C', ' '))
            .Replace('ي', 'ی')
            .Replace('ك', 'ک')
            .Replace('أ', 'ا')
            .Replace('إ', 'ا')
            .ToLowerInvariant();

    private static string NormalizeWhitespace(string value) =>
        string.Join(
            ' ',
            value
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
