using System.Security.Claims;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.ListExtracts;

/// <summary>
/// One row of the list. <see cref="Money"/> is null for a caller who may not see extract money (a site engineer
/// prepares quantities and approves nothing financial, spec.md section 9), the way the extract itself withholds
/// its figures. <see cref="WaitingOn"/> carries no money and is for everyone.
/// </summary>
public sealed record ExtractSummary(
    Guid Id,
    int Number,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    ExtractStatus Status,
    ExtractMoney? Money,
    WaitingOnResponse WaitingOn);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<Extract> extracts = await ExtractSupport.WithChildren(database)
            .AsNoTracking()
            .Where(extract => extract.ProjectId == projectId)
            .OrderBy(extract => extract.Number)
            .ToListAsync(cancellationToken);

        bool seesMoney = await ExtractSupport.MaySeeMoneyAsync(authorization, user);
        IReadOnlyDictionary<Guid, Money> collected = seesMoney
            ? await ExtractMoneyQueries.CollectedAsync(database, [.. extracts.Select(extract => extract.Id)], cancellationToken)
            : new Dictionary<Guid, Money>();
        IReadOnlyDictionary<Guid, Money> carriedHold = seesMoney
            ? await ExtractMoneyQueries.CarriedHoldAsync(database, extracts, cancellationToken)
            : new Dictionary<Guid, Money>();
        IReadOnlyList<StaffMember> staff = await ExtractWaitingOnView.StaffAsync(database, projectId, cancellationToken);

        List<ExtractSummary> rows =
        [
            .. extracts.Select(extract => new ExtractSummary(
                extract.Id,
                extract.Number,
                extract.PeriodStart,
                extract.PeriodEnd,
                extract.Status,
                seesMoney ? ExtractMoneyQueries.For(extract, collected, carriedHold) : null,
                ExtractWaitingOnView.Build(extract, staff))),
        ];

        return Microsoft.AspNetCore.Http.Results.Ok(rows);
    }
}
