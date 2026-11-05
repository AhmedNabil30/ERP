using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Closure;

/// <summary>
/// KAFF-806. The release of the 5% Kaff retains from a subcontractor (spec.md section 5.1). The ledger is the
/// subcontractor's own <see cref="AccountType.SubcontractorRetention"/> account, which has no <see cref="LedgerKind"/>:
/// it is not one of the five ledgers and is not the client <see cref="AccountType.Hold"/>. It releases on its own clock,
/// the warranty end, and never reads or writes the hold.
/// </summary>
public static class SubcontractorRetention
{
    /// <summary>
    /// Releases the whole retention balance of one subcontractor on one project. Checks in order: project state (asked
    /// of the ruled policy), the warranty end date, the accounts, and that something is retained. The amount is the
    /// retention account's balance summed from postings by the caller; it is never typed. The posting moves the
    /// retention to the subcontractor's payable, where the payment is an ordinary subcontractor payment.
    /// </summary>
    public static Result<PostingSet> Release(
        Project project,
        Guid subcontractorId,
        Account? retention,
        Account? payable,
        Money retained,
        Guid actor,
        DateTimeOffset now,
        DateOnly today,
        PostingProjectStatePolicy statePolicy)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(statePolicy);

        Result state = PostingProjectStateLegality.Validate(
            statePolicy, project.Status, PostingType.SubcontractorRetentionRelease, isReversal: false);
        if (state.IsFailure)
        {
            return Result.Failure<PostingSet>(state.Error);
        }

        // spec.md section 5.1: released when the warranty ends. No stamped end date means the warranty never began.
        if (project.WarrantyEndsOn is not { } endsOn || today < endsOn)
        {
            return Result.Failure<PostingSet>(ClosureErrors.WarrantyNotEnded);
        }

        if (retention is null || payable is null
            || retention.PartyId != subcontractorId || payable.PartyId != subcontractorId
            || retention.ProjectId != project.Id || payable.ProjectId != project.Id)
        {
            return Result.Failure<PostingSet>(ClosureErrors.AccountsMissing);
        }

        // A subcontractor whose retention was zeroed, or who never had any, reads zero here and posts nothing.
        if (!retained.IsPositive)
        {
            return Result.Failure<PostingSet>(ClosureErrors.NothingToRelease);
        }

        return new PostingSetBuilder(
                project,
                new SourceDocument(SourceDocumentType.SubcontractorRetentionRelease, subcontractorId, project.Code),
                today,
                actor,
                now,
                statePolicy)
            .Add(payable, retention, retained, PostingType.SubcontractorRetentionRelease)
            .Build();
    }
}
