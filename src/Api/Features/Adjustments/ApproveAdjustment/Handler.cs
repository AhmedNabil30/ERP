using Kaff.Api.Common.Results;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Adjustments.ApproveAdjustment;

/// <summary>
/// The Owner approves, and the credit note posts in the same save: receivable to the target account, one new CreditNote
/// posting. Nobody who raised the adjustment or raised or resolved the dispute behind it can approve. No existing
/// posting is touched, and the hold appears nowhere.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        AdjustmentSupport.RunAsync(
            projectId,
            id,
            database,
            async (adjustment, project, token) =>
            {
                Account? receivable = await database.Accounts.FirstOrDefaultAsync(
                    account => account.ProjectId == projectId && account.Type == AccountType.ClientReceivable && account.IsActive, token);
                Account? target = await database.Accounts.FirstOrDefaultAsync(account => account.Id == adjustment.TargetAccountId, token);
                IReadOnlyCollection<Guid> alsoActed = await AdjustmentSupport.DisputeActorsAsync(database, adjustment, token);

                Result<PostingSet> approved = adjustment.Approve(
                    currentUser.UserId ?? Guid.Empty, alsoActed, project, receivable, target, rulings, clock.GetUtcNow(),
                    AdjustmentSupport.Today(clock));
                return approved.IsFailure
                    ? Result.Failure<IReadOnlyList<Posting>>(approved.Error)
                    : Result.Success<IReadOnlyList<Posting>>([.. approved.Value.Postings]);
            },
            cancellationToken);
}
