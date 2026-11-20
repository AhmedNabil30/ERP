using System.Security.Claims;
using Kaff.Api.Features.Adjustments;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.ResolveDispute;

/// <summary>
/// Two resolutions. A credit note: Disputed to Issued with the note raised in the same save, through the shared
/// <c>AdjustmentSupport.RaiseCreditNoteAsync</c>, so the dispute route and the adjustment route have one shape; the extract's
/// own postings are not touched. A revised extract (D-241 item 2): Disputed to Void, the extract's standing accrual postings
/// reversed by <c>Extract.CancelForRevision</c> in the same save; the revised extract is then prepared for the same period
/// through the prepare route and runs the whole chain again.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExtractSupport.RunAsync(
            projectId,
            id,
            needsAccounts: request.Kind == DisputeResolutionKind.RevisedExtract,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            async (extract, project, context, services, token) =>
            {
                if (request.Kind is not { } kind)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.DisputeResolutionRequired);
                }

                if (kind == DisputeResolutionKind.RevisedExtract)
                {
                    return await CancelForRevisionAsync(extract, context!, request.Reason, services, token);
                }

                if (extract.Status != ExtractStatus.Disputed)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.IllegalTransition);
                }

                Guid actor = services.CurrentUser.UserId ?? Guid.Empty;
                DateTimeOffset now = services.Clock.GetUtcNow();

                Account? revenue = await services.Database.Accounts.FirstOrDefaultAsync(
                    account => account.ProjectId == projectId && account.Type == AccountType.ContractRevenue && account.IsActive, token);

                Result<Adjustment> raised = await AdjustmentSupport.RaiseCreditNoteAsync(
                    services.Database, project, extract, revenue?.Id, request.Amount, request.Reason, services.Rulings, actor, now, token);
                if (raised.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(raised.Error);
                }

                Result resolved = extract.ResolveDisputeAsCreditNote(actor, request.Reason, now);
                if (resolved.IsFailure)
                {
                    services.Database.Adjustments.Remove(raised.Value);
                    return Result.Failure<IReadOnlyList<Posting>>(resolved.Error);
                }

                return ExtractSupport.NoPostings();
            },
            cancellationToken);
    }

    private static async Task<Result<IReadOnlyList<Posting>>> CancelForRevisionAsync(
        Extract extract, ExtractPostingContext context, string? reason, StepServices services, CancellationToken token)
    {
        KaffDbContext database = services.Database;


        bool later = await ExtractSupport.LaterExtractExistsAsync(database, extract, token);
        bool moneyStanding = (await ExtractCollections.StateAsync(database, extract, token))?.BlocksRevision ?? false;

        IReadOnlyList<ExtractPostedLeg> legs = await ExtractSupport.StandingAccrualLegsAsync(database, extract.Id, token);

        return extract.CancelForRevision(
            services.CurrentUser.UserId ?? Guid.Empty, reason, context, legs, later, moneyStanding, services.Clock.GetUtcNow());
    }
}
