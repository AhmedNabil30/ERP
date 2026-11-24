using Kaff.Api.Common.Results;
using Kaff.Api.Features.ChangeOrders;
using Kaff.Domain.Boqs;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Progress;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Portal.GetProject;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        IContractTypeDispatcher dispatcher,
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

        // Paid, remaining and pending come from the one rule the project list shares (D-269).
        PortalProjectFigures figures = (await PortalProjectMoney.ReadAsync(database, [project], cancellationToken))[project.Id];
        Money? contractValue = figures.ContractValue;
        Money paid = figures.Paid;
        Money? remaining = figures.Remaining;
        int pending = figures.Pending;

        (Percentage? completion, List<PortalSection> sections) = project.ContractType == ContractType.LumpSum
            ? await ProgressAsync(database, rulings, clock, dispatcher, project, cancellationToken)
            : (null, []);

        return Results.Ok(new PortalProjectHome(
            project.Id,
            project.Name,
            project.Status.ToString(),
            project.ContractType.ToString(),
            PortalFormat.Percent(completion),
            sections,
            PortalFormat.Amount(contractValue),
            PortalFormat.Amount(paid),
            PortalFormat.Amount(remaining),
            pending,
            project.PlannedStart,
            project.PlannedEnd,
            // Derived on every read: past the planned end and under 100 percent. No planned end or no completion figure is never stalled.
            project.PlannedEnd is { } plannedEnd && completion is { } done
                && SiteTime.Today(clock) > plannedEnd && done.Percent < 100m));
    }

    private static async Task<(Percentage? Completion, List<PortalSection> Sections)> ProgressAsync(
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        IContractTypeDispatcher dispatcher,
        Project project,
        CancellationToken cancellationToken)
    {
        Result<IProgressMetric> metric = dispatcher.ProgressMetricFor(project.ContractType);
        Boq? boq = await database.Boqs.AsNoTracking()
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == project.Id, cancellationToken);

        // Lump Sum progress reads the BOQ and the daily-log quantities; it takes no cost evidence, so none is supplied.
        if (metric.IsFailure)
        {
            return (null, []);
        }

        Result<ProgressInput> input = metric.Value.InputFrom(project, new ProgressEvidence(boq, Money.Zero, rulings));
        if (input.IsFailure)
        {
            return (null, []);
        }

        Result<ProgressReading> reading = await metric.Value.MeasureAsync(
            new ProgressContext(project, SiteTime.Today(clock), input.Value), cancellationToken);
        if (reading.IsFailure || reading.Value.Kind != ProgressKind.Percentage)
        {
            return (null, []);
        }

        List<PortalSection> sections =
        [
            .. reading.Value.Segments
                .Where(segment => segment.Completion is not null)
                .Select(segment =>
                {
                    BoqLine? first = boq?.Lines.FirstOrDefault(line => line.BabCode == segment.LabelKey);
                    return new PortalSection(
                        first?.BabNameAr ?? segment.LabelKey,
                        first?.BabNameEn ?? segment.LabelKey,
                        PortalFormat.Percent(segment.Completion!.Value));
                }),
        ];

        return (reading.Value.Completion, sections);
    }
}
