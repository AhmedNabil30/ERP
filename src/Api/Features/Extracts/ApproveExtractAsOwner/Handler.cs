using System.Security.Claims;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Extracts.ApproveExtractAsOwner;

/// <summary>The Owner approves. The accrual postings, when the ruled moment is this stage, commit with the status in one save (AC-500-K).</summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
        => ExtractSupport.RunAsync(
            projectId,
            id,
            needsAccounts: true,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            (extract, project, context, services, token) =>
            {
                Guid actor = services.CurrentUser.UserId ?? Guid.Empty;
                DateTimeOffset now = services.Clock.GetUtcNow();
                return Task.FromResult(ExtractSupport.Of(extract.OwnerApprove(actor, context!, now)));
            },
            cancellationToken);
}
