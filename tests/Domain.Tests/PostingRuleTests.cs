using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// The money prohibitions, checked at the domain boundary.
/// </summary>
/// <remarks>
/// The database enforces every one of these as well, and the database is the authority. These tests
/// assert that the domain refuses the same things, so a user gets a translated message instead of a
/// PostgreSQL exception. The database-level versions live in Kaff.Api.Tests, where a real PostgreSQL
/// is running.
/// </remarks>
public sealed class PostingRuleTests
{
    [Fact]
    public void Posting_requires_a_positive_amount()
    {
        Result<Posting> result = Posting.Create(
            TestAccounts.Safe(),
            TestAccounts.CompanyExpense(),
            Money.Zero,
            PostingType.CompanyExpensePayment,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.AmountMustBePositive);
    }

    [Fact]
    public void Posting_refuses_a_roll_up_account()
    {
        // spec.md §6.3 — the project control node exists to roll up, not to hold movements.
        Result<Posting> result = Posting.Create(
            TestAccounts.Safe(),
            TestAccounts.ProjectControl(),
            new Money(1_000m),
            PostingType.SiteExpensePayment,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.AccountNotPostable);
    }

    [Fact]
    public void The_five_ledgers_do_not_net_against_each_other()
    {
        // CLAUDE.md: "The five ledgers never net against each other … No calculation may offset one
        // against another." Moving value straight from the hold to the client advance would do
        // exactly that.
        Result<Posting> result = Posting.Create(
            TestAccounts.Hold(),
            TestAccounts.ClientAdvance(),
            new Money(10_000m),
            PostingType.Adjustment,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.LedgersMustNotNet);
    }

    [Fact]
    public void Nothing_comes_out_of_the_hold_before_handover()
    {
        // spec.md §5.1: "Nothing may be taken out of it mid-project — not a snag, not a debit note,
        // not an adjustment."
        Result<Posting> result = Posting.Create(
            TestAccounts.Hold(),
            TestAccounts.ClientReceivable(),
            new Money(10_000m),
            PostingType.DebitNote,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.HoldOnlyGrows);
    }

    [Fact]
    public void The_hold_accrues_freely()
    {
        Result<Posting> result = Posting.Create(
            TestAccounts.ClientReceivable(),
            TestAccounts.Hold(),
            new Money(60_000m),
            PostingType.HoldAccrual,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void The_hold_releases_at_handover()
    {
        Result<Posting> result = Posting.Create(
            TestAccounts.Hold(),
            TestAccounts.ClientReceivable(),
            new Money(200_000m),
            PostingType.HoldRelease,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_posting_cannot_span_two_projects()
    {
        Result<Posting> result = Posting.Create(
            TestAccounts.ProjectCost(TestAccounts.ProjectId),
            TestAccounts.ClientReceivable(TestAccounts.OtherProjectId),
            new Money(1_000m),
            PostingType.Adjustment,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.CrossProjectPosting);
    }

    [Fact]
    public void A_company_level_posting_must_not_name_a_project()
    {
        // spec.md §6.10: "Every expense is tagged project or company at the moment of spending —
        // never both, never neither."
        Result<Posting> result = Posting.Create(
            TestAccounts.Safe(),
            TestAccounts.CompanyExpense(),
            new Money(500m),
            PostingType.CompanyExpensePayment,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.ProjectTagForbidden);
    }

    [Fact]
    public void Material_advance_increases_what_the_client_pays()
    {
        // spec.md §15, Extract 1: 300,000 − 60,000 − 75,000 + 75,000 = 240,000.
        // تشوينات is an ADDITION. The posting therefore runs out of the تشوينات ledger and into the
        // client receivable, which is only possible because MaterialAdvance is a liability.
        // See decisions.md D-034 — an earlier version of this test had the direction reversed, and
        // that is how the defect survived slice 0.
        // One instance of each account, reused on both postings — the factory mints a new identifier
        // every call, and comparing two different accounts would prove nothing.
        Account materialAdvance = TestAccounts.MaterialAdvance();
        Account receivable = TestAccounts.ClientReceivable();

        Result<Posting> issue = Posting.Create(
            materialAdvance,
            receivable,
            new Money(75_000m),
            PostingType.MaterialAdvanceIssue,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        issue.IsSuccess.Should().BeTrue();

        // Recovery runs the other way, reducing the ledger back towards zero.
        Result<Posting> recovery = Posting.Create(
            receivable,
            materialAdvance,
            new Money(45_000m),
            PostingType.MaterialAdvanceRecovery,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        recovery.IsSuccess.Should().BeTrue();

        // Issue and recovery must move in opposite directions. If they did not, one of them would be
        // adding to a balance the other was supposed to unwind.
        issue.Value.FromAccountId.Should().Be(recovery.Value.ToAccountId);
        issue.Value.ToAccountId.Should().Be(recovery.Value.FromAccountId);
    }

    [Fact]
    public void A_reversal_mirrors_its_original_exactly()
    {
        Account from = TestAccounts.MaterialAdvance();
        Account to = TestAccounts.ClientReceivable();

        Posting original = Posting.Create(
            from,
            to,
            new Money(75_000m),
            PostingType.MaterialAdvanceIssue,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId).Value;

        Result<Posting> reversal = Posting.Reverse(
            original,
            from,
            to,
            TestAccounts.Today,
            Guid.Parse("0195c000-0000-7000-8000-0000000000bb"),
            TestAccounts.Now);

        reversal.IsSuccess.Should().BeTrue();
        reversal.Value.Amount.Should().Be(original.Amount);
        reversal.Value.Type.Should().Be(original.Type);
        reversal.Value.FromAccountId.Should().Be(original.ToAccountId);
        reversal.Value.ToAccountId.Should().Be(original.FromAccountId);
        reversal.Value.ReversesId.Should().Be(original.Id);
    }

    [Fact]
    public void A_hold_accrual_can_be_reversed_even_though_the_hold_only_grows()
    {
        // Correcting an accrual that should never have been made is not the same as taking money out
        // of the hold. spec.md §6.1 makes reversal the only correction mechanism, so refusing it here
        // would leave a wrong hold with no way to fix it.
        Account from = TestAccounts.ClientReceivable();
        Account to = TestAccounts.Hold();

        Posting accrual = Posting.Create(
            from,
            to,
            new Money(60_000m),
            PostingType.HoldAccrual,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId).Value;

        Result<Posting> reversal = Posting.Reverse(
            accrual,
            from,
            to,
            TestAccounts.Today,
            Guid.Parse("0195c000-0000-7000-8000-0000000000bb"),
            TestAccounts.Now);

        reversal.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Posting_has_no_way_to_be_modified()
    {
        // CLAUDE.md: "There is no update path and no delete path. Do not add one." The type system
        // should make that true, not just the reviewer.
        IEnumerable<string> mutators = typeof(Posting)
            .GetProperties()
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => property.Name)
            .Concat(typeof(Posting)
                .GetMethods()
                .Where(method => method.IsPublic
                                 && !method.IsStatic
                                 && !method.IsSpecialName
                                 && method.DeclaringType == typeof(Posting))
                .Select(method => method.Name));

        mutators.Should().BeEmpty();
    }
}
