using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Closure;

/// <summary>spec.md section 11: "A project closes only when all accounts are settled: extracts issued, advance recovered, hold released, subcontractor accounts closed."</summary>
public enum ClosureCondition
{
    ExtractsIssued = 1,
    AdvanceRecovered = 2,
    HoldReleased = 3,
}

/// <summary>
/// What the ledgers read at the moment closure is asked for. Each figure is a balance summed from postings; there is no
/// request field that can mark a condition met. The ledgers are read separately and never offset.
/// </summary>
/// <param name="UnissuedExtracts">Extracts that exist and have not reached Issued.</param>
/// <param name="ClientAdvance">The client advance ledger.</param>
/// <param name="Hold">The hold ledger.</param>
public sealed record ClosureReadings(int UnissuedExtracts, Money ClientAdvance, Money Hold)
{
    /// <summary>Every unmet condition, not only the first (KAFF-807 rule 3).</summary>
    public IReadOnlyList<ClosureCondition> Unmet()
    {
        List<ClosureCondition> unmet = [];
        if (UnissuedExtracts > 0)
        {
            unmet.Add(ClosureCondition.ExtractsIssued);
        }

        if (!ClientAdvance.IsZero)
        {
            unmet.Add(ClosureCondition.AdvanceRecovered);
        }

        if (!Hold.IsZero)
        {
            unmet.Add(ClosureCondition.HoldReleased);
        }

        return unmet;
    }
}

/// <summary>
/// The closure acts on a project and the hold release. Each checks the project's contract type and state in the domain, so
/// a handler cannot forget one. The hold comes out of the ledger once, at handover, through <see cref="ReleaseHold"/> and
/// nowhere else.
/// </summary>
public static class ProjectClosure
{
    /// <summary>Active to HandoverPending. Design has no such state; every other source state is illegal.</summary>
    public static Result DeclarePracticalCompletion(Project project, DateOnly on)
    {
        ArgumentNullException.ThrowIfNull(project);

        return project.ContractType == ContractType.Design
            ? Result.Failure(ClosureErrors.NotApplicableToDesign)
            : project.TransitionTo(ProjectStatus.HandoverPending, on);
    }

    /// <summary>HandoverPending to Handover: stamps the handover date and the warranty end. Refused while a major snag stands.</summary>
    public static Result HandOver(Project project, DateOnly on, IEnumerable<Snag> snags)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(snags);

        if (project.ContractType == ContractType.Design)
        {
            return Result.Failure(ClosureErrors.NotApplicableToDesign);
        }

        // The state is checked first so a project in the wrong state is told so, not told about its snags.
        if (project.Status != ProjectStatus.HandoverPending)
        {
            return Result.Failure(ProjectErrors.IllegalTransition);
        }

        return snags.Any(snag => snag.BlocksHandover)
            ? Result.Failure(ClosureErrors.MajorSnagsOpen)
            : project.TransitionTo(ProjectStatus.Handover, on);
    }

    /// <summary>
    /// Handover to UnderWarranty. RULING: Q110 - what moves a project between the two is unruled; an explicit act with no
    /// precondition. See <c>Rulings.WarrantyStart</c>.
    /// </summary>
    public static Result BeginWarranty(Project project, DateOnly on)
    {
        ArgumentNullException.ThrowIfNull(project);

        return project.ContractType == ContractType.Design
            ? Result.Failure(ClosureErrors.NotApplicableToDesign)
            : project.TransitionTo(ProjectStatus.UnderWarranty, on);
    }

    /// <summary>UnderWarranty to Closed, only when every named condition reads settled. Design closes differently (KAFF-808).</summary>
    public static Result Close(Project project, DateOnly on, ClosureReadings readings)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(readings);

        if (project.ContractType == ContractType.Design)
        {
            return Result.Failure(ClosureErrors.NotApplicableToDesign);
        }

        if (project.Status != ProjectStatus.UnderWarranty)
        {
            return Result.Failure(ProjectErrors.IllegalTransition);
        }

        return readings.Unmet().Count > 0
            ? Result.Failure(ClosureErrors.NotSettled)
            : project.TransitionTo(ProjectStatus.Closed, on);
    }

    /// <summary>
    /// RULING: Terminated is entirely unruled. spec.md section 13 names the state and section 6.9 a "termination
    /// settlement", and nothing says what settles. This refuses, changes no state and writes no posting. See
    /// <c>Rulings.TerminationSettlement</c>.
    /// </summary>
    public static Result Terminate(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return Result.Failure(ClosureErrors.TerminationSettlementUnruled);
    }

    /// <summary>
    /// The one legal debit of the hold: a single <see cref="PostingType.HoldRelease"/> of the whole hold balance, from the
    /// hold to the client receivable, mirroring the accrual. The amount is the ledger balance passed in; there is no other
    /// way to give it. Checks in order: contract type, project state (asked of the ruled policy), already released, nothing
    /// to release. The database separately refuses a release that does not leave the hold at exactly zero.
    /// </summary>
    public static Result<PostingSet> ReleaseHold(
        Project project,
        Account? hold,
        Account? receivable,
        Money holdBalance,
        bool alreadyReleased,
        Guid actor,
        DateTimeOffset now,
        DateOnly postingDate,
        PostingProjectStatePolicy statePolicy)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(statePolicy);

        if (project.ContractType != ContractType.LumpSum)
        {
            return Result.Failure<PostingSet>(
                project.ContractType == ContractType.Design ? ClosureErrors.NotApplicableToDesign : ClosureErrors.HoldNotApplicable);
        }

        Result state = PostingProjectStateLegality.Validate(statePolicy, project.Status, PostingType.HoldRelease, isReversal: false);
        if (state.IsFailure)
        {
            return Result.Failure<PostingSet>(state.Error);
        }

        if (hold is null || receivable is null)
        {
            return Result.Failure<PostingSet>(ClosureErrors.AccountsMissing);
        }

        if (alreadyReleased)
        {
            return Result.Failure<PostingSet>(ClosureErrors.HoldAlreadyReleased);
        }

        if (!holdBalance.IsPositive)
        {
            return Result.Failure<PostingSet>(ClosureErrors.NothingToRelease);
        }

        return new PostingSetBuilder(
                project,
                new SourceDocument(SourceDocumentType.ProjectHandover, project.Id, project.Code),
                postingDate,
                actor,
                now,
                statePolicy)
            .Add(hold, receivable, holdBalance, PostingType.HoldRelease)
            .Build();
    }
}
