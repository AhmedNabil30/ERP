using System.Globalization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.ApproveSubcontractorRetentionRelease;

/// <summary>What was released.</summary>
/// <param name="ProjectId">The project.</param>
/// <param name="SubcontractorId">The subcontractor.</param>
/// <param name="Released">The retention balance at the moment of approval, summed from postings. Never typed.</param>
/// <param name="PostingIds">The release posting.</param>
public sealed record Response(Guid ProjectId, Guid SubcontractorId, Money Released, IReadOnlyList<Guid> PostingIds);

/// <summary>
/// Writes the retention release posting when the Owner approves. The amount is read from the subcontractor's retention
/// account inside the transaction that holds the project row lock. The project state, the warranty end and the
/// nothing-to-release rule are checked by <see cref="SubcontractorRetention.Release"/> through
/// <see cref="SubcontractorRetentionReleaseRequest.Approve"/>.
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
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM projects WHERE id = {projectId} FOR UPDATE", cancellationToken);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        SubcontractorRetentionReleaseRequest? request =
            await ClosureSupport.WaitingRetentionReleaseAsync(database, projectId, subcontractorId, cancellationToken);
        if (request is null)
        {
            return ResultExtensions.Problem(ClosureErrors.RetentionReleaseNotRequested);
        }

        RetentionReleaseInputs inputs = await ClosureSupport.ReadRetentionReleaseInputsAsync(
            database, projectId, subcontractorId, cancellationToken);

        DateTimeOffset now = clock.GetUtcNow();
        Result<PostingSet> built = request.Approve(
            currentUser.UserId ?? Guid.Empty,
            project,
            inputs.Retention,
            inputs.Payable,
            inputs.Retained,
            rulings.PostingProjectStates,
            now,
            SiteTime.DateOf(now));
        if (built.IsFailure)
        {
            return ResultExtensions.Problem(built.Error);
        }

        database.Postings.AddRange(built.Value.Postings);
        audit.SetReason(string.Create(
            CultureInfo.InvariantCulture,
            $"Retention released for subcontractor {subcontractorId}, amount {inputs.Retained.Amount:0.0000}, asked by {request.RequestedByUserId}"));

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
            : Microsoft.AspNetCore.Http.Results.Ok(
                new Response(projectId, subcontractorId, inputs.Retained, [.. built.Value.Postings.Select(posting => posting.Id)]));
    }
}
