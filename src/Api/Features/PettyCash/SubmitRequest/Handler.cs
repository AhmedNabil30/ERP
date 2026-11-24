using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.PettyCash.SubmitRequest;

/// <summary>Draft to Submitted. Writes no posting.</summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
        => PettyCashDecision.RunAsync(
            projectId, id, (request, actor, now) => request.Submit(actor, now), database, clock, currentUser, cancellationToken);
}
