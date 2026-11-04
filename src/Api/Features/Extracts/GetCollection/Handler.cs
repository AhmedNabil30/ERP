using Kaff.Api.Common.Results;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.GetCollection;

/// <summary>One recorded collection. <paramref name="Reversed"/> is derived, never stored.</summary>
/// <param name="Id">The collection record.</param>
/// <param name="Method">Cash, Cheque or Transfer.</param>
/// <param name="Date">The day the client paid.</param>
/// <param name="Reference">The receipt, transfer or cheque number.</param>
/// <param name="CashReceived">What reached the safe.</param>
/// <param name="WithheldAtSource">What the client withheld at source; zero when the contract withholds nothing.</param>
/// <param name="Reversed">True once a reversal has corrected this collection: it is no longer the standing one.</param>
/// <param name="ChequeId">The cheque this collection created, or null when it was not paid by cheque.</param>
/// <param name="RecordedAt">When Finance recorded it.</param>
public sealed record CollectionView(
    Guid Id,
    CollectionMethod Method,
    DateOnly Date,
    string Reference,
    Money CashReceived,
    Money WithheldAtSource,
    bool Reversed,
    Guid? ChequeId,
    DateTimeOffset RecordedAt);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId, Guid id, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        ExtractStatus? status = await database.Extracts.AsNoTracking()
            .Where(extract => extract.Id == id && extract.ProjectId == projectId)
            .Select(extract => (ExtractStatus?)extract.Status)
            .FirstOrDefaultAsync(cancellationToken);
        if (status is null)
        {
            return ResultExtensions.Problem(ExtractErrors.NotFound);
        }

        List<CollectionRecord> records = await database.CollectionRecords.AsNoTracking()
            .Where(record => record.ExtractId == id && record.ProjectId == projectId)
            .OrderBy(record => record.RecordedAt)
            .ToListAsync(cancellationToken);

        Guid[] recordIds = [.. records.Select(record => record.Id)];
        Dictionary<Guid, Guid> chequeByCollection = await database.Cheques.AsNoTracking()
            .Where(cheque => recordIds.Contains(cheque.CollectionId))
            .ToDictionaryAsync(cheque => cheque.CollectionId, cheque => cheque.Id, cancellationToken);

        // Only the latest collection can be the standing one, and only while the extract is still Collected: a
        // reversal returns the extract to Issued, so every earlier record, or all of them, were reversed.
        // No link from the record to its postings is stored, so this is read off the extract's own state.
        List<CollectionView> views =
        [
            .. records.Select((record, index) => new CollectionView(
                record.Id,
                record.Method,
                record.Date,
                record.Reference,
                record.CashReceived,
                record.WithheldAtSource,
                Reversed: !(status == ExtractStatus.Collected && index == records.Count - 1),
                chequeByCollection.TryGetValue(record.Id, out Guid chequeId) ? chequeId : null,
                record.RecordedAt)),
        ];

        return Microsoft.AspNetCore.Http.Results.Ok(views);
    }
}
