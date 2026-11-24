using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Domain.Rulings;
using Kaff.Domain.Sales;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Opportunities.ListOpportunities;

internal static class Handler
{
    private const string LikeEscape = "\\";

    /// <summary>
    /// Filters: <c>stage</c>, <c>status</c> (read status, so <c>Stalled</c> lists quiet Active rows too),
    /// <c>search</c> (client name, phone or code; opportunity code or title) and <c>followUp=true</c>
    /// (flagged opportunities only). An unknown stage or status is refused, never read as "all".
    /// </summary>
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken,
        string? stage = null,
        string? status = null,
        string? search = null,
        bool followUp = false)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);

        OpportunityStage? stageFilter = null;
        OpportunityStatus? statusFilter = null;

        if (!string.IsNullOrWhiteSpace(stage))
        {
            if (!Enum.TryParse(stage.Trim(), ignoreCase: true, out OpportunityStage parsedStage) || !Enum.IsDefined(parsedStage))
            {
                return ResultExtensions.Problem(SalesErrors.OpportunityListFilterUnknown);
            }

            stageFilter = parsedStage;
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse(status.Trim(), ignoreCase: true, out OpportunityStatus parsedStatus) || !Enum.IsDefined(parsedStatus))
            {
                return ResultExtensions.Problem(SalesErrors.OpportunityListFilterUnknown);
            }

            statusFilter = parsedStatus;
        }

        OpportunityInactivity rule = rulings.OpportunityInactivity;
        DateTimeOffset now = clock.GetUtcNow();
        DateTimeOffset stalledBefore = now - TimeSpan.FromDays(rule.StalledDays);
        DateTimeOffset flaggedBefore = now - TimeSpan.FromDays(rule.FirstAlertDays);

        IQueryable<Opportunity> query = database.Opportunities;

        if (stageFilter is { } wantedStage)
        {
            query = query.Where(o => o.Stage == wantedStage);
        }

        if (statusFilter is { } wantedStatus)
        {
            // Stalled is derived on read: a live row quiet past the window counts as Stalled, not as Active.
            query = wantedStatus switch
            {
                OpportunityStatus.Stalled => query.Where(o =>
                    o.Status == OpportunityStatus.Stalled
                    || (o.Status == OpportunityStatus.Active && o.LastActivityAt <= stalledBefore)),
                OpportunityStatus.Active => query.Where(o =>
                    o.Status == OpportunityStatus.Active && o.LastActivityAt > stalledBefore),
                _ => query.Where(o => o.Status == wantedStatus),
            };
        }

        if (followUp)
        {
            // Flagged: live and quiet at least the first-alert days. OnHold and closed rows never flag.
            query = query.Where(o =>
                (o.Status == OpportunityStatus.Active || o.Status == OpportunityStatus.Stalled)
                && o.LastActivityAt <= flaggedBefore);
        }

        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (term is not null)
        {
            string pattern = "%" + Escape(term) + "%";
            string code = term.ToUpperInvariant();
            Result<PhoneNumber> phone = PhoneNumber.Create(term);
            string? normalisedPhone = phone.IsSuccess ? phone.Value.Normalised : null;

            query = query.Where(o =>
                database.Clients.Any(c =>
                    c.Id == o.ClientId
                    && (EF.Functions.ILike(c.Name, pattern, LikeEscape)
                        || c.Code == code
                        || (normalisedPhone != null && c.PhoneNormalised == normalisedPhone)))
                || o.Code == code
                || EF.Functions.ILike(o.Title, pattern, LikeEscape));
        }

        var rows = await query
            .OrderBy(o => o.LastActivityAt)
            .ThenBy(o => o.Code)
            .Join(
                database.Clients,
                o => o.ClientId,
                c => c.Id,
                (o, c) => new { Opportunity = o, ClientName = c.Name, ClientPhone = c.PhoneEntered })
            .ToListAsync(cancellationToken);

        List<OpportunitySummary> opportunities = [.. rows.Select(row => new OpportunitySummary(
            row.Opportunity.Id,
            row.Opportunity.Code,
            row.Opportunity.ClientId,
            row.ClientName,
            row.ClientPhone,
            row.Opportunity.Title,
            row.Opportunity.Stage,
            row.Opportunity.StatusAt(now, rule),
            row.Opportunity.InactivityAt(now, rule),
            row.Opportunity.ClosedLostReason,
            row.Opportunity.LastActivityAt))];

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(opportunities));
    }

    private static string Escape(string term) =>
        term.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
