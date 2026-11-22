# Release 2, site money: draft stories

Date: 2026-11-22. Drafts for refinement. IDs map to existing backlog rows (`stories/backlog.md` lines 963-971,
1089, 1121-1125) where one exists; "R2-" numbers are for stories without a backlog row. Every AC cites a source.
An AC resting on a `[ASSUMED]` or `[QUESTION]` is marked HELD. The Scrum Master decides whether to build it
on the assumption (as `CLAUDE.md` allows for a recorded `RulingNote`) or hold it.

Order: build R2-05 and R2-06 first (every other flow pays through the safe), then 1-4, then 8, then 10-13.

---

## KAFF-609a. Junior drafts a عهدة request (3 pts, backlog KAFF-609)

Screen S1. Depends on Q6.
- AC1. Given a junior assigned to the project, when he saves a request with an amount and a purpose, then a draft
  exists in state مسودة and nothing is posted. [spec §9 line 378] [spec §8 line 359]
- AC2. Given a junior not assigned to the project, when he saves, then the request is refused. [spec §9 line 365]
- AC3. Given a supervisor, when he tries to draft, he cannot: drafting is the junior's. [`PermissionCatalogue.cs`
  line 409, `Permission.DraftCreate`] (HELD on whether the supervisor may also draft his own: Q6)
- AC4. Given an amount above 10,000 EGP, when saved, then refused. [spec §6.4 line 250] (HELD: Q6, 🟡 and ASSUMED
  nowhere)
- AC5. Given the engineer's response, then it carries no project money figure. [D-218 item 6;
  `SupervisorSeesProjectMoney`: the junior never receives money fields]

## KAFF-609b. Supervisor submits or returns a draft (3 pts, backlog KAFF-609)

Screen S2.
- AC1. Given a draft, when the supervisor submits, then its state is submitted and nobody can edit its amount.
  [spec §9 line 378]
- AC2. Given a draft, when the supervisor returns it, then a written reason is required and stored, and the draft
  goes back to the junior. [CLAUDE.md "Rejections return to origin with a reason"]
- AC3. Given the junior, when he calls submit, then refused. [`PermissionCatalogue.cs` line 431]
- AC4. Given any state change, then an audit record is written: who, when, before and after. [CLAUDE.md "Audit"]

## KAFF-311. Finance pays a عهدة (8 pts)

Screen S4. Depends on Q3, Q6.
- AC1. Given a submitted request and enough in the safe, when Finance pays, then one `PettyCashIssue` posting moves
  the amount from the safe to the holder's عهدة account for that project. [spec §6.4 line 250]
  [`PostingTypes.cs` line 61]
- AC2. Given no عهدة account for this holder and project, when Finance pays, then one is created. [`AC-302-D`,
  `ProjectAccountSetFactory.cs` line 28] (HELD: who the "employee" is, Q6)
- AC3. Given a payment above the project ceiling `collected − spent − owner safety margin`, then refused with the
  figures. [spec §6.4 line 250, 🟡] (HELD: Q6)
- AC4. Given the same user prepared and approved the movement, then refused. [spec §9 line 376]
- AC5. Given Finance not assigned to the project, then refused. [spec §9 line 365]
- AC6. Given an Owner approval step, when it is ruled, then payment waits for it. [spec §9 line 371] (HELD: Q3)
- AC7. Given a refusal of the payment by the safe floor, then S7 opens. [spec §6.1 line 218]

## KAFF-610. One عهدة open at a time, settle with receipts, return the rest (5 pts)

Screen S3, S4. Depends on Q1, Q6.
- AC1. Given a holder whose last عهدة is not cleared, when he asks for a new one, then refused. [spec §6.4 line 250]
  (HELD on scope: per person, project or site. Q6)
- AC2. Given a settlement with spend lines and receipt photos, when Finance clears it, then a `PettyCashSettlement`
  posting is written for the spent amount. [spec §6.4 line 250] [`PostingTypes.cs` line 62]
- AC3. Given unspent money, when cleared, then a `PettyCashReturn` posting returns it to the safe.
  [`PostingTypes.cs` line 63]
- AC4. Given spend lines above what was received, then refused: the account cannot go below zero.
  [spec §6.4 amendment lines 255-256] [`AccountTypeMetadata.cs` line 108]
- AC5. Given a settlement missing a receipt, then it cannot be sent. [spec §6.4 line 250: "cleared with receipts"]
- AC6. Given Finance rejects a settlement, then it returns to the holder with a stored reason. [CLAUDE.md]
- AC7. The five ledgers are never netted: the عهدة balance is read alone. [spec §6.4 line 246]

## KAFF-313. A payment the safe cannot cover prompts an owner injection (3 pts)

Screen S7. Server refusal exists. Depends on Q7.
- AC1. Given a payment that would make the safe negative, when sent, then it fails and no posting is written.
  [spec §6.1 line 218] [exists: `001_guards.sql` line 331, translation `KAFF_NEGATIVE_BALANCE`]
- AC2. Given the failure, then the screen offers an owner injection for at least the shortfall.
  [spec §6.1 line 218: "prompts an owner injection"] (the shortfall figure is a PROPOSAL, not a requirement)
- AC3. Given a user who may not record an injection, then the sheet says who may. (HELD: Q7)
- AC4. The refusal text exists in Arabic and English. [exists: `en.json` line 257, CLAUDE.md "No hardcoded
  user-facing strings"]

## KAFF-312. Owner current account: injection, withdrawal, repayment (5 pts)

Screen S8. Depends on Q7.
- AC1. Given an injection, then the Owner current account is credited and the cash account debited, and the balance
  reads as a liability Kaff repays later. [spec §6.4 line 251] [`AccountTypeMetadata.cs` line 117]
- AC2. Given a withdrawal, then the user must choose a returnable advance or a final drawing. [spec §6.4 line 251]
  [`PostingTypes.cs` lines 67-69]
- AC3. Given a repayment, then it reduces what Kaff owes the Owner. [spec §6.4 line 251] [`OwnerRepayment`]
- AC4. Given one company, then there is exactly one Owner current account, never one per project. [D-019]
- AC5. Given a posting that is not one of the four owner types, then it cannot touch this account.
  [`PostingAccountLegality.cs` line 55, rule 6]
- AC6. Given the creator of a movement, then the same user cannot approve it. [spec §9 line 376] (HELD: who approves
  a movement that benefits the Owner, Q7)

## KAFF-310. Firm advance under the owner's cap (8 pts)

Screens S6, S9. Depends on Q5.
- AC1. Given a prepared advance, when the Owner approves, then a `FirmAdvanceIssue` posting is written.
  [spec §6.4 line 249] [`Permission.FirmAdvanceApprove`] [`PostingTypes.cs` line 64]
- AC2. Given Finance, when it tries to approve, then refused. [`PermissionCatalogue.cs` line 597] [spec §9 line 371]
- AC3. Given an advance that would take total out past the cap, then refused, not warned. [spec §6.4 line 249]
  (HELD: cap value and scope, Q5)
- AC4. Given any user, when he reads the Owner's dashboard tile, then it shows aggregate exposure across all
  projects. [spec §6.4 line 249]
- AC5. Given a recovery larger than what is outstanding, then the test suite catches it; the database does not.
  [spec §6.4 amendment lines 261-263]
- AC6. Given a client with no account for it, then a project's firm advance account exists. [exists:
  `ProjectAccountSetFactory.cs` line 48]
- AC7. A firm advance never nets against another ledger. [spec §6.4 line 246]

## KAFF-608. Accounts confirms an engineer's expense draft (5 pts)

Screen S4. Depends on Q1, Q3, Q9, Q12.
- AC1. Given a draft, when Finance confirms, then a `SiteExpensePayment` posting is written against the project's
  cost. [D-225 item 3] [spec §8 line 355] [`PostingTypes.cs` line 71]
- AC2. Given the posting, then it carries the project; never company, never both. [spec §6.10 line 323]
- AC3. Given the engineer, then he cannot confirm his own draft. [spec §8 line 355] [`Permission.SiteExpenseConfirm`
  grants Finance and a named Technical Office role only]
- AC4. Given a rejected draft, then it returns to the engineer with a stored reason. [CLAUDE.md]
- AC5. Given the draft before confirmation, then no posting exists. [D-225 item 3: AC-600-H first half]
- AC6. Given a confirmed draft, then it cannot be edited or deleted; correction is a reversing posting.
  [spec §6.1 line 216]
- AC7. The amount comes from the engineer, or Accounts supplies it. (HELD: Q9, `SiteExpenseDraftAmount` ASSUMED)
- AC8. The two accounts the posting moves between are as ruled. (HELD: Q12, `PendingQ91`)

## R2-09. Owner sees every money act waiting for him (3 pts)

Screen S5. Depends on Q3.
- AC1. Given a money act the Owner must approve, then it appears on one list with the project, amount and preparer.
  [spec §9 line 371] [exists for extracts: `ExtractWaitingOnView.cs`]
- AC2. Given the Owner prepared an act, then he cannot approve it. [spec §9 line 376]
- AC3. Given a rejection, then a reason is required and the act returns to its preparer. [CLAUDE.md]
- AC4. Given an act is approved, then the preparer is told. [D-235 "not yet ruled: who is told"] (HELD: Q3)

## R2-16. A subcontract on a project (5 pts, no backlog row)

Depends on Q2. Without it, KAFF-510 has nothing to certify against.
- AC1. Given a project and a subcontractor, when the Technical Office records the agreed lines, then they belong to
  that project's sub-BOQ and not to the subcontractor's profile. [D-139 §4]
- AC2. Given a subcontractor, then he has no login and appears as a record only. [spec §9 line 367]
- AC3. Given the project account set, then a payable and a retention account exist for that subcontractor on that
  project. [`AccountTypeMetadata.cs` lines 76 and 82; neither is in `ProjectAccountSetFactory`]
- AC4. The contents of a subcontract line (unit, quantity, rate, any advance) are as ruled. (HELD: Q2)

## KAFF-510a. Prepare and certify a subcontractor extract (5 pts)

Screens S10, S11. Depends on Q2.
- AC1. Given one subcontractor on one project, then at most one extract per week. [spec §5.1 line 171]
- AC2. Given certified quantities, then the extract shows certified value this week. [spec §7 line 346]
  (HELD on who certifies, Q2)
- AC3. Given a stopped project, then a subcontractor extract can still be prepared. [spec §13 lines 554-557]
  (HELD: not confirmed for sub extracts; the amendment names payments, Q2)
- AC4. Given a user not assigned to the project, then refused. [spec §9 line 365]

## KAFF-510b. Technical Office gate and Accounts deductions (5 pts)

Screen S11.
- AC1. Given a certified extract, when the Technical Office approves quantities, then it moves to Accounts. It
  cannot touch money. [spec §7 line 346] [spec §9 line 374] [`Permission.QuantityGateApprove`]
- AC2. Given Accounts, then 5% of the extract is retained for the subcontractor and shown. [spec §5.1 line 171]
  (HELD on base of the 5%, and zeroing: Q2, Q11, `SubcontractorRetentionTiming` ASSUMED)
- AC3. Given a rejection at any gate, then it returns to Draft with a written reason. [spec §7 line 342]
- AC4. Given the same user prepared and gated, then refused. [spec §9 line 376]

## KAFF-510c. Owner approval, posting to the sub-ledger, payment (5 pts)

Screen S11. Depends on Q3.
- AC1. Given Accounts has applied deductions, when the Owner approves, then the extract posts to the
  subcontractor's sub-ledger. [spec §7 line 346]
- AC2. Given the posting, then payable is credited, and 5% moves from payable to the retention account as a
  `SubcontractorRetentionAccrual`. [`PostingTypes.cs` line 94] [`PostingAccountLegality.cs` retention]
- AC3. Given the retention accrued, then it is released only after the warranty ends. [`SubcontractorRetention.cs`
  line 44] [spec §5.1 line 171]
- AC4. Given an approved extract, when Finance pays, then a `SubcontractorPayment` posting is written. If the safe
  is short, S7 opens. [`PostingTypes.cs` line 58] [spec §6.1 line 218]
- AC5. The Owner approves every subcontractor extract. [spec §7 line 346] (HELD: Q3, no stated threshold)

## KAFF-318. Withholding on subcontractor and supplier payments (5 pts)

Screen S11, S12. Depends on Q4.
- AC1. Given a rate of 1%, 3% or 5%, then it applies to the amount before VAT. [spec §6.7 line 284]
  [`Withholding.cs` line 32]
- AC2. Given a payment, then Kaff holds the withheld amount back and carries a liability to remit (a
  `TaxWithholdingRetained` posting). [spec §6.7 line 287] [`PostingTypes.cs` line 88]
- AC3. Given the rate, then it comes from the job, not from the party's own record. [D-139 §5]
- AC4. Given the withholding, then it never nets against another of the five ledgers. [spec §6.4 line 246]
- AC5. Given an individual payee, then no withholding applies. [spec §6.7 line 288, for clients; the line does not
  say it applies to payees] (HELD: Q4)
- AC6. The base of the percentage for a payment is as ruled. (HELD: Q4; `WithholdingBaseRule` is ASSUMED for client
  extracts only)
- AC7. A supplier payment has no project to carry the category. (HELD: Q4)

## KAFF-309r. تشوينات outstanding and reconciliation (3 pts, residual of backlog KAFF-309)

Screen S13. The issue and recovery are built in the extract flow.
- AC1. Given the QC step, then the supervisor sees the outstanding تشوينات. [D-235 item 4] [D-218 item 6]
- AC2. Given a recovery, then it never exceeds what is outstanding. [spec §15 line 612]
  [exists: `MaterialAdvanceRecoveryRule.cs` line 43]
- AC3. Given a project, then تشوينات in equals تشوينات recovered when its extracts are all issued, and a read shows
  any difference. [spec §15 line 612] [spec §6.4 amendment lines 264-265]
- AC4. The recovery amount per extract is as ruled. (HELD: Q58, `MaterialAdvanceRecoveryRule` ASSUMED)
- AC5. Marking material as stockpiled from the daily log is as ruled. (HELD: Q8, D-225 item 4)

## KAFF-612r. The weekly QC report meets its criteria (2 pts, residual of backlog KAFF-612)

No new screen. The step is built in the extract chain.
- AC1. Given a draft extract, when the supervising engineer submits the report, then it moves to QC. [spec §7
  lines 332-334] [exists: `SubmitQcReport`]
- AC2. Given the junior, then refused. [`PermissionCatalogue.cs` line 561]
- AC3. Given any other role, then refused: the holder is the supervising engineer. (HELD: `ExtractQcReportAuthor`
  ASSUMED, D-189 §3)
- AC4. Whether this report is also a step of the subcontractor chain is as ruled. (HELD: Q10)

---

## Totals

16 stories, 73 points: 3 + 3 + 8 + 5 + 3 + 5 + 8 + 5 + 3 + 5 + 5 + 5 + 5 + 5 + 3 + 2.
(The two `KAFF-609` halves, three `KAFF-510` parts, `R2-09`, `R2-16`, and the two residuals.)
Backlog rows for the same work: 57 points (KAFF-310, 311, 312, 313, 318, 510, 608, 609, 610, 612) plus the
residual of KAFF-309 (8 in the backlog, 3 here since the extract flow built the rest). The difference is
`R2-09`, `R2-16`, and screen work the backlog did not price (S5, S9, S12).
