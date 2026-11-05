using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Adjustments.RejectAdjustment;

/// <summary>The Owner rejects a raised adjustment with a stored reason. Nothing was posted and nothing is.</summary>
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

        return AdjustmentSupport.RunAsync(
            projectId,
            id,
            database,
            async (adjustment, _, token) =>
            {
                IReadOnlyCollection<Guid> alsoActed = await AdjustmentSupport.DisputeActorsAsync(database, adjustment, token);
                Result rejected = adjustment.Reject(currentUser.UserId ?? Guid.Empty, alsoActed, request.Reason, clock.GetUtcNow());
                return rejected.IsFailure
                    ? Result.Failure<IReadOnlyList<Posting>>(rejected.Error)
                    : Result.Success<IReadOnlyList<Posting>>([]);
            },
            cancellationToken);
    }
}
