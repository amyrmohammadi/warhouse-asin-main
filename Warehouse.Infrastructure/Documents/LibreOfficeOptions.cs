using System.ComponentModel.DataAnnotations;

namespace Warehouse.Infrastructure.Documents;

public sealed class LibreOfficeOptions
{
    public const string SectionName = "LibreOffice";

    [Required]
    public string ExecutablePath { get; set; } = @"C:\Program Files\LibreOffice\program\soffice.exe";

    [Range(1, 3600)]
    public int TimeoutSeconds { get; set; } = 90;

    [Range(1, 16)]
    public int MaxConcurrentConversions { get; set; } = 2;

    // A dedicated directory, writable only by the conversion identity and administrators.
    public string TempRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "Warehouse", "ExcelToPdf");

    [Range(1, 512)]
    public int MaxInputSizeMegabytes { get; set; } = 50;

    [Range(1, 1024)]
    public int MaxOutputSizeMegabytes { get; set; } = 100;

    [Range(1, 2048)]
    public int MaxUncompressedSizeMegabytes { get; set; } = 256;

    [Range(1, 100000)]
    public int MaxArchiveEntries { get; set; } = 10000;

    [Range(1, 10000)]
    public int MaxCompressionRatio { get; set; } = 1000;

    [Range(1, 168)]
    public int StaleDirectoryAgeHours { get; set; } = 24;
}
