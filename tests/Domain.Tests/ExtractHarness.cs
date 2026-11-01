using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// A signed Lump Sum project, its accounts and an in-memory ledger, so extract tests can carry an extract through
/// the chain and read balances the way production does: by summing postings, never from a stored figure.
/// </summary>
internal sealed class ExtractHarness
{
    public static readonly DateOnly SigningDate = new(2026, 1, 1);
    public static readonly DateTimeOffset Now = TestAccounts.Now;

    public ExtractHarness(RulingSet? rulings = null)
    {
        Rulings = rulings ?? RulingSet.Default;
        Project = Project.Create("PRJ-X", "X", TestAccounts.ClientId, ContractType.LumpSum, Now).Value;
        Project.SetLumpSumTerms(
            ContractDefaults.AdvanceRate, ContractDefaults.HoldRate, ContractDefaults.AdvanceRecoveryRate,
            ContractDefaults.MaterialAdvanceRate, delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        Project.Sign(SigningDate, new Money(1_000_000m)).IsSuccess.Should().BeTrue();
        Project.TransitionTo(ProjectStatus.Active, SigningDate).IsSuccess.Should().BeTrue();

        Guid id = Project.Id;
        Revenue = Account.Create(
            AccountType.ContractRevenue, "PRJ-REV", "x", "Project revenue", Currency.Egp, SigningDate, id).Value;
        Receivable = TestAccounts.ClientReceivable(id);
        ClientAdvance = TestAccounts.ClientAdvance(id);
        Hold = TestAccounts.Hold(id);
        MaterialAdvance = TestAccounts.MaterialAdvance(id);
        Safe = TestAccounts.Safe();
    }

    public RulingSet Rulings { get; }

    public Project Project { get; }

    public Account Revenue { get; }

    public Account Receivable { get; }

    public Account ClientAdvance { get; }

    public Account Hold { get; }

    public Account MaterialAdvance { get; }

    public Account Safe { get; }

    public List<Posting> Ledger { get; } = [];

    public ExtractAccounts Accounts => new(Revenue, Receivable, Hold, ClientAdvance, MaterialAdvance, Safe);

    public ExtractPostingContext Context(RulingSet? rulings = null) =>
        new(Project, Accounts, rulings ?? Rulings, new DateOnly(2026, 2, 1));

    public ExtractLedgerBalances Balances() => new(Balance(ClientAdvance), Balance(MaterialAdvance), Balance(Hold));

    public Money Balance(Account account)
    {
        decimal raw = Ledger.Where(p => p.ToAccountId == account.Id).Sum(p => p.Amount.Amount)
            - Ledger.Where(p => p.FromAccountId == account.Id).Sum(p => p.Amount.Amount);
        return new Money(account.NormalBalance == NormalBalance.Debit ? raw : -raw);
    }

    public Money Sum(PostingType type) => Money.Sum(Ledger.Where(p => p.Type == type).Select(p => p.Amount));

    /// <summary>Commits a set to the ledger, asserting after every posting that the two floored ledgers hold.</summary>
    public void Commit(PostingSet? set)
    {
        if (set is null)
        {
            return;
        }

        foreach (Posting posting in set.Postings)
        {
            Ledger.Add(posting);
            (Balance(Safe) >= Money.Zero).Should().BeTrue("the safe never goes negative after any posting");
            (Balance(ClientAdvance) >= Money.Zero).Should().BeTrue("the advance ledger never goes negative");
        }
    }

    public static IReadOnlyList<ExtractPostedLeg> LegsOf(PostingSet set) =>
        [.. set.Postings.Select(p => new ExtractPostedLeg(p, set.Accounts[p.FromAccountId], set.Accounts[p.ToAccountId]))];

    public Extract Prepare(
        Guid preparer, int number = 1, decimal cumulative = 300_000m, decimal previous = 0m,
        decimal onSite = 100_000m, decimal installed = 0m) =>
        Extract.Prepare(
            Project, number, new DateOnly(2026, 1, 2), new DateOnly(2026, 2, 1),
            new Money(cumulative), new Money(previous), new Money(onSite), new Money(installed), preparer, Now).Value;

    public static Guid NewUser() => Guid.CreateVersion7();

    /// <summary>
    /// Walks an extract forward to <paramref name="target"/> with a fresh actor at every step, so no
    /// separation-of-duties refusal gets in the way. Postings it produces are committed to the ledger.
    /// </summary>
    public async Task<Extract> DriveAsync(ExtractStatus target, Percentage? portion = null)
    {
        Extract extract = Prepare(NewUser());
        if (target == ExtractStatus.Draft)
        {
            return extract;
        }

        extract.SubmitQcReport(NewUser(), Now).IsSuccess.Should().BeTrue();
        if (target == ExtractStatus.QcSubmitted)
        {
            return extract;
        }

        Percentage approved = target == ExtractStatus.PartiallyApproved
            ? portion ?? Percentage.FromPercent(60m)
            : Percentage.FromFraction(1m);
        Result approval = extract.ApproveTechnicalOffice(
            NewUser(), approved, target == ExtractStatus.PartiallyApproved ? Guid.NewGuid() : null, Rulings, Now);
        approval.IsSuccess.Should().BeTrue();
        if (target is ExtractStatus.PartiallyApproved or ExtractStatus.TechnicalApproved)
        {
            return extract;
        }

        Result<PostingSet?> deductions = await extract.ApplyDeductionsAsync(
            NewUser(), new LumpSumBillingCalculator(Rulings), Balances(), Context(), Now, CancellationToken.None);
        deductions.IsSuccess.Should().BeTrue();
        Commit(deductions.Value);
        if (target == ExtractStatus.DeductionsApplied)
        {
            return extract;
        }

        Result<PostingSet?> owner = extract.OwnerApprove(NewUser(), Context(), Now);
        owner.IsSuccess.Should().BeTrue();
        Commit(owner.Value);
        if (target == ExtractStatus.OwnerApproved)
        {
            return extract;
        }

        Result<PostingSet?> issued = extract.Issue(NewUser(), Context(), Now);
        issued.IsSuccess.Should().BeTrue();
        Commit(issued.Value);
        if (target == ExtractStatus.Issued)
        {
            return extract;
        }

        Result<PostingSet> collected = extract.Collect(NewUser(), Context(), Now);
        collected.IsSuccess.Should().BeTrue();
        Commit(collected.Value);
        return extract;
    }
}
