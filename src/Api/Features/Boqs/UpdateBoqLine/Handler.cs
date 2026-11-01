using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Boqs.UpdateBoqLine;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid lineId,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);

        Boq? boq = await database.Boqs
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);

        if (boq is null)
        {
            return ResultExtensions.Problem(BoqErrors.NotFound);
        }

        Result quantities = boq.SetQuantities(lineId, request.QuantityEstimated, request.QuantitySurveyed);

        if (quantities.IsFailure)
        {
            return ResultExtensions.Problem(quantities.Error);
        }

        Result pricing = boq.SetPricing(lineId, request.ConditionsRate, request.LineMarkup);

        if (pricing.IsFailure)
        {
            return ResultExtensions.Problem(pricing.Error);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        BoqLine line = boq.Lines.Single(candidate => candidate.Id == lineId);

        return Microsoft.AspNetCore.Http.Results.Ok(BoqLineResponse.From(line, rulings.ContractValueSource));
    }
}
