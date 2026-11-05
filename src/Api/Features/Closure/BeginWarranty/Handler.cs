using Kaff.Domain.Closure;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Closure.BeginWarranty;

/// <summary>Handover to UnderWarranty. The warranty dates were stamped on entering Handover and are not touched here.</summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, TimeProvider clock, CancellationToken cancellationToken) =>
        ClosureSupport.RunAsync(
            projectId,
            database,
            (project, _) => Task.FromResult(ClosureOutcome.Of(ProjectClosure.BeginWarranty(project, ClosureSupport.Today(clock)))),
            cancellationToken);
}
