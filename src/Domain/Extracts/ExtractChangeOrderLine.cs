using Kaff.Domain.Common;

namespace Kaff.Domain.Extracts;

/// <summary>An approved change order and the amount an extract bills for it. Passed to <see cref="Extract.Prepare"/>.</summary>
public sealed record ExtractChangeOrderBilling(Guid ChangeOrderId, Money Amount);

/// <summary>
/// One approved change order billed on an extract, in the extract's own change-order section at the price it was
/// approved at (spec.md section 5.1). A unique index on the change order means it is billed on one extract only.
/// </summary>
public sealed class ExtractChangeOrderLine : Entity
{
    private ExtractChangeOrderLine()
    {
    }

    internal ExtractChangeOrderLine(Guid extractId, Guid changeOrderId, Money amount)
        : base(NewId())
    {
        ExtractId = extractId;
        ChangeOrderId = changeOrderId;
        Amount = amount;
    }

    public Guid ExtractId { get; private set; }

    public Guid ChangeOrderId { get; private set; }

    public Money Amount { get; private set; }
}
