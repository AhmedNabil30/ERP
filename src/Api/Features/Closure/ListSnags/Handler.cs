using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.ListSnags;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<Kaff.Domain.Closure.Snag> snags = await database.Snags
            .AsNoTracking()
            .Where(snag => snag.ProjectId == projectId)
            .OrderBy(snag => snag.Number)
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(snags.Select(SnagResponse.From).ToList());
    }
}
