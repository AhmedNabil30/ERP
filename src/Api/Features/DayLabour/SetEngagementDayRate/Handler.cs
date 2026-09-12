using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DayLabour.SetEngagementDayRate;

/// <summary>
/// Records the agreed day rate on an engagement. KAFF-210, decisions.md D-152 §2 (Q76), D-153 §1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 6a boundary</b>, same as every other engagement route: the route's project must be the
/// engagement's own, because the permission gate only knows the route's project.
/// </para>
/// <para>
/// <b>Write: refused unless the caller is the Owner or the engagement's opener.</b> D-153 §1 point 6 —
/// Finance reaches <c>DayLabourRateManage</c> and reads a rate, but never writes one: Finance was not
/// on site to agree anything. <see cref="Kaff.Api.Features.DayLabour.EngagementResponsibility"/> is the
/// one guard this shares with <c>RateEngagement.Handler</c>.
/// </para>
/// <para>
/// <b>Refused on a closed engagement</b> — <c>Engagement.SetDayRate</c>'s own guard: the stretch has
/// ended and its terms are history (KAFF-210 rule 6b's analogy, D-153 §1 point 6).
/// </para>
/// <para>
/// <b>Audited as a state change</b> by <c>AuditSaveChangesInterceptor</c>, before and after, with no
/// hand-written record — the same mechanism every entity change in this codebase uses.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid engagementId,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(currentUser);

        Engagement? engagement = await database.Engagements
            .SingleOrDefaultAsync(candidate => candidate.Id == engagementId, cancellationToken);

        if (engagement is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.EngagementNotFound);
        }

        // Rule 6a: the route's project must be this engagement's own project.
        if (engagement.ProjectId != projectId)
        {
            return ResultExtensions.Problem(MasterDataErrors.EngagementProjectMismatch);
        }

        if (!await EngagementResponsibility.IsResponsibleOrOwnerAsync(
            database, currentUser.UserId, engagement, cancellationToken))
        {
            return ResultExtensions.Problem(MasterDataErrors.EngagementNotResponsibleEngineer);
        }

        if (request.DayRate is not { } dayRate)
        {
            return ResultExtensions.Problem(MasterDataErrors.EngagementDayRateRequired);
        }

        Result set = engagement.SetDayRate(dayRate);

        if (set.IsFailure)
        {
            return ResultExtensions.Problem(set.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(engagement.Id, engagement.DayRate!.Value));
    }
}
