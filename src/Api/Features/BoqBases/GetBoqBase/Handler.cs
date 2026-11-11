using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.BoqBases.GetBoqBase;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid baseId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        BoqBase? boqBase = await database.BoqBases
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.Id == baseId && candidate.ArchivedAt == null, cancellationToken);

        if (boqBase is null)
        {
            return ResultExtensions.Problem(BoqErrors.BaseNotFound);
        }

        Dictionary<Guid, CatalogueItem> items = await BoqBaseLoader.ItemsAsync(
            database, boqBase.Lines.Select(line => line.CatalogueItemId), cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(BoqBaseResponse.From(boqBase, items));
    }
}
