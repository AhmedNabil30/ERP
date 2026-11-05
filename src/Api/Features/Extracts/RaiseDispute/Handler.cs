using System.Security.Claims;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Extracts.RaiseDispute;

/// <summary>
/// Issued to Disputed with a stored reason. No posting is written, edited or reversed: the extract's postings, the hold
/// accrual among them, stand while the dispute is open (<c>Rulings.ExtractDisputeEffect</c>).
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExtractSupport.RunAsync(
            projectId,
            id,
            needsAccounts: false,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            (extract, _, _, services, _) => Task.FromResult(ExtractSupport.NoPostingsOr(
                extract.RaiseDispute(services.CurrentUser.UserId ?? Guid.Empty, request.Reason, services.Clock.GetUtcNow()))),
            cancellationToken);
    }
}
