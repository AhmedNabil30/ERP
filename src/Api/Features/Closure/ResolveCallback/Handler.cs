using Kaff.Api.Common.Results;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.ResolveCallback;

/// <summary>Marks a callback resolved, with who and when. Nothing is posted.</summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId, Guid id, KaffDbContext database, TimeProvider clock, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        WarrantyCallback? callback = await database.WarrantyCallbacks.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        if (callback is null)
        {
            return ResultExtensions.Problem(ClosureErrors.CallbackNotFound);
        }

        Result resolved = callback.Resolve(currentUser.UserId ?? Guid.Empty, clock.GetUtcNow());
        if (resolved.IsFailure)
        {
            return ResultExtensions.Problem(resolved.Error);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.Ok(WarrantyCallbackResponse.From(callback));
    }
}
