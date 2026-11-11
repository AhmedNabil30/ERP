using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Boqs.AddBoqLines;

/// <summary>
/// Adds each named catalogue item by copy, exactly as the single add does, in one save: one refusal and nothing is
/// stored. The same item may be named twice; that keeps today's behaviour (D-217 item 22 is with Karim).
/// </summary>
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

        if (request.Items is not { Count: > 0 } items)
        {
            return ResultExtensions.Problem(BoqErrors.BatchEmpty);
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

        Result<Boq> opened = await BoqOpener.GetOrOpenAsync(database, project, clock, cancellationToken);

        if (opened.IsFailure)
        {
            return ResultExtensions.Problem(opened.Error);
        }

        Boq boq = opened.Value;

        Guid[] itemIds = [.. items.Select(item => item.CatalogueItemId).Distinct()];
        Dictionary<Guid, CatalogueItem> catalogue = await database.CatalogueItems
            .AsNoTracking()
            .Where(item => itemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        Guid[] babIds = [.. catalogue.Values.Select(item => item.BabId).Distinct()];
        Dictionary<Guid, Bab> babs = await database.Babs
            .AsNoTracking()
            .Where(bab => babIds.Contains(bab.Id))
            .ToDictionaryAsync(bab => bab.Id, cancellationToken);

        var added = new List<BoqLine>();

        foreach (BoqLineItem item in items)
        {
            if (!catalogue.TryGetValue(item.CatalogueItemId, out CatalogueItem? found))
            {
                return ResultExtensions.Problem(MasterDataErrors.CatalogueItemNotFound);
            }

            if (!babs.TryGetValue(found.BabId, out Bab? bab))
            {
                return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
            }

            Result<BoqLine> line = boq.AddCatalogueLine(found, bab, item.QuantityEstimated, item.QuantitySurveyed);

            if (line.IsFailure)
            {
                return ResultExtensions.Problem(line.Error);
            }

            added.Add(line.Value);
        }

        database.AddRange(added);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/boq",
            new Response([.. added.Select(line => BoqLineResponse.From(line, rulings.ContractValueSource))]));
    }
}

public sealed record Response(IReadOnlyList<BoqLineResponse> Lines);
