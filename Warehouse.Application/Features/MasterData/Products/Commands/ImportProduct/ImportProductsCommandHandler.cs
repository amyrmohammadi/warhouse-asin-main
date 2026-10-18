using ClosedXML.Excel;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.MasterData.Products.Commands.CreateProduct;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Products.Commands.ImportProducts;

internal sealed class ImportProductsCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<ImportProductsCommand, Result<ProductImportSummary>>
{
    public async Task<Result<ProductImportSummary>> Handle(
        ImportProductsCommand request, CancellationToken cancellationToken)
    {
        var rows = new List<(int Number, CreateProductCommand Product)>();
        var emptyCount = 0;
        try
        {
            using var stream = new MemoryStream(request.FileContent, writable: false);
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet is null)
                return Result<ProductImportSummary>.Error("فایل Excel هیچ شیتی ندارد.");

            var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;
            var firstContentRow = true;
            for (var number = 1; number <= lastRow; number++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var title = sheet.Cell(number, 2).GetFormattedString().Trim();
                if (title.Length == 0)
                {
                    emptyCount++;
                    break;
                }
                var code = sheet.Cell(number, 3).GetFormattedString().Trim();
                var unit = sheet.Cell(number, 4).GetFormattedString().Trim();
                if (firstContentRow && IsHeader(title, code))
                {
                    firstContentRow = false;
                    continue;
                }
                firstContentRow = false;
                rows.Add((number, new CreateProductCommand(title, code, unit, null)));
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return Result<ProductImportSummary>.Error("فایل Excel معتبر نیست یا امکان خواندن آن وجود ندارد.");
        }

        if (rows.Count == 0)
            return Result<ProductImportSummary>.Error("هیچ ردیف کالایی در ستون‌های B، C و D پیدا نشد.");

        var repository = unitOfWork.Repository<Product>();
        var existing = await repository.GetAllAsync(cancellationToken);
        var titles = existing.GroupBy(p => Product.CreateComparisonKey(p.Title))
            .ToDictionary(group => group.Key,
                group => $"کالای موجود در دیتابیس با عنوان «{group.First().Title}» و کد «{group.First().Code}»");
        var codes = existing.Select(p => Product.CreateComparisonKey(p.Code)).ToHashSet();
        var validator = new CreateProductCommandValidator();
        var invalid = new List<ProductRowError>();
        var accepted = new List<CreateProductCommand>();
        var duplicates = 0;
        foreach (var row in rows)
        {
            var validation = await validator.ValidateAsync(row.Product, cancellationToken);
            if (!validation.IsValid)
            {
                invalid.Add(new ProductRowError(row.Number,
                    string.Join(" ", validation.Errors.Select(e => e.ErrorMessage).Distinct())));
                continue;
            }
            var titleKey = Product.CreateComparisonKey(row.Product.Title);
            var codeKey = Product.CreateComparisonKey(row.Product.Code);
            var matchingTitle = !request.CheckOnlyProductCode ? titles.GetValueOrDefault(titleKey) : null;
            if (matchingTitle is not null || (!Product.IsPlaceholderCode(row.Product.Code) && codes.Contains(codeKey)))
            {
                duplicates++;
                invalid.Add(new ProductRowError(row.Number,
                    matchingTitle is not null
                        ? $"عنوان «{row.Product.Title}» با {matchingTitle} یکسان تشخیص داده شد؛ در مقایسه فاصله، نیم‌فاصله و خط‌تیره نادیده گرفته می‌شوند."
                        : "کد کالا تکراری است."));
                continue;
            }
            titles.TryAdd(titleKey, $"ردیف {row.Number} همین فایل با عنوان «{row.Product.Title}»");
            codes.Add(codeKey);
            accepted.Add(row.Product);
        }

        if (invalid.Count > 0)
        {
            return Result<ProductImportSummary>.Success(new ProductImportSummary
            {
                ProcessedRowCount = rows.Count,
                ImportedCount = 0,
                DuplicateCount = duplicates,
                EmptyRowCount = emptyCount,
                InvalidRows = invalid
            });
        }

        if (accepted.Count > 0)
        {
            var unitsRepository = unitOfWork.Repository<UnitOfMeasure>();
            var units = (await unitsRepository.GetAllAsync(cancellationToken)).ToList();
            await unitOfWork.BeginTransaction(cancellationToken);
            try
            {
                foreach (var row in accepted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var unit = units.FirstOrDefault(u =>
                        string.Equals(u.Title.Trim(), row.UnitOfMeasureTitle, StringComparison.OrdinalIgnoreCase));
                    if (unit is null)
                    {
                        unit = new UnitOfMeasure(row.UnitOfMeasureTitle, null);
                        await unitsRepository.AddAsync(unit, cancellationToken);
                        units.Add(unit);
                    }
                    await repository.AddAsync(new Product(row.Title, row.Code, unit.Id, null), cancellationToken);
                }
                await unitOfWork.SaveAsync(cancellationToken);
                await unitOfWork.CommitTransaction(cancellationToken);
            }
            catch
            {
                await unitOfWork.RollBackTransaction(CancellationToken.None);
                throw;
            }
        }
        return Result<ProductImportSummary>.Success(new ProductImportSummary
        {
            ProcessedRowCount = rows.Count,
            ImportedCount = accepted.Count,
            DuplicateCount = duplicates,
            EmptyRowCount = emptyCount,
            InvalidRows = invalid
        });
    }

    private static bool IsHeader(string title, string code)
    {
        var titleKey = Product.CreateComparisonKey(title);
        var codeKey = Product.CreateComparisonKey(code);
        return new[] { "نام کالا", "عنوان کالا", "نام محصول", "عنوان محصول", "عنوان", "نام", "Product", "Product name", "Title" }
            .Any(value => Product.CreateComparisonKey(value) == titleKey)
            && new[] { "کد", "کد کالا", "کد محصول", "Code", "Product code" }
            .Any(value => Product.CreateComparisonKey(value) == codeKey);
    }
}
