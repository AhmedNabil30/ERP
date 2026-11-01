using Kaff.Domain.Extracts;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.ListExtracts;

/// <summary>One row of the list. Money-free on purpose.</summary>
public sealed record ExtractSummary(Guid Id, int Number, DateOnly PeriodStart, DateOnly PeriodEnd, ExtractStatus Status);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<ExtractSummary> rows = await database.Extracts
            .AsNoTracking()
            .Where(extract => extract.ProjectId == projectId)
            .OrderBy(extract => extract.Number)
            .Select(extract => new ExtractSummary(extract.Id, extract.Number, extract.PeriodStart, extract.PeriodEnd, extract.Status))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(rows);
    }
}
