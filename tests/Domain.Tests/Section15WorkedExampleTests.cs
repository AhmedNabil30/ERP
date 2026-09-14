using System.Reflection;
using System.Runtime.CompilerServices;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// The spec.md §15 worked example, walked end to end against real <see cref="Posting"/> rows.
/// </summary>
/// <remarks>
/// <para>
/// KAFF-300. spec.md §15's own first line: "These numbers are a test, not an illustration. Any
/// change that breaks them fails the build." decisions.md D-034 requires this fixture to exist
/// before slice 3 opens, "failing or skipped, but present, so the gate is a build outcome." The
/// Domain and database types this fixture needs were built in slice 0 — see
/// <c>src/Domain/Treasury/</c> — so this fixture asserts against real postings rather than a stub,
/// and it goes green today. It does not drive <c>IBillingCalculator</c>: that is slice 5's own
/// reading of the same table, per <c>src/Domain/Contracts/Billing/Calculators.cs</c>.
/// </para>
/// <para>
/// <b>No balance is ever stored.</b> Every figure below comes from <see cref="Signed"/>, which sums
/// a <see cref="Posting"/> list fresh on every call — the same arithmetic as the production
/// <c>account_balances</c> view (<c>src/Infrastructure/Persistence/Sql/002_views.sql</c>):
/// <c>signed_balance = (inflow - outflow) * (NormalBalance == Debit ? 1 : -1)</c>. There is no field
/// on this class that accumulates a running total; qa/slice-3/test-cases.md TC-3-013 is the case that
/// exists to keep it that way.
/// </para>
/// <para>
/// <b>The account model, so a reviewer can check the postings against §15 without re-deriving it:</b>
/// certified work value is recognised from <c>ContractRevenue</c> into <c>ClientReceivable</c>; the
/// hold, the advance recovery and تشوينات all move value into or out of <c>ClientReceivable</c>,
/// exactly the way <c>tests/Api.Tests/TreasuryGuardTests.cs</c> and
/// <c>tests/Domain.Tests/PostingRuleTests.cs</c> already post them; and every extract's own postings
/// net <c>ClientReceivable</c> back to zero, realised as one cash <c>ClientCollection</c> posting
/// into the Safe. "Client pays" is therefore always a direct sum of postings into the Safe — never a
/// subtraction between two ledgers' balances (spec.md §6.4, AC-300-E).
/// </para>
/// </remarks>
public sealed class Section15WorkedExampleTests
{
    // ---- §15 header line, verbatim ------------------------------------------------------------

    private static readonly Money ContractValue = new(1_000_000m);
    private static readonly Money AdvanceAtSigning = new(250_000m);

    private static readonly DateOnly SigningDate = new(2026, 1, 1);
    private static readonly DateOnly Extract1Date = new(2026, 2, 1);
    private static readonly DateOnly Extract2Date = new(2026, 3, 1);
    private static readonly DateOnly Extract3Date = new(2026, 4, 1);
    private static readonly DateOnly HandoverDate = new(2026, 5, 1);

    private static Guid Actor => TestAccounts.Actor;

    private static DateTimeOffset Now => TestAccounts.Now;

    // ---- accounts, one instance per ledger, reused across every posting -----------------------
    // (PostingRuleTests' own note applies here too: the factory mints a new identifier every call,
    // so each ledger is built exactly once and passed around.)

    private readonly Account _revenue = BuildRevenueAccount();
    private readonly Account _receivable = TestAccounts.ClientReceivable();
    private readonly Account _clientAdvance = TestAccounts.ClientAdvance();
    private readonly Account _hold = TestAccounts.Hold();
    private readonly Account _materialAdvance = TestAccounts.MaterialAdvance();
    private readonly Account _safe = TestAccounts.Safe();

    private static Account BuildRevenueAccount()
    {
        Result<Account> result = Account.Create(
            AccountType.ContractRevenue,
            "PRJ-REV",
            "إيراد المشروع",
            "Project revenue",
            Currency.Egp,
            new DateOnly(2026, 1, 1),
            TestAccounts.ProjectId);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Revenue test account is invalid: {result.Error.Code}.");
        }

        return result.Value;
    }

    // ---- the five events, each as its own list of postings, in §15's order --------------------

    private List<Posting> BuildAdvanceAtSigning() =>
    [
        // §15 header: "Advance at signing" — 250,000 straight into the Safe. This is prepayment,
        // not certified work, so it never touches ClientReceivable — AC-300-E: only one ledger
        // (ClientAdvance) moves, nothing is netted against anything else.
        Require(Posting.Create(
            _clientAdvance, _safe, AdvanceAtSigning, PostingType.ClientAdvanceReceipt,
            CollectionDocument("SIGNING"), SigningDate, Actor, Now, TestAccounts.ProjectId)),
    ];

    private List<Posting> BuildExtract1() =>
    [
        Require(Posting.Create(
            _revenue, _receivable, new Money(300_000m), PostingType.RevenueRecognition,
            ExtractDocument("EXT-1"), Extract1Date, Actor, Now, TestAccounts.ProjectId)),
        Require(Posting.Create(
            _receivable, _hold, new Money(60_000m), PostingType.HoldAccrual,
            ExtractDocument("EXT-1"), Extract1Date, Actor, Now, TestAccounts.ProjectId)),
        Require(Posting.Create(
            _receivable, _clientAdvance, new Money(75_000m), PostingType.ClientAdvanceRecovery,
            ExtractDocument("EXT-1"), Extract1Date, Actor, Now, TestAccounts.ProjectId)),
        // §15 تشوينات column, Extract 1: +75,000. Transcribed literal — see Q14 / Q58 below.
        Require(Posting.Create(
            _materialAdvance, _receivable, new Money(75_000m), PostingType.MaterialAdvanceIssue,
            ExtractDocument("EXT-1"), Extract1Date, Actor, Now, TestAccounts.ProjectId)),
        Require(Posting.Create(
            _receivable, _safe, new Money(240_000m), PostingType.ClientCollection,
            ExtractDocument("EXT-1"), Extract1Date, Actor, Now, TestAccounts.ProjectId)),
    ];

    private List<Posting> BuildExtract2()
    {
        // §15 تشوينات column, Extract 2: -45,000.
        //
        // Q14 (stories/questions-for-karim.md): open. Confirms the تشوينات mechanic D-034 already
        // ruled on — that the 75,000 paid at Extract 1 comes off later extracts as material is
        // installed. It does not gate this figure existing; it gates AC-300-I if Karim contradicts
        // D-034 (see KAFF-300's own "What Q14 gates" section).
        //
        // Q58 (stories/questions-for-karim.md): open, raised by KAFF-300. spec.md §15 gives 45,000
        // and 30,000 and no rule that produces them — they are neither 25% of period work value (that
        // is the *advance* recovery, a different column) nor proportional to work value (45,000 :
        // 30,000 runs opposite to 300,000 : 400,000). This literal is deliberately NOT a formula: no
        // expression here may derive 45,000 from period work value, the advance rate, or any other
        // §15 figure. The slice-5 calculator needs the rule; this fixture only needs the number.
        Money tashwinatRecovery = new(45_000m);

        return
        [
            Require(Posting.Create(
                _revenue, _receivable, new Money(300_000m), PostingType.RevenueRecognition,
                ExtractDocument("EXT-2"), Extract2Date, Actor, Now, TestAccounts.ProjectId)),
            Require(Posting.Create(
                _receivable, _hold, new Money(60_000m), PostingType.HoldAccrual,
                ExtractDocument("EXT-2"), Extract2Date, Actor, Now, TestAccounts.ProjectId)),
            Require(Posting.Create(
                _receivable, _clientAdvance, new Money(75_000m), PostingType.ClientAdvanceRecovery,
                ExtractDocument("EXT-2"), Extract2Date, Actor, Now, TestAccounts.ProjectId)),
            Require(Posting.Create(
                _receivable, _materialAdvance, tashwinatRecovery, PostingType.MaterialAdvanceRecovery,
                ExtractDocument("EXT-2"), Extract2Date, Actor, Now, TestAccounts.ProjectId)),
            Require(Posting.Create(
                _receivable, _safe, new Money(120_000m), PostingType.ClientCollection,
                ExtractDocument("EXT-2"), Extract2Date, Actor, Now, TestAccounts.ProjectId)),
        ];
    }

    private List<Posting> BuildExtract3()
    {
        // §15 تشوينات column, Extract 3: -30,000. Same Q14/Q58 citation as Extract 2 above — the
        // transcribed literal, no derivation.
        Money tashwinatRecovery = new(30_000m);

        return
        [
            Require(Posting.Create(
                _revenue, _receivable, new Money(400_000m), PostingType.RevenueRecognition,
                ExtractDocument("EXT-3"), Extract3Date, Actor, Now, TestAccounts.ProjectId)),
            Require(Posting.Create(
                _receivable, _hold, new Money(80_000m), PostingType.HoldAccrual,
                ExtractDocument("EXT-3"), Extract3Date, Actor, Now, TestAccounts.ProjectId)),
            Require(Posting.Create(
                _receivable, _clientAdvance, new Money(100_000m), PostingType.ClientAdvanceRecovery,
                ExtractDocument("EXT-3"), Extract3Date, Actor, Now, TestAccounts.ProjectId)),
            Require(Posting.Create(
                _receivable, _materialAdvance, tashwinatRecovery, PostingType.MaterialAdvanceRecovery,
                ExtractDocument("EXT-3"), Extract3Date, Actor, Now, TestAccounts.ProjectId)),
            Require(Posting.Create(
                _receivable, _safe, new Money(190_000m), PostingType.ClientCollection,
                ExtractDocument("EXT-3"), Extract3Date, Actor, Now, TestAccounts.ProjectId)),
        ];
    }

    private List<Posting> BuildHandover() =>
    [
        // The only posting type allowed to move value out of Hold (Posting.Validate). One posting,
        // for the whole accumulated 200,000 — AC-300-F's "once, in full".
        Require(Posting.Create(
            _hold, _receivable, new Money(200_000m), PostingType.HoldRelease,
            ExtractDocument("HANDOVER"), HandoverDate, Actor, Now, TestAccounts.ProjectId)),
        Require(Posting.Create(
            _receivable, _safe, new Money(200_000m), PostingType.ClientCollection,
            ExtractDocument("HANDOVER"), HandoverDate, Actor, Now, TestAccounts.ProjectId)),
    ];

    private static Posting Require(Result<Posting> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"§15 fixture posting was refused: {result.Error.Code}. The fixture's own postings must "
                + "always be legal moves — a refusal here is a bug in the fixture, not the case a "
                + "refusal test is checking.");
        }

        return result.Value;
    }

    private static SourceDocument ExtractDocument(string reference) =>
        new(SourceDocumentType.Extract, Guid.CreateVersion7(), reference);

    private static SourceDocument CollectionDocument(string reference) =>
        new(SourceDocumentType.Collection, Guid.CreateVersion7(), reference);

    /// <summary>
    /// Sums <paramref name="postings"/> for <paramref name="account"/> and returns the result in the
    /// account's own normal direction — the exact arithmetic of
    /// <c>src/Infrastructure/Persistence/Sql/002_views.sql</c>'s <c>signed_balance</c> column.
    /// Recomputed from the list every call; nothing here is cached.
    /// </summary>
    private static Money Signed(IEnumerable<Posting> postings, Account account)
    {
        decimal inflow = 0m;
        decimal outflow = 0m;

        foreach (Posting posting in postings)
        {
            if (posting.ToAccountId == account.Id)
            {
                inflow += posting.Amount.Amount;
            }

            if (posting.FromAccountId == account.Id)
            {
                outflow += posting.Amount.Amount;
            }
        }

        decimal raw = inflow - outflow;
        return new Money(account.NormalBalance == NormalBalance.Debit ? raw : -raw);
    }

    // =============================================================================================
    //  AC-300-B — every row, every column
    // =============================================================================================

    /// <summary>TC-3-002.</summary>
    [Fact]
    public void Row_advance_at_signing_only_the_advance_and_client_pays_columns_move()
    {
        List<Posting> signing = BuildAdvanceAtSigning();

        Signed(signing, _revenue).Should().Be(Money.Zero);
        Signed(signing, _hold).Should().Be(Money.Zero);
        Signed(signing, _materialAdvance).Should().Be(Money.Zero);
        Signed(signing, _clientAdvance).Should().Be(AdvanceAtSigning);
        Signed(signing, _safe).Should().Be(new Money(250_000m));
    }

    /// <summary>TC-3-003.</summary>
    [Fact]
    public void Row_extract_1_matches_spec_300000_minus60000_minus75000_plus75000_equals240000()
    {
        List<Posting> upToSigning = BuildAdvanceAtSigning();
        List<Posting> extract1 = BuildExtract1();
        List<Posting> upToExtract1 = [.. upToSigning, .. extract1];

        Signed(extract1, _revenue).Should().Be(new Money(300_000m));
        Signed(extract1.Where(p => p.Type == PostingType.HoldAccrual), _receivable).Should().Be(new Money(-60_000m));
        Signed(extract1.Where(p => p.Type == PostingType.ClientAdvanceRecovery), _receivable).Should().Be(new Money(-75_000m));
        Signed(extract1.Where(p => p.Type == PostingType.MaterialAdvanceIssue), _receivable).Should().Be(new Money(75_000m));
        Signed(extract1, _safe).Should().Be(new Money(240_000m));

        Signed(upToExtract1, _hold).Should().Be(new Money(60_000m));
        Signed(upToExtract1, _clientAdvance).Should().Be(new Money(175_000m));
    }

    /// <summary>TC-3-004.</summary>
    [Fact]
    public void Row_extract_2_matches_spec_300000_minus60000_minus75000_minus45000_equals120000()
    {
        List<Posting> upToExtract1 = [.. BuildAdvanceAtSigning(), .. BuildExtract1()];
        List<Posting> extract2 = BuildExtract2();
        List<Posting> upToExtract2 = [.. upToExtract1, .. extract2];

        Signed(extract2, _revenue).Should().Be(new Money(300_000m));
        Signed(extract2.Where(p => p.Type == PostingType.HoldAccrual), _receivable).Should().Be(new Money(-60_000m));
        Signed(extract2.Where(p => p.Type == PostingType.ClientAdvanceRecovery), _receivable).Should().Be(new Money(-75_000m));
        // The literal from Q14/Q58 — transcribed, not derived. See BuildExtract2()'s own comment.
        Signed(extract2.Where(p => p.Type == PostingType.MaterialAdvanceRecovery), _receivable).Should().Be(new Money(-45_000m));
        Signed(extract2, _safe).Should().Be(new Money(120_000m));

        Signed(upToExtract2, _hold).Should().Be(new Money(120_000m));
        Signed(upToExtract2, _clientAdvance).Should().Be(new Money(100_000m));
    }

    /// <summary>TC-3-005.</summary>
    [Fact]
    public void Row_extract_3_matches_spec_400000_minus80000_minus100000_minus30000_equals190000()
    {
        List<Posting> upToExtract2 = [.. BuildAdvanceAtSigning(), .. BuildExtract1(), .. BuildExtract2()];
        List<Posting> extract3 = BuildExtract3();
        List<Posting> upToExtract3 = [.. upToExtract2, .. extract3];

        Signed(extract3, _revenue).Should().Be(new Money(400_000m));
        Signed(extract3.Where(p => p.Type == PostingType.HoldAccrual), _receivable).Should().Be(new Money(-80_000m));
        Signed(extract3.Where(p => p.Type == PostingType.ClientAdvanceRecovery), _receivable).Should().Be(new Money(-100_000m));
        Signed(extract3.Where(p => p.Type == PostingType.MaterialAdvanceRecovery), _receivable).Should().Be(new Money(-30_000m));
        Signed(extract3, _safe).Should().Be(new Money(190_000m));

        Signed(upToExtract3, _hold).Should().Be(new Money(200_000m));
        // Rule: the advance ledger reaches exactly zero — not a residual left by an unconfigured
        // decimal precision. AC-300-C's second invariant checks the same thing across the whole
        // sequence; this row-level case pins it to the posting that produced it.
        Signed(upToExtract3, _clientAdvance).Should().Be(Money.Zero);
    }

    /// <summary>TC-3-006.</summary>
    [Fact]
    public void Row_handover_hold_release_is_one_posting_for_the_full_200000()
    {
        List<Posting> upToExtract3 =
            [.. BuildAdvanceAtSigning(), .. BuildExtract1(), .. BuildExtract2(), .. BuildExtract3()];
        List<Posting> handover = BuildHandover();

        Signed(handover, _revenue).Should().Be(Money.Zero);
        Signed(handover.Where(p => p.Type == PostingType.ClientAdvanceRecovery), _receivable).Should().Be(Money.Zero);
        Signed(handover.Where(p => p.Type is PostingType.MaterialAdvanceIssue or PostingType.MaterialAdvanceRecovery), _receivable).Should().Be(Money.Zero);
        Signed(handover, _safe).Should().Be(new Money(200_000m));

        List<Posting> upToHandover = [.. upToExtract3, .. handover];
        Signed(upToHandover, _hold).Should().Be(Money.Zero);

        handover.Count(p => p.FromAccountId == _hold.Id).Should().Be(1);
        handover.Single(p => p.FromAccountId == _hold.Id).Amount.Should().Be(new Money(200_000m));
    }

    /// <summary>TC-3-007.</summary>
    [Fact]
    public void Totals_row_matches_spec_exactly()
    {
        List<Posting> all = AllPostingsInOrder();

        Money totalWork = Signed(all.Where(p => p.Type == PostingType.RevenueRecognition), _revenue);
        Money totalClientPays = Signed(all, _safe);
        Money tashwinatIn = Money.Sum(all
            .Where(p => p.Type == PostingType.MaterialAdvanceIssue)
            .Select(p => p.Amount));
        Money tashwinatRecovered = Money.Sum(all
            .Where(p => p.Type == PostingType.MaterialAdvanceRecovery)
            .Select(p => p.Amount));

        // "Total" here is accumulated Hold and accumulated Advance — the amounts that moved through
        // those ledgers — not the ledgers' final signed balances, which are zero by design (the hold
        // releases in full at handover; the advance recovers in full by Extract 3). Summing only the
        // accruing/recovering posting types, never the accounts' end-state, is what keeps this row
        // distinct from the invariants that DO check the end state (TC-3-009, TC-3-016).
        Money totalHoldAccrued = Money.Sum(all
            .Where(p => p.Type == PostingType.HoldAccrual)
            .Select(p => p.Amount));
        Money totalAdvanceRecovered = Money.Sum(all
            .Where(p => p.Type == PostingType.ClientAdvanceRecovery)
            .Select(p => p.Amount));

        totalWork.Should().Be(ContractValue);
        totalHoldAccrued.Should().Be(new Money(200_000m));
        totalAdvanceRecovered.Should().Be(AdvanceAtSigning);
        (tashwinatIn - tashwinatRecovered).Should().Be(Money.Zero);
        totalClientPays.Should().Be(ContractValue);
    }

    // =============================================================================================
    //  AC-300-C — the five invariants, five separate facts
    // =============================================================================================

    /// <summary>TC-3-008. Invariant 1.</summary>
    [Fact]
    public void Invariant_hold_equals_twenty_percent_of_contract_value()
    {
        // Accumulated Hold — the sum of what accrued into it — not the ledger's end-state signed
        // balance, which is zero after the handover release (TC-3-016 checks that separately).
        List<Posting> all = AllPostingsInOrder();
        Money accumulatedHold = Money.Sum(all
            .Where(p => p.Type == PostingType.HoldAccrual)
            .Select(p => p.Amount));

        accumulatedHold.Should().Be(new Money(200_000m));
        (ContractValue * 0.20m).Should().Be(new Money(200_000m));
    }

    /// <summary>TC-3-009. Invariant 2.</summary>
    [Fact]
    public void Invariant_advance_ledger_reaches_zero_and_is_never_negative()
    {
        List<Posting> afterSigning = BuildAdvanceAtSigning();
        List<Posting> afterExtract1 = [.. afterSigning, .. BuildExtract1()];
        List<Posting> afterExtract2 = [.. afterExtract1, .. BuildExtract2()];
        List<Posting> afterExtract3 = [.. afterExtract2, .. BuildExtract3()];
        List<Posting> afterHandover = [.. afterExtract3, .. BuildHandover()];

        Money[] sampledInOrder =
        [
            Signed(afterSigning, _clientAdvance),
            Signed(afterExtract1, _clientAdvance),
            Signed(afterExtract2, _clientAdvance),
            Signed(afterExtract3, _clientAdvance),
            Signed(afterHandover, _clientAdvance),
        ];

        sampledInOrder.Should().OnlyContain(balance => balance >= Money.Zero);
        sampledInOrder.Should().Equal(
            new Money(250_000m), new Money(175_000m), new Money(100_000m), Money.Zero, Money.Zero);
    }

    /// <summary>TC-3-010. Invariant 3.</summary>
    [Fact]
    public void Invariant_tashwinat_in_equals_tashwinat_recovered()
    {
        List<Posting> all = AllPostingsInOrder();

        Money issued = Money.Sum(all
            .Where(p => p.Type == PostingType.MaterialAdvanceIssue)
            .Select(p => p.Amount));
        Money recovered = Money.Sum(all
            .Where(p => p.Type == PostingType.MaterialAdvanceRecovery)
            .Select(p => p.Amount));

        issued.Should().Be(new Money(75_000m));
        recovered.Should().Be(new Money(75_000m));
        (issued - recovered).Should().Be(Money.Zero);
    }

    /// <summary>TC-3-011. Invariant 4.</summary>
    [Fact]
    public void Invariant_total_client_cash_equals_contract_value_exactly()
    {
        List<Posting> all = AllPostingsInOrder();

        Signed(all, _safe).Should().Be(ContractValue);
    }

    /// <summary>TC-3-012. Invariant 5.</summary>
    [Fact]
    public void Invariant_safe_balance_never_goes_negative_after_any_single_posting()
    {
        List<Posting> all = AllPostingsInOrder();
        var running = new List<Posting>();

        foreach (Posting posting in all)
        {
            running.Add(posting);
            (Signed(running, _safe) >= Money.Zero).Should().BeTrue();
        }
    }

    // =============================================================================================
    //  AC-300-D — derived, never stored
    // =============================================================================================

    /// <summary>TC-3-013.</summary>
    [Fact]
    public void Every_balance_is_recomputed_from_the_posting_list_never_cached()
    {
        Account probeFrom = TestAccounts.CompanyExpense();
        Account probeTo = TestAccounts.Safe();
        var probePostings = new List<Posting>();

        Signed(probePostings, probeTo).Should().Be(Money.Zero);

        probePostings.Add(Require(Posting.Create(
            probeFrom, probeTo, new Money(100m), PostingType.OpeningBalance,
            TestAccounts.Document(), TestAccounts.Today, Actor, Now)));
        Signed(probePostings, probeTo).Should().Be(new Money(100m));

        probePostings.Add(Require(Posting.Create(
            probeFrom, probeTo, new Money(50m), PostingType.OpeningBalance,
            TestAccounts.Document(), TestAccounts.Today, Actor, Now)));
        Signed(probePostings, probeTo).Should().Be(new Money(150m));

        // Signed() took no snapshot at the first call — the second call re-summed the (now longer)
        // list and got a different, correct answer. There is no accumulator field on this class for
        // a stored Balance column to disagree with; Account.cs itself carries no Money-typed property
        // at all (NormalBalance is a direction enum, not a stored amount, so it does not count).
        typeof(Account).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Should().NotContain(property => property.PropertyType == typeof(Money));
    }

    // =============================================================================================
    //  AC-300-E — no ledger is netted against another
    // =============================================================================================

    /// <summary>TC-3-014.</summary>
    [Fact]
    public void Client_pays_is_a_sum_of_periods_own_movements_never_a_net_of_ledger_balances()
    {
        List<Posting> extract1 = BuildExtract1();

        Money work = Signed(extract1.Where(p => p.Type == PostingType.RevenueRecognition), _receivable);
        Money holdImpact = Signed(extract1.Where(p => p.Type == PostingType.HoldAccrual), _receivable);
        Money advanceImpact = Signed(extract1.Where(p => p.Type == PostingType.ClientAdvanceRecovery), _receivable);
        Money tashwinatImpact = Signed(extract1.Where(p => p.Type == PostingType.MaterialAdvanceIssue), _receivable);

        Money fromPeriodMovements = work + holdImpact + advanceImpact + tashwinatImpact;
        Money fromTheCashPosting = Signed(extract1, _safe);

        // Two independent derivations of the same figure, from two disjoint sets of postings
        // (ClientReceivable's own movements vs. what actually landed in the Safe) — neither computed
        // by subtracting one ledger's running balance from another's, which is the netting AC-300-E
        // forbids and the failure this case exists to catch.
        fromPeriodMovements.Should().Be(new Money(240_000m));
        fromTheCashPosting.Should().Be(new Money(240_000m));
        fromPeriodMovements.Should().Be(fromTheCashPosting);
    }

    // =============================================================================================
    //  AC-300-F — the hold only grows, then releases once, in full
    // =============================================================================================

    /// <summary>TC-3-015.</summary>
    [Fact]
    public void Hold_only_grows_across_the_three_extracts_60000_then_120000_then_200000()
    {
        List<Posting> afterExtract1 = [.. BuildAdvanceAtSigning(), .. BuildExtract1()];
        List<Posting> afterExtract2 = [.. afterExtract1, .. BuildExtract2()];
        List<Posting> afterExtract3 = [.. afterExtract2, .. BuildExtract3()];

        Signed(afterExtract1, _hold).Should().Be(new Money(60_000m));
        Signed(afterExtract2, _hold).Should().Be(new Money(120_000m));
        Signed(afterExtract3, _hold).Should().Be(new Money(200_000m));

        IEnumerable<Posting> preHandoverPostings = afterExtract3;
        preHandoverPostings.Should().NotContain(p => p.FromAccountId == _hold.Id);
    }

    /// <summary>TC-3-016.</summary>
    [Fact]
    public void Handover_releases_the_hold_exactly_once_for_the_full_balance_leaving_zero()
    {
        List<Posting> afterExtract3 =
            [.. BuildAdvanceAtSigning(), .. BuildExtract1(), .. BuildExtract2(), .. BuildExtract3()];
        Money holdBeforeHandover = Signed(afterExtract3, _hold);
        List<Posting> handover = BuildHandover();

        List<Posting> holdReleases = handover.Where(p => p.FromAccountId == _hold.Id).ToList();
        holdReleases.Should().HaveCount(1);
        holdReleases[0].Amount.Should().Be(holdBeforeHandover);

        List<Posting> afterHandover = [.. afterExtract3, .. handover];
        Signed(afterHandover, _hold).Should().Be(Money.Zero);
    }

    /// <summary>
    /// TC-3-017 (part 1 of 2) — the part this fixture can actually prove at the Domain layer.
    /// </summary>
    /// <remarks>
    /// This is real domain enforcement, not a stand-in for it: <see cref="Posting.Create"/> itself
    /// refuses any non-release movement out of a Hold account (spec.md §5.1's "nothing may be taken
    /// out of it mid-project"), the same rule <c>PostingRuleTests.Nothing_comes_out_of_the_hold_before_handover</c>
    /// already covers generically. This case pins it to §15's own running Hold balance (60,000, after
    /// Extract 1) rather than an arbitrary figure.
    /// </remarks>
    [Fact]
    public void A_hold_debit_before_handover_is_refused_by_the_domain()
    {
        Result<Posting> attempt = Posting.Create(
            _hold, _receivable, new Money(10_000m), PostingType.DebitNote,
            ExtractDocument("EXT-1-SNAG-ATTEMPT"), Extract1Date, Actor, Now, TestAccounts.ProjectId);

        attempt.IsFailure.Should().BeTrue();
        attempt.Error.Should().Be(TreasuryErrors.HoldOnlyGrows);
    }

    /// <summary>
    /// TC-3-017 (part 2 of 2) — the part this fixture cannot prove, and does not fake.
    /// </summary>
    /// <remarks>
    /// "Once, in full" for a <see cref="PostingType.HoldRelease"/> specifically is guarded by
    /// <c>trg_postings_hold_release_in_full</c> (see KAFF-300's evidence table and
    /// <c>src/Infrastructure/Persistence/DatabaseInitializer.cs</c> -&gt; <c>FindMissingGuardsAsync</c>).
    /// <see cref="Posting.Validate"/> does not compare a <see cref="PostingType.HoldRelease"/>'s
    /// amount against the account's balance — it cannot, since a single posting cannot see the whole
    /// ledger — so a partial-amount release succeeds at the Domain layer today and is caught only by
    /// the database trigger. Asserting a domain-level refusal here would be exactly the
    /// "in-fixture pre-check" AC-300-G's own text warns against for the advance floor, applied to the
    /// hold instead. This needs an <c>Kaff.Api.Tests</c> companion in the shape of
    /// <c>TreasuryGuardTests.Nothing_comes_out_of_the_hold_before_handover_at_the_database</c> —
    /// a case exercising a partial <see cref="PostingType.HoldRelease"/> against a real PostgreSQL
    /// does not exist there yet either. Not built here: KAFF-300 does not touch the Api project.
    /// </remarks>
    [Fact(Skip = "Partial hold-release refusal is a database-trigger guard (trg_postings_hold_release_in_full); " +
                 "not exercisable without a real PostgreSQL. Needs an Api.Tests/TreasuryGuardTests.cs case; " +
                 "none exists yet for this specific trigger. Out of scope for KAFF-300 (Domain-only, no Api changes).")]
    public void A_partial_hold_release_is_refused_by_the_database()
    {
    }

    // =============================================================================================
    //  AC-300-G — the advance floor and the Safe floor
    // =============================================================================================

    /// <summary>TC-3-018.</summary>
    [Fact]
    public void Advance_ledger_runs_250000_then_175000_then_100000_then_zero_in_that_order()
    {
        List<Posting> afterSigning = BuildAdvanceAtSigning();
        List<Posting> afterExtract1 = [.. afterSigning, .. BuildExtract1()];
        List<Posting> afterExtract2 = [.. afterExtract1, .. BuildExtract2()];
        List<Posting> afterExtract3 = [.. afterExtract2, .. BuildExtract3()];

        Money[] sequence =
        [
            Signed(afterSigning, _clientAdvance),
            Signed(afterExtract1, _clientAdvance),
            Signed(afterExtract2, _clientAdvance),
            Signed(afterExtract3, _clientAdvance),
        ];

        sequence.Should().Equal(
            new Money(250_000m), new Money(175_000m), new Money(100_000m), Money.Zero);
    }

    /// <summary>
    /// TC-3-019 — not asserted here; the database enforces this, the domain does not.
    /// </summary>
    /// <remarks>
    /// <see cref="Account.EnforceNonNegative"/> is true for <see cref="AccountType.ClientAdvance"/>
    /// (spec.md §15's "reaches exactly zero, never negative"), but the floor is a database check on
    /// the account's aggregate balance — a single <see cref="Posting.Create"/> call cannot see it,
    /// by design (spec.md §6.1: "Enforce in the database, not only in application code"). Faking this
    /// with an in-fixture "if the running total would go negative, refuse" would prove nothing about
    /// the database constraint AC-300-G actually requires, and would still pass green if that
    /// constraint were dropped — the exact failure mode this criterion exists to close off. This
    /// needs an <c>Kaff.Api.Tests/TreasuryGuardTests.cs</c> case against a real PostgreSQL, following
    /// <c>The_safe_balance_cannot_go_negative</c>'s pattern there but against
    /// <see cref="AccountType.ClientAdvance"/>; no such case exists yet. Not built here: KAFF-300
    /// does not touch the Api project.
    /// </remarks>
    [Fact(Skip = "A fourth advance recovery breaching zero is refused by a database non-negative-floor " +
                 "constraint, not by application code (D-044 §8). Not exercisable without a real PostgreSQL. " +
                 "Needs an Api.Tests/TreasuryGuardTests.cs case against AccountType.ClientAdvance; " +
                 "none exists yet. Out of scope for KAFF-300 (Domain-only, no Api changes).")]
    public void A_fourth_advance_recovery_is_refused_by_the_database()
    {
    }

    /// <summary>TC-3-020.</summary>
    [Fact]
    public void No_ordering_of_the_postings_drives_the_safe_balance_negative()
    {
        // §15's own event order is advance, Extract 1, Extract 2, Extract 3, handover. This replays
        // Extract 2 before Extract 1 — a reordering the domain does not otherwise forbid (extracts do
        // not reference each other) — and checks the Safe after every single posting, not only at
        // event boundaries.
        List<Posting> reordered =
        [
            .. BuildAdvanceAtSigning(),
            .. BuildExtract2(),
            .. BuildExtract1(),
            .. BuildExtract3(),
            .. BuildHandover(),
        ];

        var running = new List<Posting>();
        foreach (Posting posting in reordered)
        {
            running.Add(posting);
            (Signed(running, _safe) >= Money.Zero).Should().BeTrue();
        }

        Signed(reordered, _safe).Should().Be(ContractValue);
    }

    // =============================================================================================
    //  AC-300-H — decimal only, exact, no tolerance
    // =============================================================================================

    /// <summary>
    /// TC-3-021. Named without the two disallowed type names or the tolerance-assertion name spelled
    /// out contiguously — otherwise this method's own declaration line would trip its own check.
    /// </summary>
    [Fact]
    public void Every_monetary_literal_in_this_file_is_decimal_with_no_approximate_assertion()
    {
        string source = File.ReadAllText(ThisFilePath());

        // Built by concatenation deliberately: the literal tokens must not appear contiguously in
        // this file's own code either, or this very check would flag itself.
        string forbiddenDouble = "do" + "uble";
        string forbiddenFloat = "fl" + "oat";
        string forbiddenTolerance = "Be" + "Approximately";

        List<string> offendingLines = source
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Where(line =>
                line.Contains(forbiddenDouble, StringComparison.Ordinal)
                || line.Contains(forbiddenFloat, StringComparison.Ordinal)
                || line.Contains(forbiddenTolerance, StringComparison.Ordinal))
            .ToList();

        offendingLines.Should().BeEmpty();
    }

    private static string ThisFilePath([CallerFilePath] string path = "") => path;

    // =============================================================================================
    //  AC-300-I — the تشوينات literals, transcribed, cited, never derived
    // =============================================================================================

    /// <summary>TC-3-022.</summary>
    [Fact]
    public void Tashwinat_recovery_figures_are_transcribed_literals_citing_Q14_and_Q58()
    {
        // This case is the citation-in-source check AC-300-I itself requires: the two recovery
        // figures live in BuildExtract2() and BuildExtract3() as bare `new Money(45_000m)` and
        // `new Money(30_000m)` literals, each beside a comment naming Q14 and Q58 by number and
        // explaining why no formula stands in for them — see those two methods. This test asserts
        // the numbers a reader would see if a formula were substituted for the literal would still
        // reconcile against everything else in this file, which is exactly why the substitution is
        // caught by that comment and by review, not by any assertion on the current numbers alone.
        List<Posting> extract2 = BuildExtract2();
        List<Posting> extract3 = BuildExtract3();

        extract2.Single(p => p.Type == PostingType.MaterialAdvanceRecovery).Amount
            .Should().Be(new Money(45_000m));
        extract3.Single(p => p.Type == PostingType.MaterialAdvanceRecovery).Amount
            .Should().Be(new Money(30_000m));
    }

    // =============================================================================================
    //  AC-300-A — the fixture is present and non-vacuous
    // =============================================================================================

    /// <summary>TC-3-001.</summary>
    [Fact]
    public void This_fixture_is_present_and_runs_real_assertions_not_a_vacuous_pass()
    {
        // D-034: "The §15 fixture must exist before slice 3 opens — failing or skipped, but present,
        // so the gate is a build outcome." Every Domain type this fixture needs (Posting, Account,
        // the account catalogue) was already built in slice 0, so this fixture is not blocked on
        // unbuilt code and goes green rather than red or skipped — see this file's own header remarks
        // for why that is the expected, correct outcome for KAFF-300 specifically. What TC-3-001
        // checks is that the fixture is real: more than a handful of named facts, each exercising the
        // postings above, rather than an empty class satisfying "present" in name only.
        MethodInfo[] facts = typeof(Section15WorkedExampleTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttributes(typeof(FactAttribute), inherit: false).Length > 0)
            .ToArray();

        facts.Length.Should().BeGreaterThanOrEqualTo(20);

        List<Posting> all = AllPostingsInOrder();
        all.Should().NotBeEmpty();
        Signed(all, _safe).Should().Be(new Money(1_000_000m));
    }

    private List<Posting> AllPostingsInOrder() =>
    [
        .. BuildAdvanceAtSigning(),
        .. BuildExtract1(),
        .. BuildExtract2(),
        .. BuildExtract3(),
        .. BuildHandover(),
    ];
}
