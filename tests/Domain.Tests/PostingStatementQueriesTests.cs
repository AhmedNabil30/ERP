using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-306. <see cref="PostingStatementQueries"/> is the Domain-level derivation the HTTP statement
/// endpoint (blocked on questions-for-karim.md Q90, see the story's build notes) will read through
/// once it can be reached. Exercised here at the Domain layer, the same split KAFF-304's
/// <c>BalanceQueriesTests</c> uses, so the money math is proven independently of the permission gate.
/// </summary>
public sealed class PostingStatementQueriesTests
{
    private static readonly DateOnly Today = new(2026, 6, 1);

    private static readonly DateTimeOffset Now = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    // ---- AC-306-B — running balance survives a reversal, both rows stay visible ------------------

    [Fact]
    public void A_reversal_returns_the_running_balance_to_its_prior_value_with_both_postings_present()
    {
        Account capital = Account.Create(
            AccountType.PaidInCapital, "PSQ-CAP", "أ", "b", Currency.Egp, Today).Value;
        Account safe = Account.Create(
            AccountType.Safe, "PSQ-SAFE", "أ", "b", Currency.Egp, Today).Value;

        Posting funding = Posting.Create(
            capital,
            safe,
            new Money(10_000m),
            PostingType.OpeningBalance,
            new SourceDocument(SourceDocumentType.OpeningBalance, Guid.CreateVersion7(), null),
            Today,
            Guid.CreateVersion7(),
            Now).Value;

        Posting reversal = Posting.Reverse(funding, capital, safe, Today, Guid.CreateVersion7(), Now).Value;

        IReadOnlyList<PostingStatementRow> rows = PostingStatementQueries.BuildStatement(
            [funding, reversal], safe.Id, NormalBalance.Debit);

        rows.Should().HaveCount(
            2, "CLAUDE.md: postings are append-only — a correction is a new posting, both stay visible");

        rows[0].Id.Should().Be(funding.Id);
        rows[0].Inflow.Should().Be(new Money(10_000m));
        rows[0].Outflow.Should().Be(Money.Zero);
        rows[0].RunningBalance.Should().Be(new Money(10_000m));
        rows[0].IsReversal.Should().BeFalse();

        rows[1].Id.Should().Be(reversal.Id);
        rows[1].ReversesId.Should().Be(funding.Id);
        rows[1].IsReversal.Should().BeTrue();
        rows[1].Inflow.Should().Be(Money.Zero);
        rows[1].Outflow.Should().Be(new Money(10_000m));
        rows[1].RunningBalance.Should().Be(
            Money.Zero, "AC-306-B: the balance returns to its prior value after the reversal");
    }

    // ---- AC-306-C — every figure is summed, never read from a stored value ------------------------

    [Fact]
    public void The_running_balance_accumulates_across_three_postings_in_order()
    {
        Account capital = Account.Create(
            AccountType.PaidInCapital, "PSQ-CAP2", "أ", "b", Currency.Egp, Today).Value;
        Account safe = Account.Create(
            AccountType.Safe, "PSQ-SAFE2", "أ", "b", Currency.Egp, Today).Value;
        Account expense = Account.Create(
            AccountType.CompanyExpense, "PSQ-EXP", "أ", "b", Currency.Egp, Today).Value;

        Posting first = Posting.Create(
            capital, safe, new Money(10_000m), PostingType.OpeningBalance,
            new SourceDocument(SourceDocumentType.OpeningBalance, Guid.CreateVersion7(), null),
            Today, Guid.CreateVersion7(), Now).Value;

        Posting second = Posting.Create(
            capital, safe, new Money(5_000m), PostingType.OpeningBalance,
            new SourceDocument(SourceDocumentType.OpeningBalance, Guid.CreateVersion7(), null),
            Today, Guid.CreateVersion7(), Now).Value;

        Posting third = Posting.Create(
            safe, expense, new Money(3_000m), PostingType.CompanyExpensePayment,
            new SourceDocument(SourceDocumentType.Adjustment, Guid.CreateVersion7(), null),
            Today, Guid.CreateVersion7(), Now).Value;

        IReadOnlyList<PostingStatementRow> rows = PostingStatementQueries.BuildStatement(
            [first, second, third], safe.Id, NormalBalance.Debit);

        rows.Should().HaveCount(3);
        rows[0].RunningBalance.Should().Be(new Money(10_000m));
        rows[1].RunningBalance.Should().Be(new Money(15_000m));
        rows[2].RunningBalance.Should().Be(new Money(12_000m), "the third posting draws 3,000 out of the safe");
    }

    // ---- Credit-normal account: the running balance flips sign, same as AccountBalance.SignedBalance --

    [Fact]
    public void A_credit_normal_account_reads_its_running_balance_with_the_sign_flipped()
    {
        Guid projectId = Guid.CreateVersion7();
        Guid clientId = Guid.CreateVersion7();
        Account clientAdvance = Account.Create(
            AccountType.ClientAdvance, "PSQ-CA", "أ", "b", Currency.Egp, Today, projectId, PartyType.Client, clientId).Value;

        // Fund the client advance first (money in), matching spec.md §15's "advance at signing".
        Account safe = Account.Create(AccountType.Safe, "PSQ-SAFE3", "أ", "b", Currency.Egp, Today).Value;

        Posting advanceIn = Posting.Create(
            safe, clientAdvance, new Money(20_000m), PostingType.ClientAdvanceReceipt,
            new SourceDocument(SourceDocumentType.Adjustment, Guid.CreateVersion7(), null),
            Today, Guid.CreateVersion7(), Now, clientAdvance.ProjectId).Value;

        IReadOnlyList<PostingStatementRow> rows = PostingStatementQueries.BuildStatement(
            [advanceIn], clientAdvance.Id, NormalBalance.Credit);

        rows.Should().ContainSingle();
        rows[0].RunningBalance.Should().Be(
            new Money(-20_000m),
            "same sign convention account_balances.sql uses: signed = raw * (Debit ? 1 : -1) — " +
            "BalanceQueriesTests's own credit-normal case asserts the identical flip");
    }

    [Fact]
    public void An_empty_posting_history_returns_an_empty_statement_not_an_error()
    {
        PostingStatementQueries.BuildStatement([], Guid.CreateVersion7(), NormalBalance.Debit)
            .Should().BeEmpty();
    }
}
