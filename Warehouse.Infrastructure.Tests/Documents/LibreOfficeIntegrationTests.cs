using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Warehouse.Infrastructure.Documents;
using Warehouse.Application.Common.Models.Inventory;
using Xunit;

namespace Warehouse.Infrastructure.Tests.Documents;

// Opt in explicitly; ordinary unit tests never require LibreOffice or a desktop session.
public sealed class LibreOfficeIntegrationTests
{
    [LibreOfficeTheory]
    [Trait("Category", "LibreOfficeIntegration")]
    [InlineData(1)]
    [InlineData(26)]
    public async Task Generate_RealInvoiceFinanceDeliveryFromProvidedTemplate(int count)
    {
        var root = Path.Combine(Path.GetTempPath(), "WarehouseInvoicePdfIntegration", Guid.NewGuid().ToString("N"));
        using var converter = new LibreOfficeExcelToPdfService(Options.Create(new LibreOfficeOptions
        {
            ExecutablePath = Environment.GetEnvironmentVariable("EXCEL_TO_PDF_LIBREOFFICE_PATH")!, TempRootPath = root
        }), NullLoggerFactory.Instance);
        var entries = Enumerable.Range(1, count).Select(index => new Warehouse.Application.Common.Interfaces.InvoicePrintEntry(
            "INV-" + index.ToString("000"), index == 1 ? "شرکت تأمین تجهیزات صنعتی و خدمات بازرگانی ایران" : "طرف حساب " + index,
            new DateOnly(2026, 10, 31))).ToArray();
        var pdf = await new InvoicePdfService(converter).GenerateAsync(entries);
        Assert.True(pdf.AsSpan().StartsWith("%PDF-"u8));
        Assert.Equal((count + 24) / 25, Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page\b").Count);
        var preview = Environment.GetEnvironmentVariable("INVOICE_PRINT_PREVIEW_DIR");
        if (!string.IsNullOrWhiteSpace(preview))
        {
            Directory.CreateDirectory(preview);
            await File.WriteAllBytesAsync(Path.Combine(preview, $"invoices-{count}.pdf"), pdf);
        }
    }

    [LibreOfficeTheory]
    [Trait("Category", "LibreOfficeIntegration")]
    [InlineData(1)]
    [InlineData(12)]
    public async Task Generate_RealGoodsIssuePermitFromTheProvidedTemplate(int itemCount)
    {
        var root = Path.Combine(Path.GetTempPath(), "WarehousePdfIntegration", Guid.NewGuid().ToString("N"));
        try
        {
            using var converter = new LibreOfficeExcelToPdfService(Options.Create(new LibreOfficeOptions
            {
                ExecutablePath = Environment.GetEnvironmentVariable("EXCEL_TO_PDF_LIBREOFFICE_PATH")!, TempRootPath = root
            }), NullLoggerFactory.Instance);
            var pdf = await new GoodsIssuePdfService(converter).GenerateAsync(new GoodsIssueSummary
            {
                IssueNumber = "OUT-433", IssuerFullName = "علی رضایی", IssuedAtUtc = DateTimeOffset.UtcNow,
                Items = Enumerable.Range(1, itemCount).Select(index => new GoodsIssueItemSummary
                {
                    ProductTitle = $"کالای {index}", ProductCode = $"P-{index}", ProductUnitOfMeasureTitle = "عدد", Quantity = 2
                }).ToArray()
            });
            Assert.True(pdf.AsSpan().StartsWith("%PDF-"u8));
            Assert.Equal((itemCount + GoodsIssuePdfServiceTests.TemplateRowsPerPage - 1) / GoodsIssuePdfServiceTests.TemplateRowsPerPage, Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page\b").Count);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [LibreOfficeTheory]
    [Trait("Category", "LibreOfficeIntegration")]
    [InlineData("simple")]
    [InlineData("formatted")]
    [InlineData("landscape")]
    [InlineData("multiple-sheets")]
    [InlineData("multiple-pages")]
    [InlineData("print-area")]
    public async Task Convert_RealWorkbook_WithOriginalPrintSettings(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), "WarehousePdfIntegration", Guid.NewGuid().ToString("N"));
        try
        {
            using var service = new LibreOfficeExcelToPdfService(Options.Create(new LibreOfficeOptions
            {
                ExecutablePath = Environment.GetEnvironmentVariable("EXCEL_TO_PDF_LIBREOFFICE_PATH")!, TempRootPath = root
            }), NullLoggerFactory.Instance);
            var result = await service.ConvertAsync(ExcelToPdfTests.Workbook(scenario));
            Assert.True(result.AsSpan().StartsWith("%PDF-"u8));
            Assert.True(result.Length > 100);
            var syntax = Encoding.Latin1.GetString(result);
            if (scenario is "multiple-pages" or "multiple-sheets")
                Assert.True(Regex.Matches(syntax, @"/Type\s*/Page\b").Count >= 2);
            if (scenario == "landscape")
            {
                var box = Regex.Match(syntax, @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
                Assert.True(box.Success);
                Assert.True(double.Parse(box.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >
                    double.Parse(box.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            Assert.Empty(Directory.EnumerateDirectories(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [LibreOfficeTheory]
    [Trait("Category", "LibreOfficeIntegration")]
    [InlineData(true)]
    public async Task Convert_RealLegacyXls(bool _)
    {
        var root = Path.Combine(Path.GetTempPath(), "WarehousePdfIntegration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var executable = Environment.GetEnvironmentVariable("EXCEL_TO_PDF_LIBREOFFICE_PATH")!;
            if (OperatingSystem.IsWindows() && executable.EndsWith("soffice.exe", StringComparison.OrdinalIgnoreCase))
                executable = Path.ChangeExtension(executable, ".com");
            var input = Path.Combine(root, "legacy.xlsx");
            await File.WriteAllBytesAsync(input, ExcelToPdfTests.Workbook("formatted"));
            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] { "-env:UserInstallation=" + new Uri(Path.Combine(root, "test-profile") + Path.DirectorySeparatorChar).AbsoluteUri,
                         "--headless", "--convert-to", "xls:MS Excel 97", "--outdir", root, input })
                info.ArgumentList.Add(arg);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            Assert.Equal(0, await new OfficeProcessRunner(NullLogger<OfficeProcessRunner>.Instance).RunAsync(info, deadline.Token));
            using var converter = new LibreOfficeExcelToPdfService(Options.Create(new LibreOfficeOptions
            {
                ExecutablePath = executable, TempRootPath = Path.Combine(root, "conversions")
            }), NullLoggerFactory.Instance);
            var pdf = await converter.ConvertAsync(await File.ReadAllBytesAsync(Path.Combine(root, "legacy.xls")), deadline.Token);
            Assert.True(pdf.AsSpan().StartsWith("%PDF-"u8));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private sealed class LibreOfficeTheoryAttribute : TheoryAttribute
    {
        public LibreOfficeTheoryAttribute()
        {
            var path = Environment.GetEnvironmentVariable("EXCEL_TO_PDF_LIBREOFFICE_PATH");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                Skip = "Set EXCEL_TO_PDF_LIBREOFFICE_PATH to an installed LibreOffice console launcher to run real conversion tests.";
        }
    }
}
