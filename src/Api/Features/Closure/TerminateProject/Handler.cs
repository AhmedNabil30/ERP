using Kaff.Domain.Closure;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Closure.TerminateProject;

/// <summary>
/// Refuses, always. spec.md section 13 names Terminated and section 6.9 a termination settlement, and nothing says what
/// settles. No state changes and no posting is written (Rulings.TerminationSettlement).
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(Guid projectId, Request request, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ClosureSupport.RunAsync(
            projectId,
            database,
            (project, _) => Task.FromResult(ClosureOutcome.Of(ProjectClosure.Terminate(project))),
            cancellationToken);
    }
}
