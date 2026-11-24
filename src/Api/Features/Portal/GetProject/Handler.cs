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

        // Paid is all cash the client has paid on the project (advance at signing and every collection, hold release
        // included), net of reversals, from the same postings every other money read uses (spec.md section 15).
        var cash = await database.Postings.AsNoTracking()
            .Where(posting => posting.ProjectId == projectId
                && (posting.Type == PostingType.ClientCollection || posting.Type == PostingType.ClientAdvanceReceipt))
            .Select(posting => new { posting.Amount, IsReversal = posting.ReversesId != null })
            .ToListAsync(cancellationToken);
        Money paid = Money.Sum(cash.Select(leg => leg.IsReversal ? -leg.Amount : leg.Amount));

        Money approvedChangeOrders = await ChangeOrderQueries.ApprovedTotalAsync(database, projectId, cancellationToken);
        Money? contractValue = project.RevisedContractValue(approvedChangeOrders);
        Money? remaining = contractValue is { } value ? value - paid : null;

        int pending = await database.ChangeOrders.AsNoTracking()
            .CountAsync(order => order.ProjectId == projectId && order.IsPriced && order.Status == ChangeOrderStatus.ClientApproval, cancellationToken);

        (string? completion, List<PortalSection> sections) = project.ContractType == ContractType.LumpSum
            ? await ProgressAsync(database, rulings, clock, dispatcher, project, cancellationToken)
            : (null, []);

        return Results.Ok(new PortalProjectHome(
            project.Id,
            project.Name,
            project.Status.ToString(),
            project.ContractType.ToString(),
            completion,
            sections,
            PortalFormat.Amount(contractValue),
            PortalFormat.Amount(paid),
            PortalFormat.Amount(remaining),
            pending));
    }

    private static async Task<(string? Completion, List<PortalSection> Sections)> ProgressAsync(
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

        return (PortalFormat.Percent(reading.Value.Completion), sections);
    }
}
