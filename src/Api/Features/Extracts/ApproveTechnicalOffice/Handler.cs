using System.Security.Claims;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Extracts.ApproveTechnicalOffice;

/// <summary>Passes the quantity gate, in full or split. Writes no posting: the figures do not exist yet.</summary>
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
            (extract, project, context, services, token) => Task.FromResult(ExtractSupport.NoPostingsOr(
                extract.ApproveTechnicalOffice(
                    services.CurrentUser.UserId ?? Guid.Empty,
                    request.ApprovedPortion ?? Percentage.FromFraction(1m),
                    request.HeldForNcrId,
                    services.Rulings,
                    services.Clock.GetUtcNow()))),
            cancellationToken);
    }
}
