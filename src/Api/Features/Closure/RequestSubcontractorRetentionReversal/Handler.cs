using Kaff.Api.Common.Results;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.RequestSubcontractorRetentionReversal;

/// <summary>
/// Finance asks for the reversal of a posted retention release (AC-806-D). Every check the reversal will face is asked now
/// by <see cref="SubcontractorRetentionReversalRequest.Request"/>. Nothing is posted here.
/// </summary>
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

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM projects WHERE id = {projectId} FOR UPDATE", cancellationToken);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        Posting? original = await database.Postings.FirstOrDefaultAsync(posting => posting.Id == request.PostingId, cancellationToken);
        bool reversed = await database.Postings.AnyAsync(posting => posting.ReversesId == request.PostingId, cancellationToken);
        bool waiting = await ClosureSupport.WaitingRetentionReversalAsync(database, projectId, subcontractorId, cancellationToken) is not null;

        Result<SubcontractorRetentionReversalRequest> asked = SubcontractorRetentionReversalRequest.Request(
            project, subcontractorId, original, reversed, waiting, request.Reason, currentUser.UserId ?? Guid.Empty, clock.GetUtcNow());
        if (asked.IsFailure)
        {
            return ResultExtensions.Problem(asked.Error);
        }

        database.SubcontractorRetentionReversalRequests.Add(asked.Value);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        await transaction.CommitAsync(cancellationToken);
        return Microsoft.AspNetCore.Http.Results.Ok(SubcontractorRetentionReversalRequestResponse.From(asked.Value));
    }
}
