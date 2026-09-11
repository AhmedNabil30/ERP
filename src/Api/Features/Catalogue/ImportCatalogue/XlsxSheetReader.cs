using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.Catalogue.ImportCatalogue;

/// <summary>What kind of value a cell carries — decisions.md D-136.</summary>
public enum CellKind
{
    /// <summary>No <c>&lt;v&gt;</c> and no <c>&lt;is&gt;</c> — an empty cell, or a formula with no cached value.</summary>
    Blank = 0,
    Text = 1,
    Numeric = 2,
    Boolean = 3,
    Error = 4,
}

/// <summary>One cell, as the sheet stored it. Never a <c>double</c> anywhere in its path.</summary>
public readonly record struct SheetCell(string? Text, CellKind Kind)
{
    public static readonly SheetCell Blank = new(null, CellKind.Blank);

    public bool IsBlank => Kind == CellKind.Blank || string.IsNullOrEmpty(Text);

    /// <summary>
    /// Reads this cell as a decimal, per decisions.md D-135/D-136: a numeric cell's <c>&lt;v&gt;</c>
    /// text through <c>decimal.TryParse(..., NumberStyles.Float, ...)</c> — required because Excel
    /// writes exponent form such as <c>1E-4</c> — never a <c>double</c> accessor. A text cell goes
    /// through the same wire grammar <c>DecimalText</c> uses. A boolean or an error cell, and text
    /// that fails the grammar, all refuse.
    /// </summary>
    public bool TryReadDecimal(out decimal value)
    {
        value = default;

        return Kind switch
        {
            CellKind.Numeric => Text is not null
                && decimal.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            CellKind.Text => DecimalText.TryParse(Text, out value),
            _ => false,
        };
    }
}

/// <summary>One row, addressed by 0-based column index — the sheet's own <c>r</c> number, so a
/// refusal can name the row the user sees in Excel rather than an internal offset.</summary>
public sealed record SheetRow(int RowNumber, IReadOnlyDictionary<int, SheetCell> CellsByColumn);

/// <summary>Why <see cref="XlsxSheetReader.Read"/> could not produce rows. A single, file-level refusal.</summary>
public enum XlsxReadFailure
{
    None = 0,
    NotAZip,
    MissingWorkbookPart,
    UncompressedTooLarge,
    TooManyRows,
    MalformedXml,
}

/// <summary>The outcome of reading one <c>.xlsx</c> stream.</summary>
public sealed class XlsxReadResult
{
    private XlsxReadResult(bool success, XlsxReadFailure failure, IReadOnlyList<SheetRow> rows)
    {
        Success = success;
        Failure = failure;
        Rows = rows;
    }

    public bool Success { get; }

    public XlsxReadFailure Failure { get; }

    /// <summary>Every row the first worksheet holds, in document order, including the header row.</summary>
    public IReadOnlyList<SheetRow> Rows { get; }

    public static XlsxReadResult Ok(IReadOnlyList<SheetRow> rows) => new(true, XlsxReadFailure.None, rows);

    public static XlsxReadResult Fail(XlsxReadFailure failure) => new(false, failure, []);
}

/// <summary>
/// Reads Kaff's catalogue-import template by hand — <c>ZipArchive</c> plus a streaming
/// <c>XmlReader</c>, decisions.md D-136. <b>Pure format plumbing.</b> It turns a stream into rows of
/// column index → cell. It holds no Kaff rule: the header shape check is
/// <see cref="CatalogueTemplate.TryMatch"/>, and every business refusal belongs to the handler.
/// </summary>
/// <remarks>
/// Elements and attributes are matched by <b>local name</b>, so a file saved as Strict Open XML
/// (a different namespace, same shape) reads the same way. The worksheet is streamed, never loaded
/// as a DOM. DTDs are prohibited — <see cref="XmlReaderSettings.DtdProcessing"/> defaults to
/// <see cref="DtdProcessing.Prohibit"/>, and that default is kept rather than relaxed, which is what
/// closes XXE.
/// </remarks>
public static class XlsxSheetReader
{
    /// <summary>Guard against a zip bomb, not a business limit — decisions.md D-136.</summary>
    public const long MaxUncompressedBytes = 100L * 1024 * 1024;

    /// <summary>Kaff's catalogue is several hundred rows; this is a ceiling, not an expectation.</summary>
    public const int MaxDataRows = 10_000;

    private static readonly XmlReaderSettings Settings = new()
    {
        IgnoreWhitespace = false,
        DtdProcessing = DtdProcessing.Prohibit,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    public static XlsxReadResult Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        ZipArchive archive;

        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return XlsxReadResult.Fail(XlsxReadFailure.NotAZip);
        }

        using (archive)
        {
            long uncompressedTotal = 0;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                uncompressedTotal += entry.Length;

                if (uncompressedTotal > MaxUncompressedBytes)
                {
                    return XlsxReadResult.Fail(XlsxReadFailure.UncompressedTooLarge);
                }
            }

            ZipArchiveEntry? workbookEntry = archive.GetEntry("xl/workbook.xml");

            if (workbookEntry is null)
            {
                return XlsxReadResult.Fail(XlsxReadFailure.MissingWorkbookPart);
            }

            try
            {
                string? firstSheetRelId = ReadFirstSheetRelationshipId(workbookEntry);

                if (firstSheetRelId is null)
                {
                    return XlsxReadResult.Fail(XlsxReadFailure.MissingWorkbookPart);
                }

                string? worksheetPath = ResolveWorksheetPath(archive, firstSheetRelId);

                if (worksheetPath is null)
                {
                    return XlsxReadResult.Fail(XlsxReadFailure.MissingWorkbookPart);
                }

                ZipArchiveEntry? worksheetEntry = archive.GetEntry(worksheetPath);

                if (worksheetEntry is null)
                {
                    return XlsxReadResult.Fail(XlsxReadFailure.MissingWorkbookPart);
                }

                IReadOnlyList<string> sharedStrings = ReadSharedStrings(archive);

                return ReadWorksheet(worksheetEntry, sharedStrings);
            }
            catch (XmlException)
            {
                return XlsxReadResult.Fail(XlsxReadFailure.MalformedXml);
            }
            catch (InvalidDataException)
            {
                // A member's compressed bytes are themselves corrupt — same family as a bad zip,
                // caught later than the archive's central directory is. D-136: never a 500.
                return XlsxReadResult.Fail(XlsxReadFailure.MalformedXml);
            }
        }
    }

    // ---- xl/workbook.xml: the first <sheet>'s relationship id --------------------------------

    private static string? ReadFirstSheetRelationshipId(ZipArchiveEntry workbookEntry)
    {
        using Stream stream = workbookEntry.Open();
        using XmlReader reader = XmlReader.Create(stream, Settings);

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet")
            {
                return GetAttributeByLocalName(reader, "id");
            }
        }

        return null;
    }

    // ---- xl/_rels/workbook.xml.rels: that id's target ------------------------------------------

    private static string? ResolveWorksheetPath(ZipArchive archive, string relationshipId)
    {
        ZipArchiveEntry? relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

        if (relsEntry is null)
        {
            return null;
        }

        using Stream stream = relsEntry.Open();
        using XmlReader reader = XmlReader.Create(stream, Settings);

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship")
            {
                continue;
            }

            string? id = GetAttributeByLocalName(reader, "Id");

            if (!string.Equals(id, relationshipId, StringComparison.Ordinal))
            {
                continue;
            }

            string? target = GetAttributeByLocalName(reader, "Target");

            if (target is null)
            {
                return null;
            }

            return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        }

        return null;
    }

    // ---- xl/sharedStrings.xml: each <si> is every <t> descendant, concatenated ------------------

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");

        if (entry is null)
        {
            return [];
        }

        List<string> strings = [];
        StringBuilder? current = null;
        bool inText = false;

        using Stream stream = entry.Open();
        using XmlReader reader = XmlReader.Create(stream, Settings);

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element when reader.LocalName == "si":
                    current = new StringBuilder();
                    break;

                case XmlNodeType.Element when reader.LocalName == "t":
                    inText = true;
                    break;

                case XmlNodeType.EndElement when reader.LocalName == "t":
                    inText = false;
                    break;

                case XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace
                    when inText && current is not null:
                    current.Append(reader.Value);
                    break;

                case XmlNodeType.EndElement when reader.LocalName == "si":
                    strings.Add(current?.ToString() ?? string.Empty);
                    current = null;
                    break;
            }
        }

        return strings;
    }

    // ---- the worksheet itself: <row r="…"><c r="…" t="…"><v>/<is><t> -----------------------------

    private static XlsxReadResult ReadWorksheet(ZipArchiveEntry worksheetEntry, IReadOnlyList<string> sharedStrings)
    {
        List<SheetRow> rows = [];
        Dictionary<int, SheetCell>? currentRowCells = null;
        int currentRowNumber = 0;
        int nextRowNumber = 1;

        string? cellType = null;
        int cellColumnIndex = -1;
        int nextColumnIndex = 0;
        bool haveCellValue = false;
        bool inValue = false;
        bool inInlineText = false;
        StringBuilder? cellText = null;

        using Stream stream = worksheetEntry.Open();
        using XmlReader reader = XmlReader.Create(stream, Settings);

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element when reader.LocalName == "row":
                    currentRowCells = [];
                    currentRowNumber = ParseInt(GetAttributeByLocalName(reader, "r")) ?? nextRowNumber;
                    nextColumnIndex = 0;
                    break;

                case XmlNodeType.Element when reader.LocalName == "c":
                    cellType = GetAttributeByLocalName(reader, "t");
                    string? cellRef = GetAttributeByLocalName(reader, "r");
                    cellColumnIndex = cellRef is null
                        ? nextColumnIndex
                        : ColumnIndexFromCellReference(cellRef);
                    haveCellValue = false;
                    cellText = null;
                    break;

                case XmlNodeType.Element when reader.LocalName == "v":
                    inValue = true;
                    cellText = new StringBuilder();
                    break;

                case XmlNodeType.EndElement when reader.LocalName == "v":
                    inValue = false;
                    haveCellValue = true;
                    break;

                case XmlNodeType.Element when reader.LocalName == "is":
                    cellText = new StringBuilder();
                    haveCellValue = true;
                    break;

                case XmlNodeType.Element when reader.LocalName == "t":
                    inInlineText = true;
                    break;

                case XmlNodeType.EndElement when reader.LocalName == "t":
                    inInlineText = false;
                    break;

                case XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace
                    when (inValue || inInlineText) && cellText is not null:
                    cellText.Append(reader.Value);
                    break;

                case XmlNodeType.EndElement when reader.LocalName == "c":
                    // A <c> outside any <row> is a shape OOXML never produces — guarded explicitly
                    // rather than with the null-forgiving operator, so that shape is skipped instead
                    // of throwing (D-136: never a 500 on unparsable input).
#pragma warning disable IDE0031 // the suggested null-propagating form cannot target an indexer assignment
                    if (currentRowCells is not null)
#pragma warning restore IDE0031
                    {
                        currentRowCells[cellColumnIndex] =
                            BuildCell(cellType, haveCellValue, cellText?.ToString(), sharedStrings);
                    }

                    nextColumnIndex = cellColumnIndex + 1;
                    break;

                case XmlNodeType.EndElement when reader.LocalName == "row":
                    if (currentRowCells is not null)
                    {
                        rows.Add(new SheetRow(currentRowNumber, currentRowCells));

                        // Row 1 is the header (Excel always numbers from 1); every row after it is
                        // data, and that count is what the ceiling guards.
                        if (rows.Count - 1 > MaxDataRows)
                        {
                            return XlsxReadResult.Fail(XlsxReadFailure.TooManyRows);
                        }
                    }

                    currentRowCells = null;
                    nextRowNumber = currentRowNumber + 1;
                    break;
            }
        }

        return XlsxReadResult.Ok(rows);
    }

    private static SheetCell BuildCell(string? cellType, bool haveValue, string? text, IReadOnlyList<string> sharedStrings)
    {
        if (!haveValue)
        {
            return SheetCell.Blank;
        }

        if (cellType == "s")
        {
            int index = ParseInt(text) ?? -1;
            string resolved = index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : string.Empty;
            return new SheetCell(resolved, CellKind.Text);
        }

        return cellType switch
        {
            "inlineStr" => new SheetCell(text, CellKind.Text),
            "str" => new SheetCell(text, CellKind.Text),
            "b" => new SheetCell(text, CellKind.Boolean),
            "e" => new SheetCell(text, CellKind.Error),
            _ => new SheetCell(text, CellKind.Numeric),
        };
    }

    private static int ColumnIndexFromCellReference(string cellReference)
    {
        int index = 0;

        foreach (char c in cellReference)
        {
            if (c is < 'A' or > 'Z')
            {
                if (char.IsAsciiLetterLower(c))
                {
                    index = (index * 26) + (c - 'a' + 1);
                    continue;
                }

                break;
            }

            index = (index * 26) + (c - 'A' + 1);
        }

        return index - 1;
    }

    private static int? ParseInt(string? text)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : null;

    private static string? GetAttributeByLocalName(XmlReader reader, string localName)
    {
        if (!reader.HasAttributes)
        {
            return null;
        }

        for (bool has = reader.MoveToFirstAttribute(); has; has = reader.MoveToNextAttribute())
        {
            if (reader.LocalName == localName)
            {
                string value = reader.Value;
                reader.MoveToElement();
                return value;
            }
        }

        reader.MoveToElement();
        return null;
    }
}
