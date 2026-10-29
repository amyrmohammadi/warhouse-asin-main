using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models.Inventory;

namespace Warehouse.Infrastructure.Documents;

public sealed class GoodsIssuePdfService(IExcelToPdfService excelToPdfService) : IGoodsIssuePdfService
{
    public Task<byte[]> GenerateAsync(GoodsIssueSummary issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return excelToPdfService.ConvertAsync(CreateWorkbook(issue, cancellationToken), cancellationToken);
    }

    // Edit only cell XML and clone printable forms. Re-saving through a workbook library can
    // discard the template's grouped drawing/logo, printer settings or document metadata.
    internal static byte[] CreateWorkbook(GoodsIssueSummary issue, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var template = typeof(GoodsIssuePdfService).Assembly.GetManifestResourceStream(
            "Warehouse.Infrastructure.Documents.Templates.GoodsIssuePermit.xlsx")
            ?? throw new InvalidOperationException("The goods issue print template is missing.");
        using var source = new ZipArchive(template, ZipArchiveMode.Read);
        var parts = source.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var content = entry.Open();
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            return copy.ToArray();
        }, StringComparer.Ordinal);
        var originalSheet = ReadXml(parts, "xl/worksheets/sheet1.xml");
        var footerRow = originalSheet.Descendants(Main + "mergeCell")
            .Select(cell => (string?)cell.Attribute("ref"))
            .Where(reference => reference is not null && reference.StartsWith('A'))
            .Select(reference => reference!.Split(':'))
            .Where(range => range.Length == 2 && range[1].StartsWith('J'))
            .Select(range => int.Parse(range[0][1..], CultureInfo.InvariantCulture))
            .Where(row => row > FirstDataRow)
            .Min();
        var rowsPerPage = footerRow - FirstDataRow;
        var signatureRow = footerRow + 1;
        var originalRelations = ReadXml(parts, "xl/worksheets/_rels/sheet1.xml.rels");
        var workbook = ReadXml(parts, "xl/workbook.xml");
        var relations = ReadXml(parts, "xl/_rels/workbook.xml.rels");
        var types = ReadXml(parts, "[Content_Types].xml");
        var styles = ReadXml(parts, "xl/styles.xml");
        var sharedStrings = ReadXml(parts, "xl/sharedStrings.xml").Root!.Elements(Main + "si")
            .Select(item => string.Concat(item.Descendants(Main + "t").Select(text => text.Value))).ToArray();
        var rightToLeftStyles = new Dictionary<string, string>();
        var cellStyles = CreateDataStyles(styles, originalSheet, footerRow);
        var dateStyles = CreateDataStyles(styles, originalSheet, footerRow, 20);
        var dateCellStyles = cellStyles.ToDictionary(style => style.Value, style => dateStyles[style.Key]);
        var printRows = CreatePrintRows(issue, token);
        var pageCount = Math.Max(1, (printRows.Count + rowsPerPage - 1) / rowsPerPage);
        var sheetNames = new List<string>();
        var originalName = workbook.Root!.Element(Main + "sheets")!.Element(Main + "sheet")!.Attribute("name")!.Value;
        for (var page = 0; page < pageCount; page++)
        {
            token.ThrowIfCancellationRequested();
            var sheet = new XDocument(originalSheet);
            ConfigurePrintLayout(sheet);
            var sheetName = page == 0 ? originalName : $"{originalName} {PersianDigits((page + 1).ToString(CultureInfo.InvariantCulture))}";
            sheetNames.Add(sheetName);
            SetText(sheet, "A6", "شماره: " + issue.IssueNumber);
            SetText(sheet, "E6", "تاریخ: " + PersianDate(issue.IssuedAtUtc));
            SetText(sheet, "A" + footerRow, $"خروج اقلام و کالاهای فوق توسط آقای {issue.IssuerFullName} با وسیله نقلیه/ بدون وسیله نقلیه به شماره شهربانی .......................... بلامانع است.");
            SetText(sheet, "A" + signatureRow, "خارج‌کننده: " + issue.IssuerFullName);
            for (var slot = 0; slot < rowsPerPage; slot++)
            {
                var rowNumber = FirstDataRow + slot;
                var index = page * rowsPerPage + slot;
                var row = index < printRows.Count ? printRows[index] : null;
                foreach (var column in new[] { "A", "B", "D", "E", "F", "G", "H" })
                    SetText(sheet, column + rowNumber, "", cellStyles);
                if (row is null) continue;
                SetText(sheet, "A" + rowNumber, row.Number, cellStyles);
                SetText(sheet, "B" + rowNumber, row.Product, cellStyles);
                SetText(sheet, "D" + rowNumber, row.Quantity, cellStyles);
                SetText(sheet, "E" + rowNumber, row.Unit, cellStyles);
                SetText(sheet, "F" + rowNumber, row.Returnable, cellStyles);
                SetText(sheet, "G" + rowNumber, row.ReturnDate, dateCellStyles);
                SetText(sheet, "H" + rowNumber, row.Description, cellStyles);
            }
            var sheetPath = $"xl/worksheets/sheet{page + 1}.xml";
            if (page > 0)
            {
                var view = sheet.Root!.Element(Main + "sheetViews")?.Element(Main + "sheetView");
                view?.SetAttributeValue("tabSelected", "0");
                var sheetRelationId = $"rIdGoodsIssuePage{page + 1}";
                workbook.Root.Element(Main + "sheets")!.Add(new XElement(Main + "sheet",
                    new XAttribute("name", sheetName), new XAttribute("sheetId", page + 1),
                    new XAttribute(OfficeRelations + "id", sheetRelationId)));
                workbook.Root.Element(Main + "definedNames")!.Add(new XElement(Main + "definedName",
                    new XAttribute("name", "_xlnm.Print_Area"), new XAttribute("localSheetId", page),
                    $"'{sheetName}'!$A$1:$J${signatureRow}"));
                relations.Root!.Add(new XElement(PackageRelations + "Relationship",
                    new XAttribute("Id", sheetRelationId), new XAttribute("Type", OfficeRelations.NamespaceName + "/worksheet"),
                    new XAttribute("Target", $"worksheets/sheet{page + 1}.xml")));
                AddContentType(types, "/" + sheetPath,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                var sheetRelations = new XDocument(originalRelations);
                sheetRelations.Root!.Elements().Single(element => element.Attribute("Type")!.Value.EndsWith("/drawing", StringComparison.Ordinal))
                    .SetAttributeValue("Target", $"../drawings/drawing{page + 1}.xml");
                parts[$"xl/worksheets/_rels/sheet{page + 1}.xml.rels"] = WriteXml(sheetRelations);
                parts[$"xl/drawings/drawing{page + 1}.xml"] = parts["xl/drawings/drawing1.xml"];
                parts[$"xl/drawings/_rels/drawing{page + 1}.xml.rels"] = parts["xl/drawings/_rels/drawing1.xml.rels"];
                AddContentType(types, $"/xl/drawings/drawing{page + 1}.xml",
                    "application/vnd.openxmlformats-officedocument.drawing+xml");
            }
            SetRightToLeftTextDirection(sheet, styles, sharedStrings, rightToLeftStyles);
            parts[sheetPath] = WriteXml(sheet);
        }
        foreach (var area in workbook.Descendants(Main + "definedName")
                     .Where(name => (string?)name.Attribute("name") == "_xlnm.Print_Area"))
        {
            var page = (int?)area.Attribute("localSheetId") ?? 0;
            area.Value = $"'{sheetNames[page].Replace("'", "''")}'!$A$1:$J${signatureRow}";
        }
        parts["xl/workbook.xml"] = WriteXml(workbook);
        parts["xl/_rels/workbook.xml.rels"] = WriteXml(relations);
        parts["[Content_Types].xml"] = WriteXml(types);
        parts["xl/styles.xml"] = WriteXml(styles);
        UpdateSheetMetadata(parts, sheetNames);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var part in parts)
            {
                token.ThrowIfCancellationRequested();
                using var content = archive.CreateEntry(part.Key).Open();
                content.Write(part.Value);
            }
        return output.ToArray();
    }

    private static List<PrintRow> CreatePrintRows(GoodsIssueSummary issue, CancellationToken token)
    {
        var rows = new List<PrintRow>();
        var number = 0;
        foreach (var item in issue.Items)
        {
            token.ThrowIfCancellationRequested();
            number++;
            var product = SplitText(item.ProductTitle ?? "");
            var notes = SplitText(item.Description ?? "");
            for (var segment = 0; segment < Math.Max(product.Count, notes.Count); segment++)
                rows.Add(new PrintRow(segment == 0 ? PersianDigits(number.ToString(CultureInfo.InvariantCulture)) : "ادامه",
                    segment < product.Count ? product[segment] : "", segment < notes.Count ? notes[segment] : "",
                    segment == 0 ? PersianDigits(item.Quantity.ToString("0.###", CultureInfo.InvariantCulture)) : "",
                    segment == 0 ? item.ProductUnitOfMeasureTitle ?? "" : "",
                    segment == 0 ? item.IsReturnable ? "دارد" : "ندارد" : "",
                    segment == 0 && item.IsReturnable && item.ReturnAtUtc.HasValue ? PersianDate(item.ReturnAtUtc.Value) : ""));
        }
        return rows;
    }

    // At 24pt, three short lines fit inside the template's 99pt data row.
    // Continuation rows preserve all text without reducing the font or losing quantities.
    private static List<string> SplitText(string text)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var remaining = paragraph.Trim();
            while (remaining.Length > 0)
            {
                var elements = StringInfo.ParseCombiningCharacters(remaining);
                var length = elements.Length <= 32 ? remaining.Length : elements[32];
                if (length < remaining.Length)
                {
                    var space = remaining.LastIndexOf(' ', length - 1, length);
                    if (space > length / 2) length = space;
                }
                lines.Add(remaining[..length].Trim());
                remaining = remaining[length..].TrimStart();
            }
        }
        return lines.Count == 0 ? [""] : lines.Chunk(3).Select(group => string.Join('\n', group)).ToList();
    }

    private static Dictionary<string, string> CreateDataStyles(XDocument styles, XDocument sheet, int footerRow, int fontSize = 24)
    {
        var fonts = styles.Root!.Element(Main + "fonts")!;
        var font = new XElement(fonts.Elements().ElementAt(15));
        font.Element(Main + "sz")!.SetAttributeValue("val", fontSize);
        var fontId = fonts.Elements().Count();
        fonts.Add(font);
        fonts.SetAttributeValue("count", fontId + 1);
        var formats = styles.Root.Element(Main + "cellXfs")!;
        var result = new Dictionary<string, string>();
        var templateStyles = sheet.Descendants(Main + "row")
            .Where(row => (int)row.Attribute("r")! >= FirstDataRow && (int)row.Attribute("r")! < footerRow)
            .Elements(Main + "c").Select(cell => (int?)cell.Attribute("s") ?? 0).Distinct().ToArray();
        foreach (var index in templateStyles)
        {
            var style = new XElement(formats.Elements().ElementAt(index));
            style.SetAttributeValue("fontId", fontId);
            var alignment = style.Element(Main + "alignment")!;
            alignment.SetAttributeValue("wrapText", "1");
            result[index.ToString(CultureInfo.InvariantCulture)] = formats.Elements().Count().ToString(CultureInfo.InvariantCulture);
            formats.Add(style);
        }
        formats.SetAttributeValue("count", formats.Elements().Count());
        return result;
    }

    private static void ConfigurePrintLayout(XDocument sheet)
    {
        var root = sheet.Root!;
        root.Element(Main + "sheetPr")!.Element(Main + "pageSetUpPr")!.SetAttributeValue("fitToPage", "1");
        var printOptions = root.Element(Main + "printOptions")!;
        printOptions.SetAttributeValue("horizontalCentered", "1");
        printOptions.SetAttributeValue("verticalCentered", "1");
        var margins = root.Element(Main + "pageMargins")!;
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margins.SetAttributeValue(side, "0.2");
        var setup = root.Element(Main + "pageSetup")!;
        setup.SetAttributeValue("paperSize", "11"); // A5, matching the supplied form.
        setup.SetAttributeValue("orientation", "portrait");
        setup.SetAttributeValue("fitToWidth", "1");
        setup.SetAttributeValue("fitToHeight", "1");
        setup.SetAttributeValue("usePrinterDefaults", "0");
        setup.SetAttributeValue("scale", null);
        setup.SetAttributeValue("horizontalDpi", null);
        setup.SetAttributeValue("verticalDpi", null);
        // Do not let the template author's printer driver override the portable page layout.
        setup.SetAttributeValue(OfficeRelations + "id", null);
    }

    private static void SetRightToLeftTextDirection(XDocument sheet, XDocument styles,
        string[] sharedStrings, Dictionary<string, string> rightToLeftStyles)
    {
        var formats = styles.Root!.Element(Main + "cellXfs")!;
        foreach (var cell in sheet.Descendants(Main + "c"))
        {
            var text = (string?)cell.Attribute("t") == "s"
                ? sharedStrings[(int)cell.Element(Main + "v")!]
                : string.Concat(cell.Descendants(Main + "t").Select(value => value.Value));
            if (!text.Any(character => char.IsLetter(character) &&
                    (character is >= '\u0590' and <= '\u08ff' or >= '\ufb1d' and <= '\ufefc'))) continue;
            var originalStyle = (string?)cell.Attribute("s") ?? "0";
            if (!rightToLeftStyles.TryGetValue(originalStyle, out var rightToLeftStyle))
            {
                var format = new XElement(formats.Elements().ElementAt(int.Parse(originalStyle, CultureInfo.InvariantCulture)));
                var alignment = format.Element(Main + "alignment");
                if (alignment is null)
                {
                    alignment = new XElement(Main + "alignment");
                    var following = format.Elements().FirstOrDefault(element => element.Name.LocalName is "protection" or "extLst");
                    if (following is null) format.Add(alignment);
                    else following.AddBeforeSelf(alignment);
                }
                // Sheet RTL controls column order; cell readingOrder controls neutral punctuation.
                alignment.SetAttributeValue("readingOrder", "2");
                format.SetAttributeValue("applyAlignment", "1");
                rightToLeftStyle = formats.Elements().Count().ToString(CultureInfo.InvariantCulture);
                formats.Add(format);
                rightToLeftStyles.Add(originalStyle, rightToLeftStyle);
            }
            cell.SetAttributeValue("s", rightToLeftStyle);
        }
        formats.SetAttributeValue("count", formats.Elements().Count());
    }

    private static void SetText(XDocument sheet, string address, string text, Dictionary<string, string>? styles = null)
    {
        var cell = sheet.Descendants(Main + "c").Single(element => (string?)element.Attribute("r") == address);
        if (styles is not null && styles.TryGetValue((string?)cell.Attribute("s") ?? "", out var style))
            cell.SetAttributeValue("s", style);
        cell.SetAttributeValue("t", "inlineStr");
        cell.ReplaceNodes(new XElement(Main + "is", new XElement(Main + "t",
            new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
    }

    private static void UpdateSheetMetadata(Dictionary<string, byte[]> parts, List<string> names)
    {
        var properties = ReadXml(parts, "docProps/app.xml");
        XNamespace extended = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
        XNamespace vector = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
        var titles = properties.Root!.Element(extended + "TitlesOfParts")!.Element(vector + "vector")!;
        titles.SetAttributeValue("size", names.Count);
        titles.ReplaceNodes(names.Select(name => new XElement(vector + "lpstr", name)));
        properties.Root.Element(extended + "HeadingPairs")!.Descendants(vector + "i4").First().Value = names.Count.ToString(CultureInfo.InvariantCulture);
        parts["docProps/app.xml"] = WriteXml(properties);
    }

    private static XDocument ReadXml(Dictionary<string, byte[]> parts, string path)
    {
        using var stream = new MemoryStream(parts[path]);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static byte[] WriteXml(XDocument xml)
    {
        using var output = new MemoryStream();
        xml.Save(output, SaveOptions.DisableFormatting);
        return output.ToArray();
    }

    private static void AddContentType(XDocument types, string path, string contentType) =>
        types.Root!.Add(new XElement(types.Root.Name.Namespace + "Override",
            new XAttribute("PartName", path), new XAttribute("ContentType", contentType)));

    private static string PersianDate(DateTimeOffset date)
    {
        var local = TimeZoneInfo.ConvertTime(date, TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran")).DateTime;
        var calendar = new PersianCalendar();
        return PersianDigits(FormattableString.Invariant($"{calendar.GetYear(local):0000}/{calendar.GetMonth(local):00}/{calendar.GetDayOfMonth(local):00}"));
    }

    private static string PersianDigits(string value) => string.Concat(value.Select(character =>
        character is >= '0' and <= '9' ? (char)('۰' + character - '0') : character));

    private sealed record PrintRow(string Number, string Product, string Description, string Quantity,
        string Unit, string Returnable, string ReturnDate);
    private const int FirstDataRow = 9;
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelations = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelations = "http://schemas.openxmlformats.org/package/2006/relationships";
}
