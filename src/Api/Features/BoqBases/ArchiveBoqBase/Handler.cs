using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.BoqBases.ArchiveBoqBase;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid baseId, KaffDbContext database, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);

        BoqBase? boqBase = await database.BoqBases.FirstOrDefaultAsync(candidate => candidate.Id == baseId, cancellationToken);

        if (boqBase is null)
        {
            return ResultExtensions.Problem(BoqErrors.BaseNotFound);
        }

        Result archived = boqBase.Archive(clock.GetUtcNow());
        if (archived.IsFailure)
        {
            return ResultExtensions.Problem(archived.Error);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        return refused is not null ? ResultExtensions.Problem(refused) : Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
