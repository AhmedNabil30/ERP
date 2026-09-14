using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-305 — the posting-type × account-pair legality table. AC-305-A..G.
/// </summary>
public sealed class PostingAccountLegalityTests
{
    // ---- AC-305-A — a legal pair is accepted ----

    [Fact]
    public void A_client_advance_receipt_between_a_cash_instrument_and_client_advance_succeeds()
    {
        Result<Posting> result = Posting.Create(
            TestAccounts.Safe(),
            TestAccounts.ClientAdvance(),
            new Money(75_000m),
            PostingType.ClientAdvanceReceipt,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsSuccess.Should().BeTrue();
    }

    // ---- AC-305-B — an illegal pair is refused ----

    [Fact]
    public void A_supplier_payment_cannot_touch_the_hold_ledger()
    {
        // Rule 1/10. spec.md names no business event where a SupplierPayment involves the hold at
        // all; only HoldAccrual/HoldRelease may touch a Hold account (rule 2).
        Result<Posting> result = Posting.Create(
            TestAccounts.ClientReceivable(),
            TestAccounts.Hold(),
            new Money(10_000m),
            PostingType.SupplierPayment,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.PostingTypeAccountMismatch);
    }

    // ---- AC-305-C — only HoldRelease may debit the hold, and this is a type-aware table check ----

    [Theory]
    [InlineData(PostingType.SupplierPayment)]
    [InlineData(PostingType.DebitNote)]
    [InlineData(PostingType.Adjustment)]
    public void Only_hold_release_may_take_value_out_of_the_hold_ledger(PostingType attempted)
    {
        Result<Posting> result = Posting.Create(
            TestAccounts.Hold(),
            TestAccounts.ClientReceivable(),
            new Money(10_000m),
            attempted,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsFailure.Should().BeTrue();
    }

    // ---- AC-305-D — تشوينات only moves via its two named types ----

    [Theory]
    [InlineData(PostingType.SupplierPayment)]
    [InlineData(PostingType.ClientCollection)]
    [InlineData(PostingType.Adjustment)]
    public void Material_advance_refuses_every_type_other_than_its_two_named_ones(PostingType attempted)
    {
        Result<Posting> result = Posting.Create(
            TestAccounts.MaterialAdvance(),
            TestAccounts.ClientReceivable(),
            new Money(10_000m),
            attempted,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.PostingTypeAccountMismatch);
    }

    // ---- AC-305-E — a non-cash posting type never touches Safe or Bank ----

    [Fact]
    public void Depreciation_refuses_a_safe_account_on_either_side()
    {
        Result<Posting> result = Posting.Create(
            TestAccounts.Safe(),
            TestAccounts.AccumulatedDepreciation(),
            new Money(5_000m),
            PostingType.Depreciation,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.PostingTypeAccountMismatch);
    }

    [Fact]
    public void Depreciation_refuses_a_bank_account_on_either_side()
    {
        Result<Posting> result = Posting.Create(
            TestAccounts.DepreciationExpense(),
            TestAccounts.Bank(),
            new Money(5_000m),
            PostingType.Depreciation,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.PostingTypeAccountMismatch);
    }

    [Fact]
    public void Depreciation_between_two_non_cash_accounts_succeeds()
    {
        Result<Posting> result = Posting.Create(
            TestAccounts.DepreciationExpense(),
            TestAccounts.AccumulatedDepreciation(),
            new Money(5_000m),
            PostingType.Depreciation,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now);

        result.IsSuccess.Should().BeTrue();
    }

    // ---- AC-305-F — every PostingType has at least one legal pair, or is provably held pending Q91 ----

    [Fact]
    public void Every_posting_type_has_at_least_one_legal_pair_or_is_named_pending_q91()
    {
        // Mirrors PostingTypes.NatureOf's own discipline: a test enumerates all 41 members. Brute
        // force over the full AccountType catalogue rather than a hand-picked example, so this fails
        // the moment a future edit to PostingAccountLegality accidentally makes some type's allowed
        // set empty — the same way an over-tight restricted-ledger row would.
        AccountType[] allAccountTypes = Enum.GetValues<AccountType>();

        foreach (PostingType type in Enum.GetValues<PostingType>())
        {
            if (PostingAccountLegality.PendingQ91.Contains(type))
            {
                continue;
            }

            bool hasLegalPair = allAccountTypes
                .SelectMany(from => allAccountTypes.Select(to => (from, to)))
                .Where(pair => pair.from != pair.to)
                .Any(pair => PostingAccountLegality.Validate(pair.from, pair.to, type).IsSuccess);

            hasLegalPair.Should().BeTrue(
                $"{type} has no legal account pair under rules 2-8 and is not named in PendingQ91");
        }
    }

    [Fact]
    public void Pending_q91_is_exactly_the_types_spec_md_names_no_pair_for()
    {
        // Pinned as an exact set, both directions, same reasoning as
        // CatalogueCompletenessTests.Exactly_three_account_types_are_floored_at_zero: adding a type
        // here silently would hide a gap Q91 needs to answer, and removing one silently would mean
        // this table started asserting a pair nobody confirmed. Changing this list is the conversation
        // with Karim that closes Q91, not a code edit made alone.
        PostingType[] expected =
        [
            // Named by the story itself (KAFF-305, Q91):
            PostingType.SiteExpensePayment,
            PostingType.AssetPurchase,
            PostingType.LoanDrawdown,
            PostingType.PeriodCloseTransfer,
            PostingType.YearEndProfitTransfer,

            // Found by this story's own exhaustiveness sweep, reported back through Q91:
            PostingType.OpeningBalance,
            PostingType.ChequeDeposit,
            PostingType.ChequeClearance,
            PostingType.ChequeBounce,
        ];

        PostingAccountLegality.PendingQ91.Should().BeEquivalentTo(expected);
    }

    // ---- AC-305-G — a reversal is checked against the same table using its own type ----

    [Fact]
    public void A_reversal_of_a_material_advance_issue_is_checked_against_the_same_table()
    {
        Account materialAdvance = TestAccounts.MaterialAdvance();
        Account receivable = TestAccounts.ClientReceivable();

        Posting original = Posting.Create(
            materialAdvance,
            receivable,
            new Money(75_000m),
            PostingType.MaterialAdvanceIssue,
            TestAccounts.Document(),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            TestAccounts.ProjectId).Value;

        Result<Posting> reversal = Posting.Reverse(
            original,
            materialAdvance,
            receivable,
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now);

        // The reversal runs receivable -> materialAdvance under MaterialAdvanceIssue (its inherited
        // type). That is still one of MaterialAdvance's two allowed types, so it passes — but it is
        // passing the same restricted-ledger check a forward posting would, not an exemption from it.
        reversal.IsSuccess.Should().BeTrue();
        reversal.Value.Type.Should().Be(PostingType.MaterialAdvanceIssue);
    }

    [Fact]
    public void A_rule_violating_reversal_would_be_refused_by_the_same_table_rule_9_no_exemption()
    {
        // Constructs the illegal case directly through PostingAccountLegality.Validate, the exact
        // function Posting.Reverse calls with the swapped pair — proving rule 9's "no special
        // exemption" without needing an illegal original to reverse (Posting.Create would already
        // refuse one, so there is no legal way to obtain an illegal original to reverse).
        Result result = PostingAccountLegality.Validate(
            AccountType.ClientReceivable, AccountType.Hold, PostingType.SupplierPayment);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TreasuryErrors.PostingTypeAccountMismatch);
    }
}
