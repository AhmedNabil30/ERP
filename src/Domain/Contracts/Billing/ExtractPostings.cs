using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Contracts.Billing;

/// <summary>The accounts a Lump Sum extract posts across. One instance of each, reused for every extract.</summary>
public sealed record ExtractAccounts(
    Account Revenue,
    Account ClientReceivable,
    Account Hold,
    Account ClientAdvance,
    Account MaterialAdvance,
    Account Safe,
    Account? TaxWithheldAtSource = null);

/// <summary>
/// Turns a Lump Sum <see cref="BillingResult"/> into the postings of one extract, as spec.md §15 lays them
/// out. Pure: it reads the result's components and writes no figure of its own, so a calculator that is
/// wrong produces wrong postings and a test over them fails.
/// </summary>
/// <remarks>
/// <para>
/// <b>An extract writes two posting sets, at two different stages, because spec.md §7 has two acts.</b>
/// Its chain is "Owner approval → Issued → Collected": approval recognises the revenue and accrues the
/// deductions, and collection is what brings the client's cash in, two stages later.
/// </para>
/// <para>
/// <see cref="BuildAccrual"/> is the first set. It leaves the net payable standing as a debit in
/// <see cref="ExtractAccounts.ClientReceivable"/> — that is the receivable, and it is what the client
/// owes until it pays. <see cref="BuildCollection"/> is the second set, and the only one that touches
/// <see cref="ExtractAccounts.Safe"/>.
/// </para>
/// <para>
/// They were one set until 2026-11-01, which credited the safe with money nobody had received. The
/// safe floor is a database constraint evaluated against the summed postings, so an inflated safe let a
/// payment out pass that should have been refused, and the receivable netted to zero at approval and
/// never showed the debt. Postings are append-only, so this could only ever have been reversed.
/// </para>
/// </remarks>
public static class ExtractPostings
{
    /// <summary>
    /// Builds, without saving, the accrual postings an extract writes when it reaches
    /// <paramref name="reachedStage"/>: revenue recognition, the hold accrual, the client-advance
    /// recovery and the تشوينات issue and recovery. No cash moves here, and the net payable is left
    /// standing in <see cref="ExtractAccounts.ClientReceivable"/> for <see cref="BuildCollection"/>.
    /// Fails with <see cref="BillingErrors.NotAtPostingMoment"/> at any other stage: when an extract
    /// accrues is <see cref="ExtractPostingMoment"/>'s ruling, not this method's.
    /// </summary>
    public static Result<PostingSet> BuildAccrual(
        Project project,
        BillingResult result,
        ExtractAccounts accounts,
        SourceDocument source,
        DateOnly postingDate,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        ExtractStage reachedStage,
        RulingSet rulings)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(rulings);

        if (!rulings.ExtractPostingMoment.PostsOn(reachedStage))
        {
            return Result.Failure<PostingSet>(BillingErrors.NotAtPostingMoment);
        }

        Money Amount(BillingComponentKind kind) =>
            Money.Sum(result.Components.Where(c => c.Kind == kind).Select(c => c.Amount));

        return new PostingSetBuilder(
                project, source, postingDate, createdByUserId, createdAt, rulings.PostingProjectStates)
            .AddIfPositive(accounts.Revenue, accounts.ClientReceivable, Amount(BillingComponentKind.WorkValue), PostingType.RevenueRecognition)
            .AddIfPositive(accounts.ClientReceivable, accounts.Hold, Amount(BillingComponentKind.Hold), PostingType.HoldAccrual)
            .AddIfPositive(accounts.ClientReceivable, accounts.ClientAdvance, Amount(BillingComponentKind.AdvanceRecovery), PostingType.ClientAdvanceRecovery)
            .AddIfPositive(accounts.MaterialAdvance, accounts.ClientReceivable, Amount(BillingComponentKind.MaterialAdvance), PostingType.MaterialAdvanceIssue)
            .AddIfPositive(accounts.ClientReceivable, accounts.MaterialAdvance, Amount(BillingComponentKind.MaterialAdvanceRecovery), PostingType.MaterialAdvanceRecovery)
            .Build();
    }

    /// <summary>
    /// Builds, without saving, the postings that settle the receivable <see cref="BuildAccrual"/> left
    /// standing: the client's cash into the safe, and the amount it withholds at source into the
    /// withholding account. Fails with <see cref="BillingErrors.NotAtPostingMoment"/> at any stage other
    /// than <see cref="ExtractStage.Collected"/>.
    /// </summary>
    /// <remarks>
    /// The collection stage is <b>not</b> a ruling and is not <see cref="ExtractPostingMoment"/>'s to
    /// move. spec.md §7 states it: the chain ends "→ Issued → Collected", so cash arrives at
    /// <see cref="ExtractStage.Collected"/> and nowhere earlier. §15 constrains the amounts and says
    /// nothing about the timing, so the two are not in conflict.
    /// </remarks>
    public static Result<PostingSet> BuildCollection(
        Project project,
        BillingResult result,
        ExtractAccounts accounts,
        SourceDocument source,
        DateOnly postingDate,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        ExtractStage reachedStage,
        RulingSet rulings)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(rulings);

        if (reachedStage != ExtractStage.Collected)
        {
            return Result.Failure<PostingSet>(BillingErrors.NotAtPostingMoment);
        }

        Money withheld = Money.Sum(result.Components
            .Where(c => c.Kind == BillingComponentKind.TaxAtSource)
            .Select(c => c.Amount));

        if (withheld.IsPositive && accounts.TaxWithheldAtSource is null)
        {
            return Result.Failure<PostingSet>(BillingErrors.TaxAtSourceAccountRequired);
        }

        // Cash that reaches the safe is the net the client owes less what it withholds at source.
        Money cash = result.NetPayable - withheld;

        PostingSetBuilder builder = new PostingSetBuilder(
                project, source, postingDate, createdByUserId, createdAt, rulings.PostingProjectStates)
            .AddIfPositive(accounts.ClientReceivable, accounts.Safe, cash, PostingType.ClientCollection);

        if (withheld.IsPositive)
        {
            builder.Add(accounts.ClientReceivable, accounts.TaxWithheldAtSource!, withheld, PostingType.TaxWithheldAtSource);
        }

        return builder.Build();
    }
}
