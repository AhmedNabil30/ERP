using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.ReopenProject;

/// <summary>
/// Restarts the follow-up period of an archived project. The project is a tracked entity, so the audit interceptor
/// records <c>FollowUpStartedOn</c> before and after in the same save.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);

        Project? project = await database.Projects
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        Result reopened = project.Reopen(clock.GetUtcNow(), rulings.FollowUp);

        if (reopened.IsFailure)
        {
            return ResultExtensions.Problem(reopened.Error);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(project.Id, project.FollowUpStartedOn, project.FollowUpEndsOn(rulings.FollowUp)));
    }
}
