using System.Security.Claims;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.ListExtracts;

/// <summary>
/// One row of the list. <see cref="Money"/> is null for a caller who may not see extract money (a site engineer
/// prepares quantities and approves nothing financial, spec.md section 9), the way the extract itself withholds
/// its figures. <see cref="WaitingOn"/> carries no money and is for everyone.
/// </summary>
public sealed record ExtractSummary(
    Guid Id,
    int Number,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    ExtractStatus Status,
    ExtractMoney? Money,
    WaitingOnResponse WaitingOn,
    ExtractLink? Revises = null,
    ExtractLink? RevisedBy = null,
    WaitingLineResponse? CreditNoteWaitingOn = null);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<Extract> extracts = await ExtractSupport.WithChildren(database)
            .AsNoTracking()
            .Where(extract => extract.ProjectId == projectId)
            .OrderBy(extract => extract.Number)
            .ToListAsync(cancellationToken);

        bool seesMoney = await ExtractSupport.MaySeeMoneyAsync(authorization, user);
        IReadOnlyDictionary<Guid, Money> collected = seesMoney
            ? await ExtractMoneyQueries.CollectedAsync(database, [.. extracts.Select(extract => extract.Id)], cancellationToken)
            : new Dictionary<Guid, Money>();
        IReadOnlyDictionary<Guid, Money> carriedHold = seesMoney
            ? await ExtractMoneyQueries.CarriedHoldAsync(database, extracts, cancellationToken)
            : new Dictionary<Guid, Money>();
        IReadOnlyList<StaffMember> staff = await ExtractWaitingOnView.StaffAsync(database, projectId, cancellationToken);

        // D-260 item 5: a Raised credit note holds collection, so the row names who decides it, built as the extract's
        // own read builds it. The lowest-numbered Raised note per extract, as the read picks it.
        Dictionary<Guid, Adjustment> raisedNotes = (await database.Adjustments.AsNoTracking()
                .Where(adjustment => adjustment.ProjectId == projectId
                    && adjustment.SourceDocumentType == SourceDocumentType.Extract
                    && adjustment.Status == AdjustmentStatus.Raised)
                .ToListAsync(cancellationToken))
            .GroupBy(adjustment => adjustment.SourceDocumentId)
            .ToDictionary(group => group.Key, group => group.MinBy(adjustment => adjustment.Number)!);

        // The same position the extract's own read derives, so the row and the extract cannot disagree.
        // ponytail: one position read per billed extract; batch it if a project's list grows past a few dozen.
        Dictionary<Guid, CollectionPosition> positions = [];
        foreach (Extract extract in seesMoney ? extracts : [])
        {
            if (await ExtractCollections.StateAsync(database, extract, cancellationToken) is { } state)
            {
                positions[extract.Id] = state.Position;
            }
        }

        ExtractLink? Link(Extract? other) => other is null ? null : new ExtractLink(other.Id, other.Number, other.Status);

        List<ExtractSummary> rows =
        [
            .. extracts.Select(extract => new ExtractSummary(
                extract.Id,
                extract.Number,
                extract.PeriodStart,
                extract.PeriodEnd,
                extract.Status,
                seesMoney
                    ? ExtractMoneyQueries.For(extract, collected, carriedHold) with
                    {
                        InCheques = positions.GetValueOrDefault(extract.Id)?.InCheques,
                        Remaining = positions.GetValueOrDefault(extract.Id)?.Remaining,
                    }
                    : null,
                ExtractWaitingOnView.Build(extract, staff),
                Link(extracts.Find(other => other.Id == extract.RevisesExtractId)),
                Link(extracts.Find(other => other.RevisesExtractId == extract.Id)),
                raisedNotes.TryGetValue(extract.Id, out Adjustment? raised) ? WaitingLine.ForCreditNote(raised, extract, staff) : null)),
        ];

        return Microsoft.AspNetCore.Http.Results.Ok(rows);
    }
}
