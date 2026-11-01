using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.ListBoqLines;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Guid? boqId = await database.Boqs
            .AsNoTracking()
            .Where(candidate => candidate.ProjectId == projectId)
            .Select(candidate => (Guid?)candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (boqId is null)
        {
            return ResultExtensions.Problem(BoqErrors.NotFound);
        }

        // Projected in the query: the cost and sell columns are never read, let alone serialised.
        List<QuantityLine> lines = await database.Set<BoqLine>()
            .AsNoTracking()
            .Where(line => line.BoqId == boqId)
            .OrderBy(line => line.Position)
            .Select(line => new QuantityLine(
                line.Id,
                line.Position,
                line.ItemCode,
                line.DescriptionAr,
                line.DescriptionEn,
                line.Unit,
                line.QuantityEstimated,
                line.QuantitySurveyed,
                line.QuantityExecuted))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(boqId.Value, lines));
    }
}
