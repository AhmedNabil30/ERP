using Kaff.Api.Common.Results;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Cheques.GetCheque;

/// <summary>The collection a cheque belongs to.</summary>
/// <param name="Id">The collection record.</param>
/// <param name="ExtractId">The extract it paid.</param>
/// <param name="Method">Always Cheque.</param>
/// <param name="Date">The day the client paid.</param>
/// <param name="Reference">The cheque number.</param>
/// <param name="CashReceived">What reached the safe.</param>
/// <param name="WithheldAtSource">What the client withheld at source.</param>
public sealed record CollectionSummary(
    Guid Id, Guid ExtractId, CollectionMethod Method, DateOnly Date, string Reference, Money CashReceived, Money WithheldAtSource);

/// <summary>A cheque and where it stands.</summary>
/// <param name="Id">The cheque.</param>
/// <param name="State">Received, Deposited, Cleared or Bounced.</param>
/// <param name="StateChangedAt">When it last moved.</param>
/// <param name="Collection">The collection that created it.</param>
public sealed record Response(Guid Id, ChequeState State, DateTimeOffset StateChangedAt, CollectionSummary Collection);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId, Guid id, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Response? response = await (
            from cheque in database.Cheques.AsNoTracking()
            join collection in database.CollectionRecords.AsNoTracking() on cheque.CollectionId equals collection.Id
            where cheque.Id == id && cheque.ProjectId == projectId
            select new Response(
                cheque.Id,
                cheque.State,
                cheque.StateChangedAt,
                new CollectionSummary(
                    collection.Id, collection.ExtractId, collection.Method, collection.Date, collection.Reference,
                    collection.CashReceived, collection.WithheldAtSource)))
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? ResultExtensions.Problem(CollectionErrors.ChequeNotFound)
            : Microsoft.AspNetCore.Http.Results.Ok(response);
    }
}
