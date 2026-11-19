using Kaff.Api.Common.Results;
using Kaff.Api.Features.Extracts;
using Kaff.Domain.Authorization;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Cheques.GetCheque;

/// <summary>The collection a cheque belongs to.</summary>
/// <param name="Id">The collection record.</param>
/// <param name="ExtractId">The extract it paid, or null when it paid the released hold (D-244 item 2).</param>
/// <param name="Method">Always Cheque.</param>
/// <param name="Date">The day the client paid.</param>
/// <param name="Reference">The cheque number.</param>
/// <param name="CashReceived">The cheque's face value. It reaches the safe only when the cheque clears (D-235 item 1).</param>
/// <param name="WithheldAtSource">What the client withheld at source, recognised with the clearance.</param>
public sealed record CollectionSummary(
    Guid Id, Guid? ExtractId, CollectionMethod Method, DateOnly Date, string Reference, Money CashReceived, Money WithheldAtSource);

/// <summary>A cheque and where it stands.</summary>
/// <param name="Id">The cheque.</param>
/// <param name="State">Received, Deposited, Cleared or Bounced.</param>
/// <param name="StateChangedAt">When it last moved: the date of its last state change.</param>
/// <param name="ExtractNumber">The number of the extract it paid, as the extract list shows it. Null for the released hold.</param>
/// <param name="Collection">The collection that created it.</param>
/// <param name="BounceReason">The bank's reason, once it bounced.</param>
/// <param name="WaitingOn">Who a cheque in hand waits on: Finance to deposit it, then to record its clearance or bounce.</param>
public sealed record Response(
    Guid Id,
    ChequeState State,
    DateTimeOffset StateChangedAt,
    int? ExtractNumber,
    CollectionSummary Collection,
    string? BounceReason,
    WaitingLineResponse? WaitingOn = null);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId, Guid id, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Response? response = await Project(database, projectId, id).FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? ResultExtensions.Problem(CollectionErrors.ChequeNotFound)
            : Microsoft.AspNetCore.Http.Results.Ok(
                WithWaiting([response], await ExtractWaitingOnView.StaffAsync(database, projectId, cancellationToken))[0]);
    }

    /// <summary>The waiting line of each cheque still in hand. Moving a cheque is not a decision, so nobody is barred.</summary>
    internal static IReadOnlyList<Response> WithWaiting(IReadOnlyList<Response> cheques, IReadOnlyList<StaffMember> staff) =>
        [
            .. cheques.Select(cheque => cheque with
            {
                WaitingOn = cheque.State switch
                {
                    ChequeState.Received => WaitingLine.Build("waiting.task.cheque_deposit", Permission.ExtractCollect, staff, []),
                    ChequeState.Deposited => WaitingLine.Build("waiting.task.cheque_settlement", Permission.ExtractCollect, staff, []),
                    _ => null,
                },
            }),
        ];

    /// <summary>The project's cheques as read, or the one named. The single read and the project's list share it.</summary>
    internal static IQueryable<Response> Project(KaffDbContext database, Guid projectId, Guid? id = null) =>
        from cheque in database.Cheques.AsNoTracking()
        join collection in database.CollectionRecords.AsNoTracking() on cheque.CollectionId equals collection.Id
        join extract in database.Extracts.AsNoTracking() on collection.ExtractId equals extract.Id into paid
        from extract in paid.DefaultIfEmpty()
        where cheque.ProjectId == projectId && (id == null || cheque.Id == id)
        select new Response(
            cheque.Id,
            cheque.State,
            cheque.StateChangedAt,
            extract == null ? null : extract.Number,
            new CollectionSummary(
                collection.Id, collection.ExtractId, collection.Method, collection.Date, collection.Reference,
                collection.CashReceived, collection.WithheldAtSource),
            cheque.BounceReason);
}
