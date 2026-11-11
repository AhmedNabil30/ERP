using Kaff.Domain.Common;

namespace Kaff.Domain.Boqs;

/// <summary>One line of a <see cref="BoqBase"/>: a catalogue item and a quantity. No rate is held.</summary>
public sealed class BoqBaseLine : Entity
{
    private BoqBaseLine()
    {
    }

    internal BoqBaseLine(Guid boqBaseId, int position, Guid catalogueItemId, decimal quantity)
        : base(NewId())
    {
        BoqBaseId = boqBaseId;
        Position = position;
        CatalogueItemId = catalogueItemId;
        Quantity = quantity;
    }

    public Guid BoqBaseId { get; private set; }

    public int Position { get; private set; }

    public Guid CatalogueItemId { get; private set; }

    public decimal Quantity { get; private set; }
}
