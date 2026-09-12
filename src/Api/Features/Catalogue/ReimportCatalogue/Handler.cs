using System.Globalization;
using Kaff.Api.Common;
using Kaff.Api.Common.Results;
using Kaff.Api.Features.Catalogue.ImportCatalogue;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Catalogue.ReimportCatalogue;

/// <summary>
/// A second import over a catalogue that already holds items. KAFF-201.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a sync.</b> spec.md §4.1 forbids the catalogue tracking a spreadsheet — there is no
/// schedule, no watched folder, no background job here or anywhere in this codebase. Every change
/// this handler makes happens inside one human-triggered HTTP request.
/// </para>
/// <para>
/// <b>Preview then confirm, both stateless.</b> D-129 §4 (<c>Q62</c>): a second import is accepted,
/// its effect shown, and confirmed by a human. Rather than caching a preview server-side and trusting
/// a token back, <see cref="PreviewAsync"/> and <see cref="ConfirmAsync"/> both parse the same
/// uploaded file independently and compute the identical plan — the confirm call is simply the
/// preview call that also saves. Nothing changes until <see cref="ConfirmAsync"/> runs (<c>AC-201-B</c>),
/// and cancelling on the client is just not calling it — the catalogue was never touched.
/// </para>
/// <para>
/// <b>Existing codes are re-priced, never re-described.</b> <see cref="CatalogueItem.Reprice"/> only
/// ever touches <c>CostPrice</c> and <c>BaseSellRate</c> — rule 2. A code the catalogue does not yet
/// hold is created exactly as <c>KAFF-200</c> creates it.
/// </para>
/// <para>
/// <b>Audit reuses the one mechanism.</b> Every repriced item is a tracked entity mutated in place,
/// so <c>AuditSaveChangesInterceptor</c> writes its own <c>Modified</c> record with the before and
/// after of every property that actually moved — <c>AC-201-E</c> — with no new audit code. Every
/// created item gets its own <c>Created</c> record the same way KAFF-200's does. The
/// <see cref="AuditEventKind.CatalogueImported"/> event adds the one fact no entity diff carries: the
/// file name and both counts, in <see cref="IAuditContext.Reason"/>.
/// </para>
/// <para>
/// <b>Signed BOQs and open estimates are untouched by construction, not by a check here</b> —
/// <c>AC-201-C</c>, <c>AC-201-D</c>. Neither a BOQ nor an Estimate entity exists yet in this codebase
/// (both are slice 4); <see cref="CatalogueItem"/> carries no foreign key for either to hold, so there
/// is nothing this handler could reach even if it tried, and nothing to raise an alert through.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> PreviewAsync(
        IFormFile file, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(database);

        (Plan? plan, IResult? refusal) = await BuildPlanFromFileAsync(file, database, track: false, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        Plan built = plan!;

        return Microsoft.AspNetCore.Http.Results.Ok(new PreviewResponse(
            built.Created.Count,
            built.Repriced.Count,
            [.. built.Created.Select(item => ToPlannedCreate(item, built.BabCodeById))],
            [.. built.Repriced.Select(ToPlannedReprice)],
            built.Failures));
    }

    public static async Task<IResult> ConfirmAsync(
        IFormFile file, KaffDbContext database, IAuditContext audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(audit);

        (Plan? plan, IResult? refusal) = await BuildPlanFromFileAsync(file, database, track: true, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        Plan built = plan!;

        if (built.Created.Count > 0)
        {
            database.CatalogueItems.AddRange(built.Created);
        }

        // Repriced entities are already tracked (fetched without AsNoTracking below) and were mutated
        // in BuildPlan through CatalogueItem.Reprice — nothing further to attach.
        audit.SetReason(string.Create(
            CultureInfo.InvariantCulture,
            $"Re-imported {file.FileName}: {built.Created.Count} item(s) created, "
            + $"{built.Repriced.Count} item(s) re-priced"));
        audit.Record<CatalogueItem>(AuditEventKind.CatalogueImported, subjectId: null);

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            new ConfirmResponse(built.Created.Count, built.Repriced.Count, built.Failures));
    }

    /// <summary>One parse of the uploaded file into what it would create and what it would re-price.</summary>
    private sealed record Plan(
        List<CatalogueItem> Created,
        List<RepriceOutcome> Repriced,
        List<RowFailure> Failures,
        IReadOnlyDictionary<Guid, string> BabCodeById);

    private sealed record RepriceOutcome(
        string Code, Money OldCostPrice, Money OldBaseSellRate, CatalogueItem Item);

    private static async Task<(Plan? Plan, IResult? Refusal)> BuildPlanFromFileAsync(
        IFormFile file, KaffDbContext database, bool track, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > ImportCatalogue.Handler.MaxUploadBytes)
        {
            return (null, ResultExtensions.Problem(MasterDataErrors.CatalogueImportFailed));
        }

        XlsxReadResult read;

        await using (Stream stream = file.OpenReadStream())
        {
            read = XlsxSheetReader.Read(stream);
        }

        if (!read.Success || read.Rows.Count == 0)
        {
            return (null, ResultExtensions.Problem(MasterDataErrors.CatalogueImportFailed));
        }

        SheetRow headerRow = read.Rows[0];

        Dictionary<int, string?> headerText = headerRow.CellsByColumn
            .ToDictionary(pair => pair.Key, pair => pair.Value.Text);

        if (!CatalogueTemplate.TryMatch(headerText, out IReadOnlyDictionary<string, int> columnIndex))
        {
            return (null, ResultExtensions.Problem(MasterDataErrors.CatalogueImportFailed));
        }

        List<SheetRow> dataRows = [.. read.Rows.Skip(1)];

        if (dataRows.Count == 0)
        {
            return (null, ResultExtensions.Problem(MasterDataErrors.CatalogueImportFailed));
        }

        Dictionary<string, Guid> babIdByCode = await database.Babs
            .Select(bab => new { bab.Code, bab.Id })
            .ToDictionaryAsync(
                bab => bab.Code, bab => bab.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        IQueryable<CatalogueItem> existingQuery = database.CatalogueItems;

        if (!track)
        {
            existingQuery = existingQuery.AsNoTracking();
        }

        Dictionary<string, CatalogueItem> existingByCode = await existingQuery
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<RowFailure> failures = [];
        List<CatalogueItem> created = [];
        List<RepriceOutcome> repriced = [];

        foreach (SheetRow row in dataRows)
        {
            if (IsBlank(row, columnIndex))
            {
                continue;
            }

            ProcessRow(row, columnIndex, babIdByCode, existingByCode, seenInFile, created, repriced, failures);
        }

        Dictionary<Guid, string> babCodeById = new();

        foreach (KeyValuePair<string, Guid> pair in babIdByCode)
        {
            babCodeById[pair.Value] = pair.Key;
        }

        return (new Plan(created, repriced, failures, babCodeById), null);
    }

    private static void ProcessRow(
        SheetRow row,
        IReadOnlyDictionary<string, int> columnIndex,
        Dictionary<string, Guid> babIdByCode,
        Dictionary<string, CatalogueItem> existingByCode,
        HashSet<string> seenInFile,
        List<CatalogueItem> created,
        List<RepriceOutcome> repriced,
        List<RowFailure> failures)
    {
        string code = Text(row, columnIndex, CatalogueTemplate.Code);
        string descriptionAr = Text(row, columnIndex, CatalogueTemplate.DescriptionAr);
        string? descriptionEn = NullableText(row, columnIndex, CatalogueTemplate.DescriptionEn);
        string unit = Text(row, columnIndex, CatalogueTemplate.Unit);
        string babCode = Text(row, columnIndex, CatalogueTemplate.Bab);

        CatalogueItem? existingItem = null;
        bool isExisting = code.Length > 0 && existingByCode.TryGetValue(code, out existingItem);

        if (code.Length > 0)
        {
            if (seenInFile.Contains(code))
            {
                failures.Add(new RowFailure(
                    row.RowNumber, CatalogueTemplate.Code,
                    MasterDataErrors.CatalogueItemCodeRepeatedInFile.MessageKey, code));
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
            failures.Add(new RowFailure(
                row.RowNumber, CatalogueTemplate.CostPrice, MasterDataErrors.CatalogueImportBadNumber.MessageKey,
                null));
            return;
        }

        if (isExisting)
        {
            CatalogueItem item = existingItem!;
            Money oldCost = item.CostPrice;
            Money oldSell = item.BaseSellRate;

            Result repriceResult = item.Reprice(costPrice, baseSellRate);

            if (repriceResult.IsFailure)
            {
                failures.Add(new RowFailure(row.RowNumber, string.Empty, repriceResult.Error.MessageKey, null));
                return;
            }

            repriced.Add(new RepriceOutcome(code, oldCost, oldSell, item));
        }
        else
        {
            Result<CatalogueItem> itemResult = CatalogueItem.Create(
                code, descriptionAr, unit, babId, costPrice, baseSellRate, descriptionEn);

            if (itemResult.IsFailure)
            {
                failures.Add(new RowFailure(row.RowNumber, string.Empty, itemResult.Error.MessageKey, null));
                return;
            }

            created.Add(itemResult.Value);
        }

        if (code.Length > 0)
        {
            seenInFile.Add(code);
        }
    }

    private static PlannedCreate ToPlannedCreate(CatalogueItem item, IReadOnlyDictionary<Guid, string> babCodeById) => new(
        item.Code, item.DescriptionAr, item.DescriptionEn, item.Unit,
        babCodeById.GetValueOrDefault(item.BabId, string.Empty), item.CostPrice.Amount, item.BaseSellRate.Amount);

    private static PlannedReprice ToPlannedReprice(RepriceOutcome outcome) => new(
        outcome.Code,
        outcome.OldCostPrice.Amount, outcome.OldBaseSellRate.Amount,
        outcome.Item.CostPrice.Amount, outcome.Item.BaseSellRate.Amount);

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
}
