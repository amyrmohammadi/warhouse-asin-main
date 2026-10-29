using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;

namespace Warehouse.Infrastructure.Documents;

public sealed class LibreOfficeExcelToPdfService : IExcelToPdfService, IDisposable
{
    private readonly LibreOfficeOptions _options;
    private readonly ILogger<LibreOfficeExcelToPdfService> _logger;
    private readonly IOfficeProcessRunner _runner;
    private readonly SemaphoreSlim _slots;

    public LibreOfficeExcelToPdfService(IOptions<LibreOfficeOptions> options, ILoggerFactory loggerFactory)
        : this(options.Value, loggerFactory.CreateLogger<LibreOfficeExcelToPdfService>(),
            new OfficeProcessRunner(loggerFactory.CreateLogger<OfficeProcessRunner>())) { }

    internal LibreOfficeExcelToPdfService(LibreOfficeOptions options,
        ILogger<LibreOfficeExcelToPdfService> logger, IOfficeProcessRunner runner)
    {
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        if (!Path.IsPathFullyQualified(options.ExecutablePath) || !Path.IsPathFullyQualified(options.TempRootPath))
            throw new ArgumentException("LibreOffice executable and temporary root must be absolute paths.");
        _options = options;
        _logger = logger;
        _runner = runner;
        _slots = new SemaphoreSlim(options.MaxConcurrentConversions, options.MaxConcurrentConversions);
    }

    public async Task<byte[]> ConvertAsync(byte[] excelBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(excelBytes);
        if (excelBytes.Length == 0) throw new ArgumentException("Excel data cannot be empty.", nameof(excelBytes));
        if (excelBytes.LongLength > _options.MaxInputSizeMegabytes * 1024L * 1024)
            throw new ExcelToPdfConversionException("The Excel file exceeds the configured input size limit.");

        // Queue waiting is cancellable; the conversion timeout starts after acquiring a slot.
        await _slots.WaitAsync(cancellationToken);
        var conversionId = Guid.NewGuid().ToString("N");
        string? jobDirectory = null;
        FileStream? lease = null;
        var stopwatch = Stopwatch.StartNew();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var token = linked.Token;
            var snapshot = excelBytes.ToArray();
            var extension = await ExcelInputValidator.ValidateAsync(snapshot, _options, token);
            token.ThrowIfCancellationRequested();
            var executable = ResolveExecutable();
            Directory.CreateDirectory(_options.TempRootPath);
            if ((File.GetAttributes(_options.TempRootPath) & FileAttributes.ReparsePoint) != 0)
                throw new ExcelToPdfConversionException("The temporary root cannot be a symbolic link or junction.");
            jobDirectory = Path.Combine(_options.TempRootPath, conversionId);
            Directory.CreateDirectory(jobDirectory);
            lease = new FileStream(Path.Combine(jobDirectory, ".lease"), FileMode.CreateNew,
                FileAccess.ReadWrite, FileShare.None);
            var profile = Path.Combine(jobDirectory, "profile");
            Directory.CreateDirectory(Path.Combine(profile, "user"));
            await File.WriteAllTextAsync(Path.Combine(profile, "user", "registrymodifications.xcu"), ProfileSettings, token);
            var input = Path.Combine(jobDirectory, "input" + extension);
            await File.WriteAllBytesAsync(input, snapshot, token);

            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = jobDirectory
            };
            // Each process also uses its own OS temp directory (not the IIS profile's shared temp).
            var processTemp = Path.Combine(jobDirectory, "temp");
            Directory.CreateDirectory(processTemp);
            info.Environment["TEMP"] = processTemp;
            info.Environment["TMP"] = processTemp;
            info.Environment["TMPDIR"] = processTemp;
            info.ArgumentList.Add("-env:UserInstallation=" + new Uri(profile + Path.DirectorySeparatorChar).AbsoluteUri);
            foreach (var argument in new[] { "--headless", "--invisible", "--nologo", "--nodefault", "--nofirststartwizard", "--norestore" })
                info.ArgumentList.Add(argument);
            info.ArgumentList.Add(extension == ".xls" ? "--infilter=MS Excel 97" : "--infilter=Calc MS Excel 2007 XML");
            info.ArgumentList.Add("--convert-to");
            info.ArgumentList.Add("pdf:calc_pdf_Export");
            info.ArgumentList.Add("--outdir");
            info.ArgumentList.Add(jobDirectory);
            info.ArgumentList.Add(input);
            var exitCode = await _runner.RunAsync(info, token);
            token.ThrowIfCancellationRequested();
            if (exitCode != 0)
            {
                _logger.LogError("Conversion {ConversionId} exited with code {ExitCode}", conversionId, exitCode);
                throw new ExcelToPdfConversionException("LibreOffice reported a conversion error. The workbook may be corrupt or unsupported.");
            }
            var output = Path.Combine(jobDirectory, "input.pdf");
            if (!File.Exists(output))
                throw new ExcelToPdfConversionException("LibreOffice did not produce a PDF. Check the workbook and its printable sheets.");
            // Read a bounded stream instead of trusting metadata followed by an unbounded read.
            await using var pdf = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
            if (pdf.Length < 8 || pdf.Length > _options.MaxOutputSizeMegabytes * 1024L * 1024)
                throw new ExcelToPdfConversionException("The produced PDF is empty or exceeds the output size limit.");
            var result = new byte[checked((int)pdf.Length)];
            await pdf.ReadExactlyAsync(result, token);
            if (!result.AsSpan().StartsWith("%PDF-"u8) ||
                result.AsSpan(Math.Max(0, result.Length - 1024)).IndexOf("%%EOF"u8) < 0)
                throw new ExcelToPdfConversionException("LibreOffice produced an invalid PDF file.");
            _logger.LogInformation("Conversion {ConversionId} completed in {ElapsedMs} ms; PDF size {OutputBytes} bytes",
                conversionId, stopwatch.ElapsedMilliseconds, result.Length);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Conversion {ConversionId} was cancelled by its caller", conversionId);
            throw new OperationCanceledException("Excel to PDF conversion was cancelled.", cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            _logger.LogError("Conversion {ConversionId} timed out after {TimeoutSeconds} seconds", conversionId, _options.TimeoutSeconds);
            throw new TimeoutException($"Excel to PDF conversion exceeded {_options.TimeoutSeconds} seconds.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError("Conversion {ConversionId} encountered a filesystem error: {ErrorType}", conversionId, ex.GetType().Name);
            throw new ExcelToPdfConversionException("Could not access conversion files. Check temporary storage permissions and free disk space.");
        }
        catch (ExcelToPdfConversionException)
        {
            _logger.LogError("Conversion {ConversionId} failed validation or conversion; elapsed {ElapsedMs} ms",
                conversionId, stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            try
            {
                try { lease?.Dispose(); }
                catch (IOException)
                {
                    _logger.LogError("Could not close the lease for conversion {ConversionId}", conversionId);
                }
                if (jobDirectory is not null) await DeleteDirectoryAsync(jobDirectory);
            }
            finally { _slots.Release(); }
        }
    }

    private string ResolveExecutable()
    {
        var executable = _options.ExecutablePath;
        if (OperatingSystem.IsWindows() && Path.GetFileName(executable).Equals("soffice.exe", StringComparison.OrdinalIgnoreCase))
        {
            // The console launcher waits for soffice.bin and supplies reliable redirected output on Windows.
            var consoleLauncher = Path.ChangeExtension(executable, ".com");
            if (!File.Exists(consoleLauncher))
                throw new ExcelToPdfConversionException("LibreOffice's soffice.com console launcher is missing. Repair its installation or configure the console launcher path.");
            executable = consoleLauncher;
        }
        if (!File.Exists(executable))
            throw new ExcelToPdfConversionException("LibreOffice is not installed at the configured executable path.");
        return executable;
    }

    internal async Task CleanupStaleDirectoriesAsync(CancellationToken token)
    {
        if (!Directory.Exists(_options.TempRootPath) ||
            (File.GetAttributes(_options.TempRootPath) & FileAttributes.ReparsePoint) != 0) return;
        var cutoff = DateTime.UtcNow.AddHours(-_options.StaleDirectoryAgeHours);
        foreach (var directory in Directory.EnumerateDirectories(_options.TempRootPath))
        {
            token.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _) ||
                (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
                Directory.GetLastWriteTimeUtc(directory) >= cutoff) continue;
            var marker = Path.Combine(directory, ".lease");
            if (!File.Exists(marker))
            {
                // A crash can occur between creation of the GUID directory and its lease.
                await DeleteDirectoryAsync(directory);
                continue;
            }
            try
            {
                // An active conversion (including another IIS worker) holds an exclusive lease.
                using (new FileStream(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                await DeleteDirectoryAsync(directory);
            }
            catch (IOException) { /* Active or already removed by another worker. */ }
        }
    }

    private async Task DeleteDirectoryAsync(string directory)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 2)
                    _logger.LogError("Could not remove conversion directory {JobDirectory}; error type {ErrorType}. The stale directory cleaner will retry.", directory, ex.GetType().Name);
                else await Task.Delay(100 * (attempt + 1));
            }
        }
    }

    public void Dispose() => _slots.Dispose();

    private const string ProfileSettings = """
        <?xml version="1.0" encoding="UTF-8"?>
        <oor:items xmlns:oor="http://openoffice.org/2001/registry">
          <item oor:path="/org.openoffice.Office.Common/Security/Scripting">
            <prop oor:name="DisableMacrosExecution" oor:op="fuse"><value>true</value></prop>
            <prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop>
            <prop oor:name="SecureURL" oor:op="fuse"><value/></prop>
            <prop oor:name="DisableActiveContent" oor:op="fuse"><value>true</value></prop>
            <prop oor:name="BlockUntrustedRefererLinks" oor:op="fuse"><value>true</value></prop>
          </item>
          <item oor:path="/org.openoffice.Office.Calc/Content/Update">
            <prop oor:name="Link" oor:op="fuse"><value>1</value></prop>
          </item>
        </oor:items>
        """;
}
