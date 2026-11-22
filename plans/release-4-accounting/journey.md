# Release 4 (slice 7), accounting: the journey

Status: planning draft, 2026-11-22. Nothing here is built or approved. No web research was done for this
draft. Items tagged `practice` are general accounting habits, not fetched and not cited to a URL, and none of
them is a requirement.

Tags used on every step:

- `[spec §x line n]` required by `spec.md` (line numbers are from the file as read today).
- `[D-nnn]` decided by Kaff or Nabil.
- `[exists: path]` already in the code.
- `[practice: source]` how comparable work is run. Context only.
- `[QUESTION Qn]` nobody has ruled. A default is running or proposed; see `questions.md`.

Roles: Finance prepares and posts. The Owner approves financial movements. Technical Office, Site Engineers,
HR and clients have no part in this release. Everything is desktop work; the Owner may glance at budget
alerts on a phone.

## What exists today (checked in `src/`)

- `AccountingPeriod` entity: Open or Closed, `Close(...)`, no `Reopen`. A database trigger refuses a posting
  dated inside a closed period. The close workflow is not built.
  [exists: `src/Domain/Treasury/AccountingPeriod.cs`]
- Posting types for every non-cash event the spec names: `RevenueRecognition`, `ExpenseAccrual`,
  `AccrualRelease`, `Prepayment`, `PrepaymentAmortisation`, `Depreciation`, `WipAdjustment`,
  `PeriodCloseTransfer`, `YearEndProfitTransfer`, plus `AssetPurchase`, `LoanDrawdown`,
  `LoanPrincipalRepayment`, `LoanInterestPayment`. [exists: `src/Domain/Treasury/PostingType.cs`]
- Account types for the accounting layer: `FixedAsset`, `AccumulatedDepreciation` (contra, credit-normal),
  `Prepayment`, `AccruedExpense`, `ContractAsset`, `ContractLiability`, `PaidInCapital`, `RetainedEarnings`,
  `CurrentYearProfit`, `OwnerDrawings`, `LoanPayable`, `DepreciationExpense`, `InterestExpense`,
  `CompanyExpense`, `ProjectCost`. [exists: `src/Domain/Treasury/AccountTypeMetadata.cs`]
- `RevenueRecognition` is already written when an extract reaches Owner approval, for work value plus change
  order value, before deductions. [exists: `src/Domain/Contracts/Billing/ExtractPostings.cs`; moment is
  ASSUMED in `src/Domain/Rulings/ExtractPostingMoment.cs`]
- `IProgressMetric` and `ExecutedValueToDate` exist for Lump Sum. [exists:
  `src/Domain/Contracts/Progress/IProgressMetric.cs`]
- Permission `PeriodClose`: Finance, company-wide, marked Unresolved. [exists:
  `src/Domain/Authorization/PermissionCatalogue.cs`, Q23]
- Treasury API has only list postings, balances, post movement, reverse posting, proof of numbers. [exists:
  `src/Api/Features/Treasury/`]
- Web has one treasury-adjacent screen, `projects/:projectId/proof`. No accounting route exists. [exists:
  `src/Web/src/app/app.routes.ts`]
- Not found in `src/Domain` (grep): any asset entity, budget, purchase order, supplier invoice, expense
  category, or committed-money concept.
- Nine posting types have no ruled account pair and are listed as pending: `OpeningBalance`,
  `ChequeDeposit`, `ChequeClearance`, `ChequeBounce`, `SiteExpensePayment`, `AssetPurchase`,
  `LoanDrawdown`, `PeriodCloseTransfer`, `YearEndProfitTransfer`. [exists:
  `src/Domain/Treasury/PostingAccountLegality.cs`, `PendingQ91`]

The posting model is one from-account and one to-account per posting, amount always positive. A trial balance
therefore comes from summing each account's in and out and applying its normal balance. It balances by
construction unless an account has the wrong class or sign, an opening balance has no counter-account, or a
posting type has nowhere to land. Those are the real risks to the gate. [spec §6.1 line 212-218; exists:
`AccountType.cs`]

## A. Go-live set-up (once)

1. Owner and Finance enter the opening balances at go-live: cash, banks, receivables, payables, fixed assets,
   paid-in capital, retained earnings. The screen refuses to save until assets equal liabilities plus equity.
   [spec §6.6 line 278; spec §16 #17 line 638; QUESTION Q2]
2. The opening entry needs a counter-account. None exists for `OpeningBalance`. [exists: `PendingQ91`;
   QUESTION A2 in questions.md]
3. Finance enters the fiscal-year start. Default calendar year. [PROPOSAL; QUESTION Q2]
4. Owner sets the default budget tolerance. [spec §6.8 line 317; QUESTION Q6]

## B. During the month

5. Finance records a company expense: picks one of six categories (rent and utilities, admin and office
   payroll, assets and vehicles, general materials, bank charges, other), the amount, date, who was paid, and
   the tag, company or project. The tag cannot be left empty or set to both. [spec §6.10 line 323; ASSUMED
   that the screen is Finance only, `TreasuryPostCompany` is Finance; exists:
   `src/Domain/Authorization/PermissionCatalogue.cs`]
6. The tag is already structural: `ProjectCost` requires a project, `CompanyExpense` forbids one. A mistaken
   tag is corrected by a reversal and a new expense, never an edit. [exists: `AccountTypeMetadata.cs`;
   spec §6.1 line 216]
7. Site and payroll spending reach the same tags through their own flows (Release 2). This release only
   proves the rule and reports on it. [spec §10 line 515]
8. Finance records an asset purchase on the asset register: name, category, cost, purchase date, rate. The
   rate defaults from the category and Finance may edit it per asset. Defaults: computers, software and
   storage 50%; buildings 5%; purchased intangibles 10%; all other 25%. [spec §6.6 line 274]
9. Finance records an accrual (cost incurred, not yet paid) or a prepayment (paid, belongs to later months,
   with the number of months). Each is tagged project or company. [spec §6.2 line 233; spec §6.6 line 275;
   tag rule spec §6.10 line 323; months and release method PROPOSAL]
10. When the real invoice lands, Finance links it to the accrual and the accrual releases. [exists:
    `PostingType.AccrualRelease`; link step PROPOSAL]
11. If Kaff has a bank loan or financed equipment, Finance records the drawdown, then each instalment as
    principal plus interest, taken from the bank statement. [spec §6.6 line 279, assumption 16 line 637;
    QUESTION Q8; instalment-by-statement is a PROPOSAL]
12. The Owner sees budget status per project. Committed money (approved invoices and orders) counts, not only
    cash paid. An alert appears when committed spend passes baseline plus tolerance. [spec §6.8 line 317;
    QUESTION Q6: nothing named "committed" exists yet]
13. An approved change order raises that project's baseline. Nothing else can edit it. [spec §6.8 line 317;
    change order approval exists: `src/Api/Features/ChangeOrders`]

## C. Month-end close

14. Finance opens the close checklist for the oldest open month. Months close in order. [spec §6.6 line 276;
    order is a PROPOSAL]
15. Depreciation run: the screen lists each asset's charge for the month, Finance confirms, the postings are
    written dated in the month. Fully depreciated assets stop. [spec §6.6 line 274; method and start month
    QUESTION Q7]
16. Prepayment amortisation: each prepayment's monthly share moves to expense. Accrual leftovers are listed
    for review. [spec §6.6 line 275; PROPOSAL for the batch]
17. Revenue recognition review: per project, executed value to date, billed to date, and the difference as a
    contract asset (executed above billed) or contract liability (billed above executed). [spec §6.6 line 281;
    spec §16 #14 line 635; QUESTION Q1: posted or shown only]
18. Finance posts the `WipAdjustment`. The posting is the change since last month, not the full amount again.
    [PROPOSAL; Q1]
19. Finance reads the trial balance for the month. It must balance. If it does not, close is refused. [spec
    §6.6 line 277; gate wording backlog Slice 7]
20. Finance requests close. Whether the Owner must approve is open; Finance alone is the assumed performer.
    [spec §9 line 372 "Owner approves all financial movements"; ASSUMED Finance in `PermissionCatalogue.cs`;
    QUESTION Q3]
21. The month becomes closed. No posting can be dated inside it. A correction is a reversing posting dated in
    an open month. [spec §6.6 line 276; exists: `AccountingPeriod.cs`]
22. Overhead spread report for the closed month: company overhead shown per project in proportion to size.
    Report only, no postings. [spec §6.10 line 325; spec §16 #8 line 629; basis QUESTION Q9]

## D. Statements and year end

23. Finance or Owner opens the trial balance for any date range and exports it. [spec §6.6 line 277; format
    QUESTION Q10]
24. Finance or Owner opens the balance sheet at a month end. It shows assets, liabilities and equity, with
    the five ledgers kept as separate lines, never netted. Assets equal liabilities plus equity. [backlog
    gate "balance sheet balances"; spec §6.4 line 246; CLAUDE.md "five ledgers never net"; the screen itself
    is a PROPOSAL, spec names only the export]
25. At year close, current-year profit rolls into retained earnings with a `YearEndProfitTransfer`. [spec
    §6.6 line 278; what happens to owner drawings, QUESTION Q4; how profit reaches `CurrentYearProfit`,
    QUESTION A1]
26. Owner reads the year-end summary and confirms. [spec §9 line 372; Owner approval of year close is
    ASSUMED, QUESTION Q3]

## Practice, as context only

- Close checklist with blockers listed before the button works. [practice: general accounting practice, not
  fetched]
- Depreciation as a batch run with a preview. [practice: general, not fetched]
- Percentage-of-completion revenue as a monthly adjustment. Egyptian reporting practice may follow
  international revenue rules; not checked. [practice: not fetched, verify with Karim's accountant]
- A large contractor's ERP has full journal entry and a chart of accounts. Does not transfer: Kaff forbids
  free-form journals and an open chart. [spec §1; CLAUDE.md out of scope]

## What the journey does not include

Tax module, VAT lines (pending Q5), multi-currency, free-form journals, stored balances, a reopen of a closed
month, asset disposal (spec silent, see Q7).
