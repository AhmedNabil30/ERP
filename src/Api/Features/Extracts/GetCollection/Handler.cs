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
/// <param name="CashReceived">What the client paid by this method. For a cheque, its face value.</param>
/// <param name="WithheldAtSource">What the client withheld at source; zero when the contract withholds nothing.</param>
/// <param name="Reversed">True once a reversal has corrected this collection's postings: it no longer stands.</param>
/// <param name="ChequeId">The cheque this collection created, or null when it was not paid by cheque.</param>
/// <param name="ChequeState">Where that cheque stands; only a cleared cheque was posted (D-235 item 1).</param>
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
    ChequeState? ChequeState,
    DateTimeOffset RecordedAt);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId, Guid id, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        bool exists = await database.Extracts.AsNoTracking()
            .AnyAsync(extract => extract.Id == id && extract.ProjectId == projectId, cancellationToken);
        if (!exists)
        {
            return ResultExtensions.Problem(ExtractErrors.NotFound);
        }

        List<CollectionRecord> records = await database.CollectionRecords.AsNoTracking()
            .Where(record => record.ExtractId == id && record.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        Guid[] recordIds = [.. records.Select(record => record.Id)];
        Dictionary<Guid, Cheque> chequeByCollection = await database.Cheques.AsNoTracking()
            .Where(cheque => recordIds.Contains(cheque.CollectionId))
            .ToDictionaryAsync(cheque => cheque.CollectionId, cancellationToken);

        Money standing = (await ExtractMoneyQueries.CollectedAsync(database, [id], cancellationToken)).GetValueOrDefault(id, Money.Zero);

        // A record was posted when it was recorded (cash, transfer) or when its cheque cleared. A reversal reverses every
        // standing collection posting at once, so what stands now is the latest posted records whose amounts add up to
        // the standing total; every earlier posted record was reversed. No link from a record to its postings is stored.
        // ponytail: posting order is read from the record and clearance times (ties broken by id); a per-collection link
        // on the posting is the upgrade if reversals ever become per collection.
        DateTimeOffset? PostedAt(CollectionRecord record) =>
            !chequeByCollection.TryGetValue(record.Id, out Cheque? cheque) ? record.RecordedAt
            : cheque.State == ChequeState.Cleared ? cheque.StateChangedAt
            : null;

        HashSet<Guid> stands = [];
        Money counted = Money.Zero;
        foreach (CollectionRecord record in records
            .Where(record => PostedAt(record) is not null)
            .OrderByDescending(record => PostedAt(record))
            .ThenByDescending(record => record.Id))
        {
            if (counted >= standing)
            {
                break;
            }

            stands.Add(record.Id);
            counted += record.Settles;
        }

        List<CollectionView> views =
        [
            .. records.OrderBy(record => record.RecordedAt).ThenBy(record => record.Id).Select(record =>
            {
                Cheque? cheque = chequeByCollection.GetValueOrDefault(record.Id);
                return new CollectionView(
                    record.Id,
                    record.Method,
                    record.Date,
                    record.Reference,
                    record.CashReceived,
                    record.WithheldAtSource,
                    Reversed: PostedAt(record) is not null && !stands.Contains(record.Id),
                    cheque?.Id,
                    cheque?.State,
                    record.RecordedAt);
            }),
        ];

        return Microsoft.AspNetCore.Http.Results.Ok(views);
    }
}
