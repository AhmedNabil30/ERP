namespace Kaff.Domain.Common;

/// <summary>
/// Currencies the system records.
/// </summary>
/// <remarks>
/// spec.md §1 and §16 assumption 12: EGP only. The field exists so the schema does not need
/// changing later; conversion logic is out of scope and MUST NOT be added. Postings whose two
/// accounts disagree on currency are rejected by a database trigger, not by application code.
/// </remarks>
public enum Currency
{
    /// <summary>Egyptian pound. The only currency in operational use.</summary>
    Egp = 1,
}
