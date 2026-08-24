namespace Kaff.Domain.Treasury;

/// <summary>
/// Whether a posting moves cash or only recognises an accounting fact.
/// </summary>
/// <remarks>
/// spec.md §6.2: "The engine MUST support non-cash postings from day one: revenue recognition,
/// expense accrual, prepayment, depreciation, WIP adjustment, tax withheld. Building it cash-only
/// means rewriting the core later."
/// </remarks>
public enum PostingNature
{
    /// <summary>Cash or bank actually moved.</summary>
    Cash = 1,

    /// <summary>No cash moved. An obligation, a recognition or an allocation.</summary>
    NonCash = 2,
}

/// <summary>
/// What a posting represents. Every financial event in the system is one of these.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately closed. spec.md §1 puts "a general ledger with free-form manual journal entries"
/// out of scope, so there is no <c>Manual</c> or <c>Other</c> member and no way for a user to invent
/// one. Every posting traces to a business event that spec.md names.
/// </para>
/// <para>
/// A reversing posting keeps the type of the posting it reverses and sets <c>ReversesId</c>. There
/// is no <c>Reversal</c> type, because a reversal that lost its original classification would break
/// every report that groups by type.
/// </para>
/// </remarks>
public enum PostingType
{
    // ================= Cash =================

    /// <summary>Opening balance at go-live (spec.md §6.6, assumption 17 🟡).</summary>
    OpeningBalance = 1,

    /// <summary>Client pays the contract advance (spec.md §15, "Advance at signing").</summary>
    ClientAdvanceReceipt = 10,

    /// <summary>Client pays against an issued extract (spec.md §6.5).</summary>
    ClientCollection = 11,

    /// <summary>A cheque is lodged with the bank. Cheque state received → deposited (spec.md §6.5).</summary>
    ChequeDeposit = 12,

    /// <summary>A deposited cheque clears (spec.md §6.5).</summary>
    ChequeClearance = 13,

    /// <summary>A deposited cheque bounces (spec.md §6.5).</summary>
    ChequeBounce = 14,

    /// <summary>Payment to a supplier (spec.md §2, §6.3).</summary>
    SupplierPayment = 20,

    /// <summary>Payment against a certified subcontractor extract (spec.md §7).</summary>
    SubcontractorPayment = 21,

    /// <summary>Monthly salary payment (spec.md §10, assumption 10 🟡).</summary>
    PayrollPayment = 22,

    /// <summary>Weekly يومية payment (spec.md §10, assumption 10 🟡).</summary>
    DayLabourPayment = 23,

    /// <summary>عهدة handed to a holder (spec.md §6.4.4).</summary>
    PettyCashIssue = 30,

    /// <summary>عهدة cleared against receipts (spec.md §6.4.4).</summary>
    PettyCashSettlement = 31,

    /// <summary>Unspent عهدة returned to the safe (spec.md §6.4.4).</summary>
    PettyCashReturn = 32,

    /// <summary>Kaff spends on the client's behalf, under the owner-approved cap (spec.md §6.4.3).</summary>
    FirmAdvanceIssue = 33,

    /// <summary>A firm advance is recovered from the client (spec.md §6.4.3).</summary>
    FirmAdvanceRecovery = 34,

    /// <summary>Owner puts money in. A liability repaid later (spec.md §6.4.5).</summary>
    OwnerInjection = 40,

    /// <summary>Owner takes a returnable advance (spec.md §6.4.5).</summary>
    OwnerWithdrawal = 41,

    /// <summary>Kaff repays the owner (spec.md §6.4.5).</summary>
    OwnerRepayment = 42,

    /// <summary>Owner takes a final drawing against equity (spec.md §6.4.5).</summary>
    OwnerDrawing = 43,

    /// <summary>Movement between safe and bank, or between banks (spec.md §6.3).</summary>
    CashTransfer = 50,

    /// <summary>A site expense confirmed and paid by Finance (spec.md §8, §6.10).</summary>
    SiteExpensePayment = 51,

    /// <summary>A company-tagged expense (spec.md §6.10).</summary>
    CompanyExpensePayment = 52,

    /// <summary>Bank charge (spec.md §6.10).</summary>
    BankCharge = 53,

    /// <summary>Purchase of an asset onto the register (spec.md §6.6).</summary>
    AssetPurchase = 54,

    /// <summary>Withheld tax remitted to the authority (spec.md §6.7).</summary>
    TaxRemittance = 55,

    /// <summary>Loan received (spec.md §6.6, assumption 16 🟡).</summary>
    LoanDrawdown = 60,

    /// <summary>Loan instalment principal (spec.md §6.6, assumption 16 🟡).</summary>
    LoanPrincipalRepayment = 61,

    /// <summary>Loan interest paid (spec.md §6.6, assumption 16 🟡).</summary>
    LoanInterestPayment = 62,

    // ================= Non-cash =================

    /// <summary>Revenue recognised on a certified extract (spec.md §6.6).</summary>
    RevenueRecognition = 100,

    /// <summary>Expense accrued before it is paid (spec.md §6.6).</summary>
    ExpenseAccrual = 101,

    /// <summary>An accrual released when the actual cost lands (spec.md §6.6).</summary>
    AccrualRelease = 102,

    /// <summary>A cost paid in advance and carried forward (spec.md §6.6).</summary>
    Prepayment = 103,

    /// <summary>A prepayment consumed into the period (spec.md §6.6).</summary>
    PrepaymentAmortisation = 104,

    /// <summary>Depreciation charge (spec.md §6.6).</summary>
    Depreciation = 105,

    /// <summary>
    /// Month-end percentage-of-completion difference, presented as a contract asset or contract
    /// liability (spec.md §6.6).
    /// </summary>
    WipAdjustment = 106,

    /// <summary>
    /// A corporate client withheld tax at source. Recorded as a recoverable asset so cash reconciles
    /// against the extract (spec.md §6.7).
    /// </summary>
    TaxWithheldAtSource = 107,

    /// <summary>Kaff withheld tax from a subcontractor or supplier and now carries the liability (spec.md §6.7).</summary>
    TaxWithholdingRetained = 108,

    /// <summary>
    /// محجوز accrues on certified work — 20% of period work value (spec.md §5.1, §15).
    /// Postings into the Hold ledger are the only direction permitted during the project.
    /// </summary>
    HoldAccrual = 110,

    /// <summary>
    /// The hold releases once, in full, at handover (spec.md §5.1). The only posting type permitted
    /// to move value out of a Hold account.
    /// </summary>
    HoldRelease = 111,

    /// <summary>تشوينات advanced at 75% of material value (spec.md §5.1, §15).</summary>
    MaterialAdvanceIssue = 112,

    /// <summary>تشوينات recovered as material is installed (spec.md §5.1, §15).</summary>
    MaterialAdvanceRecovery = 113,

    /// <summary>Client advance recovered through an extract (spec.md §5.1, §15).</summary>
    ClientAdvanceRecovery = 114,

    /// <summary>5% retained from a subcontractor extract (spec.md §5.1).</summary>
    SubcontractorRetentionAccrual = 115,

    /// <summary>Subcontractor retention released at warranty end (spec.md §5.1, §11).</summary>
    SubcontractorRetentionRelease = 116,

    /// <summary>Credit note to a client (spec.md §6.9).</summary>
    CreditNote = 120,

    /// <summary>Debit note to a subcontractor (spec.md §6.9, §11).</summary>
    DebitNote = 121,

    /// <summary>
    /// Any other movement covered by the single Adjustment object of spec.md §6.9 — including the
    /// 30% design credit on a linked execution contract (spec.md §5.4) and termination settlements.
    /// </summary>
    Adjustment = 122,

    /// <summary>Period-close transfer (spec.md §6.6).</summary>
    PeriodCloseTransfer = 130,

    /// <summary>Current-year profit rolled into retained earnings at year close (spec.md §6.6).</summary>
    YearEndProfitTransfer = 131,
}
