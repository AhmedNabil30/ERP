using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Extracts.RejectExtract;

/// <summary>
/// Rejects at the gate the route names. When the ruled accrual moment has passed, the standing accrual postings
/// are handed to <c>Extract.Reject</c>, which writes reversing postings referencing the originals through
/// <c>ReversesId</c> (AC-500-G). Nothing here updates or deletes a posting, and there is no path that could.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        ExtractStatus[] waitingAt,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(waitingAt);

        return ExtractSupport.RunAsync(
            projectId,
            id,
            needsAccounts: true,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            async (extract, project, context, services, token) =>
            {
                if (!waitingAt.Contains(extract.Status))
                {
                    return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.NotWaitingAtThisGate);
                }

                IReadOnlyList<ExtractPostedLeg> legs =
                    await ExtractSupport.StandingAccrualLegsAsync(services.Database, extract.Id, token);

                return extract.Reject(
                    services.CurrentUser.UserId ?? Guid.Empty, request.Reason, context!, legs, services.Clock.GetUtcNow());
            },
            cancellationToken);
    }
}
