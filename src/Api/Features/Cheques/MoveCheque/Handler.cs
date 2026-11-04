using Kaff.Api.Common.Results;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Cheques.MoveCheque;

/// <summary>The cheque's new state. Its own state machine refuses every other move.</summary>
/// <param name="Id">The cheque.</param>
/// <param name="State">The state it is now in.</param>
public sealed record Response(Guid Id, ChequeState State);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        ChequeState target,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        Cheque? cheque = await database.Cheques
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        if (cheque is null)
        {
            return ResultExtensions.Problem(CollectionErrors.ChequeNotFound);
        }

        Result moved = cheque.MoveTo(target, currentUser.UserId ?? Guid.Empty, clock.GetUtcNow());
        if (moved.IsFailure)
        {
            return ResultExtensions.Problem(moved.Error);
        }

        await database.SaveChangesAsync(cancellationToken);
        return Microsoft.AspNetCore.Http.Results.Ok(new Response(cheque.Id, cheque.State));
    }
}
