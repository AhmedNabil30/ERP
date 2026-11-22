# Release 4 draft stories (slice 7, KAFF-700 to 712)

Drafts, not yet Definition of Ready. Points include the server, the screen, and the permission test. The
backlog total was 61 for KAFF-700 to 711; this draft is 93, because screens are counted and 712 is new.
Every AC cites its source. An AC tagged ASSUMED waits on the named question. All money is `decimal(18,4)`,
shown at two decimals. Permission tests hit endpoints directly (CLAUDE.md).

Gate for the slice: the balance sheet balances (backlog, Slice 7).

Prerequisites outside the slice: A2 in `questions.md` (account pairs for the nine pending posting types); Q2
(opening figures); Q23/Q3 (who closes).

## KAFF-700 Asset register and depreciation (13)
- AC1 Given a purchase of 12,000 in category computers, when it is saved, then the rate shows 50% and Finance
  may change it per asset. [spec §6.6 line 274]
- AC2 Given an asset, when depreciation runs for a month, then a `Depreciation` posting from
  `AccumulatedDepreciation` to `DepreciationExpense` is written dated in that month, and book value equals
  cost minus accumulated. [spec §6.2 line 233, §6.6 line 274; exists: `AccountType.cs` contra account]
- AC3 Given an asset fully depreciated, when depreciation runs, then no charge is written. [PROPOSAL; Q7]
- AC4 Given a month already closed, when an asset is dated inside it, then the save is refused. [spec §6.6
  line 276; exists: closed-period trigger]
- AC5 Given a depreciation run, when it is run twice for one month, then the second writes nothing.
  [PROPOSAL, guard against double posting]
- AC6 Given Owner, HR, Technical Office, when they call the write endpoint, then they are refused. [spec §9
  line 371-376; PROPOSAL for the permission name]
- AC7 Method, start month, rounding, disposal. [ASSUMED straight-line, full month from purchase month; Q7]

## KAFF-701 Accruals and prepayments (8)
- AC1 Given an accrual of 5,000 tagged company, when saved, then an `ExpenseAccrual` posting is written to
  `AccruedExpense` and the open list shows it. [spec §6.2 line 233, §6.6 line 275, §6.10 line 323]
- AC2 Given a prepayment of 12,000 over 12 months, when each month closes, then 1,000 moves to expense by
  `PrepaymentAmortisation`, and the last month leaves the balance at exactly zero. [spec §6.6 line 275;
  months PROPOSAL]
- AC3 Given an accrual, when the real cost lands, then `AccrualRelease` clears it with no double cost.
  [exists: `PostingType.AccrualRelease`; link PROPOSAL]
- AC4 Given a save with no tag, or both, then it is refused. [spec §6.10 line 323]

## KAFF-702 Month-end close (8)
- AC1 Given an open month, when Finance closes it, then it is `Closed` with who and when stored, and no
  posting dated in it is accepted afterwards. [spec §6.6 line 276; exists: `AccountingPeriod.cs`]
- AC2 Given a closed month, when someone wants to correct it, then there is no reopen, and a reversing posting
  dated in an open month is the only route. [exists: `AccountingPeriod.cs` remarks; spec §6.1 line 216]
- AC3 Given the checklist, when depreciation, amortisation, revenue recognition or a balanced trial balance
  is missing, then close is refused and names the step. [PROPOSAL; practice: close checklist, not fetched]
- AC4 Given months open in sequence, when Finance closes the later one first, then it is refused. [PROPOSAL]
- AC5 Every close writes an audit record with before and after. [CLAUDE.md audit rule]
- AC6 Given a month that is closing, when any change order, extract or posting tries to date inside it, then
  it is refused with the closed-month reason shown on screen. [exists: closed-period trigger; spec §6.6]

## KAFF-703 Who performs the close (2)
- AC1 Given Q23 is answered "Finance", when Finance closes, then it succeeds; no other role can. [spec §9
  line 372; ASSUMED Finance, `PermissionCatalogue.cs`]
- AC2 Given Q3 answers "Owner approves", when Finance requests close, then the month waits as "requested" and
  the Owner approves; the same user cannot request and approve. [spec §9 line 371-376, CLAUDE.md]
- Removes the `Unresolved` flag on `PeriodClose`.

## KAFF-704 Trial balance and export (5)
- AC1 Given a date range, when opened, then each account shows its balance in the debit or credit column by
  its normal balance, a contra account in the credit column, and totals are equal. [spec §6.6 line 277;
  exists: `NormalBalance`]
- AC2 Given the five ledgers, then each shows as its own account row and nothing nets across them.
  [CLAUDE.md; spec §6.4 line 246]
- AC3 Given export, then a file downloads with Arabic names readable in Excel. [format PROPOSAL; Q10]
- AC4 Given the safe and each bank, then balances derive from postings, never from a stored column.
  [CLAUDE.md]

## KAFF-705 Equity, opening balances, year close (8)
- AC1 Given opening figures, when saved, then assets equal liabilities plus equity or save is refused.
  [spec §6.6 line 278; Q25; backlog gate]
- AC2 Given a year with all 12 months closed, when year close runs, then profit for the year moves from
  `CurrentYearProfit` to `RetainedEarnings` by `YearEndProfitTransfer`, and current-year profit is zero.
  [spec §6.6 line 278; mechanics QUESTION A1]
- AC3 Given a month open in the year, when year close is tried, then it is refused naming the month.
  [PROPOSAL]
- AC4 Given owner drawings, then they show as their own line and their year-end treatment is as ruled. [spec
  §6.4 line 251; QUESTION Q4]
- AC5 Fiscal year start. [ASSUMED calendar year, Q2]

## KAFF-706 Revenue recognition at close (13)
- AC1 Given a Lump Sum project with executed value 600,000 and billed 450,000, when the close runs, then a
  `WipAdjustment` of 150,000 is posted from `ContractRevenue` to `ContractAsset`. [spec §6.6 line 281;
  exists: `ExecutedValueToDate`; Q1]
- AC2 Given billed above executed by 80,000, then 80,000 is posted to `ContractLiability` instead. [spec §6.6
  line 281]
- AC3 Given the next month, then only the change since last month is posted, not the full amount again.
  [PROPOSAL; Q1]
- AC4 Given extracts already recognise revenue at Owner approval, then month-end does not recognise the same
  value twice, and the sum of revenue equals executed value. [exists: `ExtractPostings.cs`; ASSUMED moment
  `ExtractPostingMoment.cs`]
- AC5 Given Cost Plus or Design contracts. [QUESTION Q12; not specified]
- AC6 Given a project with no progress reading, then it is skipped and shown as skipped. [PROPOSAL]
- AC7 The section 15 worked example stays unchanged. [CLAUDE.md, spec §15 line 596]

## KAFF-707 Expense tagging (5)
- AC1 Given a company expense with category and tag, when saved, then it posts to `CompanyExpense` (no
  project) or `ProjectCost` (a project), and never both or neither. [spec §6.10 line 323; exists:
  `AccountTypeMetadata.cs` scope]
- AC2 Given the six categories, then only those are offered. [spec §6.10 line 323; confirm Q11]
- AC3 Given a wrong tag, then the correction is a reversal plus a new expense, with no edit path. [spec §6.1
  line 216]
- AC4 Given an expense that would take the safe below zero, then it fails with the owner-injection prompt.
  [spec §6.1 line 218]
- AC5 Gross and net margin per project use the tag. [spec §6.10 line 323]

## KAFF-708 Overhead spread report (5)
- AC1 Given a month, when opened, then company overhead is spread across projects in proportion to size, and
  the total of shares equals the overhead. [spec §6.10 line 325; basis ASSUMED, Q9]
- AC2 Given the report, then no posting is written and project actual cost is unchanged. [spec §6.10 line 325]
- AC3 Rounding leftover goes to the largest project. [PROPOSAL]

## KAFF-709 Budget baseline (5)
- AC1 Given a signed BOQ, then baseline equals its cost total, and the Owner sets the tolerance. [spec §6.8
  line 317; tolerance ASSUMED, Q6]
- AC2 Given an approved change order, then the baseline rises by its cost impact. [spec §6.8 line 317; cost
  impact of a priced change order is not yet defined, QUESTION Q6]
- AC3 Given any other request to edit the baseline, then it is refused. [spec §6.8 line 317]
- AC4 The baseline is a copy of the signed BOQ values, not a reference. [CLAUDE.md frozen-by-copy]

## KAFF-710 Budget alerts on committed money (8)
- AC1 Given committed money above baseline plus tolerance, when an approved invoice or order lands, then the
  project shows an alert. [spec §6.8 line 317; "committed" has no source today, Q6]
- AC2 Given cash paid below the limit but committed above it, then the alert still fires. [spec §6.8 line 317]
- AC3 Who is told. [ASSUMED Owner, in the budgets overview and the dashboard; Q6]

## KAFF-711 Loans (5, only if Q8 is yes)
- AC1 Given a drawdown of 500,000, then `LoanPayable` rises by 500,000 and cash by the same. [spec §6.6 line
  279; exists: `LoanDrawdown`]
- AC2 Given an instalment of principal 40,000 and interest 5,000, then principal reduces the loan, interest
  goes to `InterestExpense`, and remaining principal shows on the balance sheet. [spec §6.6 line 279]
- AC3 Given Q8 is no, then the story is dropped. [spec §16 #16]

## KAFF-712 Balance sheet and the gate (8)
- AC1 Given the section 15 worked example plus an asset, an accrual, a prepayment and a closed month, then
  assets equal liabilities plus equity to four decimals. [backlog gate; spec §15 line 596]
- AC2 Given the five ledgers, then each is its own line and nothing nets. [CLAUDE.md; spec §6.4 line 246]
- AC3 Given any account with a wrong class or sign, a posting type with no account pair, or an opening
  balance with no counter-account, then a test fails and names it. [exists: `PendingQ91`; the real risks to the
  gate]
- AC4 Given a month closed, then its balance sheet figures do not change on re-reading. [spec §6.6 line 276]
- AC5 Balance sheet figures are derived from postings only. [CLAUDE.md]
