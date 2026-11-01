using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Boqs.RemoveBoqLine;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid lineId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Boq? boq = await database.Boqs
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);

        if (boq is null)
        {
            return ResultExtensions.Problem(BoqErrors.NotFound);
        }

        BoqLine? line = boq.Lines.FirstOrDefault(candidate => candidate.Id == lineId);

        Result removed = boq.RemoveLine(lineId);

        if (removed.IsFailure)
        {
            return ResultExtensions.Problem(removed.Error);
        }

        // The domain took the line out of the list; say so to EF as well rather than rely on orphan detection.
        database.Remove(line!);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
