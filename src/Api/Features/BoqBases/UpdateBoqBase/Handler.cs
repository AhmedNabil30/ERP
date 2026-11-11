using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.BoqBases.UpdateBoqBase;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid baseId, Request request, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);

        BoqBase? boqBase = await database.BoqBases
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.Id == baseId, cancellationToken);

        if (boqBase is null)
        {
            return ResultExtensions.Problem(BoqErrors.BaseNotFound);
        }

        IReadOnlyList<BoqBaseLineRequest> lines = request.Lines ?? [];

        Dictionary<Guid, CatalogueItem> items = await BoqBaseLoader.ItemsAsync(
            database, lines.Select(line => line.CatalogueItemId), cancellationToken);

        Error? invalid = BoqBaseLoader.Refuse(items, lines);
        if (invalid is not null)
        {
            return ResultExtensions.Problem(invalid);
        }

        Result renamed = boqBase.Rename(request.Name);
        if (renamed.IsFailure)
        {
            return ResultExtensions.Problem(renamed.Error);
        }

        BoqBaseLine[] old = [.. boqBase.Lines];
        Result<IReadOnlyList<BoqBaseLine>> replaced = boqBase.ReplaceLines(lines.Select(line => (line.CatalogueItemId, line.Quantity)));
        if (replaced.IsFailure)
        {
            return ResultExtensions.Problem(replaced.Error);
        }

        database.RemoveRange(old);
        database.AddRange(replaced.Value);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        return Microsoft.AspNetCore.Http.Results.Ok(BoqBaseResponse.From(boqBase, items));
    }
}
