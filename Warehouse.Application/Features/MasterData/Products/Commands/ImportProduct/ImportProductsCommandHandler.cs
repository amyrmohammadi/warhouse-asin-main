using ClosedXML.Excel;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.MasterData.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Products.Commands.ImportProducts;

internal sealed class ImportProductsCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<ImportProductsCommand, Result<ProductImportSummary>>
{
    private const int ProductNameColumnNumber = 2;
    private const int ProductCodeColumnNumber = 3;
    private const int ProductUnitOfMeasureColumnNumber = 4;

    private List<UnitOfMeasure> _unitOfMeasures = [];
    public async Task<Result<ProductImportSummary>> Handle(
        ImportProductsCommand request,
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
                return Result<ProductImportSummary>.Error(
                    "فایل Excel هیچ شیتی ندارد.");
            }

            var lastUsedRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

            for (var rowNumber = 1; rowNumber <= lastUsedRow; rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawTitle = worksheet
                    .Cell(rowNumber, ProductNameColumnNumber)
                    .GetFormattedString();

                var rawCode = worksheet
                    .Cell(rowNumber, ProductCodeColumnNumber)
                    .GetFormattedString();
                var rawUnit = worksheet
                    .Cell(rowNumber, ProductCodeColumnNumber)
                    .GetFormattedString();

                var code = NormalizeWhitespace(rawCode);
                var title = NormalizeWhitespace(rawTitle);
                var unit = GetUnitId(rawUnit);

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
            return Result<ProductImportSummary>.Error(
                "فایل Excel معتبر نیست یا امکان خواندن آن وجود ندارد.");
        }

        if (sourceRows.Count > 0 && IsHeader(sourceRows[0].Title))
        {
            sourceRows.RemoveAt(0);
        }

        if (sourceRows.Count == 0)
        {
            return Result<ProductImportSummary>.Error(
                "در ستون B هیچ نام ‌lمحصولی‌ پیدا نشد.");
        }

        var repository = unitOfWork.Repository<Product>();
        var existingProducts = await repository.GetAllAsync(cancellationToken);
        var knownTitles = new HashSet<string>(
            existingProducts.Select(item => CreateComparisonKey(item.Title)),
            StringComparer.OrdinalIgnoreCase);
        var invalidRows = new List<ProductRowError>();
        var importedCount = 0;
        var duplicateCount = 0;

        foreach (var sourceRow in sourceRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sourceRow.Title.Length > MasterDataValidationRules.TitleMaxLength)
            {
                invalidRows.Add(new ProductRowError(
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
                new Product(sourceRow.Title, null),
                cancellationToken);
            importedCount++;
        }

        if (importedCount > 0)
        {
            await unitOfWork.SaveAsync(cancellationToken);
        }

        return Result<ProductImportSummary>.Success(new ProductImportSummary
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
        return key is "نام تامین کننده" or "تامین کننده" or "عنوان" or "Product" or "Product name";
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


    private async Task LoadUnitOfMeasures()
    {
        var units = await unitOfWork.Repository<UnitOfMeasure>()
            .GetAllAsync();
        _unitOfMeasures=units.ToList();
    }

    private  Guid? GetUnitId(string name)
    {
        var f =  _unitOfMeasures.FirstOrDefault(p=>p.Title.Trim().ToLower()==name.Trim().ToLower());
        if(f==null)
            return null;
        return f.Id;
    }
}
