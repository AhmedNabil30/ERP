using System.IO.Compression;
using System.Text;

namespace Kaff.Api.Features.Catalogue.ImportCatalogue;

/// <summary>
/// Writes the downloadable catalogue-import template, from the same column list
/// <see cref="CatalogueTemplate"/> validates against — decisions.md D-136. The file this endpoint
/// hands out is therefore a file <see cref="XlsxSheetReader"/> can read back exactly, which is the
/// whole point of D-129 §2. A checked-in static <c>.xlsx</c> was rejected for the same reason: it
/// would be a second copy of the column list, and the two could drift.
/// </summary>
/// <remarks>
/// Headers are written as inline strings (<c>t="inlineStr"</c>), so the file carries no
/// <c>sharedStrings.xml</c> part at all — one fewer part to build correctly for a one-row sheet.
/// </remarks>
public static class XlsxTemplateWriter
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Write()
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml());
            WriteEntry(archive, "_rels/.rels", RootRelsXml());
            WriteEntry(archive, "xl/workbook.xml", WorkbookXml());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", WorksheetXml());
        }

        return buffer.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Fastest);

        using Stream stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        writer.Write(content);
    }

    private static string ContentTypesXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/xml" />
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
        </Types>
        """;

    private static string RootRelsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
        </Relationships>
        """;

    private static string WorkbookXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="Catalogue" sheetId="1" r:id="rId1" />
          </sheets>
        </workbook>
        """;

    private static string WorkbookRelsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
        </Relationships>
        """;

    private static string WorksheetXml()
    {
        var cells = new StringBuilder();
        int columnIndex = 0;

        foreach (string header in CatalogueTemplate.Columns)
        {
            string cellRef = ColumnLetter(columnIndex) + "1";
            cells.Append(
                System.Globalization.CultureInfo.InvariantCulture,
                $"""<c r="{cellRef}" t="inlineStr"><is><t>{System.Security.SecurityElement.Escape(header)}</t></is></c>""");
            columnIndex++;
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>
                <row r="1">{cells}</row>
              </sheetData>
            </worksheet>
            """;
    }

    private static string ColumnLetter(int index)
    {
        // 0-based index to Excel column letters — A, B, ..., Z, AA, AB, ...
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
}
