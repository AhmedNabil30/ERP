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
public static class ExtractPostings
{
    /// <summary>
    /// Builds, without saving, the postings an extract writes when it reaches <paramref name="reachedStage"/>.
    /// Fails with <see cref="BillingErrors.NotAtPostingMoment"/> at any other stage: when an extract posts is
    /// <see cref="ExtractPostingMoment"/>'s ruling, not this method's.
    /// </summary>
    public static Result<PostingSet> Build(
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

        Money work = Amount(BillingComponentKind.WorkValue);
        Money withheld = Amount(BillingComponentKind.TaxAtSource);

        if (withheld.IsPositive && accounts.TaxWithheldAtSource is null)
        {
            return Result.Failure<PostingSet>(BillingErrors.TaxAtSourceAccountRequired);
        }

        // Cash that reaches the safe is the net the client owes less what it withholds at source.
        Money cash = result.NetPayable - withheld;

        PostingSetBuilder builder = new PostingSetBuilder(
                project, source, postingDate, createdByUserId, createdAt, rulings.PostingProjectStates)
            .AddIfPositive(accounts.Revenue, accounts.ClientReceivable, work, PostingType.RevenueRecognition)
            .AddIfPositive(accounts.ClientReceivable, accounts.Hold, Amount(BillingComponentKind.Hold), PostingType.HoldAccrual)
            .AddIfPositive(accounts.ClientReceivable, accounts.ClientAdvance, Amount(BillingComponentKind.AdvanceRecovery), PostingType.ClientAdvanceRecovery)
            .AddIfPositive(accounts.MaterialAdvance, accounts.ClientReceivable, Amount(BillingComponentKind.MaterialAdvance), PostingType.MaterialAdvanceIssue)
            .AddIfPositive(accounts.ClientReceivable, accounts.MaterialAdvance, Amount(BillingComponentKind.MaterialAdvanceRecovery), PostingType.MaterialAdvanceRecovery)
            .AddIfPositive(accounts.ClientReceivable, accounts.Safe, cash, PostingType.ClientCollection);

        if (withheld.IsPositive)
        {
            builder.Add(accounts.ClientReceivable, accounts.TaxWithheldAtSource!, withheld, PostingType.TaxWithheldAtSource);
        }

        return builder.Build();
    }
}
