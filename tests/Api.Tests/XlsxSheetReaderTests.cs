using Kaff.Api.Common;
using Kaff.Api.Tests.Infrastructure;

namespace Kaff.Api.Tests;

/// <summary>
/// <c>XlsxSheetReader</c> — decisions.md D-136's own list of cases a hand-rolled <c>.xlsx</c> reader
/// must cover. Pure format plumbing, no database and no HTTP: every test here builds bytes with
/// <c>TestWorkbook</c> and reads them back, independent of what <c>XlsxTemplateWriter</c> happens to
/// produce, so the reader and the writer drifting apart would still be caught here.
/// </summary>
public sealed class XlsxSheetReaderTests
{
    // ---- a shared-string cell and an inline-string cell -----------------------------------------

    [Fact]
    public void A_shared_string_cell_and_an_inline_string_cell_both_read_as_text()
    {
        string sheetData = TestWorkbook.Row(1, TestWorkbook.SharedString(0, 1, 0), TestWorkbook.InlineText(1, 1, "خرسانة"));
        byte[] file = TestWorkbook.Build(sheetData, TestWorkbook.SharedStrings("CON-001"));

        XlsxReadResult result = ReadFrom(file);

        result.Success.Should().BeTrue();
        SheetRow row = result.Rows.Single();
        row.CellsByColumn[0].Should().Be(new SheetCell("CON-001", CellKind.Text));
        row.CellsByColumn[1].Should().Be(new SheetCell("خرسانة", CellKind.Text));
    }

    // ---- a numeric <v> in exponent form ------------------------------------------------------

    [Fact]
    public void A_numeric_cell_in_exponent_form_reads_as_the_exact_decimal_never_a_double()
    {
        string sheetData = TestWorkbook.Row(1, TestWorkbook.Number(0, 1, "1.5E+3"));
        byte[] file = TestWorkbook.Build(sheetData);

        SheetRow row = ReadFrom(file).Rows.Single();

        row.CellsByColumn[0].Kind.Should().Be(CellKind.Numeric);
        row.CellsByColumn[0].TryReadDecimal(out decimal value).Should().BeTrue();
        value.Should().Be(1500m);
    }

    // ---- a formula cell with a cached value, and one without ------------------------------------

    [Fact]
    public void A_formula_cells_cached_value_is_read_and_an_uncached_formula_is_blank()
    {
        string sheetData = TestWorkbook.Row(
            1,
            TestWorkbook.FormulaCached(0, 1, "A1*1.15", "1149.9999999999998"),
            TestWorkbook.FormulaNoCache(1, 1, "B1*1.15"));

        byte[] file = TestWorkbook.Build(sheetData);

        SheetRow row = ReadFrom(file).Rows.Single();

        row.CellsByColumn[0].Kind.Should().Be(CellKind.Numeric);
        row.CellsByColumn[0].TryReadDecimal(out decimal cached).Should().BeTrue();
        cached.Should().Be(1149.9999999999998m);

        row.CellsByColumn[1].IsBlank.Should().BeTrue("a formula with no cached value counts as a missing cell — D-136");
    }

    // ---- a rich-text shared string ----------------------------------------------------------

    [Fact]
    public void A_rich_text_shared_string_concatenates_every_run()
    {
        string sheetData = TestWorkbook.Row(1, TestWorkbook.SharedString(0, 1, 0));
        byte[] file = TestWorkbook.Build(sheetData, TestWorkbook.SharedStringsWithRichText(["خرسانة ", "عادية"]));

        SheetRow row = ReadFrom(file).Rows.Single();

        row.CellsByColumn[0].Should().Be(new SheetCell("خرسانة عادية", CellKind.Text));
    }

    // ---- a Strict-namespace file ------------------------------------------------------------

    [Fact]
    public void A_strict_namespace_file_reads_the_same_way()
    {
        string sheetData = TestWorkbook.Row(1, TestWorkbook.InlineText(0, 1, "م٣"));
        byte[] file = TestWorkbook.Build(sheetData, strictNamespace: true);

        XlsxReadResult result = ReadFrom(file);

        result.Success.Should().BeTrue("elements and attributes are matched by local name, not by namespace URI");
        result.Rows.Single().CellsByColumn[0].Text.Should().Be("م٣");
    }

    // ---- boolean and error cells refuse as money -------------------------------------------

    [Fact]
    public void A_boolean_cell_and_an_error_cell_both_refuse_as_a_decimal()
    {
        string sheetData = TestWorkbook.Row(1, TestWorkbook.Boolean(0, 1, true), TestWorkbook.ErrorCell(1, 1, "#VALUE!"));
        byte[] file = TestWorkbook.Build(sheetData);

        SheetRow row = ReadFrom(file).Rows.Single();

        row.CellsByColumn[0].Kind.Should().Be(CellKind.Boolean);
        row.CellsByColumn[0].TryReadDecimal(out _).Should().BeFalse();

        row.CellsByColumn[1].Kind.Should().Be(CellKind.Error);
        row.CellsByColumn[1].TryReadDecimal(out _).Should().BeFalse();
    }

    // ---- a text cell goes through the wire grammar, not decimal.TryParse alone -----------------

    [Fact]
    public void A_text_money_cell_with_a_thousands_separator_refuses()
    {
        string sheetData = TestWorkbook.Row(1, TestWorkbook.InlineText(0, 1, "1,234.50"));
        byte[] file = TestWorkbook.Build(sheetData);

        SheetRow row = ReadFrom(file).Rows.Single();

        row.CellsByColumn[0].TryReadDecimal(out _).Should().BeFalse(
            "DecimalText's grammar refuses a thousands separator even though decimal.TryParse alone would not");
    }

    // ---- .xls refused at file level ----------------------------------------------------------

    [Fact]
    public void A_file_that_is_not_a_zip_is_refused_at_the_file_level()
    {
        byte[] notAZip = [0xD0, 0xCF, 0x11, 0xE0, 0x00, 0x00, 0x00, 0x00]; // the BIFF/.xls magic bytes

        XlsxReadResult result = XlsxSheetReader.Read(new MemoryStream(notAZip));

        result.Success.Should().BeFalse();
        result.Failure.Should().Be(XlsxReadFailure.NotAZip);
    }

    // ---- rows above the ceiling are refused ---------------------------------------------------

    [Fact]
    public void A_file_above_the_row_ceiling_is_refused()
    {
        var rows = new List<string> { TestWorkbook.Row(1, TestWorkbook.InlineText(0, 1, "header")) };

        for (int i = 0; i < XlsxSheetReader.MaxDataRows + 1; i++)
        {
            int rowNumber = i + 2;
            rows.Add(TestWorkbook.Row(rowNumber, TestWorkbook.Number(0, rowNumber, i.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }

        byte[] file = TestWorkbook.Build(string.Concat(rows));

        XlsxReadResult result = ReadFrom(file);

        result.Success.Should().BeFalse();
        result.Failure.Should().Be(XlsxReadFailure.TooManyRows);
    }

    // ---- malformed XML never throws, never 500s ------------------------------------------------

    [Fact]
    public void Malformed_xml_is_reported_as_not_the_template_rather_than_thrown()
    {
        byte[] file = TestWorkbook.Build("<row r=\"1\"><c r=\"A1\"><v>unterminated");

        XlsxReadResult result = ReadFrom(file);

        result.Success.Should().BeFalse();
        result.Failure.Should().Be(XlsxReadFailure.MalformedXml);
    }

    // ---- a blank cell is distinct from a zero ---------------------------------------------------

    [Fact]
    public void An_empty_cell_is_blank_and_an_explicit_zero_is_not()
    {
        string sheetData = TestWorkbook.Row(1, TestWorkbook.Number(1, 1, "0"));
        byte[] file = TestWorkbook.Build(sheetData);

        SheetRow row = ReadFrom(file).Rows.Single();

        row.CellsByColumn.GetValueOrDefault(0, SheetCell.Blank).IsBlank.Should().BeTrue();
        row.CellsByColumn[1].IsBlank.Should().BeFalse("an explicit 0 is a value, not an absence — V-36-H's rule applies here too");
    }

    private static XlsxReadResult ReadFrom(byte[] file) => XlsxSheetReader.Read(new MemoryStream(file));
}
