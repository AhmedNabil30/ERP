using Kaff.Api.Common.Results;
using Kaff.Domain.Adjustments;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Adjustments.GetAdjustment;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, Guid id, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Adjustment? adjustment = await database.Adjustments
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);

        return adjustment is null
            ? ResultExtensions.Problem(AdjustmentErrors.NotFound)
            : Microsoft.AspNetCore.Http.Results.Ok((await AdjustmentReads.BuildAsync(database, projectId, [adjustment], cancellationToken))[0]);
    }
}
