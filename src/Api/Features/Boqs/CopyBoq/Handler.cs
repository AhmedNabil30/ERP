using Kaff.Api.Common.Results;
using Kaff.Domain.Authorization;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Boqs.CopyBoq;

/// <summary>
/// Fills an empty project BOQ from a prepared base or from an earlier project. Items and quantities come across and
/// stay editable; rates are re-read from today's catalogue; the hold and the advance are not copied (D-217 items 19, 20).
/// </summary>
/// <remarks>
/// <para>
/// From a base, each line becomes a catalogue line priced today, its quantity the estimated quantity. From a project, a
/// catalogue line is matched to the catalogue by its item code (the frozen line holds no key) and priced today; its
/// quantity is the project's one quantity, the surveyed value if there is one and the estimated otherwise (D-217 item 21),
/// and it comes across as estimated. A custom line comes across as a custom line with the values it stored.
/// </para>
/// <para>
/// An item the catalogue no longer has, or has archived, is skipped and named in <c>skipped</c>; nothing is invented.
/// </para>
/// </remarks>
internal static class Handler
{
    public const string SkipItemNotFound = "catalogue_item_not_found";
    public const string SkipItemArchived = "catalogue_item_archived";
    public const string SkipBabNotFound = "bab_not_found";

    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IPermissionSubjectReader subjectReader,
        IProjectAccessPolicy accessPolicy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);

        if ((request.BaseId is null) == (request.SourceProjectId is null) || request.SourceProjectId == projectId)
        {
            return ResultExtensions.Problem(BoqErrors.CopySourceInvalid);
        }

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        if (project.IsArchived(clock.GetUtcNow(), rulings.FollowUp))
        {
            return ResultExtensions.Problem(ProjectErrors.Archived);
        }

        if (request.SourceProjectId is { } sourceId
            && !await SourceProjectAccess.MayOpenBoqAsync(currentUser, subjectReader, accessPolicy, sourceId, cancellationToken))
        {
            return ResultExtensions.Problem(AuthorizationErrors.Forbidden);
        }

        Result<Boq> opened = await BoqOpener.GetOrOpenAsync(database, project, clock, cancellationToken);

        if (opened.IsFailure)
        {
            return ResultExtensions.Problem(opened.Error);
        }

        Boq boq = opened.Value;
        Result empty = boq.EnsureEmptyToFill();

        if (empty.IsFailure)
        {
            return ResultExtensions.Problem(empty.Error);
        }

        var added = new List<BoqLine>();
        var skipped = new List<SkippedLine>();

        Result copied = request.BaseId is { } baseId
            ? await CopyFromBaseAsync(boq, baseId, database, added, skipped, cancellationToken)
            : await CopyFromProjectAsync(boq, request.SourceProjectId!.Value, database, added, skipped, cancellationToken);

        if (copied.IsFailure)
        {
            return ResultExtensions.Problem(copied.Error);
        }

        if (added.Count > 0)
        {
            database.AddRange(added);

            Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

            if (refused is not null)
            {
                return ResultExtensions.Problem(refused);
            }
        }

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(
            [.. added.Select(line => BoqLineResponse.From(line, rulings.ContractValueSource))],
            skipped));
    }

    private static async Task<Result> CopyFromBaseAsync(
        Boq boq,
        Guid baseId,
        KaffDbContext database,
        List<BoqLine> added,
        List<SkippedLine> skipped,
        CancellationToken cancellationToken)
    {
        BoqBase? boqBase = await database.BoqBases
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.Id == baseId && candidate.ArchivedAt == null, cancellationToken);

        if (boqBase is null)
        {
            return Result.Failure(BoqErrors.BaseNotFound);
        }

        if (boqBase.Lines.Count == 0)
        {
            return Result.Failure(BoqErrors.CopySourceEmpty);
        }

        Guid[] itemIds = [.. boqBase.Lines.Select(line => line.CatalogueItemId).Distinct()];
        Dictionary<Guid, CatalogueItem> items = await database.CatalogueItems
            .AsNoTracking()
            .Where(item => itemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        Dictionary<Guid, Bab> babs = await BabsAsync(database, items.Values.Select(item => item.BabId), cancellationToken);

        foreach (BoqBaseLine line in boqBase.Lines.OrderBy(candidate => candidate.Position))
        {
            if (!items.TryGetValue(line.CatalogueItemId, out CatalogueItem? item))
            {
                skipped.Add(new SkippedLine(null, null, SkipItemNotFound));
                continue;
            }

            Result skip = AddCatalogue(boq, item, babs, line.Quantity, added, skipped);

            if (skip.IsFailure)
            {
                return skip;
            }
        }

        return Result.Success();
    }

    private static async Task<Result> CopyFromProjectAsync(
        Boq boq,
        Guid sourceProjectId,
        KaffDbContext database,
        List<BoqLine> added,
        List<SkippedLine> skipped,
        CancellationToken cancellationToken)
    {
        bool sourceExists = await database.Projects.AnyAsync(candidate => candidate.Id == sourceProjectId, cancellationToken);

        if (!sourceExists)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }

        Boq? source = await database.Boqs
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == sourceProjectId, cancellationToken);

        if (source is null || source.Lines.Count == 0)
        {
            return Result.Failure(BoqErrors.CopySourceEmpty);
        }

        string[] codes = [.. source.Lines.Where(line => !line.IsCustom && line.ItemCode != null).Select(line => line.ItemCode!).Distinct()];
        Dictionary<string, CatalogueItem> items = await database.CatalogueItems
            .AsNoTracking()
            .Where(item => codes.Contains(item.Code))
            .ToDictionaryAsync(item => item.Code, cancellationToken);
        string[] babCodes = [.. source.Lines.Where(line => line.IsCustom).Select(line => line.BabCode).Distinct()];
        Dictionary<Guid, Bab> babs = await BabsAsync(database, items.Values.Select(item => item.BabId), cancellationToken);
        Dictionary<string, Bab> babsByCode = await database.Babs
            .AsNoTracking()
            .Where(bab => babCodes.Contains(bab.Code))
            .ToDictionaryAsync(bab => bab.Code, cancellationToken);

        foreach (BoqLine line in source.Lines.OrderBy(candidate => candidate.Position))
        {
            decimal? quantity = line.QuantitySurveyed ?? line.QuantityEstimated;

            if (line.IsCustom)
            {
                if (!babsByCode.TryGetValue(line.BabCode, out Bab? customBab))
                {
                    skipped.Add(new SkippedLine(line.ItemCode, line.DescriptionAr, SkipBabNotFound));
                    continue;
                }

                Result<BoqLine> custom = boq.AddCustomLine(
                    line.ItemCode, line.DescriptionAr, line.DescriptionEn, line.Unit, customBab,
                    line.CostPrice, line.BaseSellRate, quantity, null, line.ConditionsRate, line.LineMarkup);

                if (custom.IsFailure)
                {
                    return Result.Failure(custom.Error);
                }

                added.Add(custom.Value);
                continue;
            }

            if (line.ItemCode is null || !items.TryGetValue(line.ItemCode, out CatalogueItem? item))
            {
                skipped.Add(new SkippedLine(line.ItemCode, line.DescriptionAr, SkipItemNotFound));
                continue;
            }

            Result skip = AddCatalogue(boq, item, babs, quantity, added, skipped);

            if (skip.IsFailure)
            {
                return skip;
            }
        }

        return Result.Success();
    }

    /// <summary>Adds a catalogue line priced today, or records why the item was skipped. Only an unexpected refusal fails.</summary>
    private static Result AddCatalogue(
        Boq boq,
        CatalogueItem item,
        Dictionary<Guid, Bab> babs,
        decimal? quantity,
        List<BoqLine> added,
        List<SkippedLine> skipped)
    {
        if (item.Status == CatalogueItemStatus.Archived)
        {
            skipped.Add(new SkippedLine(item.Code, item.DescriptionAr, SkipItemArchived));
            return Result.Success();
        }

        if (!babs.TryGetValue(item.BabId, out Bab? bab))
        {
            skipped.Add(new SkippedLine(item.Code, item.DescriptionAr, SkipBabNotFound));
            return Result.Success();
        }

        Result<BoqLine> line = boq.AddCatalogueLine(item, bab, quantity, null);

        if (line.IsFailure)
        {
            return Result.Failure(line.Error);
        }

        added.Add(line.Value);
        return Result.Success();
    }

    private static async Task<Dictionary<Guid, Bab>> BabsAsync(
        KaffDbContext database, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        Guid[] wanted = [.. ids.Distinct()];

        return await database.Babs.AsNoTracking().Where(bab => wanted.Contains(bab.Id)).ToDictionaryAsync(bab => bab.Id, cancellationToken);
    }
}

/// <summary>An item left out of the copy. <c>Reason</c> is a stable code the screen translates.</summary>
public sealed record SkippedLine(string? ItemCode, string? DescriptionAr, string Reason);

public sealed record Response(IReadOnlyList<BoqLineResponse> Lines, IReadOnlyList<SkippedLine> Skipped);
