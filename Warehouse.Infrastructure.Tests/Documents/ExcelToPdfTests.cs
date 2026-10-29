using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Infrastructure.Documents;
using Xunit;

namespace Warehouse.Infrastructure.Tests.Documents;

public sealed class ExcelToPdfTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Warehouse PDF تست", Guid.NewGuid().ToString("N"));
    private static readonly byte[] FakePdf = "%PDF-1.7\n% simulated output for unit tests\n%%EOF\n"u8.ToArray();

    private LibreOfficeOptions Options() => new()
    {
        ExecutablePath = Environment.ProcessPath!, TempRootPath = _root
    };

    [Fact]
    public async Task Convert_PreservesWorkbookBytes_UsesIndependentProfile_AndRemovesFiles()
    {
        var input = Workbook("formatted");
        var runner = new FakeRunner(async (info, token) =>
        {
            Assert.False(info.UseShellExecute);
            Assert.True(info.CreateNoWindow && info.RedirectStandardOutput && info.RedirectStandardError);
            Assert.Contains("pdf:calc_pdf_Export", info.ArgumentList);
            Assert.Contains("--infilter=Calc MS Excel 2007 XML", info.ArgumentList);
            Assert.Equal(input, await File.ReadAllBytesAsync(info.ArgumentList.Last(), token));
            var profile = new Uri(info.ArgumentList.Single(a => a.StartsWith("-env:UserInstallation="))[22..]);
            Assert.True(profile.IsFile);
            var settings = await File.ReadAllTextAsync(Path.Combine(profile.LocalPath, "user", "registrymodifications.xcu"), token);
            Assert.Contains("DisableMacrosExecution", settings);
            Assert.Contains("DisableActiveContent", settings);
            await File.WriteAllBytesAsync(Path.Combine(info.WorkingDirectory, "input.pdf"), FakePdf, token);
            return 0;
        });
        using var service = Create(Options(), runner);

        Assert.Equal(FakePdf, await service.ConvertAsync(input));
        Assert.Empty(Directory.EnumerateDirectories(_root));
    }

    [Fact]
    public async Task Convert_NullEmptyInvalidAndTruncatedInputs_DoNotStartProcess()
    {
        var runner = new FakeRunner((_, _) => throw new InvalidOperationException("Must not launch."));
        using var service = Create(Options(), runner);
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ConvertAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ConvertAsync([]));
        foreach (var bytes in new[] { new byte[] { 1, 2, 3 }, "PK\x03\x04broken"u8.ToArray(),
                     new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 } })
            await Assert.ThrowsAsync<ExcelToPdfConversionException>(() => service.ConvertAsync(bytes));
        Assert.Equal(0, runner.Calls);
    }

    [Theory]
    [InlineData("xl/worksheets/_rels/sheet1.xml.rels", "<Relationships><Relationship Type='image' TargetMode='External' Target='https://example.invalid/image'/></Relationships>")]
    [InlineData("xl/worksheets/sheet1.xml", "<worksheet><f>WEBSERVICE(&quot;https://example.invalid&quot;)</f></worksheet>")]
    [InlineData("xl/connections.xml", "<connections/>")]
    [InlineData("xl/workbook.xml", "<!DOCTYPE workbook [<!ENTITY secret SYSTEM 'file:///private'>]><workbook>&secret;</workbook>")]
    public async Task Convert_RejectsExternalResourcesAndDtd(string part, string xml)
    {
        using var stream = new MemoryStream();
        stream.Write(Workbook("simple"));
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            zip.GetEntry(part)?.Delete();
            using var writer = new StreamWriter(zip.CreateEntry(part).Open());
            writer.Write(xml);
        }
        var runner = new FakeRunner((_, _) => throw new InvalidOperationException("Must not launch."));
        using var service = Create(Options(), runner);
        await Assert.ThrowsAsync<ExcelToPdfConversionException>(() => service.ConvertAsync(stream.ToArray()));
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task Convert_EnforcesInputAndExpandedArchiveLimits()
    {
        var options = Options();
        options.MaxInputSizeMegabytes = 1;
        options.MaxUncompressedSizeMegabytes = 1;
        var runner = new FakeRunner((_, _) => throw new InvalidOperationException("Must not launch."));
        using var service = Create(options, runner);
        await Assert.ThrowsAsync<ExcelToPdfConversionException>(() => service.ConvertAsync(new byte[1024 * 1024 + 1]));
        using var stream = new MemoryStream();
        stream.Write(Workbook("simple"));
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("large.xml").Open());
            writer.Write(new string('a', 2 * 1024 * 1024));
        }
        await Assert.ThrowsAsync<ExcelToPdfConversionException>(() => service.ConvertAsync(stream.ToArray()));
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task Convert_UsesXlsFilterForCompoundWorkbook()
    {
        var runner = new FakeRunner(async (info, token) =>
        {
            Assert.EndsWith(".xls", info.ArgumentList.Last());
            Assert.Contains("--infilter=MS Excel 97", info.ArgumentList);
            await File.WriteAllBytesAsync(Path.Combine(info.WorkingDirectory, "input.pdf"), FakePdf, token);
            return 0;
        });
        using var service = Create(Options(), runner);
        // Only a structural fixture: real BIFF import is covered by the opt-in integration test.
        Assert.Equal(FakePdf, await service.ConvertAsync(CompoundWorkbookHeader()));
    }

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(0, false, false)]
    [InlineData(0, true, false)]
    [InlineData(0, false, true)]
    public async Task Convert_RejectsFailedMissingInvalidOrOversizedOutput_AndCleansUp(int exit, bool invalid, bool oversized)
    {
        var options = Options();
        options.MaxOutputSizeMegabytes = 1;
        var runner = new FakeRunner(async (info, token) =>
        {
            if (invalid || oversized)
                await File.WriteAllBytesAsync(Path.Combine(info.WorkingDirectory, "input.pdf"),
                    oversized ? new byte[1024 * 1024 + 1] : "not a pdf document"u8.ToArray(), token);
            return exit;
        });
        using var service = Create(options, runner);
        await Assert.ThrowsAsync<ExcelToPdfConversionException>(() => service.ConvertAsync(Workbook("simple")));
        Assert.Empty(Directory.EnumerateDirectories(_root));
    }

    [Fact]
    public async Task Convert_SeparatesTimeoutFromCancellation_AndReleasesSlot()
    {
        var options = Options();
        options.MaxConcurrentConversions = 1;
        options.TimeoutSeconds = 1;
        var runner = new FakeRunner(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return 0; });
        using var service = Create(options, runner);
        var input = Workbook("simple");
        await Assert.ThrowsAsync<TimeoutException>(() => service.ConvertAsync(input));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => service.ConvertAsync(input, cancellation.Token));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(2, runner.Calls);
        Assert.Empty(Directory.EnumerateDirectories(_root));
    }

    [Fact]
    public async Task Convert_LimitsConcurrencyAndCancelsQueuedWork()
    {
        var active = 0;
        var maximum = 0;
        var directories = new ConcurrentBag<string>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeRunner(async (info, token) =>
        {
            var count = Interlocked.Increment(ref active);
            maximum = Math.Max(maximum, count);
            directories.Add(info.WorkingDirectory);
            if (count == 2) entered.TrySetResult();
            await release.Task.WaitAsync(token);
            await File.WriteAllBytesAsync(Path.Combine(info.WorkingDirectory, "input.pdf"), FakePdf, token);
            Interlocked.Decrement(ref active);
            return 0;
        });
        using var service = Create(Options(), runner);
        var input = Workbook("simple");
        var first = service.ConvertAsync(input);
        var second = service.ConvertAsync(input);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ConvertAsync(input, cancellation.Token));
        release.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(2, maximum);
        Assert.Equal(2, directories.Distinct().Count());
        Assert.Empty(Directory.EnumerateDirectories(_root));
    }

    [Fact]
    public async Task Convert_MissingExecutableAndUnwritableTempPath_HaveMeaningfulFailures()
    {
        var runner = new FakeRunner((_, _) => throw new InvalidOperationException("Must not launch."));
        var options = Options();
        options.ExecutablePath = Path.Combine(_root, "does-not-exist");
        using (var service = Create(options, runner))
            await Assert.ThrowsAsync<ExcelToPdfConversionException>(() => service.ConvertAsync(Workbook("simple")));
        Directory.CreateDirectory(_root);
        var blocked = Path.Combine(_root, "file-instead-of-directory");
        await File.WriteAllTextAsync(blocked, "test");
        options = Options();
        options.TempRootPath = blocked;
        using (var service = Create(options, runner))
            await Assert.ThrowsAsync<ExcelToPdfConversionException>(() => service.ConvertAsync(Workbook("simple")));
    }

    [Fact]
    public async Task Cleanup_RemovesOnlyStaleOwnedUnlockedDirectories()
    {
        string Job()
        {
            var directory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, ".lease"), "");
            Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow.AddDays(-2));
            return directory;
        }
        var stale = Job();
        var active = Job();
        var recent = Job();
        Directory.SetLastWriteTimeUtc(recent, DateTime.UtcNow);
        var unrelated = Path.Combine(_root, "unrelated");
        Directory.CreateDirectory(unrelated);
        using var lease = new FileStream(Path.Combine(active, ".lease"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var service = Create(Options(), new FakeRunner((_, _) => Task.FromResult(0)));
        await service.CleanupStaleDirectoriesAsync(default);
        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(active));
        Assert.True(Directory.Exists(recent));
        Assert.True(Directory.Exists(unrelated));
    }

    [Fact]
    public async Task CleanupFailure_DoesNotHideConversionFailure_AndStaleCleanupCanRetry()
    {
        // Windows denies deletion while another handle omits FileShare.Delete.
        if (!OperatingSystem.IsWindows()) return;
        FileStream? locked = null;
        var runner = new FakeRunner((info, _) =>
        {
            locked = new FileStream(Path.Combine(info.WorkingDirectory, "locked.tmp"), FileMode.Create,
                FileAccess.ReadWrite, FileShare.None);
            return Task.FromResult(9);
        });
        using var service = Create(Options(), runner);
        try
        {
            var error = await Assert.ThrowsAsync<ExcelToPdfConversionException>(() => service.ConvertAsync(Workbook("simple")));
            Assert.Contains("conversion error", error.Message);
        }
        finally { locked?.Dispose(); }
        var remaining = Assert.Single(Directory.EnumerateDirectories(_root));
        Directory.SetLastWriteTimeUtc(remaining, DateTime.UtcNow.AddDays(-2));
        await service.CleanupStaleDirectoriesAsync(default);
        Assert.Empty(Directory.EnumerateDirectories(_root));
    }

    [Fact]
    public void DependencyInjection_SharesOneConverterAcrossScopes_AndValidatesOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["LibreOffice:ExecutablePath"] = Environment.ProcessPath!, ["LibreOffice:TempRootPath"] = _root }).Build());
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.Same(first.ServiceProvider.GetRequiredService<IExcelToPdfService>(), second.ServiceProvider.GetRequiredService<IExcelToPdfService>());

        services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["LibreOffice:MaxConcurrentConversions"] = "0" }).Build());
        services.AddInfrastructure();
        using var invalid = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => invalid.GetRequiredService<IExcelToPdfService>());
    }

    internal static byte[] Workbook(string scenario)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("گزارش");
        sheet.Cell("A1").Value = scenario == "simple" ? "Test report" : "گزارش ورود کالا - Report ۱۲۳";
        sheet.Cell("A2").Value = 12.5;
        if (scenario != "simple")
        {
            sheet.RightToLeft = true;
            sheet.Range("A1:D1").Merge();
            sheet.Range("A1:D5").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Range("A1:D1").Style.Fill.BackgroundColor = XLColor.LightBlue;
            sheet.Style.Font.FontName = "Tahoma";
            sheet.Column(1).Width = 24;
            sheet.Row(1).Height = 32;
            sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
            sheet.PageSetup.Header.Center.AddText("گزارش انبار");
            sheet.PageSetup.Footer.Center.AddText(XLHFPredefinedText.PageNumber);
            sheet.Cell("A2").Style.NumberFormat.Format = "#,##0.00";
            if (scenario == "landscape") sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            if (scenario == "print-area") sheet.PageSetup.PrintAreas.Add("A1:D5");
            if (scenario == "multiple-sheets") workbook.Worksheets.Add("Second").Cell("A1").Value = "Second sheet";
            if (scenario == "multiple-pages")
            {
                for (var row = 3; row <= 250; row++) sheet.Cell(row, 1).Value = $"ردیف {row}";
                sheet.PageSetup.AddHorizontalPageBreak(50);
            }
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] CompoundWorkbookHeader()
    {
        var bytes = new byte[512 * 3];
        byte[] signature = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];
        signature.CopyTo(bytes, 0);
        void W16(int offset, ushort value) => BitConverter.GetBytes(value).CopyTo(bytes, offset);
        void W32(int offset, uint value) => BitConverter.GetBytes(value).CopyTo(bytes, offset);
        W16(26, 3); W16(28, 0xfffe); W16(30, 9); W16(32, 6);
        W32(44, 1); W32(48, 1); W32(76, 0);
        W32(512, 0xfffffffd); W32(516, 0xfffffffe);
        Encoding.Unicode.GetBytes("Workbook\0").CopyTo(bytes, 1024);
        W16(1024 + 64, 18); bytes[1024 + 66] = 2; W32(1024 + 120, 8);
        return bytes;
    }

    private static LibreOfficeExcelToPdfService Create(LibreOfficeOptions options, IOfficeProcessRunner runner) =>
        new(options, NullLogger<LibreOfficeExcelToPdfService>.Instance, runner);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeRunner(Func<ProcessStartInfo, CancellationToken, Task<int>> action) : IOfficeProcessRunner
    {
        public int Calls;
        public Task<int> RunAsync(ProcessStartInfo info, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            return action(info, token);
        }
    }
}
