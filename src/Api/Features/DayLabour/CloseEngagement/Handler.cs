using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DayLabour.CloseEngagement;

/// <summary>
/// Closes an engagement — an explicit manual close, never a timeout. KAFF-210 rule 6, decisions.md
/// D-139 §3.
/// </summary>
/// <remarks>
/// <b>Rule 6a — the route's project must match the engagement's own.</b> The permission is
/// project-scoped to the route (decisions.md D-140), not to the engagement, so an engineer assigned to
/// project A could otherwise pair A's route with B's engagement id and close a project he has no
/// authority over. This is D-140's own SM-30 test 14
/// (<c>An_engagement_cannot_be_closed_through_another_projects_route</c>), and the check below is what
/// makes it pass. Forbidden (403), not NotFound: the id is real, just not this route's to touch.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid engagementId,
        KaffDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clock);

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

        DateTimeOffset now = clock.GetUtcNow();

        Result closed = engagement.Close(DateOnly.FromDateTime(now.UtcDateTime), now);

        if (closed.IsFailure)
        {
            return ResultExtensions.Problem(closed.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(engagement.Id, engagement.ClosedOn!.Value));
    }
}
