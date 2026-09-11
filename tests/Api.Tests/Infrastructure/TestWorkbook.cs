using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Kaff.Api.Tests.Infrastructure;

/// <summary>
/// Builds minimal, hand-crafted <c>.xlsx</c> byte arrays for <c>XlsxSheetReaderTests</c> —
/// decisions.md D-136's own list of cases a hand-rolled reader must cover. Deliberately independent
/// of <c>XlsxTemplateWriter</c>: a reader test that only ever reads what the shipped writer produces
/// would never notice the writer and the reader drifting together.
/// </summary>
internal static class TestWorkbook
{
    private const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string StrictMainNamespace = "http://purl.oclc.org/ooxml/spreadsheetml/main";
    private const string RelationshipsNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string StrictRelationshipsNamespace =
        "http://purl.oclc.org/ooxml/officeDocument/relationships";

    public static byte[] Build(string sheetDataXml, string? sharedStringsXml = null, bool strictNamespace = false)
    {
        using var buffer = new MemoryStream();
        string mainNs = strictNamespace ? StrictMainNamespace : MainNamespace;
        string relNs = strictNamespace ? StrictRelationshipsNamespace : RelationshipsNamespace;

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
                  <Default Extension="xml" ContentType="application/xml" />
                </Types>
                """);

            WriteEntry(
                archive,
                "_rels/.rels",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="{relNs}/officeDocument" Target="xl/workbook.xml" />
                </Relationships>
                """);

            WriteEntry(
                archive,
                "xl/workbook.xml",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="{mainNs}" xmlns:r="{relNs}">
                  <sheets>
                    <sheet name="Catalogue" sheetId="1" r:id="rId1" />
                  </sheets>
                </workbook>
                """);

            WriteEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="{relNs}/worksheet" Target="worksheets/sheet1.xml" />
                </Relationships>
                """);

            WriteEntry(
                archive,
                "xl/worksheets/sheet1.xml",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <worksheet xmlns="{mainNs}">
                  <sheetData>
                    {sheetDataXml}
                  </sheetData>
                </worksheet>
                """);

            if (sharedStringsXml is not null)
            {
                WriteEntry(archive, "xl/sharedStrings.xml", sharedStringsXml);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>A single header row, columns as inline strings — the shape <c>XlsxTemplateWriter</c> writes.</summary>
    public static string HeaderRow(params string[] headers)
    {
        var cells = new StringBuilder();

        for (int i = 0; i < headers.Length; i++)
        {
            cells.Append(InlineText(i, 1, headers[i]));
        }

        return $"<row r=\"1\">{cells}</row>";
    }

    public static string Row(int rowNumber, params string[] cellsXml) =>
        $"<row r=\"{rowNumber.ToString(CultureInfo.InvariantCulture)}\">{string.Concat(cellsXml)}</row>";

    public static string InlineText(int column, int row, string text) =>
        $"""<c r="{Ref(column, row)}" t="inlineStr"><is><t>{System.Security.SecurityElement.Escape(text)}</t></is></c>""";

    public static string SharedString(int column, int row, int sharedStringIndex) =>
        $"""<c r="{Ref(column, row)}" t="s"><v>{sharedStringIndex.ToString(CultureInfo.InvariantCulture)}</v></c>""";

    public static string Number(int column, int row, string numberText) =>
        $"""<c r="{Ref(column, row)}"><v>{numberText}</v></c>""";

    public static string FormulaCached(int column, int row, string formula, string cachedValue) =>
        $"""<c r="{Ref(column, row)}"><f>{formula}</f><v>{cachedValue}</v></c>""";

    public static string FormulaNoCache(int column, int row, string formula) =>
        $"""<c r="{Ref(column, row)}"><f>{formula}</f></c>""";

    public static string Boolean(int column, int row, bool value) =>
        $"""<c r="{Ref(column, row)}" t="b"><v>{(value ? "1" : "0")}</v></c>""";

    public static string ErrorCell(int column, int row, string errorCode) =>
        $"""<c r="{Ref(column, row)}" t="e"><v>{errorCode}</v></c>""";

    /// <summary>One <c>&lt;si&gt;</c> whose text is spread across rich-text <c>&lt;r&gt;</c> runs.</summary>
    public static string SharedStringsWithRichText(params string[][] runsPerEntry)
    {
        var entries = new StringBuilder();

        foreach (string[] runs in runsPerEntry)
        {
            entries.Append("<si>");

            foreach (string run in runs)
            {
                entries.Append("<r><t>").Append(System.Security.SecurityElement.Escape(run)).Append("</t></r>");
            }

            entries.Append("</si>");
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <sst xmlns="{MainNamespace}">{entries}</sst>
            """;
    }

    public static string SharedStrings(params string[] plainEntries)
    {
        var entries = new StringBuilder();

        foreach (string entry in plainEntries)
        {
            entries.Append("<si><t>").Append(System.Security.SecurityElement.Escape(entry)).Append("</t></si>");
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <sst xmlns="{MainNamespace}">{entries}</sst>
            """;
    }

    private static string Ref(int column, int row) => ColumnLetter(column) + row.ToString(CultureInfo.InvariantCulture);

    private static string ColumnLetter(int index)
    {
        var letters = new StringBuilder();
        int value = index;

        do
        {
            letters.Insert(0, (char)('A' + (value % 26)));
            value = (value / 26) - 1;
        }
        while (value >= 0);

        return letters.ToString();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Fastest);

        using Stream stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        writer.Write(content);
    }
}
