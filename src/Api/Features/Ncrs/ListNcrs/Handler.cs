using System.Security.Claims;
using Kaff.Api.Features.Extracts;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Ncrs.ListNcrs;

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

        bool seesMoney = await ExtractSupport.MaySeeMoneyAsync(authorization, user);
        bool mayClose = await NcrSupport.MayCloseAsync(authorization, user);

        var rows = await (
                from ncr in database.Ncrs.AsNoTracking()
                join extract in database.Extracts.AsNoTracking() on ncr.ExtractId equals extract.Id
                where ncr.ProjectId == projectId
                orderby ncr.Status, ncr.OpenedAt descending
                select new { Ncr = ncr, Extract = extract })
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            rows.Select(row => NcrResponse.From(row.Ncr, row.Extract, seesMoney, mayClose)).ToList());
    }
}
