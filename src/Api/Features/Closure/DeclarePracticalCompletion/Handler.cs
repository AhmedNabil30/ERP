using Kaff.Domain.Closure;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Closure.DeclarePracticalCompletion;

/// <summary>Active to HandoverPending. The domain refuses every other source state and a Design project.</summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, TimeProvider clock, CancellationToken cancellationToken) =>
        ClosureSupport.RunAsync(
            projectId,
            database,
            (project, _) => Task.FromResult(ClosureOutcome.Of(ProjectClosure.DeclarePracticalCompletion(project, ClosureSupport.Today(clock)))),
            cancellationToken);
}
