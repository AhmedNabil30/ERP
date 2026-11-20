using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.ReviseEvidence;

/// <summary>
/// Re-derives the certified work from the BOQ as it stands and replaces the evidence of a rejected Draft extract.
/// Writes no posting. Takes no body: D-235 item 4, the preparer enters no money, so the material values stay as the
/// supervising engineer entered them and are entered again with the next QC report.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        return ExtractSupport.RunAsync(
            projectId,
            id,
            needsAccounts: false,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            async (extract, project, context, services, token) =>
            {
                Boq? boq = await services.Database.Boqs
                    .AsNoTracking()
                    .Include(candidate => candidate.Lines)
                    .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, token);
                if (boq is null)
                {
                    return Result.Failure<IReadOnlyList<Kaff.Domain.Treasury.Posting>>(BoqErrors.NotFound);
                }

                if (!boq.IsFrozen)
                {
                    return Result.Failure<IReadOnlyList<Kaff.Domain.Treasury.Posting>>(DailyLogErrors.BoqNotFrozen);
                }

                Guid actor = services.CurrentUser.UserId ?? Guid.Empty;
                Result revised = extract.ReviseEvidence(
                    actor,
                    ExtractSupport.CumulativeCertifiedWork(boq, services.Rulings),
                    extract.MaterialValueOnSite,
                    extract.MaterialInstalled,
                    services.Clock.GetUtcNow());
                if (revised.IsSuccess)
                {
                    // D-248: the revised evidence keeps a new take of the quantities. The previous extract has not
                    // changed, so each line keeps the previous it was prepared with; no row is changed.
                    List<ExtractQuantityLine> kept = await ExtractQuantities.LatestAsync(services.Database, extract.Id, token);
                    services.Database.ExtractQuantityLines.AddRange(ExtractQuantityLine.Capture(
                        extract.Id,
                        kept.Count == 0 ? 1 : kept[0].Take + 1,
                        boq.Lines,
                        ExtractQuantities.Lookup(kept, line => line.PreviousQuantity)));
                }

                return ExtractSupport.NoPostingsOr(revised);
            },
            cancellationToken);
    }
}
