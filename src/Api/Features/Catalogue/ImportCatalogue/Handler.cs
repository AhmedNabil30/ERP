using System.Globalization;
using Kaff.Api.Common;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Catalogue.ImportCatalogue;

/// <summary>
/// Loads the catalogue from the template <c>.xlsx</c>. KAFF-200.
/// </summary>
/// <remarks>
/// <para>
/// <b>The good rows import; the bad ones are named.</b> Rule 7, <c>AC-200-H</c> — this is not
/// all-or-nothing. Every row is validated independently and a refusal on one never removes another
/// row already queued. The whole batch is still written in one <c>SaveChangesAsync</c>: the
/// in-memory checks against the file's own codes and against the catalogue's existing codes already
/// rule out every duplicate this story's acceptance criteria name, so the only way this single save
/// could still fail on a code collision is a second import racing this one for the same code at the
/// same instant — outside every case <c>AC-200-H</c> or `TC-2-008` describes, and not guarded here.
/// </para>
/// <para>
/// <b>Every guard the entity already has is the entity's, not this handler's</b> — the same rule
/// <c>CreateCatalogueItem.Handler</c> follows. A blank code, a blank Arabic description, a blank unit
/// or a negative price all come back as the domain factory's own refusal (D-136's row table, last
/// row), not a second copy of the check here.
/// </para>
/// <para>
/// <b>Money never touches a <c>double</c>.</b> <see cref="SheetCell.TryReadDecimal"/> reads a numeric
/// cell's text through <c>decimal.TryParse(..., NumberStyles.Float, ...)</c> and a text cell through
/// <c>DecimalText</c> — decisions.md D-135/D-136. A rate beyond four decimals is not refused: it
/// passes through <see cref="Money"/> unchanged, exactly like every other caller (D-144 §6).
/// </para>
/// <para>
/// <b>Audit.</b> D-136 question 1 (audit granularity, per import or per row) was routed to the
/// Architect and never ruled. This uses the existing mechanism as it stands rather than inventing a
/// second one: every created <see cref="CatalogueItem"/> gets its own <c>Created</c> record from
/// <c>AuditSaveChangesInterceptor</c> (per-row, for free), and <see cref="AuditEventKind.CatalogueImported"/>
/// adds the one fact no entity diff carries — the file name and the created count, in
/// <see cref="IAuditContext.Reason"/> — satisfying <c>AC-200-G</c> without a new mechanism. ⚑ Flagged:
/// if the Architect later rules a different shape, this is the one place to change.
/// </para>
/// </remarks>
internal static class Handler
{
    /// <summary>Decisions.md D-136 — a file-level ceiling, not a business limit.</summary>
    public const long MaxUploadBytes = 10L * 1024 * 1024;

    public static async Task<IResult> ImportAsync(
        IFormFile file,
        KaffDbContext database,
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(audit);

        if (file.Length == 0 || file.Length > MaxUploadBytes)
        {
            return ResultExtensions.Problem(MasterDataErrors.CatalogueImportFailed);
        }

        XlsxReadResult read;

        await using (Stream stream = file.OpenReadStream())
        {
            read = XlsxSheetReader.Read(stream);
        }

        if (!read.Success || read.Rows.Count == 0)
        {
            return ResultExtensions.Problem(MasterDataErrors.CatalogueImportFailed);
        }

        SheetRow headerRow = read.Rows[0];

        Dictionary<int, string?> headerText = headerRow.CellsByColumn
            .ToDictionary(pair => pair.Key, pair => pair.Value.Text);

        if (!CatalogueTemplate.TryMatch(headerText, out IReadOnlyDictionary<string, int> columnIndex))
        {
            return ResultExtensions.Problem(MasterDataErrors.CatalogueImportFailed);
        }

        List<SheetRow> dataRows = [.. read.Rows.Skip(1)];

        if (dataRows.Count == 0)
        {
            return ResultExtensions.Problem(MasterDataErrors.CatalogueImportFailed);
        }

        Dictionary<string, Guid> babIdByCode = await database.Babs
            .Select(bab => new { bab.Code, bab.Id })
            .ToDictionaryAsync(
                bab => bab.Code, bab => bab.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        HashSet<string> existingCodes = new(
            await database.CatalogueItems.Select(item => item.Code).ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<RowFailure> failures = [];
        List<CatalogueItem> created = [];

        foreach (SheetRow row in dataRows)
        {
            if (IsBlank(row, columnIndex))
            {
                continue;
            }

            ProcessRow(row, columnIndex, babIdByCode, existingCodes, seenInFile, created, failures);
        }

        if (created.Count > 0)
        {
            database.CatalogueItems.AddRange(created);
        }

        audit.SetReason(string.Create(
            CultureInfo.InvariantCulture, $"Imported {file.FileName}: {created.Count} item(s) created"));
        audit.Record<CatalogueItem>(AuditEventKind.CatalogueImported, subjectId: null);

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(created.Count, failures));
    }

    private static void ProcessRow(
        SheetRow row,
        IReadOnlyDictionary<string, int> columnIndex,
        Dictionary<string, Guid> babIdByCode,
        HashSet<string> existingCodes,
        HashSet<string> seenInFile,
        List<CatalogueItem> created,
        List<RowFailure> failures)
    {
        string code = Text(row, columnIndex, CatalogueTemplate.Code);
        string descriptionAr = Text(row, columnIndex, CatalogueTemplate.DescriptionAr);
        string? descriptionEn = NullableText(row, columnIndex, CatalogueTemplate.DescriptionEn);
        string unit = Text(row, columnIndex, CatalogueTemplate.Unit);
        string babCode = Text(row, columnIndex, CatalogueTemplate.Bab);

        if (code.Length > 0)
        {
            if (seenInFile.Contains(code))
            {
                failures.Add(new RowFailure(
                    row.RowNumber, CatalogueTemplate.Code,
                    MasterDataErrors.CatalogueItemCodeRepeatedInFile.MessageKey, code));
                return;
            }

            if (existingCodes.Contains(code))
            {
                failures.Add(new RowFailure(
                    row.RowNumber, CatalogueTemplate.Code, MasterDataErrors.CatalogueItemCodeTaken.MessageKey, code));
                return;
            }
        }

        if (!babIdByCode.TryGetValue(babCode, out Guid babId))
        {
            failures.Add(new RowFailure(
                row.RowNumber, CatalogueTemplate.Bab, MasterDataErrors.BabNotFound.MessageKey, babCode));
            return;
        }

        SheetCell costCell = Cell(row, columnIndex, CatalogueTemplate.CostPrice);
        SheetCell sellCell = Cell(row, columnIndex, CatalogueTemplate.BaseSellRate);

        if (costCell.IsBlank)
        {
            failures.Add(new RowFailure(
                row.RowNumber, CatalogueTemplate.CostPrice, MasterDataErrors.CostPriceRequired.MessageKey, null));
            return;
        }

        if (!costCell.TryReadDecimal(out decimal costPriceValue))
        {
            failures.Add(new RowFailure(
                row.RowNumber, CatalogueTemplate.CostPrice, MasterDataErrors.CatalogueImportBadNumber.MessageKey,
                costCell.Text));
            return;
        }

        if (sellCell.IsBlank)
        {
            failures.Add(new RowFailure(
                row.RowNumber, CatalogueTemplate.BaseSellRate, MasterDataErrors.SellRateRequired.MessageKey, null));
            return;
        }

        if (!sellCell.TryReadDecimal(out decimal baseSellRateValue))
        {
            failures.Add(new RowFailure(
                row.RowNumber, CatalogueTemplate.BaseSellRate, MasterDataErrors.CatalogueImportBadNumber.MessageKey,
                sellCell.Text));
            return;
        }

        Money costPrice;
        Money baseSellRate;

        try
        {
            costPrice = Money.From(costPriceValue);
            baseSellRate = Money.From(baseSellRateValue);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A value decimal.TryParse accepted but too large for decimal(18,4) to store — the same
            // family of refusal as a malformed cell, not a crash.
            failures.Add(new RowFailure(
                row.RowNumber, CatalogueTemplate.CostPrice, MasterDataErrors.CatalogueImportBadNumber.MessageKey,
                null));
            return;
        }

        Result<CatalogueItem> item = CatalogueItem.Create(
            code, descriptionAr, unit, babId, costPrice, baseSellRate, descriptionEn);

        if (item.IsFailure)
        {
            failures.Add(new RowFailure(row.RowNumber, string.Empty, item.Error.MessageKey, null));
            return;
        }

        created.Add(item.Value);

        if (code.Length > 0)
        {
            seenInFile.Add(code);
        }
    }

    /// <summary>A row with every template cell empty — a formatted blank row Excel keeps, not one the user entered.</summary>
    private static bool IsBlank(SheetRow row, IReadOnlyDictionary<string, int> columnIndex)
        => CatalogueTemplate.Columns.All(column => Cell(row, columnIndex, column).IsBlank);

    private static SheetCell Cell(SheetRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
        => row.CellsByColumn.GetValueOrDefault(columnIndex[column], SheetCell.Blank);

    private static string Text(SheetRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
        => Cell(row, columnIndex, column).Text?.Trim() ?? string.Empty;

    private static string? NullableText(SheetRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
    {
        string text = Text(row, columnIndex, column);
        return text.Length == 0 ? null : text;
    }

    public static IResult DownloadTemplate()
        => Microsoft.AspNetCore.Http.Results.File(
            XlsxTemplateWriter.Write(), XlsxTemplateWriter.ContentType, CatalogueTemplate.FileName);
}
