using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Extracts;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.GetExtractQuantities;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        RulingSet rulings,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!await database.Extracts.AnyAsync(extract => extract.Id == id && extract.ProjectId == projectId, cancellationToken))
        {
            return ResultExtensions.Problem(ExtractErrors.NotFound);
        }

        // An extract prepared before D-248 kept no quantities and reads an empty list; nothing is back-filled.
        List<ExtractQuantityLine> kept = await ExtractQuantities.LatestAsync(database, id, cancellationToken);
        Guid[] lineIds = [.. kept.Select(line => line.BoqLineId)];
        Dictionary<Guid, BoqLine> lines = await database.Set<BoqLine>().AsNoTracking()
            .Where(line => lineIds.Contains(line.Id))
            .ToDictionaryAsync(line => line.Id, cancellationToken);
        bool seesMoney = await ExtractSupport.MaySeeMoneyAsync(authorization, user);

        return Microsoft.AspNetCore.Http.Results.Ok(kept
            .Select(row => (Line: lines[row.BoqLineId], Row: row))
            .OrderBy(pair => pair.Line.Position)
            .Select(pair => ExtractQuantities.Row(pair.Line, pair.Row.PreviousQuantity, pair.Row.CumulativeQuantity, seesMoney, rulings))
            .ToList());
    }
}
