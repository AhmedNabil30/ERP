using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DayLabour.RateEngagement;

/// <summary>
/// Records a rating out of 5 for an engagement. KAFF-210 rule 7, decisions.md D-139 §3.
/// </summary>
/// <remarks>
/// Same rule 6a boundary as <c>CloseEngagement.Handler</c>: the route's project must be the
/// engagement's own, because the permission gate only knows the route's project.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid engagementId,
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Engagement? engagement = await database.Engagements
            .SingleOrDefaultAsync(candidate => candidate.Id == engagementId, cancellationToken);

        if (engagement is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.EngagementNotFound);
        }

        if (engagement.ProjectId != projectId)
        {
            return ResultExtensions.Problem(MasterDataErrors.EngagementProjectMismatch);
        }

        if (request.Rating is not { } rating)
        {
            return ResultExtensions.Problem(MasterDataErrors.EngagementRatingOutOfRange);
        }

        Result rated = engagement.Rate(rating);

        if (rated.IsFailure)
        {
            return ResultExtensions.Problem(rated.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(engagement.Id, engagement.Rating!.Value));
    }
}
