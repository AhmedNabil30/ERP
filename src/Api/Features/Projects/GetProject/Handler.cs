using Kaff.Api.Common.Results;
using Kaff.Api.Features.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.GetProject;

/// <summary>Reads one project's header. KAFF-400's S-052 loads its page from here.</summary>
/// <remarks>
/// The project is guaranteed to exist by the time this runs for any caller who can reach it:
/// <c>Permission.ProjectRead</c> is <c>ProjectScoped</c>, and every one of <c>IProjectAccessPolicy</c>'s
/// branches requires the project to exist before it grants access — a route naming a project that was
/// never created is refused at the gate, not here. The <c>NotFound</c> branch below is therefore
/// defence in depth, not the ordinary path.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var found = await database.Projects
            .Where(candidate => candidate.Id == projectId)
            .Select(candidate => new
            {
                Project = candidate,
                ClientName = database.Clients.Where(client => client.Id == candidate.ClientId)
                    .Select(client => client.Name).FirstOrDefault() ?? string.Empty,
                TeamSize = database.ProjectAssignments.Count(assignment =>
                    assignment.ProjectId == candidate.Id && assignment.RevokedAt == null),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        // The contract value is derived: the signed BOQ total plus the approved change orders (ChangeOrderDisplayedValue).
        Money approved = await ChangeOrderQueries.ApprovedTotalAsync(database, projectId, cancellationToken);
        Project candidateProject = found.Project;
        Response project = new(
            candidateProject.Id,
            candidateProject.Code,
            candidateProject.Name,
            candidateProject.ClientId,
            found.ClientName,
            candidateProject.ContractType,
            candidateProject.Status,
            candidateProject.RevisedContractValue(approved),
            candidateProject.HoldRate,
            candidateProject.AdvanceRate,
            found.TeamSize,
            candidateProject.ContractValue,
            approved);

        return Microsoft.AspNetCore.Http.Results.Ok(project);
    }
}
