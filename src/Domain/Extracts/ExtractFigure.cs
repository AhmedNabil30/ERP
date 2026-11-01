using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;

namespace Kaff.Domain.Extracts;

/// <summary>
/// One line of the figures Accounts' calculation produced, kept on the extract so the document and the postings
/// read the same numbers. A snapshot of a calculation, not a balance: nothing here is summed from or into a ledger.
/// </summary>
public sealed class ExtractFigure : Entity
{
    private ExtractFigure()
    {
    }

    internal ExtractFigure(Guid extractId, BillingComponent component)
        : base(NewId())
    {
        ExtractId = extractId;
        Kind = component.Kind;
        Sign = component.Sign;
        Amount = component.Amount;
        LabelKey = component.LabelKey;
    }

    public Guid ExtractId { get; private set; }

    public BillingComponentKind Kind { get; private set; }

    public BillingSign Sign { get; private set; }

    public Money Amount { get; private set; }

    public string LabelKey { get; private set; } = null!;

    internal BillingComponent ToComponent() => new(Kind, Sign, Amount, LabelKey);
}
