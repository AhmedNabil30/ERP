using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Clients.GetClient;

/// <summary>Reads one client. KAFF-126's S-014 loads its form from here.</summary>
/// <remarks>
/// <b>Archived clients are returned.</b> The list hides them by default (KAFF-124 rule 2); this does
/// not, because a screen reached by id was asked for that client specifically, and spec.md §3 attaches
/// a reopened opportunity to the original. <c>IsActive</c> is in the payload so the screen can say so.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid clientId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Response? client = await database.Clients
            .Where(candidate => candidate.Id == clientId)
            .Select(candidate => new Response(
                candidate.Id,
                candidate.Code,
                candidate.Name,
                candidate.PhoneEntered,
                candidate.Kind,
                candidate.AlternatePhone,
                candidate.Email,
                candidate.Address,
                candidate.TaxRegistrationNumber,
                candidate.Notes,
                candidate.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return client is null
            ? ResultExtensions.Problem(MasterDataErrors.ClientNotFound)
            : Microsoft.AspNetCore.Http.Results.Ok(client);
    }
}
