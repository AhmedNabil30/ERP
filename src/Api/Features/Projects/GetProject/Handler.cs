using Kaff.Api.Common.Results;
using System.Security.Claims;
using Kaff.Api.Features.ChangeOrders;
using Kaff.Api.Features.Extracts;
using Kaff.Domain.Extracts;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
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
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        RulingSet rulings,
        TimeProvider clock,
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

                // KAFF-415, AC-415-I: the link is stored on the execution project, pointing at its design project.
                ExecutionProjectId = database.Projects
                    .Where(other => other.LinkedProjectId == candidate.Id && other.LinkType == ProjectLinkType.DesignToExecution)
                    .Select(other => (Guid?)other.Id)
                    .FirstOrDefault(),
                DesignAreaSquareMetres = database.Projects
                    .Where(other => candidate.LinkType == ProjectLinkType.DesignToExecution && other.Id == candidate.LinkedProjectId)
                    .Select(other => other.AreaSquareMetres)
                    .FirstOrDefault(),
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
            approved,
            candidateProject.SignedOn,
            candidateProject.StartedOn,
            candidateProject.HandoverOn,
            candidateProject.WarrantyEndsOn,
            candidateProject.ClosedOn,
            await database.DailyLogs.Where(log => log.ProjectId == projectId)
                .Select(log => (DateOnly?)log.LogDate)
                .MaxAsync(cancellationToken),
            await ExtractSupport.MaySeeMoneyAsync(authorization, user)
                ? await MoneyAsync(database, projectId, cancellationToken)
                : null,
            candidateProject.IsArchived(clock.GetUtcNow(), rulings.FollowUp),
            candidateProject.FollowUpEndsOn(rulings.FollowUp),
            candidateProject.LinkType == ProjectLinkType.DesignToExecution ? candidateProject.LinkedProjectId : null,
            found.ExecutionProjectId,
            found.DesignAreaSquareMetres);

        return Microsoft.AspNetCore.Http.Results.Ok(project);
    }

    private static async Task<ProjectMoney> MoneyAsync(KaffDbContext database, Guid projectId, CancellationToken cancellationToken)
    {
        List<Extract> billed = [.. (await database.Extracts.AsNoTracking()
            .Where(extract => extract.ProjectId == projectId)
            .ToListAsync(cancellationToken)).Where(extract => ExtractMoneyQueries.IsBilled(extract.Status))];

        IReadOnlyDictionary<Guid, Money> collected = await ExtractMoneyQueries.CollectedAsync(
            database, [.. billed.Select(extract => extract.Id)], cancellationToken);

        Money certified = Money.Sum(billed.Select(extract => extract.CertifiedValue ?? Money.Zero));
        Money net = Money.Sum(billed.Select(extract => extract.NetPayable ?? Money.Zero));
        Money received = Money.Sum(billed.Select(extract => collected.GetValueOrDefault(extract.Id, Money.Zero)));
        return new ProjectMoney(certified, net, received, net - received);
    }
}
