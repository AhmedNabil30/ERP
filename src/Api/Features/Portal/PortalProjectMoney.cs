using Kaff.Api.Features.ChangeOrders;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Portal;

/// <summary>What the client has paid, what remains and how many decisions wait on him, for one project.</summary>
internal sealed record PortalProjectFigures(Money? ContractValue, Money Paid, Money? Remaining, int Pending);

/// <summary>
/// The one read-side rule behind the portal's paid, remaining and pending figures (D-269), shared by the project list and the
/// project home so the two cannot drift. Paid is the advance receipt, every collection and the tax withheld at source, net of
/// reversals, from postings. Nothing here is stored.
/// </summary>
internal static class PortalProjectMoney
{
    public static async Task<Dictionary<Guid, PortalProjectFigures>> ReadAsync(
        KaffDbContext database, IReadOnlyCollection<Project> projects, CancellationToken cancellationToken)
    {
        List<Guid> ids = [.. projects.Select(project => project.Id)];

        var cash = await database.Postings.AsNoTracking()
            .Where(posting => posting.ProjectId != null && ids.Contains(posting.ProjectId.Value)
                && (posting.Type == PostingType.ClientCollection || posting.Type == PostingType.ClientAdvanceReceipt
                    || posting.Type == PostingType.TaxWithheldAtSource))
            .Select(posting => new { ProjectId = posting.ProjectId!.Value, posting.Amount, IsReversal = posting.ReversesId != null })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, Money> approved = await ChangeOrderQueries.ApprovedTotalsAsync(database, ids, cancellationToken);

        Dictionary<Guid, int> pending = await database.ChangeOrders.AsNoTracking()
            .Where(order => ids.Contains(order.ProjectId) && order.IsPriced && order.Status == ChangeOrderStatus.ClientApproval)
            .GroupBy(order => order.ProjectId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count, cancellationToken);

        return projects.ToDictionary(
            project => project.Id,
            project =>
            {
                Money paid = Money.Sum(cash.Where(leg => leg.ProjectId == project.Id).Select(leg => leg.IsReversal ? -leg.Amount : leg.Amount));
                Money? value = project.RevisedContractValue(approved.TryGetValue(project.Id, out Money total) ? total : Money.Zero);
                return new PortalProjectFigures(value, paid, value is { } present ? present - paid : null, pending.GetValueOrDefault(project.Id));
            });
    }
}
