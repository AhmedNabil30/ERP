using Kaff.Api.Common.Results;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Closure.RaiseSnag;

/// <summary>Raises a snag in status open. The unique index on (project, number) is the enforcement of the number.</summary>
internal static class Handler
{
    private const string NumberIndex = "ux_snags_project_number";

    public static async Task<IResult> HandleAsync(
        Guid projectId, Request request, KaffDbContext database, TimeProvider clock, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        if (request.SubcontractorId is { } subcontractorId
            && !await database.Subcontractors.AnyAsync(candidate => candidate.Id == subcontractorId, cancellationToken))
        {
            return ResultExtensions.Problem(ClosureErrors.SubcontractorNotFound);
        }

        Result<Snag> raised = Snag.Raise(
            project,
            await ClosureSupport.NextSnagNumberAsync(database, projectId, cancellationToken),
            request.Description,
            request.Location,
            request.OwnerKind,
            request.SubcontractorId,
            request.Priority,
            currentUser.UserId ?? Guid.Empty,
            clock.GetUtcNow());
        if (raised.IsFailure)
        {
            return ResultExtensions.Problem(raised.Error);
        }

        database.Snags.Add(raised.Value);

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
            return ResultExtensions.Problem(Error.Conflict("closure.snag_number_taken", "errors.closure.snag_number_taken"));
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/snags/{raised.Value.Id}", SnagResponse.From(raised.Value));
    }
}
