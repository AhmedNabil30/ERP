using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.PettyCash;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.PettyCash;

/// <summary>
/// The one shape of the supervisor's two decisions: lock the project row, find the request on that project, apply the
/// decision, save. The lock is the same one the approval handlers take, so two decisions racing cannot both pass.
/// </summary>
internal static class PettyCashDecision
{
    public static async Task<IResult> RunAsync(
        Guid projectId,
        Guid id,
        Func<PettyCashRequest, Guid, DateTimeOffset, Result> decide,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM projects WHERE id = {projectId} FOR UPDATE", cancellationToken);

        PettyCashRequest? request = await database.PettyCashRequests
            .SingleOrDefaultAsync(found => found.Id == id && found.ProjectId == projectId, cancellationToken);
        if (request is null)
        {
            return ResultExtensions.Problem(PettyCashErrors.RequestNotFound);
        }

        Result decided = decide(request, currentUser.UserId ?? Guid.Empty, clock.GetUtcNow());
        if (decided.IsFailure)
        {
            return ResultExtensions.Problem(decided.Error);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        await transaction.CommitAsync(cancellationToken);
        return Microsoft.AspNetCore.Http.Results.Ok(PettyCashRequestResponse.From(request));
    }
}
