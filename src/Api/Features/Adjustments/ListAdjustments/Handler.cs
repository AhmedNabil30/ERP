using Kaff.Domain.Adjustments;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Adjustments.ListAdjustments;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<Adjustment> adjustments = await database.Adjustments
            .AsNoTracking()
            .Where(candidate => candidate.ProjectId == projectId)
            .OrderBy(candidate => candidate.Number)
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(adjustments.Select(AdjustmentResponse.From).ToList());
    }
}
