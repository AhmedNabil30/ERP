using System.Collections.Frozen;

namespace Kaff.Domain.Treasury;

/// <summary>Whether an account of a given type belongs to a project.</summary>
public enum AccountScope
{
    /// <summary>Company level. The account MUST NOT carry a project (spec.md §6.10).</summary>
    CompanyWide = 1,

    /// <summary>The account MUST carry a project.</summary>
    ProjectRequired = 2,

    /// <summary>Either form is legitimate; separate accounts exist for each.</summary>
    ProjectOptional = 3,
}

/// <summary>
/// The fixed semantics of one <see cref="AccountType"/>.
/// </summary>
/// <remarks>
/// One table, one truth. The account factory validates against it, the seeder builds from it, the
/// database view derives signed balances from <see cref="NormalBalance"/>, and a test asserts every
/// enum member has a row. Scattering these facts across configuration files is how a project ends up
/// with an account that is a liability in one place and an asset in another.
/// </remarks>
public sealed record AccountTypeMetadata(
    AccountType Type,
    AccountClass Class,
    NormalBalance NormalBalance,
    LedgerKind? LedgerKind,
    AccountScope Scope,
    PartyType? RequiredParty,
    bool IsPostable,
    bool EnforceNonNegative,
    string SpecReference);

/// <summary>The catalogue of account semantics. See <see cref="AccountTypeMetadata"/>.</summary>
public static class AccountTypes
{
    private static readonly FrozenDictionary<AccountType, AccountTypeMetadata> Catalogue = Build();

    public static AccountTypeMetadata Of(AccountType type) => Catalogue.TryGetValue(type, out AccountTypeMetadata? meta)
        ? meta
        : throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            "Account type has no metadata row. Add one to AccountTypes with its spec.md reference.");

    public static IReadOnlyCollection<AccountTypeMetadata> All => Catalogue.Values;

    public static bool IsDefined(AccountType type) => Catalogue.ContainsKey(type);

    private static FrozenDictionary<AccountType, AccountTypeMetadata> Build()
    {
        AccountTypeMetadata[] rows =
        [
            // ---- Cash instruments ----
            new(AccountType.Safe, AccountClass.Asset, NormalBalance.Debit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: true, "§6.1, §6.3"),

            // 🟡 Bank overdraft: spec.md mandates non-negative for the safe only. Left configurable
            // per account and defaulted off until Nabil confirms whether any facility exists.
            new(AccountType.Bank, AccountClass.Asset, NormalBalance.Debit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.3"),

            // ---- Structural nodes ----
            new(AccountType.ProjectControl, AccountClass.Asset, NormalBalance.Debit, null,
                AccountScope.ProjectRequired, null, IsPostable: false, EnforceNonNegative: false, "§6.3"),
            new(AccountType.CompanyControl, AccountClass.Asset, NormalBalance.Debit, null,
                AccountScope.CompanyWide, null, IsPostable: false, EnforceNonNegative: false, "§6.3"),

            // ---- Party sub-ledgers ----
            new(AccountType.ClientReceivable, AccountClass.Asset, NormalBalance.Debit, null,
                AccountScope.ProjectRequired, PartyType.Client, IsPostable: true, EnforceNonNegative: false, "§6.3"),
            new(AccountType.SubcontractorPayable, AccountClass.Liability, NormalBalance.Credit, null,
                AccountScope.ProjectRequired, PartyType.Subcontractor, IsPostable: true, EnforceNonNegative: false, "§6.3"),
            new(AccountType.SupplierPayable, AccountClass.Liability, NormalBalance.Credit, null,
                AccountScope.CompanyWide, PartyType.Supplier, IsPostable: true, EnforceNonNegative: false, "§2, §6.3"),
            new(AccountType.EmployeePayable, AccountClass.Liability, NormalBalance.Credit, null,
                AccountScope.ProjectOptional, PartyType.Employee, IsPostable: true, EnforceNonNegative: false, "§10"),
            new(AccountType.SubcontractorRetention, AccountClass.Liability, NormalBalance.Credit, null,
                AccountScope.ProjectRequired, PartyType.Subcontractor, IsPostable: true, EnforceNonNegative: false, "§5.1"),

            // ---- The five ledgers of §6.4 ----
            // ClientAdvance: §15 invariant "Advance ledger reaches exactly zero, never negative".
            new(AccountType.ClientAdvance, AccountClass.Liability, NormalBalance.Credit, Treasury.LedgerKind.ClientAdvance,
                AccountScope.ProjectRequired, PartyType.Client, IsPostable: true, EnforceNonNegative: true, "§6.4.1, §15"),

            // Hold: NOT floored. Karim, 2026-08-20 — the hard floors are the safe, the client
            // advance and عهدة, and no others. The hold loses little by it: guard 3 already refuses
            // any posting out of a hold account before handover ("the hold only grows"), and guard
            // 3b requires a HoldRelease to leave the hold at exactly zero. The floor was the third
            // lock on a door with two. See decisions.md D-044.
            new(AccountType.Hold, AccountClass.Asset, NormalBalance.Debit, Treasury.LedgerKind.Hold,
                AccountScope.ProjectRequired, PartyType.Client, IsPostable: true, EnforceNonNegative: false, "§5.1, §6.4.2"),

            // FirmAdvance: NOT floored, by the same ruling. This one does lose real protection —
            // nothing now stops a firm advance being recovered past zero, which would show as Kaff
            // owing the client on an advance the client never made. §6.4.3 gives this ledger a hard
            // CAP, not a floor, and the cap is slice 3's to build. Recorded as an accepted exposure
            // in decisions.md D-044, not as an oversight.
            new(AccountType.FirmAdvance, AccountClass.Asset, NormalBalance.Debit, Treasury.LedgerKind.FirmAdvance,
                AccountScope.ProjectRequired, PartyType.Client, IsPostable: true, EnforceNonNegative: false, "§6.4.3"),

            // عهدة: floored. Named explicitly in Karim's 2026-08-20 ruling. An employee cannot have
            // settled more than they were ever advanced.
            new(AccountType.PettyCashAdvance, AccountClass.Asset, NormalBalance.Debit, Treasury.LedgerKind.PettyCashAdvance,
                AccountScope.ProjectRequired, PartyType.Employee, IsPostable: true, EnforceNonNegative: true, "§6.4.4"),

            // §6.4.5: a withdrawal may be a returnable advance, which puts the owner in debit.
            // The account must therefore be allowed to swing both ways.
            //
            // No party is required: spec.md speaks of "the owner current account" in the singular,
            // and Kaff has one owner. If a second partner ever appears this becomes a party
            // sub-ledger — see decisions.md D-019.
            new(AccountType.OwnerCurrentAccount, AccountClass.Liability, NormalBalance.Credit, Treasury.LedgerKind.OwnerCurrentAccount,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.4.5"),

            // تشوينات. A LIABILITY, not an asset — see decisions.md D-034.
            //
            // §15 Extract 1: 300,000 − 60,000 − 75,000 + 75,000 = 240,000. تشوينات ADDS to what the
            // client pays. The client hands Kaff 75% of the value of material delivered to site but
            // not yet installed — money received for work not yet certified, which is the same shape
            // as ClientAdvance above, and is recovered as the material is installed.
            //
            // Modelled as an asset it would only be postable in the direction that REDUCES the
            // client payment, and Extract 1 would net 90,000 instead of 240,000.
            //
            // NOT floored. Karim's 2026-08-20 ruling names three floored accounts and تشوينات is not
            // among them. §15 still requires "تشوينات in equals تشوينات recovered", so the invariant
            // stands — it is simply no longer enforced by the database, and over-recovery would now
            // be caught by the §15 reconciliation in slice 5 rather than refused at the point of
            // posting. The second real exposure this ruling accepts. See decisions.md D-044.
            new(AccountType.MaterialAdvance, AccountClass.Liability, NormalBalance.Credit, null,
                AccountScope.ProjectRequired, PartyType.Client, IsPostable: true, EnforceNonNegative: false, "§5.1, §15"),

            // ---- Revenue and expense ----
            new(AccountType.ContractRevenue, AccountClass.Revenue, NormalBalance.Credit, null,
                AccountScope.ProjectRequired, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),
            new(AccountType.ProjectCost, AccountClass.Expense, NormalBalance.Debit, null,
                AccountScope.ProjectRequired, null, IsPostable: true, EnforceNonNegative: false, "§6.10"),
            new(AccountType.CompanyExpense, AccountClass.Expense, NormalBalance.Debit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.10"),
            new(AccountType.BankCharge, AccountClass.Expense, NormalBalance.Debit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.10"),
            new(AccountType.DepreciationExpense, AccountClass.Expense, NormalBalance.Debit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),
            new(AccountType.InterestExpense, AccountClass.Expense, NormalBalance.Debit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.6 🟡"),

            // ---- Accounting layer ----
            new(AccountType.FixedAsset, AccountClass.Asset, NormalBalance.Debit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),

            // Contra-asset: an asset by classification, credit by normal balance.
            new(AccountType.AccumulatedDepreciation, AccountClass.Asset, NormalBalance.Credit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),

            new(AccountType.Prepayment, AccountClass.Asset, NormalBalance.Debit, null,
                AccountScope.ProjectOptional, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),
            new(AccountType.AccruedExpense, AccountClass.Liability, NormalBalance.Credit, null,
                AccountScope.ProjectOptional, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),
            new(AccountType.ContractAsset, AccountClass.Asset, NormalBalance.Debit, null,
                AccountScope.ProjectRequired, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),
            new(AccountType.ContractLiability, AccountClass.Liability, NormalBalance.Credit, null,
                AccountScope.ProjectRequired, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),

            // ---- Equity ----
            new(AccountType.PaidInCapital, AccountClass.Equity, NormalBalance.Credit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),
            new(AccountType.RetainedEarnings, AccountClass.Equity, NormalBalance.Credit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),
            new(AccountType.CurrentYearProfit, AccountClass.Equity, NormalBalance.Credit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.6"),
            new(AccountType.OwnerDrawings, AccountClass.Equity, NormalBalance.Debit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.4.5"),

            // ---- Withholding tax ----
            new(AccountType.TaxWithheldAtSource, AccountClass.Asset, NormalBalance.Debit, null,
                AccountScope.ProjectOptional, null, IsPostable: true, EnforceNonNegative: false, "§6.7"),
            new(AccountType.TaxWithholdingPayable, AccountClass.Liability, NormalBalance.Credit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.7"),
            new(AccountType.VatPayable, AccountClass.Liability, NormalBalance.Credit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.7 🟡"),

            // ---- Financing ----
            new(AccountType.LoanPayable, AccountClass.Liability, NormalBalance.Credit, null,
                AccountScope.CompanyWide, null, IsPostable: true, EnforceNonNegative: false, "§6.6 🟡"),
        ];

        return rows.ToFrozenDictionary(row => row.Type);
    }
}
