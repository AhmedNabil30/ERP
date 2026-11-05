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
/// Disputed to Issued with a credit note raised in the same save. The credit note is the shared
/// <c>AdjustmentSupport.RaiseCreditNoteAsync</c>, so the dispute route and the adjustment route have one shape. It lands on
/// the project's contract revenue account and cannot name the hold. The extract's own postings are not touched. A
/// revised-extract resolution is refused before anything is read or written (<c>Rulings.ExtractDisputeResolution</c>).
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
            needsAccounts: false,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            async (extract, project, _, services, token) =>
            {
                if (request.Kind is not { } kind)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.DisputeResolutionRequired);
                }

                if (kind != DisputeResolutionKind.CreditNote)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.DisputeRevisionUnruled);
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
}
