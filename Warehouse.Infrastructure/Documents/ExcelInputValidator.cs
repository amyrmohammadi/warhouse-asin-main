using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Warehouse.Application.Common.Models;

namespace Warehouse.Infrastructure.Documents;

internal static partial class ExcelInputValidator
{
    private const string SpreadsheetType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";

    public static async Task<string> ValidateAsync(byte[] bytes, LibreOfficeOptions options, CancellationToken token)
    {
        try
        {
            if (bytes.AsSpan().StartsWith(new byte[] { 0x50, 0x4b, 0x03, 0x04 }))
            {
                await ValidateOpenXmlAsync(bytes, options, token);
                return ".xlsx";
            }
            if (bytes.AsSpan().StartsWith(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }))
            {
                ValidateCompoundWorkbook(bytes, token);
                return ".xls";
            }
            throw new ExcelToPdfConversionException("Only unencrypted XLSX and OLE/BIFF XLS workbooks are supported.");
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or ArgumentException or OverflowException)
        {
            // Parser messages can contain document data; do not propagate them to logs/callers.
            throw new ExcelToPdfConversionException("The Excel file is corrupt or has an invalid workbook structure.");
        }
    }

    private static async Task ValidateOpenXmlAsync(byte[] bytes, LibreOfficeOptions options, CancellationToken token)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        if (archive.Entries.Count > options.MaxArchiveEntries)
            throw new ExcelToPdfConversionException("The workbook contains too many ZIP entries.");

        long expanded = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (!names.Add(entry.FullName) || entry.FullName.Contains('\\') ||
                entry.FullName.Split('/').Any(part => part == "..") || entry.FullName.StartsWith('/'))
                throw new InvalidDataException();
            expanded = checked(expanded + entry.Length);
            if (expanded > options.MaxUncompressedSizeMegabytes * 1024L * 1024 ||
                entry.Length / (double)Math.Max(1, entry.CompressedLength) > options.MaxCompressionRatio)
                throw new ExcelToPdfConversionException("The workbook exceeds the decompression limits.");
            if (entry.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.Equals("xl/connections.xml", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))
                throw new ExcelToPdfConversionException("Workbooks with macros, external data links or connections are not supported.");
        }
        if (!names.Contains("[Content_Types].xml") || !names.Contains("xl/workbook.xml") ||
            !names.Any(name => name.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".xml")))
            throw new InvalidDataException();

        string? defaultXmlType = null;
        string? workbookType = null;
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            using var rawContent = entry.Open();
            using var content = new BoundedEntryStream(rawContent, entry.Length);
            if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                !entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            {
                // Also decompress binary parts to catch damaged streams before starting LibreOffice.
                var buffer = new byte[8192];
                while (await content.ReadAsync(buffer, token) != 0) { }
                continue;
            }
            using var xml = XmlReader.Create(content, new XmlReaderSettings
            {
                Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = options.MaxUncompressedSizeMegabytes * 1024L * 1024
            });
            while (await xml.ReadAsync())
            {
                token.ThrowIfCancellationRequested();
                if (xml.NodeType != XmlNodeType.Element) continue;
                if (entry.FullName == "[Content_Types].xml")
                {
                    if (xml.LocalName == "Default" && xml.GetAttribute("Extension") == "xml")
                        defaultXmlType = xml.GetAttribute("ContentType");
                    if (xml.LocalName == "Override" && xml.GetAttribute("PartName") == "/xl/workbook.xml")
                        workbookType = xml.GetAttribute("ContentType");
                }
                if (xml.LocalName == "Relationship" && xml.GetAttribute("TargetMode") == "External" &&
                    !(xml.GetAttribute("Type")?.EndsWith("/hyperlink", StringComparison.Ordinal) ?? false))
                    throw new ExcelToPdfConversionException("The workbook references an external file or resource.");
                if (xml.LocalName is "f" or "definedName")
                {
                    // ReadSubtree avoids skipping an adjacent formula when consuming its text.
                    using var formula = xml.ReadSubtree();
                    while (await formula.ReadAsync())
                        if (formula.NodeType is XmlNodeType.Text or XmlNodeType.CDATA && ExternalFormula().IsMatch(formula.Value))
                            throw new ExcelToPdfConversionException("The workbook contains a formula that can access external resources.");
                }
            }
        }
        if ((workbookType ?? defaultXmlType) != SpreadsheetType) throw new InvalidDataException();
    }

    // Inspect the OLE header, DIFAT/FAT directory chain and workbook stream entry.
    // BIFF cell records are validated by LibreOffice's explicit Excel import filter.
    private static void ValidateCompoundWorkbook(byte[] bytes, CancellationToken token)
    {
        uint Read32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        ushort Read16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
        if (bytes.Length < 512 || Read16(28) != 0xfffe || Read16(32) != 6 ||
            !((Read16(26) == 3 && Read16(30) == 9) || (Read16(26) == 4 && Read16(30) == 12)))
            throw new InvalidDataException();
        var sectorSize = 1 << Read16(30);
        var sectorCount = bytes.Length / sectorSize - 1;
        if (bytes.Length % sectorSize != 0 || sectorCount < 1) throw new InvalidDataException();
        int Offset(uint sector)
        {
            if (sector >= sectorCount) throw new InvalidDataException();
            return checked(((int)sector + 1) * sectorSize);
        }
        var fatCount = Read32(44);
        if (fatCount == 0 || fatCount > sectorCount) throw new InvalidDataException();
        var fat = new List<uint>();
        for (var i = 0; i < 109 && fat.Count < fatCount; i++) fat.Add(Read32(76 + i * 4));
        var difat = Read32(68);
        var seen = new HashSet<uint>();
        while (fat.Count < fatCount)
        {
            token.ThrowIfCancellationRequested();
            if (!seen.Add(difat)) throw new InvalidDataException();
            var offset = Offset(difat);
            for (var i = 0; i < sectorSize / 4 - 1 && fat.Count < fatCount; i++) fat.Add(Read32(offset + i * 4));
            difat = Read32(offset + sectorSize - 4);
        }
        foreach (var sector in fat) _ = Offset(sector);
        var directory = Read32(48);
        seen.Clear();
        var foundWorkbook = false;
        while (directory != 0xfffffffe)
        {
            token.ThrowIfCancellationRequested();
            if (!seen.Add(directory)) throw new InvalidDataException();
            var offset = Offset(directory);
            for (var i = 0; i < sectorSize; i += 128)
            {
                var nameLength = Read16(offset + i + 64);
                if (nameLength is < 2 or > 64 || nameLength % 2 != 0 || bytes[offset + i + 66] != 2) continue;
                var name = Encoding.Unicode.GetString(bytes, offset + i, nameLength - 2);
                if (name is "EncryptedPackage" or "EncryptionInfo")
                    throw new ExcelToPdfConversionException("Password-protected Excel files are not supported.");
                if (name is "Workbook" or "Book")
                {
                    if (Read32(offset + i + 120) == 0) throw new InvalidDataException();
                    foundWorkbook = true;
                }
            }
            var fatIndex = directory / (sectorSize / 4);
            if (fatIndex >= fat.Count) throw new InvalidDataException();
            directory = Read32(Offset(fat[(int)fatIndex]) + (int)(directory % (sectorSize / 4)) * 4);
        }
        if (!foundWorkbook) throw new InvalidDataException();
    }

    [GeneratedRegex(@"(?:^|[^A-Z0-9_])(?:_xlfn\.)?(?:WEBSERVICE|IMAGE|RTD|DDE)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalFormula();

    // Do not trust a forged ZIP central directory's declared expanded length.
    private sealed class BoundedEntryStream(Stream inner, long maximumBytes) : Stream
    {
        private long _read;
        private int Count(int count)
        {
            _read = checked(_read + count);
            if (_read > maximumBytes) throw new InvalidDataException();
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));
        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Count(await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => maximumBytes;
        public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
