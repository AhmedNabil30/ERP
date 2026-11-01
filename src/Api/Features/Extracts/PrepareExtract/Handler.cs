using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Extracts.PrepareExtract;

/// <summary>
/// Prepares an extract from executed quantities.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cumulative certified work is the sum, over the frozen BOQ's lines, of the line's rate times its executed
/// quantity</b>, rounded as <c>ContractValueSource</c> rules for a line total. Nothing is typed. The executed
/// quantity is the running total the daily logs maintain from period deltas.
/// </para>
/// <para>
/// <b>Previously certified work</b> is the cumulative figure of the project's last extract. That is only
/// meaningful when no other extract is in flight, so a second one is refused until the first is issued
/// (<c>extract.another_extract_open</c>). That refusal is ASSUMED, not ruled (D-194).
/// </para>
/// <para>
/// <b>The number</b> is the project's highest plus one. The unique index on (project, number) is the enforcement
/// of record: two simultaneous prepares get <c>extract.number_taken</c> for the loser, who retries.
/// </para>
/// </remarks>
internal static class Handler
{
    private const string NumberIndex = "ux_extracts_project_number";

    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        Boq? boq = await database.Boqs
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);
        if (boq is null)
        {
            return ResultExtensions.Problem(BoqErrors.NotFound);
        }

        if (!boq.IsFrozen)
        {
            return ResultExtensions.Problem(DailyLogErrors.BoqNotFrozen);
        }

        var existing = await database.Extracts
            .AsNoTracking()
            .Where(candidate => candidate.ProjectId == projectId)
            .Select(candidate => new { candidate.Number, candidate.Status, candidate.CumulativeCertifiedWork })
            .ToListAsync(cancellationToken);

        if (existing.Any(candidate => candidate.Status is not (ExtractStatus.Issued or ExtractStatus.Collected)))
        {
            return ResultExtensions.Problem(ExtractErrors.AnotherExtractOpen);
        }

        Money previous = existing.Count == 0
            ? Money.Zero
            : existing.OrderByDescending(candidate => candidate.Number).First().CumulativeCertifiedWork;
        int number = existing.Count == 0 ? 1 : existing.Max(candidate => candidate.Number) + 1;

        Money cumulative = Money.Sum(boq.Lines.Select(line =>
            rulings.ContractValueSource.LineTotal(line.SellRate, line.QuantityExecuted)));

        Guid actor = currentUser.UserId ?? Guid.Empty;
        DateTimeOffset now = clock.GetUtcNow();

        Result<Extract> prepared = Extract.Prepare(
            project, number, request.PeriodStart, request.PeriodEnd, cumulative, previous,
            request.MaterialValueOnSite, request.MaterialInstalled, actor, now);
        if (prepared.IsFailure)
        {
            return ResultExtensions.Problem(prepared.Error);
        }

        Extract extract = prepared.Value;
        database.Extracts.Add(extract);

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
            return ResultExtensions.Problem(ExtractErrors.NumberTaken);
        }

        ExtractViewer viewer = await ExtractSupport.ViewerAsync(
            new StepServices(database, rulings, clock, currentUser, authorization, user));

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/extracts/{extract.Id}",
            ExtractView.Build(extract, project, rulings, viewer));
    }
}
