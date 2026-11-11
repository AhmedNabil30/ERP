using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.BoqBases;

/// <summary>What the base endpoints share: the catalogue rows a base names, and the check that a line may be saved.</summary>
internal static class BoqBaseLoader
{
    public static async Task<Dictionary<Guid, CatalogueItem>> ItemsAsync(
        KaffDbContext database, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        Guid[] wanted = [.. ids.Distinct()];

        return await database.CatalogueItems
            .AsNoTracking()
            .Where(item => wanted.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
    }

    /// <summary>Every named item must exist and be active; the first failure is returned.</summary>
    public static Error? Refuse(IReadOnlyDictionary<Guid, CatalogueItem> items, IEnumerable<BoqBaseLineRequest> lines)
    {
        foreach (BoqBaseLineRequest line in lines)
        {
            if (!items.TryGetValue(line.CatalogueItemId, out CatalogueItem? item))
            {
                return MasterDataErrors.CatalogueItemNotFound;
            }

            if (item.Status == CatalogueItemStatus.Archived)
            {
                return BoqErrors.CatalogueItemArchived;
            }
        }

        return null;
    }
}
