using Kaff.Domain.Boqs;
using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.BoqBases;

/// <summary>A base line as the picker and the editor show it. The text is the catalogue item's today; no rate is returned.</summary>
public sealed record BoqBaseLineResponse(
    Guid Id,
    int Position,
    Guid CatalogueItemId,
    string ItemCode,
    string DescriptionAr,
    string? DescriptionEn,
    string Unit,
    bool CatalogueItemArchived,
    decimal Quantity);

public sealed record BoqBaseResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<BoqBaseLineResponse> Lines)
{
    public static BoqBaseResponse From(BoqBase boqBase, IReadOnlyDictionary<Guid, CatalogueItem> items)
    {
        ArgumentNullException.ThrowIfNull(boqBase);
        ArgumentNullException.ThrowIfNull(items);

        return new BoqBaseResponse(
            boqBase.Id,
            boqBase.Name,
            boqBase.CreatedAt,
            [.. boqBase.Lines.OrderBy(line => line.Position).Select(line =>
            {
                CatalogueItem item = items[line.CatalogueItemId];
                return new BoqBaseLineResponse(
                    line.Id, line.Position, item.Id, item.Code, item.DescriptionAr, item.DescriptionEn, item.Unit,
                    item.Status == CatalogueItemStatus.Archived, line.Quantity);
            })]);
    }
}
