using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Api.Features.Treasury.ListPostings;

/// <summary>One row of an account statement. KAFF-306, AC-306-B.</summary>
/// <remarks>
/// <b>A reversed posting is still returned as its own row, unmarked.</b> CLAUDE.md: "Both stay
/// visible, forever." A caller renders the strike-through by matching this row's <see cref="Id"/>
/// against another row's <see cref="ReversesId"/> — the reversed original does not disappear and is
/// not flagged here, because it was legitimate when it posted and stays a real event in the ledger.
/// </remarks>
/// <param name="Inflow">This posting's amount, if it moved value into the queried account. Zero otherwise.</param>
/// <param name="Outflow">This posting's amount, if it moved value out of the queried account. Zero otherwise.</param>
/// <param name="RunningBalance">
/// Signed to the account's own direction, cumulative through this row — summed fresh on every
/// request, never read from a stored column (CLAUDE.md: "Never store a balance").
/// </param>
/// <param name="ReversesId">Set when this row is itself a reversal.</param>
/// <param name="IsReversal">True for a reversing posting.</param>
public sealed record PostingRow(
    Guid Id,
    DateOnly PostingDate,
    PostingType Type,
    Money Inflow,
    Money Outflow,
    Money RunningBalance,
    SourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    string? SourceDocumentReference,
    Guid? ReversesId,
    bool IsReversal);

/// <summary>A page of the statement, oldest first. <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record Response(IReadOnlyList<PostingRow> Items, string? NextCursor);
