using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.GetOverheadSpread;

/// <summary>
/// The monthly analytical report: company overhead spread across projects in proportion to contract value, with each project's
/// gross and net margin. A report only: it writes no posting, so a project's actual cost is unchanged. KAFF-708, KAFF-707 AC5.
/// </summary>
/// <remarks>
/// <b>Overhead</b> is the month's net debit on every company-level expense account (no project on it). <b>Gross margin</b> is the
/// project's revenue less the cost posted to its own expense accounts, so it moves with the project tag (spec.md section 6.10). <b>Net
/// margin</b> is gross margin less the project's overhead share. Only what is posted is counted: the report invents no cost.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/treasury/overhead-spread";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, (KaffDbContext database, RulingSet rulings, DateOnly? month, CancellationToken cancellationToken)
                => Handler.HandleAsync(month, database, rulings, cancellationToken))
            .RequirePermission(Permission.TreasuryReadCompany)
            .WithName("GetOverheadSpread")
            .WithTags("Accounting");
    }
}

public sealed record ProjectLine(
    Guid ProjectId, string ProjectCode, string ProjectName, Money Size, Money OverheadShare, Money Revenue, Money ProjectCost, Money GrossMargin, Money NetMargin);

public sealed record Response(DateOnly Month, Money Overhead, IReadOnlyList<ProjectLine> Projects, Money SharesTotal);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(DateOnly? month, KaffDbContext database, RulingSet rulings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);

        if (month is not { } given || given < TypedPostingDate.Earliest)
        {
            return ResultExtensions.Problem(AccountingErrors.MonthInvalid);
        }

        DateOnly first = ScheduleLedger.MonthStart(given);
        DateOnly last = ScheduleLedger.MonthEnd(given);

        var accounts = await database.Accounts
            .Where(account => account.Class == AccountClass.Expense || account.Class == AccountClass.Revenue)
            .Select(account => new { account.Id, account.Class, account.ProjectId })
            .ToListAsync(cancellationToken);

        List<Guid> ids = [.. accounts.Select(account => account.Id)];

        var movements = await database.Postings
            .Where(posting => posting.PostingDate >= first && posting.PostingDate <= last
                              && (ids.Contains(posting.FromAccountId) || ids.Contains(posting.ToAccountId)))
            .Select(posting => new { posting.FromAccountId, posting.ToAccountId, posting.Amount })
            .ToListAsync(cancellationToken);

        // Debit minus credit on each account, over the month.
        Dictionary<Guid, decimal> net = [];
        foreach (var movement in movements)
        {
            net[movement.ToAccountId] = net.GetValueOrDefault(movement.ToAccountId) + movement.Amount.Amount;
            net[movement.FromAccountId] = net.GetValueOrDefault(movement.FromAccountId) - movement.Amount.Amount;
        }

        Money overhead = new(accounts
            .Where(account => account.Class == AccountClass.Expense && account.ProjectId is null)
            .Sum(account => net.GetValueOrDefault(account.Id)));

        IQueryable<Project> projects = database.Projects.Where(project => project.ContractValue != null);
        if (rulings.OverheadSpreadBasis.ActiveProjectsOnly)
        {
            projects = projects.Where(project => project.Status == ProjectStatus.Active);
        }

        var sized = await projects.Select(project => new { project.Id, project.Code, project.ContractValue }).ToListAsync(cancellationToken);

        IReadOnlyList<OverheadShare> shares = OverheadSpread.Spread(
            overhead, sized.Select(project => new ProjectSize(project.Id, project.ContractValue!.Value)));

        // A project with revenue or cost this month shows even when it takes no share.
        HashSet<Guid> withActivity = [.. accounts.Where(account => account.ProjectId is not null && net.ContainsKey(account.Id)).Select(account => account.ProjectId!.Value)];
        List<Guid> shown = [.. shares.Select(share => share.ProjectId).Union(withActivity)];

        var named = await database.Projects
            .Where(project => shown.Contains(project.Id))
            .Select(project => new { project.Id, project.Code, project.Name })
            .ToListAsync(cancellationToken);
        Dictionary<Guid, string> codes = named.ToDictionary(project => project.Id, project => project.Code);
        Dictionary<Guid, string> names = named.ToDictionary(project => project.Id, project => project.Name);

        List<ProjectLine> lines =
        [
            .. shown.OrderBy(id => codes.GetValueOrDefault(id), StringComparer.Ordinal).Select(id =>
            {
                OverheadShare? share = shares.FirstOrDefault(candidate => candidate.ProjectId == id);

                // Revenue is credit-normal, so its net is negative: flip it. Cost is debit-normal.
                Money revenue = new(-accounts.Where(a => a.ProjectId == id && a.Class == AccountClass.Revenue).Sum(a => net.GetValueOrDefault(a.Id)));
                Money cost = new(accounts.Where(a => a.ProjectId == id && a.Class == AccountClass.Expense).Sum(a => net.GetValueOrDefault(a.Id)));
                Money shareAmount = share?.Share ?? Money.Zero;

                return new ProjectLine(
                    id, codes.GetValueOrDefault(id) ?? string.Empty, names.GetValueOrDefault(id) ?? string.Empty, share?.Size ?? Money.Zero, shareAmount, revenue, cost,
                    revenue - cost, revenue - cost - shareAmount);
            }),
        ];

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(first, overhead, lines, Money.Sum(shares.Select(share => share.Share))));
    }
}
