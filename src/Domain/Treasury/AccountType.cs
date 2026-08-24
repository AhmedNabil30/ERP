namespace Kaff.Domain.Treasury;

/// <summary>
/// Where an account sits in the financial statements. Used for grouping and for the trial balance.
/// </summary>
public enum AccountClass
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5,
}

/// <summary>
/// Which direction increases the account.
/// </summary>
/// <remarks>
/// Held explicitly rather than derived from <see cref="AccountClass"/> because contra accounts
/// break the derivation: accumulated depreciation is classified as an asset but increases on the
/// credit side. The non-negative database guard multiplies by this sign, so getting it from the
/// class would put the wrong sign on every contra account.
/// </remarks>
public enum NormalBalance
{
    Debit = 1,
    Credit = 2,
}

/// <summary>
/// Which party a sub-ledger belongs to. The second of the two dimensions in spec.md §6.3
/// ("Two dimensions only: project × party").
/// </summary>
public enum PartyType
{
    Client = 1,
    Subcontractor = 2,
    Supplier = 3,
    Employee = 4,

    /// <summary>The owner, for جاري المالك. spec.md §6.4.5.</summary>
    Owner = 5,
}

/// <summary>
/// The five ledgers of spec.md §6.4 that MUST NOT be netted against each other.
/// </summary>
/// <remarks>
/// CLAUDE.md: "The five ledgers never net against each other: client advance, hold, firm advance,
/// عهدة, owner current account. No calculation may offset one against another."
///
/// Enforcement is structural rather than advisory: a database trigger refuses any posting whose two
/// accounts carry different non-null ledger kinds, so no code path — application, report or manual
/// SQL — can move value directly from one restricted ledger to another. Balance reporting keeps them
/// as five separate figures; see <see cref="LedgerBalances"/>.
///
/// تشوينات (<see cref="AccountType.MaterialAdvance"/>) is deliberately NOT one of the five. spec.md
/// §6.4 lists exactly five and تشوينات is not among them.
/// </remarks>
public enum LedgerKind
{
    /// <summary>spec.md §6.4.1 — in, recovered through extracts, reaches zero.</summary>
    ClientAdvance = 1,

    /// <summary>spec.md §6.4.2 — محجوز. Accumulates only; releases once at handover.</summary>
    Hold = 2,

    /// <summary>spec.md §6.4.3 — Kaff spending on a client's behalf, under a hard cap.</summary>
    FirmAdvance = 3,

    /// <summary>spec.md §6.4.4 — عهدة, petty cash advanced to staff.</summary>
    PettyCashAdvance = 4,

    /// <summary>spec.md §6.4.5 — جاري المالك.</summary>
    OwnerCurrentAccount = 5,
}

/// <summary>
/// The kinds of account in the tree of spec.md §6.3, plus the accounts the non-cash posting types
/// of spec.md §6.2 and the withholding rules of §6.7 need somewhere to land.
/// </summary>
/// <remarks>
/// <para>
/// This is a fixed vocabulary, not an open-ended chart of accounts. spec.md §6.3: "Two dimensions
/// only: project × party. This is not an open-ended chart of accounts." Adding a member is a
/// deliberate act reviewed against spec.md, not a data-entry operation available to users.
/// </para>
/// <para>
/// Several members exist so that the posting engine is complete from day one — spec.md §6.2 warns
/// that "building it cash-only means rewriting the core later". The corresponding *features*
/// (depreciation schedules, month-end close, statements) are slice 7 and are not built here.
/// </para>
/// </remarks>
public enum AccountType
{
    // ---- Cash instruments (spec.md §6.3) ----

    /// <summary>خزنة. The cash safe. Its balance MUST NOT go negative (spec.md §6.1).</summary>
    Safe = 1,

    /// <summary>A bank account — QNB, CIB, الأهلي and so on (spec.md §6.3).</summary>
    Bank = 2,

    // ---- Structural nodes. Not postable; they exist to roll up. ----

    /// <summary>A project's control node. Sub-ledgers and project ledgers hang off it.</summary>
    ProjectControl = 10,

    /// <summary>The company / overhead control node (spec.md §6.3).</summary>
    CompanyControl = 11,

    // ---- Party sub-ledgers (spec.md §6.3) ----

    /// <summary>Client sub-ledger. What the client owes Kaff on this project.</summary>
    ClientReceivable = 20,

    /// <summary>Subcontractor sub-ledger. What Kaff owes this subcontractor.</summary>
    SubcontractorPayable = 21,

    /// <summary>Supplier account. One per supplier, serving many projects (spec.md §2).</summary>
    SupplierPayable = 22,

    /// <summary>Payroll payable to an employee or day-labour worker (spec.md §10).</summary>
    EmployeePayable = 23,

    /// <summary>
    /// Retention Kaff withholds from a subcontractor — 5%, released at warranty end (spec.md §5.1).
    /// Distinct from <see cref="Hold"/>, which is what the client withholds from Kaff.
    /// </summary>
    SubcontractorRetention = 24,

    // ---- The five ledgers of spec.md §6.4 ----

    /// <summary>spec.md §6.4.1. Liability: money taken for work not yet done. Never negative.</summary>
    ClientAdvance = 30,

    /// <summary>spec.md §6.4.2. محجوز, 20%. Only grows; releases once in full at handover.</summary>
    Hold = 31,

    /// <summary>spec.md §6.4.3. Kaff spending on the client's behalf, under an owner-set cap.</summary>
    FirmAdvance = 32,

    /// <summary>spec.md §6.4.4. عهدة outstanding with a holder until cleared with receipts.</summary>
    PettyCashAdvance = 33,

    /// <summary>spec.md §6.4.5. جاري المالك. Injections are a liability; drawings reduce equity.</summary>
    OwnerCurrentAccount = 34,

    /// <summary>
    /// تشوينات. Material advanced at 75% of value and recovered as the material is installed
    /// (spec.md §5.1, §15). Not one of the five restricted ledgers.
    /// </summary>
    /// <remarks>
    /// A liability: the client pays for material that is on site but not yet built into certified
    /// work, and it is recovered from later extracts. See decisions.md D-034.
    /// </remarks>
    MaterialAdvance = 35,

    // ---- Revenue and expense ----

    /// <summary>Certified contract revenue (spec.md §6.6).</summary>
    ContractRevenue = 40,

    /// <summary>Cost tagged to a project (spec.md §6.10).</summary>
    ProjectCost = 42,

    /// <summary>Cost tagged to the company (spec.md §6.10).</summary>
    CompanyExpense = 43,

    /// <summary>Bank charges (spec.md §6.10).</summary>
    BankCharge = 44,

    /// <summary>Depreciation charge (spec.md §6.6).</summary>
    DepreciationExpense = 45,

    /// <summary>Loan interest taken to expense (spec.md §6.6, assumption 16 🟡).</summary>
    InterestExpense = 46,

    // ---- Accounting layer (spec.md §6.6) ----

    /// <summary>An asset on the register.</summary>
    FixedAsset = 50,

    /// <summary>Contra-asset. Classified Asset, but increases on the credit side.</summary>
    AccumulatedDepreciation = 51,

    /// <summary>Prepaid expense (spec.md §6.6).</summary>
    Prepayment = 52,

    /// <summary>Accrued expense (spec.md §6.6).</summary>
    AccruedExpense = 53,

    /// <summary>Executed exceeds billed (spec.md §6.6 revenue recognition).</summary>
    ContractAsset = 54,

    /// <summary>Billed exceeds executed (spec.md §6.6 revenue recognition).</summary>
    ContractLiability = 55,

    // ---- Equity (spec.md §6.6) ----

    PaidInCapital = 60,

    RetainedEarnings = 61,

    CurrentYearProfit = 62,

    /// <summary>A final drawing by the owner, as distinct from a returnable advance (spec.md §6.4.5).</summary>
    OwnerDrawings = 63,

    // ---- Withholding tax (spec.md §6.7) ----

    /// <summary>
    /// Tax a corporate client withheld from a payment to Kaff. An asset, recoverable against income
    /// tax. Without it, collections never match issued extracts (spec.md §6.7).
    /// </summary>
    TaxWithheldAtSource = 70,

    /// <summary>Tax Kaff withheld from a subcontractor or supplier and must remit (spec.md §6.7).</summary>
    TaxWithholdingPayable = 71,

    /// <summary>
    /// Output VAT payable. 🟡 spec.md §6.7 and assumption 15: only used if Kaff turns out to be
    /// VAT-registered. No account of this type is seeded until that is confirmed.
    /// </summary>
    VatPayable = 72,

    // ---- Financing (spec.md §6.6, assumption 16 🟡) ----

    /// <summary>Remaining principal on a bank loan or equipment finance agreement.</summary>
    LoanPayable = 80,
}
