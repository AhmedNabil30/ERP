# Release 2, site money: journey

Date: 2026-11-22. Planning only. Nothing here changes `spec.md`. Where a step differs from the spec, it is
tagged `[QUESTION]` and the matching entry is in `questions.md`.

## How to read the tags

- `[spec §x line n]`: required by `spec.md`, line number as of 2026-11-22.
- `[D-nnn]`: decided in `decisions.md`.
- `[exists: path]`: found in `src/` on 2026-11-22. Existing code is a fact about the app, not a business rule.
- `[ASSUMED: note]`: running on a `RulingNote` default nobody confirmed.
- `[practice: ...]`: how comparable contractors work. Context only, never a rule. No practice source was
  searched for this plan (kept lean), so no step carries one. `ux/research/` holds no petty-cash or
  subcontractor-extract file either.
- `[QUESTION Qn]`: the spec is silent or ambiguous. Not decided here.

## What exists today (checked 2026-11-22)

- Posting types and the account legality table for all six ledgers: `src/Domain/Treasury/PostingTypes.cs`,
  `PostingAccountLegality.cs`, `AccountTypeMetadata.cs`.
- The safe floor is a database guard, with a translated refusal that says "Record an owner injection first"
  (`src/Infrastructure/Persistence/Sql/001_guards.sql` line 331, `src/Web/public/locales/en.json` line 257).
- Generic movement endpoints: `POST /api/projects/{projectId}/treasury/postings` and `POST /api/treasury/postings`
  (Finance only). No عهدة, firm advance, owner-account or subcontractor endpoint exists. No such screen exists
  (`src/Web/src/app/app.routes.ts`).
- Engineer's expense draft: `src/Api/Features/DailyLogs/DraftExpense`. It stores a draft and posts nothing.
  The permission `SiteExpenseConfirm` exists in the catalogue (`PermissionCatalogue.cs` line 445); no handler uses it.
- The weekly QC report is built as a step of the extract chain: `src/Api/Features/Extracts/SubmitQcReport`.
  It takes the two material values (D-235 item 4).
- تشوينات issue and recovery are written by the extract flow (`src/Domain/Contracts/Billing/ExtractPostings.cs`).
- Subcontractor retention release is built (`src/Domain/Closure/SubcontractorRetention.cs`). Nothing accrues it.
- No sub-BOQ or subcontract record on a project exists (grep `sub-BOQ` finds only `CreateSubcontractor/Request.cs`).
- Project account sets exclude عهدة, and the code names no subcontractor accounts
  (`ProjectAccountSetFactory.cs` line 28, `ClosureRulings.cs` line 163). عهدة is per employee. I found no link
  between a signed-in user and an `Employee` record in `Domain/Identity/User.cs` or `Domain/MasterData/Employee.cs`.

---

## Flow 1. عهدة (petty cash) from site

Actors: Junior site engineer, supervising site engineer, Finance, (Owner, see Q3).

1. Junior opens the عهدة request on his phone, types an amount and a purpose, saves it as a draft.
   [spec §9 line 378] [spec §8 line 359] [exists: `Permission.DraftCreate`, `PermissionCatalogue.cs` line 409]
2. The system refuses an amount above 10,000 EGP in one request.
   [spec §6.4 line 250, marked 🟡] [ASSUMED: none registered; Q22 open] [QUESTION Q6]
3. The system refuses a request that takes the project's open عهدة past `collected − spent − owner safety margin`.
   [spec §6.4 line 250, 🟡] [QUESTION Q6: "collected", "spent" and the margin are not defined]
4. The system refuses a new request while the holder's previous عهدة is not cleared with receipts.
   [spec §6.4 line 250] [QUESTION Q6: one open per person, per project or per site]
5. Supervisor sees the drafts of his project and submits one, or returns it to the junior with a written reason.
   [spec §9 line 378] [spec CLAUDE.md "Rejections return to origin with a reason"] [exists: `Permission.DraftSubmit`]
6. Finance sees submitted requests and pays one: money moves from the safe to the holder's عهدة account.
   [spec §6.4 line 250] [exists: `PostingType.PettyCashIssue`]
7. If the safe cannot cover the payment, the payment is refused and the screen offers an owner injection (Flow 4).
   [spec §6.1 line 218] [exists: guard `KAFF_NEGATIVE_BALANCE`]
8. Whether the Owner approves the request before step 6 is not stated for عهدة.
   [spec §9 line 371: "Owner approves all financial movements"] against [spec §6.4 line 250: "accounts pays"]
   [QUESTION Q3]
9. The holder spends, keeps receipts (invoice photos), and returns to the app to settle: lists what was spent with
   a photo of each receipt, and the unspent amount.
   [spec §6.4 line 250: "cleared with receipts"] [spec §8 line 359: "invoice photos"] [exists: `PettyCashSettlement`]
10. Finance checks the receipts, clears the عهدة. The unspent amount goes back to the safe.
    [exists: `PettyCashReturn`] [QUESTION Q1: whether the expense lands in project cost through this settlement
    or through a separate site-expense draft (Flow 5)]
11. A rejected settlement returns to the holder with a stored reason.
    [CLAUDE.md "Rejections return to origin with a reason"]
12. The عهدة account can never go below zero (settled more than advanced). The database refuses it.
    [spec §6.4 amendment lines 255-256] [exists: `EnforceNonNegative: true`, `AccountTypeMetadata.cs` line 108]
13. Offline: not in this release.
    [spec §8 line 359] [plan: Release 6, offline drafts]

Account to hold the عهدة: one per employee per project, created when first needed.
[exists: `ProjectAccountSetFactory.cs` line 28, `AC-302-D`] [QUESTION Q6: who is the "employee" for a site engineer]

## Flow 2. Firm advance (سلفة الشركة) under the owner's cap

Actors: Finance, Owner.

1. Finance prepares a firm advance for a project: amount and what it pays for.
   [spec §6.4 line 249] [exists: `FinancialMovementPrepare`] [QUESTION Q5: what spending counts]
2. The system checks the advance against the hard cap. A breach is refused, not warned.
   [spec §6.4 line 249: "hard cap the system enforces"] [spec §6.4 amendment lines 261-263: "it is not built yet"]
   [QUESTION Q5: the cap's size and scope, and who sets it]
3. Owner approves the advance. Finance cannot.
   [spec §6.4 line 249: "owner approval"] [exists: `Permission.FirmAdvanceApprove`, owner only]
4. The advance posts to the project's firm-advance account (`FirmAdvanceIssue`), cash out of the safe.
   [exists: `PostingTypes.cs` line 64] [PostingAccountLegality rule 5] If the safe is short, see Flow 4 step 4.
5. Owner sees the total firm-advance exposure across all projects on his dashboard.
   [spec §6.4 line 249] [no dashboard route exists, `app.routes.ts`]
6. The client repays the firm advance: `FirmAdvanceRecovery`.
   [exists: `PostingTypes.cs` line 65] [QUESTION Q5: recovered through an extract, or as a separate collection]
7. Nothing stops a recovery running past zero. The tests carry this, not the database.
   [spec §6.4 amendment lines 261-263] [exists: `EnforceNonNegative: false`, `AccountTypeMetadata.cs` line 103]

## Flow 3. تشوينات (material advance) issue and recovery

Actors: Supervising engineer, Finance, Owner (through the extract chain).

1. The supervising engineer confirms the value of material stockpiled and the value installed this period when he
   submits the QC report. The junior never touches money.
   [D-235 item 4] [exists: `SubmitQcReport/Request.cs`]
2. The extract issues تشوينات at 75% of stockpiled material, recovers as material is installed. Neither is a hold.
   [spec §5.1 lines 152, 164] [spec §15 lines 603-605] [exists: `ExtractPostings.cs`]
3. The recovery per extract is 75% of installed material value, capped at what is outstanding.
   [ASSUMED: `MaterialAdvanceRecoveryRule`, Q58] [exists: `MaterialAdvanceRecoveryRule.cs` line 23]
4. Whether a site entry can mark material as stockpiled is not decided. Every daily-log material entry is recorded as
   not stockpiled today.
   [D-225 item 4] [QUESTION Q8]
5. "تشوينات in" must equal "تشوينات recovered". The database no longer refuses over-recovery, so a reconciliation read
   must show it.
   [spec §15 line 612] [spec §6.4 amendment lines 264-265] [exists: `ProofOfNumbers` handler includes the ledger]

This flow has no new screen of its own. Release 2 adds one read of the outstanding balance and the reconciliation test.

## Flow 4. Owner current account, injection, and the safe-overdraw prompt

Actors: Finance, Owner.

1. A payment from the safe would make it negative. The database refuses it. The user sees a translated message that
   says to record an owner injection first.
   [spec §6.1 line 218] [exists: `en.json` line 257, `DatabaseGuardTranslation.cs`]
2. The screen shows a button that starts an owner injection for at least the shortfall.
   [spec §6.1 line 218: "prompts an owner injection instead"] [PROPOSAL: the shortfall figure on the button; the
   spec says "prompts" and gives no screen]
3. The injection is recorded: the Owner current account is credited (a liability Kaff repays later), the safe is
   debited.
   [spec §6.4 line 251] [exists: `PostingType.OwnerInjection`, `AccountType.OwnerCurrentAccount`]
4. Who records it, and whether a second person approves, is not stated. The Owner is the source of the money, and
   "nobody creates and approves the same movement".
   [spec §9 line 376] [QUESTION Q7]
5. Into which cash account the injection lands (safe or a bank) is not stated.
   [spec §6.3 lines 236-243] [QUESTION Q7]
6. The user returns to the refused payment and repeats it. The refused payment is not kept.
   [exists: the guard fails the request, nothing is stored] [QUESTION Q7: keep it as a pending payment]
7. Later, the Owner takes a withdrawal (returnable advance or final drawing), or Kaff repays an injection.
   [spec §6.4 line 251] [exists: `OwnerWithdrawal`, `OwnerDrawing`, `OwnerRepayment`]
8. Repaying the Owner approves money to himself. Who approves it is not stated.
   [spec §9 lines 371, 376] [QUESTION Q7]
9. One company-wide account, never one per project.
   [D-019] [exists: `AccountScope.CompanyWide`, `AccountTypeMetadata.cs` line 117]
10. Owner current account never nets against another of the five ledgers.
    [spec §6.4 line 246] [CLAUDE.md]

## Flow 5. Accounts confirms an engineer's expense draft

Actors: Site engineer (junior, as drafter), Finance (confirm).

1. The engineer enters an expense in his daily log. It is a draft and posts nothing.
   [spec §8 line 355] [D-225 item 3] [exists: `DraftExpense`]
2. Whether the engineer types an amount is unresolved. Today he types it, and the screen never shows it back to him.
   [ASSUMED: `SiteExpenseDraftAmount`] [QUESTION Q9]
3. Finance opens the list of expense drafts for the project, sees what was drafted, and confirms or rejects.
   [spec §8 line 355] [D-225 item 3] [exists: `Permission.SiteExpenseConfirm`, Finance and a named Technical Office grant]
4. Rejection returns to the engineer with a reason.
   [CLAUDE.md "Rejections return to origin with a reason"]
5. Confirming posts a `SiteExpensePayment` against the project's cost, tagged project, never company.
   [spec §6.10 line 323] [exists: `PostingType.SiteExpensePayment`, source type `SiteExpense`]
   [QUESTION Q12: the account pair is in the unruled list `PendingQ91`]
6. Whether the Owner approves the confirmation is not stated.
   [spec §9 line 371] [QUESTION Q3]
7. Whether this flow and a عهدة receipt are the same spend recorded twice is not stated.
   [QUESTION Q1]

## Flow 6. Subcontractor extract with 5% retained

Actors: Supervising engineer, Technical Office, Finance, Owner.

0. Before any extract, the system needs the subcontract on the project: which subcontractor, what is agreed.
   [D-139 §4: rates live on each project's sub-BOQ] [no such record exists in `src/`] [QUESTION Q2]
1. One extract per subcontractor per week.
   [spec §5.1 line 171]
2. The extract is certified. Who certifies is not named.
   [spec §7 line 346: "certify"] [QUESTION Q2]
3. Technical Office gates the quantities. It never touches money.
   [spec §7 line 346] [spec §9 line 374] [exists: `Permission.QuantityGateApprove`]
4. Accounts applies deductions: 5% retention, and withholding (Flow 7).
   [spec §7 line 346] [spec §5.1 line 171] [QUESTION Q2: whether other deductions apply, for instance debit notes]
5. Retention is 5% of what, and whether a subcontractor can be set to zero, is open.
   [spec §5.1 line 171: "zeroable per subcontractor 🟡"] [spec §16 row 19 line 640] [QUESTION Q2, Q11]
6. Owner approves every subcontractor extract.
   [spec §7 line 346, spec §9 line 371] [exists: `FinancialMovementApprove`]
   [QUESTION Q3: the spec's "no threshold" line is for client extracts]
7. On approval the extract posts to the subcontractor's sub-ledger: payable credited, 5% moved to the retention
   account.
   [spec §7 line 346] [exists: `SubcontractorRetentionAccrual`; accounts `SubcontractorPayable` and
   `SubcontractorRetention`, `AccountTypeMetadata.cs` lines 76 and 82]
8. A rejection at any gate returns the extract to Draft with a reason.
   [spec §7 line 342] [CLAUDE.md]
9. Finance pays the subcontractor: `SubcontractorPayment`, safe to payable. Safe short, see Flow 4.
   [exists: `PostingTypes.cs` line 58] [QUESTION Q2: whether the payment is part of this flow or separate]
10. A stopped project may still pay subcontractors.
    [spec §13 amendment lines 554-557] [exists: `PostingProjectStatePolicy.cs` line 121]
11. The retention is released after the warranty ends, by the flow already built.
    [exists: `SubcontractorRetention.Release`] [ASSUMED: `SubcontractorRetentionTiming`, Q26]
12. The QC report is not a step of the subcontractor chain.
    [spec §7 line 346 names certify, Technical Office, Accounts, Owner, post] [QUESTION Q10]

## Flow 7. Withholding on subcontractor and supplier payments

Actors: Finance.

1. Finance records the withholding category on the job, as for a client contract. Not on the firm's record.
   [D-139 §5] [spec §6.7 lines 290-310]
2. Rate: 1% contracting and supplies, 3% services, 5% professional fees, on the amount before VAT.
   [spec §6.7 line 284] [exists: `WithholdingRates`, `Withholding.cs` line 32]
3. When Kaff pays, Kaff holds the withheld amount back and carries a liability to remit.
   [spec §6.7 line 287] [exists: `TaxWithholdingRetained`, account `TaxWithholdingPayable`]
4. Which amount the percentage applies to is not ruled for these payments. The client rule is `NetPayable`.
   [ASSUMED: `WithholdingBaseRule`, Q114, written for client extracts] [QUESTION Q4]
5. A supplier is a company-wide payable with no project, so "per job" has nothing to attach to.
   [exists: `SupplierPayable` is `CompanyWide`, `AccountTypeMetadata.cs` line 78] [D-139 §5] [QUESTION Q4]
6. Remitting to the tax authority is a cash posting that already exists as a type. Whether Release 2 builds it is open.
   [spec §6.7 line 287: "carries a liability to remit"] [exists: `PostingType.TaxRemittance`]
   [CLAUDE.md out of scope: tax module] [QUESTION Q4]

## Flow 8. The weekly QC report

Actors: Supervising engineer.

1. The supervising engineer submits the weekly QC report. The extract moves from Draft to QC.
   [spec §7 line 332-334] [exists: `SubmitQcReport`, built in the extract chain]
2. Holder of the right is the supervising engineer. Nobody has confirmed it.
   [ASSUMED: `ExtractQcReportAuthor`, D-189 §3]
3. For Release 2 this is a check that the built step meets KAFF-612's acceptance criteria. No new screen.
   [`stories/backlog.md` line 1125] [QUESTION Q10: whether weekly QC exists for subcontractor extracts or alone]
