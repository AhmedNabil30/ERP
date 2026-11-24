using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.PettyCash.ReturnRequest;

/// <summary>Draft to Returned with a stored reason. Writes no posting.</summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return PettyCashDecision.RunAsync(
            projectId, id, (found, actor, now) => found.Return(actor, request.Reason, now), database, clock, currentUser, cancellationToken);
    }
}
