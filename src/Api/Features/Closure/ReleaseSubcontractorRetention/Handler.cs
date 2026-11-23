using Kaff.Api.Common.Results;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.ReleaseSubcontractorRetention;

/// <summary>
/// Finance asks for a subcontractor's retention release (AC-806-E, spec.md section 9). Every check the release posting
/// will face is asked now by <see cref="SubcontractorRetentionReleaseRequest.Request"/>, so a request that could never be
/// approved is refused at once. Nothing is posted here. The ledger is that subcontractor's own SubcontractorRetention
/// account, which is none of the five ledgers and is never the client hold.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid subcontractorId,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM projects WHERE id = {projectId} FOR UPDATE", cancellationToken);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        RetentionReleaseInputs inputs = await ClosureSupport.ReadRetentionReleaseInputsAsync(
            database, projectId, subcontractorId, cancellationToken);
        bool waiting = await ClosureSupport.WaitingRetentionReleaseAsync(database, projectId, subcontractorId, cancellationToken) is not null;

        DateTimeOffset now = clock.GetUtcNow();
        Result<SubcontractorRetentionReleaseRequest> asked = SubcontractorRetentionReleaseRequest.Request(
            project,
            subcontractorId,
            inputs.Retention,
            inputs.Payable,
            inputs.Retained,
            waiting,
            rulings.PostingProjectStates,
            currentUser.UserId ?? Guid.Empty,
            now,
            SiteTime.DateOf(now));
        if (asked.IsFailure)
        {
            return ResultExtensions.Problem(asked.Error);
        }

        database.SubcontractorRetentionReleaseRequests.Add(asked.Value);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        await transaction.CommitAsync(cancellationToken);
        return Microsoft.AspNetCore.Http.Results.Ok(SubcontractorRetentionReleaseRequestResponse.From(asked.Value));
    }
}
