using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.HandOverProject;

/// <summary>
/// HandoverPending to Handover. A major snag that is not verified refuses it and the refusal names the snags. The hold is
/// not touched here: its release is its own act, <c>ReleaseHold</c>.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, TimeProvider clock, CancellationToken cancellationToken) =>
        ClosureSupport.RunAsync(
            projectId,
            database,
            async (project, token) =>
            {
                List<Snag> snags = await database.Snags.Where(snag => snag.ProjectId == project.Id).ToListAsync(token);
                Result result = ProjectClosure.HandOver(project, ClosureSupport.Today(clock), snags);

                return result.IsFailure && result.Error == ClosureErrors.MajorSnagsOpen
                    ? new ClosureOutcome(
                        result,
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["snags"] = snags.Where(snag => snag.BlocksHandover).OrderBy(snag => snag.Number).Select(snag => snag.Number).ToList(),
                        })
                    : ClosureOutcome.Of(result);
            },
            cancellationToken);
}
