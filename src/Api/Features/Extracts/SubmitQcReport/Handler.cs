using System.Security.Claims;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Extracts.SubmitQcReport;

/// <summary>Draft to QcSubmitted. Writes no posting, so no accounts are needed.</summary>
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
            needsAccounts: false,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            (extract, project, context, services, token) =>
            {
                Guid actor = services.CurrentUser.UserId ?? Guid.Empty;
                DateTimeOffset now = services.Clock.GetUtcNow();
                return Task.FromResult(ExtractSupport.NoPostingsOr(extract.SubmitQcReport(actor, now)));
            },
            cancellationToken);
}
