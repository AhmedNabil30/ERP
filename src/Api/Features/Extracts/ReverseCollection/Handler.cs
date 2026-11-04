using System.Security.Claims;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Extracts.ReverseCollection;

/// <summary>
/// Reverses the standing collection: <c>Extract.ReverseCollection</c> mirrors the cash and withholding postings by
/// new postings referencing the originals through <c>ReversesId</c>, and returns the extract to Issued with the
/// stored reason, in one save. The collection record and the cheque are not touched: nothing is updated or deleted.
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
            needsAccounts: true,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            async (extract, project, context, services, token) =>
            {
                IReadOnlyList<ExtractPostedLeg> legs =
                    await ExtractSupport.StandingCollectionLegsAsync(services.Database, extract.Id, token);

                // RULING: Q98/Q100/Q101 - what a reversed collection does to the extract is unruled. Built as: the
                // extract returns to Issued (the receivable stands again) and may be collected again, with a new
                // collection record. Question for Karim: after a collection is reversed, is the extract Issued again
                // and collectable, or does it stay Collected pending a corrected receipt? And what happens to a cheque
                // already Deposited or Cleared (it is left untouched here, so a reversed cheque collection can still
                // be moved)?
                return extract.ReverseCollection(
                    services.CurrentUser.UserId ?? Guid.Empty, request.Reason, context!, legs, services.Clock.GetUtcNow());
            },
            cancellationToken);
    }
}
