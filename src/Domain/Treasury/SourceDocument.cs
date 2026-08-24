namespace Kaff.Domain.Treasury;

/// <summary>
/// The kind of business document a posting traces back to.
/// </summary>
/// <remarks>
/// There is no <c>Manual</c> or <c>JournalEntry</c> member, and there must never be one. spec.md §1
/// puts "a general ledger with free-form manual journal entries" out of scope. Every posting names
/// the document that caused it, which is also what groups the several postings an extract produces
/// into one readable movement.
/// </remarks>
public enum SourceDocumentType
{
    /// <summary>Go-live opening figures (spec.md §6.6, assumption 17 🟡).</summary>
    OpeningBalance = 1,

    /// <summary>مستخلص to the client (spec.md §7).</summary>
    Extract = 2,

    /// <summary>مستخلص from a subcontractor (spec.md §7).</summary>
    SubcontractorExtract = 3,

    /// <summary>A collection from a client, in any method (spec.md §6.5).</summary>
    Collection = 4,

    /// <summary>A disbursement to a supplier, subcontractor or employee.</summary>
    Payment = 5,

    /// <summary>عهدة request and its settlement (spec.md §6.4.4).</summary>
    PettyCashRequest = 6,

    /// <summary>Firm advance authorisation (spec.md §6.4.3).</summary>
    FirmAdvanceRequest = 7,

    /// <summary>أمر تغيير (spec.md §13).</summary>
    ChangeOrder = 8,

    /// <summary>The single Adjustment object of spec.md §6.9.</summary>
    Adjustment = 9,

    /// <summary>A payroll run (spec.md §10).</summary>
    PayrollRun = 10,

    /// <summary>A site expense confirmed by Accounts (spec.md §8).</summary>
    SiteExpense = 11,

    /// <summary>A company-tagged expense (spec.md §6.10).</summary>
    CompanyExpense = 12,

    /// <summary>Supplier invoice (spec.md §6.8, committed money).</summary>
    SupplierInvoice = 13,

    /// <summary>An entry on the asset register (spec.md §6.6).</summary>
    AssetRegisterEntry = 14,

    /// <summary>A month-end or year-end close (spec.md §6.6).</summary>
    PeriodClose = 15,

    /// <summary>Owner injection, withdrawal, repayment or drawing (spec.md §6.4.5).</summary>
    OwnerTransaction = 16,

    /// <summary>A movement between Kaff's own cash accounts (spec.md §6.3).</summary>
    TreasuryTransfer = 17,

    /// <summary>A remittance of withheld tax (spec.md §6.7).</summary>
    TaxRemittance = 18,

    /// <summary>A design stage invoice (spec.md §5.3).</summary>
    DesignStageInvoice = 19,

    /// <summary>Loan agreement or instalment (spec.md §6.6 🟡).</summary>
    LoanSchedule = 20,
}

/// <summary>
/// The document a posting traces to. Part of the <c>Posting</c> shape mandated by spec.md §6.1.
/// </summary>
/// <remarks>
/// Persisted as three plain columns on <c>postings</c> rather than as an owned type, so that the
/// database triggers and the balances view can read it without navigating EF's mapping. Grouping
/// the several postings that one extract produces is a query on
/// (<see cref="Type"/>, <see cref="Id"/>) — there is no separate batch entity, because a batch
/// with no business meaning would be a second thing to keep consistent.
/// </remarks>
public readonly record struct SourceDocument(SourceDocumentType Type, Guid Id, string? Reference)
{
    public const int MaxReferenceLength = 128;

    /// <summary>A human-visible reference such as an extract number. Optional.</summary>
    public string? Reference { get; } = Reference?.Trim() is { Length: > 0 } trimmed
        ? trimmed[..Math.Min(trimmed.Length, MaxReferenceLength)]
        : null;
}
