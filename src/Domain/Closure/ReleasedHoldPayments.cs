using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Closure;

/// <summary>
/// D-244 item 2: the client pays the released hold through the same collection flow as an extract, against the
/// project's handover. Cash and transfer post at once, a cheque only when it clears, part payments and withholding as on
/// an extract, never more than remains. The payment rules and the postings are the extract's own
/// (<see cref="Extract.CheckPayment"/>, <see cref="Extract.EnsureProjectTakesPayment"/>,
/// <see cref="ExtractPostings.BuildCollection(Project, Money, Money, ExtractAccounts, SourceDocument, DateOnly, Guid, DateTimeOffset, ExtractStage, RulingSet)"/>);
/// only the document they post against differs.
/// </summary>
/// <remarks>
/// The position is derived by the caller on every request: expected is the standing hold release, collected the
/// collection postings against the handover, in cheques the handover's cheques still in hand. Nothing is stored.
/// </remarks>
public static class ReleasedHoldPayments
{
    /// <summary>The document a released-hold collection posts against: the handover, as the release itself does.</summary>
    public static SourceDocument SourceFor(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new SourceDocument(SourceDocumentType.ProjectHandover, project.Id, project.Code);
    }

    /// <summary>Whether a payment or a cheque may be recorded now: the hold was released and something of it remains.</summary>
    public static Result EnsureMayRecord(Project project, RulingSet rulings, CollectionPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (!position.Expected.IsPositive)
        {
            return Result.Failure(ClosureErrors.HoldNotReleased);
        }

        return Specific(Extract.EnsureProjectTakesPayment(project, rulings, position));
    }

    /// <summary>A cheque is recorded as received and posts nothing; it counts against what remains until it clears or bounces.</summary>
    /// <remarks>
    /// AC-317-E, D-250 item 4: withholding is recorded on the released hold only while
    /// <see cref="WithholdingBaseRule.HoldReleaseCarriesWithholding"/> says the release carries it.
    /// </remarks>
    public static Result AcceptCheque(Project project, RulingSet rulings, CollectionPosition position, Money cash, Money withheld)
    {
        ArgumentNullException.ThrowIfNull(rulings);

        return Result.FirstFailureOrSuccess(
            EnsureMayRecord(project, rulings, position),
            withheld.IsPositive && !rulings.WithholdingBase.HoldReleaseCarriesWithholding
                ? Result.Failure(ClosureErrors.ReleasedHoldCarriesNoWithholding)
                : Result.Success(),
            Specific(Extract.CheckPayment(project, position, cash, withheld)));
    }

    /// <summary>A payment by cash or transfer: the cash to the safe and the withheld amount to the withholding account.</summary>
    public static Result<PostingSet> Collect(
        Project project, RulingSet rulings, ExtractAccounts accounts, CollectionPosition position, Money cash, Money withheld,
        DateOnly postingDate, Guid actorUserId, DateTimeOffset now)
    {
        Result gate = AcceptCheque(project, rulings, position, cash, withheld);
        return gate.IsFailure
            ? Result.Failure<PostingSet>(gate.Error)
            : Post(project, rulings, accounts, cash, withheld, postingDate, actorUserId, now);
    }

    /// <summary>
    /// A cheque on the released hold cleared, so it is cash. <paramref name="position"/> still counts the cheque in hand;
    /// it is released here. As on an extract, a clearance is not refused for nothing remaining: the bank has paid.
    /// </summary>
    public static Result<PostingSet> ClearCheque(
        Project project, RulingSet rulings, ExtractAccounts accounts, CollectionPosition position, Money cash, Money withheld,
        DateOnly postingDate, Guid actorUserId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(position);

        Result gate = Specific(Extract.CheckPayment(project, position.Releasing(cash + withheld), cash, withheld));
        return gate.IsFailure
            ? Result.Failure<PostingSet>(gate.Error)
            : Post(project, rulings, accounts, cash, withheld, postingDate, actorUserId, now);
    }

    private static Result<PostingSet> Post(
        Project project, RulingSet rulings, ExtractAccounts accounts, Money cash, Money withheld,
        DateOnly postingDate, Guid actorUserId, DateTimeOffset now) =>
        ExtractPostings.BuildCollection(
            project, cash, withheld, accounts, SourceFor(project), postingDate, actorUserId, now, ExtractStage.Collected, rulings);

    /// <summary>The extract's refusals name the extract; the same refusals on the released hold name the hold.</summary>
    private static Result Specific(Result result) =>
        result.IsSuccess ? result
        : result.Error == CollectionErrors.NothingRemaining ? Result.Failure(ClosureErrors.ReleasedHoldNothingRemaining)
        : result.Error == CollectionErrors.ExceedsExpected ? Result.Failure(ClosureErrors.ReleasedHoldExceeded)
        : result;
}
