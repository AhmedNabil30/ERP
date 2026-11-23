using Kaff.Api.Common.Results;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.RejectSubcontractorRetentionRelease;

/// <summary>The Owner rejects the waiting retention release request with a stored reason. Nothing was posted and nothing is.</summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid subcontractorId,
        Request request,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Same project row lock as the approval, so a decision racing a decision cannot both pass.
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM projects WHERE id = {projectId} FOR UPDATE", cancellationToken);

        SubcontractorRetentionReleaseRequest? waiting =
            await ClosureSupport.WaitingRetentionReleaseAsync(database, projectId, subcontractorId, cancellationToken);
        if (waiting is null)
        {
            return ResultExtensions.Problem(ClosureErrors.RetentionReleaseNotRequested);
        }

        Result rejected = waiting.Reject(currentUser.UserId ?? Guid.Empty, request.Reason, clock.GetUtcNow());
        if (rejected.IsFailure)
        {
            return ResultExtensions.Problem(rejected.Error);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.Ok(SubcontractorRetentionReleaseRequestResponse.From(waiting));
    }
}
