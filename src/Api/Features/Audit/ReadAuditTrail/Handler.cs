using System.Text.Json.Nodes;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Audit.ReadAuditTrail;

/// <summary>
/// Reads the audit trail, narrowed by project, actor and date. KAFF-117.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three filters, all optional, and with none of them the answer is every record in Kaff</b> —
/// <c>AC-117-A</c>. That is the criterion, not a convenience: the Owner asking "what happened" without
/// naming a project must be shown company-level changes and project changes together, because half of
/// what he is checking (a user created, a client edited) belongs to no project at all.
/// </para>
/// <para>
/// <b>The filters narrow; they never widen and they never gate.</b> Nothing in this method decides who
/// may call it — the gate on the endpoint did, and it admits the Owner alone. A reader looking for the
/// "even for their own projects" rule will not find it here, and should not: it is one line above, and
/// putting a second copy of it in the query is how the two would eventually disagree.
/// </para>
/// <para>
/// <b>An inverted date range is refused rather than answered with nothing.</b> It matches no row, so
/// defaulting it would render exactly like a quiet week. See <c>AuditErrors.DateRangeInverted</c>.
/// </para>
/// <para>
/// <b>Newest first, with the id as a tiebreak.</b> One save writes every one of its records at the
/// same instant — the interceptor stamps <c>OccurredAt</c> once per save — so ordering on the
/// timestamp alone leaves the rows of a single action in whatever order the server felt like. The same
/// request must return the same order twice, or the screen shuffles on refresh.
/// </para>
/// <para>
/// <b>No paging, following <c>ListClients</c> and D-110 §4</b>: a page contract invented before a
/// screen exists is one that will be wrong, and the response is already a wrapper object so a total
/// and a page do not break the shape. <b>The ceiling is real and it is worse here than there</b> —
/// this table takes a row per state change and from slice 3 a row per posting, so an unfiltered read
/// grows without bound while a client list does not. KAFF-128 is the screen and is where the page
/// contract should be decided against a real one. A silent <c>Take</c> in the meantime would be the
/// same defect as the inverted range above: a truncated trail that looks like a complete one.
/// </para>
/// <para>
/// <b>No audit record.</b> It is a read (story rule 10).
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken,
        Guid? projectId = null,
        Guid? actorUserId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (from is not null && to is not null && from > to)
        {
            return ResultExtensions.Problem(AuditErrors.DateRangeInverted);
        }

        IQueryable<AuditRecord> query = database.AuditRecords.AsNoTracking();

        if (projectId is not null)
        {
            query = query.Where(record => record.ProjectId == projectId);
        }

        if (actorUserId is not null)
        {
            query = query.Where(record => record.ActorUserId == actorUserId);
        }

        if (from is not null)
        {
            query = query.Where(record => record.OccurredAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(record => record.OccurredAt <= to);
        }

        List<AuditRecord> records = await query
            .OrderByDescending(record => record.OccurredAt)
            .ThenByDescending(record => record.Id)
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response([.. records.Select(ToEntry)]));
    }

    /// <summary>
    /// The stored row, as the wire sees it.
    /// </summary>
    /// <remarks>
    /// The two snapshots are parsed rather than passed through as text: the column is <c>jsonb</c> and
    /// the caller should not have to run a second parser over data that is already structured. Nothing
    /// here filters, rewrites or redacts a value — redaction happened on the way in, where the secret
    /// still existed to be withheld.
    /// </remarks>
    private static AuditEntry ToEntry(AuditRecord record) => new(
        record.Id,
        record.OccurredAt,
        record.Action,
        record.EventType,
        record.EntityType,
        record.EntityId,
        record.ActorUserId,
        record.ActorDisplayName,
        record.ActorRole,
        record.BeforeJson is null ? null : JsonNode.Parse(record.BeforeJson),
        record.AfterJson is null ? null : JsonNode.Parse(record.AfterJson),
        record.ChangedProperties,
        record.Reason,
        record.CorrelationId,
        record.ProjectId,
        record.GrantPath,
        record.RequestPath,
        record.IpAddress?.ToString());
}
