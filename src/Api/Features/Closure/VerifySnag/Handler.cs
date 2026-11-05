using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Closure.VerifySnag;

/// <summary>Fixed to verified. Verifying an open snag is refused by the snag's own transition table.</summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId, Guid id, KaffDbContext database, TimeProvider clock, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        ClosureSupport.MutateSnagAsync(
            projectId, id, database, snag => snag.Verify(currentUser.UserId ?? Guid.Empty, clock.GetUtcNow()), cancellationToken);
}
