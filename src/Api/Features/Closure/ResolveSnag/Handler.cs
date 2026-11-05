using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Closure.ResolveSnag;

/// <summary>
/// Records the dual toggle. Nothing is posted and the hold is not referenced: a snag never takes anything out of it
/// (spec.md section 5.1). The reason goes onto the audit record as well as the snag.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audit);

        return ClosureSupport.MutateSnagAsync(
            projectId,
            id,
            database,
            snag =>
            {
                Kaff.Domain.Common.Result resolved = snag.Resolve(request.Kind, request.Reason, currentUser.UserId ?? Guid.Empty, clock.GetUtcNow());
                if (resolved.IsSuccess)
                {
                    audit.SetReason(request.Reason!.Trim());
                }

                return resolved;
            },
            cancellationToken);
    }
}
