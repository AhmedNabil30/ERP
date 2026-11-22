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

namespace Kaff.Api.Features.Closure.ApproveHoldRelease;

/// <summary>What was released.</summary>
/// <param name="ProjectId">The project.</param>
/// <param name="Released">The hold balance at the moment of approval, summed from postings. Never typed.</param>
/// <param name="PostingIds">The release posting.</param>
public sealed record Response(Guid ProjectId, Money Released, IReadOnlyList<Guid> PostingIds);

/// <summary>
/// The single legal debit of the hold, written when the Owner approves. The amount is read from the hold ledger inside the
/// transaction that holds the project row lock. The project state, the once rule and the contract type are checked by
/// <see cref="ProjectClosure.ReleaseHold"/> through <see cref="HoldReleaseRequest.Approve"/>. The audit reason carries the
/// amount and the project state (AC-802-M). The database independently requires the release to leave the hold at zero.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
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

        HoldReleaseRequest? request = await ClosureSupport.WaitingHoldReleaseAsync(database, projectId, cancellationToken);
        if (request is null)
        {
            return ResultExtensions.Problem(ClosureErrors.HoldReleaseNotRequested);
        }

        HoldReleaseInputs inputs = await ClosureSupport.ReadHoldReleaseInputsAsync(database, projectId, cancellationToken);

        DateTimeOffset now = clock.GetUtcNow();
        Result<PostingSet> built = request.Approve(
            currentUser.UserId ?? Guid.Empty,
            project,
            inputs.Hold,
            inputs.Receivable,
            inputs.HoldBalance,
            inputs.AlreadyReleased,
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
            $"Hold released in project state {project.Status}, amount {inputs.HoldBalance.Amount:0.0000}, asked by {request.RequestedByUserId}"));

        // The exact-zero guard fires on the save or at COMMIT, so both are inside the translation.
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
                new Response(projectId, inputs.HoldBalance, [.. built.Value.Postings.Select(posting => posting.Id)]));
    }
}
