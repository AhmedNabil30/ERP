using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Boqs.AddBoqLine;

/// <summary>
/// Adds one line to a project's BOQ, copying catalogue values into it (spec.md §4.4). Nothing here writes
/// to the catalogue, for a custom line as much as for a catalogue one (spec.md §4.5).
/// </summary>
/// <remarks>
/// A catalogue line copies the item's code, text, unit, cost and base sell rate, and the item's باب code
/// and names; the line keeps no key to either row. The markup defaults from the باب and the caller may
/// override it. Each new line is an ordinary tracked entity, so the interceptor audits it; no record is
/// written by hand.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);

        Project? project = await database.Projects
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        if (project.IsArchived(clock.GetUtcNow(), rulings.FollowUp))
        {
            return ResultExtensions.Problem(ProjectErrors.Archived);
        }

        Result<Boq> opened = await BoqOpener.GetOrOpenAsync(database, project, clock, cancellationToken);

        if (opened.IsFailure)
        {
            return ResultExtensions.Problem(opened.Error);
        }

        Boq boq = opened.Value;

        Result<BoqLine> added = request.CatalogueItemId is { } itemId
            ? await AddFromCatalogueAsync(boq, itemId, request, database, cancellationToken)
            : await AddCustomAsync(boq, request, database, cancellationToken);

        if (added.IsFailure)
        {
            return ResultExtensions.Problem(added.Error);
        }

        BoqLine line = added.Value;
        database.Add(line);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/boq",
            BoqLineResponse.From(line, rulings.ContractValueSource));
    }

    private static async Task<Result<BoqLine>> AddFromCatalogueAsync(
        Boq boq,
        Guid itemId,
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        bool mixed = request.DescriptionAr is not null || request.Unit is not null || request.BabId is not null
                     || request.CostPrice is not null || request.BaseSellRate is not null || request.ItemCode is not null;

        if (mixed)
        {
            return Result.Failure<BoqLine>(BoqErrors.LineShapeInvalid);
        }

        CatalogueItem? item = await database.CatalogueItems
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

        if (item is null)
        {
            return Result.Failure<BoqLine>(MasterDataErrors.CatalogueItemNotFound);
        }

        Bab? bab = await database.Babs
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == item.BabId, cancellationToken);

        if (bab is null)
        {
            return Result.Failure<BoqLine>(MasterDataErrors.BabNotFound);
        }

        return boq.AddCatalogueLine(
            item, bab, request.QuantityEstimated, request.QuantitySurveyed, request.ConditionsRate, request.LineMarkup);
    }

    private static async Task<Result<BoqLine>> AddCustomAsync(
        Boq boq,
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        if (request.BabId is not { } babId || request.CostPrice is not { } cost || request.BaseSellRate is not { } sell)
        {
            return Result.Failure<BoqLine>(BoqErrors.LineShapeInvalid);
        }

        Bab? bab = await database.Babs
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == babId, cancellationToken);

        if (bab is null)
        {
            return Result.Failure<BoqLine>(MasterDataErrors.BabNotFound);
        }

        return boq.AddCustomLine(
            request.ItemCode,
            request.DescriptionAr ?? string.Empty,
            request.DescriptionEn,
            request.Unit ?? string.Empty,
            bab,
            cost,
            sell,
            request.QuantityEstimated,
            request.QuantitySurveyed,
            request.ConditionsRate,
            request.LineMarkup);
    }
}
