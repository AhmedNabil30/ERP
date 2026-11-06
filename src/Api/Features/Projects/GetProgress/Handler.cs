using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Api.Features.Extracts;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Progress;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.GetProgress;

/// <summary>
/// Reads a project's progress through the dispatcher's metric for its contract type. The handler gathers evidence
/// that does not depend on the type (the BOQ if there is one, the cost paid) and asks the metric to build its own
/// input, so no branch on the contract type lives here.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        IAuthorizationService authorization,
        IContractTypeDispatcher dispatcher,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(dispatcher);

        Project? project = await database.Projects.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        Result<IProgressMetric> metric = dispatcher.ProgressMetricFor(project.ContractType);
        if (metric.IsFailure)
        {
            return ResultExtensions.Problem(metric.Error);
        }

        Boq? boq = await database.Boqs.AsNoTracking()
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);
        ProgressEvidence evidence = new(boq, await CostToDateAsync(database, projectId, cancellationToken), rulings);

        Result<ProgressInput> input = metric.Value.InputFrom(project, evidence);
        if (input.IsFailure)
        {
            return ResultExtensions.Problem(input.Error);
        }

        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        Result<ProgressReading> reading = await metric.Value.MeasureAsync(new ProgressContext(project, today, input.Value), cancellationToken);
        if (reading.IsFailure)
        {
            return ResultExtensions.Problem(reading.Error);
        }

        bool seesMoney = await ExtractSupport.MaySeeMoneyAsync(authorization, user);
        Money? certified = reading.Value.Kind == ProgressKind.Percentage && seesMoney
            ? await CertifiedToDateAsync(database, projectId, cancellationToken)
            : null;

        return Microsoft.AspNetCore.Http.Results.Ok(Describe(reading.Value, today, project, boq, seesMoney, certified));
    }

    private static ProgressResponse Describe(
        ProgressReading reading, DateOnly asOf, Project project, Boq? boq, bool seesMoney, Money? certified)
    {
        Money? Visible(Money? value) => seesMoney ? value : null;

        List<BabProgress>? babs = reading.Segments.Count == 0
            ? null
            : [.. reading.Segments.Select(segment =>
            {
                BoqLine? first = boq?.Lines.FirstOrDefault(line => line.BabCode == segment.LabelKey);
                return new BabProgress(
                    segment.LabelKey,
                    first?.BabNameAr ?? segment.LabelKey,
                    first?.BabNameEn ?? segment.LabelKey,
                    segment.Completion,
                    Visible(segment.Value),
                    BabActivity.IsActive(segment));
            })];

        return new ProgressResponse(
            reading.Kind,
            asOf,
            reading.Completion,
            Visible(reading.ValueToDate),
            reading.Kind == ProgressKind.Percentage ? Visible(project.ContractValue) : null,
            certified,
            Visible(reading.CostToDate),
            Visible(reading.SupervisionToDate),
            babs);
    }

    /// <summary>Cash paid against the project through the ruled cost types, net of reversals. Summed in memory: EF cannot sum a Money.</summary>
    private static async Task<Money> CostToDateAsync(KaffDbContext database, Guid projectId, CancellationToken cancellationToken)
    {
        PostingType[] types = [.. CostPlusCostBasis.CostTypes];
        var legs = await database.Postings.AsNoTracking()
            .Where(posting => posting.ProjectId == projectId && types.Contains(posting.Type))
            .Select(posting => new { posting.Amount, IsReversal = posting.ReversesId != null })
            .ToListAsync(cancellationToken);

        return Money.Sum(legs.Select(leg => leg.IsReversal ? -leg.Amount : leg.Amount));
    }

    private static async Task<Money> CertifiedToDateAsync(KaffDbContext database, Guid projectId, CancellationToken cancellationToken)
    {
        List<Money?> certified = await database.Extracts.AsNoTracking()
            .Where(extract => extract.ProjectId == projectId
                && (extract.Status == ExtractStatus.Issued || extract.Status == ExtractStatus.Collected || extract.Status == ExtractStatus.Disputed))
            .Select(extract => extract.CertifiedValue)
            .ToListAsync(cancellationToken);

        return Money.Sum(certified.Where(value => value is not null).Select(value => value!.Value));
    }
}
