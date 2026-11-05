using Kaff.Api.Common.Results;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Closure.RaiseCallback;

/// <summary>Raises a callback. The warranty window is checked by <see cref="WarrantyCallback.Raise"/>. Nothing is posted.</summary>
internal static class Handler
{
    private const string NumberIndex = "ux_warranty_callbacks_project_number";

    public static async Task<IResult> HandleAsync(
        Guid projectId, Request request, KaffDbContext database, TimeProvider clock, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        Result<WarrantyCallback> raised = WarrantyCallback.Raise(
            project,
            await ClosureSupport.NextCallbackNumberAsync(database, projectId, cancellationToken),
            request.Description,
            ClosureSupport.Today(clock),
            currentUser.UserId ?? Guid.Empty,
            clock.GetUtcNow());
        if (raised.IsFailure)
        {
            return ResultExtensions.Problem(raised.Error);
        }

        database.WarrantyCallbacks.Add(raised.Value);

        try
        {
            Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
            if (refused is not null)
            {
                return ResultExtensions.Problem(refused);
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { ConstraintName: NumberIndex })
        {
            return ResultExtensions.Problem(Error.Conflict("closure.callback_number_taken", "errors.closure.callback_number_taken"));
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/warranty-callbacks/{raised.Value.Id}", WarrantyCallbackResponse.From(raised.Value));
    }
}
