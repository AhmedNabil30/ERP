using Kaff.Domain.Common;

namespace Kaff.Domain.Treasury;

/// <summary>
/// KAFF-306. Turns an ordered run of postings against one account into a statement with a running
/// balance, entirely by summation over the rows given.
/// </summary>
/// <remarks>
/// <para>
/// CLAUDE.md: "Never store a balance. Balances are derived by summing postings, always." This is
/// that derivation carried through a single account's history, not only its current total —
/// <see cref="BalanceQueries"/> covers the current total; this covers the row-by-row running figure
/// spec.md's account statement (S-033) needs. AC-306-B, AC-306-C.
/// </para>
/// <para>
/// <b>A reversed posting is never dropped.</b> CLAUDE.md: "Never update or delete a posting…
/// Corrections are new reversing postings that reference the original through <c>ReversesId</c>."
/// Both stay visible, forever. This method walks every posting it is given, in order, including a
/// reversal and the original it reverses — dropping either would be the exact defect CLAUDE.md names.
/// The reversed original is returned as an ordinary row, unmarked; a caller renders the strike-through
/// by matching a row's <see cref="PostingStatementRow.Id"/> against another row's
/// <see cref="PostingStatementRow.ReversesId"/>.
/// </para>
/// <para>
/// <b>Caller's job, not this method's:</b> filtering to one account, ordering ascending by
/// <c>(PostingDate, Id)</c> so the running balance accumulates correctly, and paging. This method
/// assumes its input already has both properties — it does not re-sort or re-filter — see
/// <c>Kaff.Api.Features.Treasury.ListPostings.Handler</c>.
/// </para>
/// </remarks>
public static class PostingStatementQueries
{
    /// <summary>
    /// Builds the statement. <paramref name="accountId"/> fixes which side of each posting is this
    /// account's inflow versus outflow; <paramref name="normalBalance"/> is the sign the running
    /// balance reads in, exactly as <see cref="AccountBalance.SignedBalance"/> does for the current
    /// total (<c>account_balances</c> view: <c>signed_balance = raw_balance * (Debit ? 1 : -1)</c>).
    /// </summary>
    public static IReadOnlyList<PostingStatementRow> BuildStatement(
        IEnumerable<Posting> orderedPostings, Guid accountId, NormalBalance normalBalance)
    {
        ArgumentNullException.ThrowIfNull(orderedPostings);

        var rows = new List<PostingStatementRow>();
        decimal runningRaw = 0m;
        int sign = normalBalance == NormalBalance.Debit ? 1 : -1;

        foreach (Posting posting in orderedPostings)
        {
            Money inflow = posting.ToAccountId == accountId ? posting.Amount : Money.Zero;
            Money outflow = posting.FromAccountId == accountId ? posting.Amount : Money.Zero;

            runningRaw += inflow.Amount - outflow.Amount;

            rows.Add(new PostingStatementRow(
                posting.Id,
                posting.PostingDate,
                posting.Type,
                inflow,
                outflow,
                new Money(runningRaw * sign),
                posting.SourceDocument,
                posting.ReversesId,
                posting.IsReversal));
        }

        return rows;
    }
}

/// <summary>One row of an account statement — spec.md §6.1, S-033. AC-306-B.</summary>
/// <param name="Inflow">This posting's amount, if it moved value into the account. Zero otherwise.</param>
/// <param name="Outflow">This posting's amount, if it moved value out of the account. Zero otherwise.</param>
/// <param name="RunningBalance">
/// Signed to the account's own direction (its normal balance), cumulative through this row.
/// Recomputed from the postings given every time — never read from a stored column.
/// </param>
/// <param name="ReversesId">Set when this row is itself a reversal. Null otherwise.</param>
/// <param name="IsReversal">True for a reversing posting.</param>
public sealed record PostingStatementRow(
    Guid Id,
    DateOnly PostingDate,
    PostingType Type,
    Money Inflow,
    Money Outflow,
    Money RunningBalance,
    SourceDocument SourceDocument,
    Guid? ReversesId,
    bool IsReversal);
