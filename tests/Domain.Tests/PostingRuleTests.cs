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
    public void A_hold_accrual_cannot_be_reversed_because_a_reversal_would_take_value_out_of_the_hold()
    {
        // spec.md §5.1: "Nothing may be taken out of it mid-project". A reversal of an accrual debits the
        // hold exactly as a withdrawal would, so it is refused like one (Verifier F-5, 2026-11-18).
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

        reversal.IsFailure.Should().BeTrue();
        reversal.Error.Should().Be(TreasuryErrors.HoldPostingNotReversible);
    }

    [Fact]
    public void A_hold_release_cannot_be_reversed_because_the_hold_releases_once()
    {
        // spec.md §5.1: "It releases once, in full, at handover." Reversing the release would put the
        // hold back and let it be released a second time (Verifier F-5, 2026-11-18).
        Account hold = TestAccounts.Hold();
        Account receivable = TestAccounts.ClientReceivable();

        Posting release = Posting.Create(
            hold,
            receivable,
            new Money(200_000m),
            PostingType.HoldRelease,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId).Value;

        Result<Posting> reversal = Posting.Reverse(
            release,
            hold,
            receivable,
            TestAccounts.Today,
            Guid.Parse("0195c000-0000-7000-8000-0000000000bb"),
            TestAccounts.Now);

        reversal.IsFailure.Should().BeTrue();
        reversal.Error.Should().Be(TreasuryErrors.HoldPostingNotReversible);
    }

    /// <summary>
    /// D-236 "Still open": a posting a flow wrote is reversed by that flow, never by a generic reversal. The flow's
    /// own reversal moves the owning document with it (the extract back to Issued, the adjustment to Reversed); a
    /// generic one would leave the document saying the money is still there. A posting is the flow's when its source
    /// document is of a type a flow writes AND that document exists: a movement posted by hand may carry the same
    /// type with an id that names nothing.
    /// </summary>
    [Theory]
    [InlineData(SourceDocumentType.Extract)]
    [InlineData(SourceDocumentType.Adjustment)]
    [InlineData(SourceDocumentType.ProjectHandover)]
    [InlineData(SourceDocumentType.SubcontractorRetentionRelease)]
    public void A_posting_a_flow_wrote_cannot_be_reversed_as_a_movement(SourceDocumentType owner)
    {
        Account receivable = TestAccounts.ClientReceivable();
        Account safe = TestAccounts.Safe();
        Posting collection = Posting.Create(
            receivable, safe, new Money(240_000m), PostingType.ClientCollection,
            new SourceDocument(owner, Guid.Parse("0195c000-0000-7000-8000-0000000000e1"), "EXT-1"),
            TestAccounts.Today, TestAccounts.Actor, TestAccounts.Now, TestAccounts.ProjectId).Value;

        Result<Posting> reversal = Posting.ReverseMovement(
            collection, receivable, safe, TestAccounts.Today, TestAccounts.Actor, TestAccounts.Now, sourceDocumentExists: true);

        reversal.IsFailure.Should().BeTrue();
        reversal.Error.Should().Be(TreasuryErrors.FlowPostingNotReversible);
    }

    [Fact]
    public void The_owning_flow_still_reverses_its_own_posting()
    {
        Account receivable = TestAccounts.ClientReceivable();
        Account safe = TestAccounts.Safe();
        Posting collection = Posting.Create(
            receivable, safe, new Money(240_000m), PostingType.ClientCollection,
            new SourceDocument(SourceDocumentType.Extract, Guid.Parse("0195c000-0000-7000-8000-0000000000e1"), "EXT-1"),
            TestAccounts.Today, TestAccounts.Actor, TestAccounts.Now, TestAccounts.ProjectId).Value;

        Posting.Reverse(collection, receivable, safe, TestAccounts.Today, TestAccounts.Actor, TestAccounts.Now)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_movement_with_no_owning_flow_is_reversed_as_a_movement()
    {
        Account safe = TestAccounts.Safe();
        Account expense = TestAccounts.CompanyExpense();
        Posting payment = Posting.Create(
            safe, expense, new Money(1_000m), PostingType.CompanyExpensePayment,
            new SourceDocument(SourceDocumentType.CompanyExpense, Guid.Parse("0195c000-0000-7000-8000-0000000000e2"), null),
            TestAccounts.Today, TestAccounts.Actor, TestAccounts.Now).Value;

        Result<Posting> reversal = Posting.ReverseMovement(
            payment, safe, expense, TestAccounts.Today, TestAccounts.Actor, TestAccounts.Now, sourceDocumentExists: true);

        reversal.IsSuccess.Should().BeTrue();
        reversal.Value.ReversesId.Should().Be(payment.Id);
    }

    [Fact]
    public void A_movement_labelled_with_a_flow_type_but_no_such_document_is_reversed_as_a_movement()
    {
        Account receivable = TestAccounts.ClientReceivable();
        Account safe = TestAccounts.Safe();
        Posting labelled = Posting.Create(
            receivable, safe, new Money(1_000m), PostingType.ClientCollection,
            TestAccounts.Document(), TestAccounts.Today, TestAccounts.Actor, TestAccounts.Now, TestAccounts.ProjectId).Value;

        Posting.ReverseMovement(labelled, receivable, safe, TestAccounts.Today, TestAccounts.Actor, TestAccounts.Now, sourceDocumentExists: false)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_hold_posting_a_flow_wrote_still_reports_the_hold_rule_first()
    {
        Account receivable = TestAccounts.ClientReceivable();
        Account hold = TestAccounts.Hold();
        Posting accrual = Posting.Create(
            receivable, hold, new Money(60_000m), PostingType.HoldAccrual,
            new SourceDocument(SourceDocumentType.Extract, Guid.Parse("0195c000-0000-7000-8000-0000000000e1"), "EXT-1"),
            TestAccounts.Today, TestAccounts.Actor, TestAccounts.Now, TestAccounts.ProjectId).Value;

        Posting.ReverseMovement(accrual, receivable, hold, TestAccounts.Today, TestAccounts.Actor, TestAccounts.Now, sourceDocumentExists: true)
            .Error.Should().Be(TreasuryErrors.HoldPostingNotReversible);
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
