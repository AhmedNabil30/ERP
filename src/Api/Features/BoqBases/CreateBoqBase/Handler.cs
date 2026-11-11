using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.BoqBases.CreateBoqBase;

/// <remarks>The base and its lines are tracked entities, so the audit interceptor writes their records in the same save.</remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request, KaffDbContext database, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);

        IReadOnlyList<BoqBaseLineRequest> lines = request.Lines ?? [];

        Result<BoqBase> created = BoqBase.Create(request.Name, clock.GetUtcNow());
        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Dictionary<Guid, CatalogueItem> items = await BoqBaseLoader.ItemsAsync(
            database, lines.Select(line => line.CatalogueItemId), cancellationToken);

        Error? invalid = BoqBaseLoader.Refuse(items, lines);
        if (invalid is not null)
        {
            return ResultExtensions.Problem(invalid);
        }

        BoqBase boqBase = created.Value;
        Result<IReadOnlyList<BoqBaseLine>> replaced = boqBase.ReplaceLines(lines.Select(line => (line.CatalogueItemId, line.Quantity)));
        if (replaced.IsFailure)
        {
            return ResultExtensions.Problem(replaced.Error);
        }

        database.BoqBases.Add(boqBase);
        database.AddRange(replaced.Value);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        return Microsoft.AspNetCore.Http.Results.Created($"/api/boq-bases/{boqBase.Id}", BoqBaseResponse.From(boqBase, items));
    }
}
