using Kaff.Domain.Common;

namespace Kaff.Domain.Collections;

/// <summary>
/// Where an issued extract stands against what the client owes on it (D-235 item 2). Derived by the caller on every
/// request from the extract's net payable, the approved credit notes, the collection postings and the cheques in hand.
/// Never stored.
/// </summary>
/// <param name="Expected">Net payable less the credit notes the Owner approved against the extract.</param>
/// <param name="Collected">What the standing collection postings brought in: cash plus tax withheld at source, net of reversals.</param>
/// <param name="InCheques">Cheques received or deposited and not yet cleared: promised, not yet money, so not yet posted.</param>
public sealed record CollectionPosition(Money Expected, Money Collected, Money InCheques)
{
    /// <summary>What may still be recorded against the extract. A cheque in hand already counts against it.</summary>
    public Money Remaining => Expected - Collected - InCheques;

    /// <summary>The same position with <paramref name="cheque"/> no longer in hand, for the moment it clears.</summary>
    public CollectionPosition Releasing(Money cheque) => this with { InCheques = InCheques - cheque };
}
