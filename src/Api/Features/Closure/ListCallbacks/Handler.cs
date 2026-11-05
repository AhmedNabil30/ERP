using Kaff.Domain.Closure;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.ListCallbacks;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<WarrantyCallback> callbacks = await database.WarrantyCallbacks
            .AsNoTracking()
            .Where(callback => callback.ProjectId == projectId)
            .OrderBy(callback => callback.Number)
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(callbacks.Select(WarrantyCallbackResponse.From).ToList());
    }
}
