using System.Globalization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.ApproveSubcontractorRetentionReversal;

/// <summary>What was reversed.</summary>
/// <param name="ProjectId">The project.</param>
/// <param name="SubcontractorId">The subcontractor.</param>
/// <param name="Reversed">The amount of the release that is taken back, the same as the original posting.</param>
/// <param name="PostingId">The reversing posting; its ReversesId is the release posting.</param>
public sealed record Response(Guid ProjectId, Guid SubcontractorId, Money Reversed, Guid PostingId);

/// <summary>
/// Writes the one reversing posting when the Owner approves (AC-806-D). The request and the posting carry the same instant,
/// which is what the database guard <c>KAFF_FLOW_REVERSAL</c> looks for. The generic reversal stays refused for the release.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid subcontractorId,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM projects WHERE id = {projectId} FOR UPDATE", cancellationToken);

        SubcontractorRetentionReversalRequest? request =
            await ClosureSupport.WaitingRetentionReversalAsync(database, projectId, subcontractorId, cancellationToken);
        if (request is null)
        {
            return ResultExtensions.Problem(ClosureErrors.RetentionReversalNotRequested);
        }

        Posting? original = await database.Postings.FirstOrDefaultAsync(posting => posting.Id == request.OriginalPostingId, cancellationToken);
        bool reversed = await database.Postings.AnyAsync(posting => posting.ReversesId == request.OriginalPostingId, cancellationToken);
        Account? from = original is null ? null : await database.Accounts.FirstOrDefaultAsync(account => account.Id == original.FromAccountId, cancellationToken);
        Account? to = original is null ? null : await database.Accounts.FirstOrDefaultAsync(account => account.Id == original.ToAccountId, cancellationToken);

        DateTimeOffset now = clock.GetUtcNow();
        Result<Posting> mirror = request.Approve(
            currentUser.UserId ?? Guid.Empty, original, reversed, from, to, now, SiteTime.DateOf(now));
        if (mirror.IsFailure)
        {
            return ResultExtensions.Problem(mirror.Error);
        }

        database.Postings.Add(mirror.Value);
        audit.SetReason(string.Create(
            CultureInfo.InvariantCulture,
            $"Retention release reversed for subcontractor {subcontractorId}, amount {mirror.Value.Amount.Amount:0.0000}, asked by {request.RequestedByUserId}, reason: {request.Reason}"));

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is null)
        {
            try
            {
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception) when (DatabaseGuardTranslation.Translate(exception) is { } violation)
            {
                refused = violation;
            }
        }

        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.Ok(new Response(projectId, subcontractorId, mirror.Value.Amount, mirror.Value.Id));
    }
}
