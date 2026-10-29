using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Warehouse.Application.Common.Models;

namespace Warehouse.Infrastructure.Documents;

internal interface IOfficeProcessRunner
{
    Task<int> RunAsync(ProcessStartInfo startInfo, CancellationToken token);
}

internal sealed class OfficeProcessRunner(ILogger<OfficeProcessRunner> logger) : IOfficeProcessRunner
{
    public async Task<int> RunAsync(ProcessStartInfo startInfo, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) throw new ExcelToPdfConversionException("LibreOffice could not be started.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            logger.LogError("Could not launch the conversion process. Error type: {ErrorType}", ex.GetType().Name);
            throw new ExcelToPdfConversionException("LibreOffice could not be started. Check its installation path and service account permissions.");
        }

        // Drain both pipes concurrently in fixed-size buffers. Document text/errors are never logged.
        using var drainCancellation = new CancellationTokenSource();
        var stdout = DrainAsync(process.StandardOutput, drainCancellation.Token);
        var stderr = DrainAsync(process.StandardError, drainCancellation.Token);
        try
        {
            await process.WaitForExitAsync(token);
            await Task.WhenAll(stdout, stderr).WaitAsync(token);
            return process.ExitCode;
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or TimeoutException)
            {
                logger.LogCritical("Could not terminate conversion process {ProcessId}. Error type: {ErrorType}",
                    process.Id, ex.GetType().Name);
            }
            finally
            {
                await drainCancellation.CancelAsync();
                try { await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (Exception ex) when (ex is OperationCanceledException or IOException or TimeoutException or ObjectDisposedException)
                {
                    logger.LogDebug("Conversion pipe draining ended with {ErrorType}", ex.GetType().Name);
                }
            }
        }
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory(), token) != 0) { }
    }
}
