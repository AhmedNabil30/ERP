using Kaff.Api.Common.Results;
using System.Security.Claims;
using Kaff.Api.Features.ChangeOrders;
using Kaff.Api.Features.Extracts;
using Kaff.Domain.Extracts;
using Kaff.Domain.Common;
using Kaff.Domain.Authorization;
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
        // The daily log judges "today" in the site zone (DailyLogEntryRule), so the overview uses the same rule.
        DateOnly today = rulings.DailyLogEntry.SiteDate(clock.GetUtcNow());
        bool seesMoney = await CallerPermissions.HoldsAsync(authorization, user, Permission.ProjectMoneyRead);
        Response project = new(
            candidateProject.Id,
            candidateProject.Code,
            candidateProject.Name,
            candidateProject.ClientId,
            found.ClientName,
            candidateProject.ContractType,
            candidateProject.Status,
            seesMoney ? candidateProject.RevisedContractValue(approved) : null,
            seesMoney ? candidateProject.HoldRate : null,
            seesMoney ? candidateProject.AdvanceRate : null,
            found.TeamSize,
            seesMoney ? candidateProject.ContractValue : null,
            seesMoney ? approved : null,
            candidateProject.SignedOn,
            candidateProject.StartedOn,
            candidateProject.HandoverOn,
            candidateProject.WarrantyEndsOn,
            candidateProject.ClosedOn,
            await database.DailyLogs.Where(log => log.ProjectId == projectId)
                .Select(log => (DateOnly?)log.LogDate)
                .MaxAsync(cancellationToken),
            seesMoney
                ? await MoneyAsync(database, projectId, cancellationToken)
                : null,
            candidateProject.IsArchived(clock.GetUtcNow(), rulings.FollowUp),
            candidateProject.FollowUpEndsOn(rulings.FollowUp),
            candidateProject.LinkType == ProjectLinkType.DesignToExecution ? candidateProject.LinkedProjectId : null,
            found.ExecutionProjectId,
            found.DesignAreaSquareMetres,
            StageOf.Of(candidateProject),
            candidateProject.IsSigned,
            await database.DailyLogs.AnyAsync(log => log.ProjectId == projectId && log.LogDate == today, cancellationToken),
            await TabsAsync(authorization, user),
            seesMoney ? candidateProject.AdvanceRecoveryRate : null);

        return Microsoft.AspNetCore.Http.Results.Ok(project);
    }

    /// <summary>
    /// Tabs follow the permission behind each route (D-218 items 5, 6): BOQ is <c>BoqRead</c>, extracts <c>ExtractRead</c>,
    /// the daily log is <c>DailyLogWrite</c> (any assigned site engineer) or <c>DailyLogReview</c> (owner, supervising engineer).
    /// The overview and the team are open to every <c>ProjectRead</c> holder, which is what reached this handler.
    /// </summary>
    private static async Task<ProjectTabs> TabsAsync(IAuthorizationService authorization, ClaimsPrincipal user)
    {
        // Writing wins: a supervising engineer holds both, and he records the log.
        string? mode = await CallerPermissions.HoldsAsync(authorization, user, Permission.DailyLogWrite) ? "write"
            : await CallerPermissions.HoldsAsync(authorization, user, Permission.DailyLogReview) ? "review"
            : null;

        return new ProjectTabs(
            Overview: true,
            Boq: await CallerPermissions.HoldsAsync(authorization, user, Permission.BoqRead),
            DailyLog: mode is not null,
            Extracts: await CallerPermissions.HoldsAsync(authorization, user, Permission.ExtractRead),
            Team: true,
            DailyLogMode: mode);
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
