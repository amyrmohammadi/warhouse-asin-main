using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Warehouse.Infrastructure.Documents;

internal sealed class ExcelToPdfTempCleanupService(
    LibreOfficeExcelToPdfService converter, ILogger<ExcelToPdfTempCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try { await converter.CleanupStaleDirectoriesAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Excel to PDF stale-file cleanup failed with {ErrorType}", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
