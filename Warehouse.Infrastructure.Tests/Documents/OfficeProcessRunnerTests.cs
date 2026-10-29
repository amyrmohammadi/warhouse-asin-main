using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Warehouse.Application.Common.Models;
using Warehouse.Infrastructure.Documents;
using Xunit;

namespace Warehouse.Infrastructure.Tests.Documents;

public sealed class OfficeProcessRunnerTests
{
    [Fact]
    public async Task Run_DrainsBothPipesWithoutDeadlock_AndReturnsNonzeroExit()
    {
        var info = Command(OperatingSystem.IsWindows()
            ? "for ($i=0; $i -lt 12000; $i++) { [Console]::Out.WriteLine('stdout data'); [Console]::Error.WriteLine('stderr data') }; exit 7"
            : "i=0; while [ $i -lt 12000 ]; do echo stdout; echo stderr >&2; i=$((i+1)); done; exit 7");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Assert.Equal(7, await Runner().RunAsync(info, cancellation.Token));
    }

    [Fact]
    public async Task Run_CancellationTerminatesProcessTree()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WarehouseProcessTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var parentFile = Path.Combine(directory, "parent.pid");
        var childFile = Path.Combine(directory, "child.pid");
        var shell = Command("").FileName;
        // Test-controlled paths only. Production code never constructs a shell command.
        var script = OperatingSystem.IsWindows()
            ? $"[IO.File]::WriteAllText('{parentFile}', [string]$PID); " +
              $"$child = Start-Process -FilePath '{shell}' -ArgumentList '-NoProfile -NonInteractive -Command Start-Sleep -Seconds 60' -WindowStyle Hidden -PassThru; " +
              $"[IO.File]::WriteAllText('{childFile}', [string]$child.Id); Start-Sleep -Seconds 60"
            : $"echo $$ > '{parentFile}'; sleep 60 & echo $! > '{childFile}'; wait";
        using var cancellation = new CancellationTokenSource();
        var run = Runner().RunAsync(Command(script), cancellation.Token);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!File.Exists(childFile)) await Task.Delay(25, deadline.Token);
            var parentId = int.Parse(await File.ReadAllTextAsync(parentFile, deadline.Token));
            var childId = int.Parse(await File.ReadAllTextAsync(childFile, deadline.Token));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.False(IsAlive(parentId));
            for (var attempt = 0; attempt < 40 && IsAlive(childId); attempt++) await Task.Delay(25);
            Assert.False(IsAlive(childId));
        }
        finally
        {
            cancellation.Cancel();
            try { await run; } catch (OperationCanceledException) { }
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Run_MissingExecutableReportsMeaningfulError()
    {
        var info = Command("");
        info.FileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing");
        await Assert.ThrowsAsync<ExcelToPdfConversionException>(() => Runner().RunAsync(info, default));
    }

    private static bool IsAlive(int id)
    {
        try { using var process = Process.GetProcessById(id); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private static OfficeProcessRunner Runner() => new(NullLogger<OfficeProcessRunner>.Instance);

    private static ProcessStartInfo Command(string script)
    {
        var executable = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe")
            : "/bin/sh";
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (OperatingSystem.IsWindows())
        {
            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-Command");
        }
        else info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        return info;
    }
}
