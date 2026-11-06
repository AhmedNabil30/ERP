using Kaff.Domain.Extracts;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.GetWaitingOn;

/// <summary>One open extract and what it is waiting for.</summary>
public sealed record WaitingOnItem(Guid ExtractId, int Number, WaitingOnResponse WaitingOn);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<Extract> extracts = await ExtractSupport.WithChildren(database)
            .AsNoTracking()
            .Where(extract => extract.ProjectId == projectId)
            .OrderBy(extract => extract.Number)
            .ToListAsync(cancellationToken);
        IReadOnlyList<StaffMember> staff = await ExtractWaitingOnView.StaffAsync(database, projectId, cancellationToken);

        // An extract with nothing left to do (collected, void) is not waiting on anyone.
        List<WaitingOnItem> open =
        [
            .. extracts
                .Select(extract => new WaitingOnItem(extract.Id, extract.Number, ExtractWaitingOnView.Build(extract, staff)))
                .Where(item => !item.WaitingOn.IsTerminal),
        ];

        return Microsoft.AspNetCore.Http.Results.Ok(open);
    }
}
