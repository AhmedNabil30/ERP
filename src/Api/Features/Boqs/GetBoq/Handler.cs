using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Boqs.GetBoq;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        RulingSet rulings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);

        Boq? boq = await database.Boqs
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);

        if (boq is null)
        {
            return ResultExtensions.Problem(BoqErrors.NotFound);
        }

        Result<Money> total = boq.Total(rulings.ContractValueSource);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(
            boq.Id,
            boq.ProjectId,
            boq.FrozenOn,
            total.IsSuccess ? total.Value : null,
            [.. boq.Lines
                .OrderBy(line => line.Position)
                .Select(line => BoqLineResponse.From(line, rulings.ContractValueSource))]));
    }
}
