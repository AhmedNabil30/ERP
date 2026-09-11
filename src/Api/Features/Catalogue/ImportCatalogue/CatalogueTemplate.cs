namespace Kaff.Api.Features.Catalogue.ImportCatalogue;

/// <summary>
/// The one column list <c>KAFF-200</c> reads and writes — decisions.md D-136, D-144 §§3–5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two description columns, باب by <c>Code</c>, no <c>status</c> column.</b> Every imported item is
/// <c>Active</c> (D-144 §5); nothing on the sheet can say otherwise. <see cref="Columns"/> is read by
/// the header check (<c>AC-200-I</c>) and by <c>XlsxTemplateWriter</c> — the file this endpoint hands
/// out is therefore a file this endpoint can also read back, which is the whole point of D-129 §2.
/// </para>
/// <para>
/// Column order here is the order the template writes them in. The header check matches by
/// <b>name</b>, not by position — a file whose columns are reordered but otherwise complete is not
/// what <c>AC-200-I</c> refuses; a file with an extra or a missing column is.
/// </para>
/// </remarks>
public static class CatalogueTemplate
{
    public const string Code = "code";
    public const string DescriptionAr = "descriptionAr";
    public const string DescriptionEn = "descriptionEn";
    public const string Unit = "unit";
    public const string Bab = "bab";
    public const string CostPrice = "costPrice";
    public const string BaseSellRate = "baseSellRate";

    /// <summary>Exactly the template's columns, spec.md §4.1 · D-144 §§3–5. No <c>status</c>.</summary>
    public static readonly IReadOnlyList<string> Columns =
    [
        Code, DescriptionAr, DescriptionEn, Unit, Bab, CostPrice, BaseSellRate,
    ];

    /// <summary>The downloadable file's name — <c>AC-200-I</c>'s "reachable from S-019" half.</summary>
    public const string FileName = "catalogue-import-template.xlsx";

    /// <summary>
    /// Matches a header row against <see cref="Columns"/> by name, order-independent.
    /// </summary>
    /// <param name="headerRow">The sheet's first row, column index → cell.</param>
    /// <param name="columnIndexByName">
    /// On success, every template column name mapped to the file's column index for that name.
    /// </param>
    /// <returns><c>true</c> when the header row carries exactly the template's columns, no more and no fewer.</returns>
    public static bool TryMatch(
        IReadOnlyDictionary<int, SheetCell> headerRow,
        out IReadOnlyDictionary<string, int> columnIndexByName)
    {
        ArgumentNullException.ThrowIfNull(headerRow);

        var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach ((int index, SheetCell cell) in headerRow)
        {
            string? text = cell.Text?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            // Two cells naming the same header is a malformed template either way — refused below by
            // the count check, since byName then holds fewer entries than headerRow has non-blank cells.
            byName[text] = index;
        }

        columnIndexByName = byName;

        if (byName.Count != Columns.Count)
        {
            return false;
        }

        foreach (string column in Columns)
        {
            if (!byName.ContainsKey(column))
            {
                return false;
            }
        }

        return true;
    }
}
