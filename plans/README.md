# Rest-of-project roadmap

Date: 2026-11-22. Sources: `release-2..6/stories.md` and `questions.md` (draft, not yet Ready), `STATUS.md` "Points by slice", and the staged-release plan (Release 0 and 1 stabilise, then 2 to 6). Points are the figures the plans state. Every sum marked **(computed)** is arithmetic done for this page, not a figure from a plan. "Start now" means the story can begin on a recorded default (a `RulingNote`, per the "build on recorded assumptions" ruling); "Hold" means at least one AC waits on a question, listed by id. Question ids are per release, for example R2-Q3 is Release 2 question 3.

## Totals

| Release | Stories | Points | Start now | Hold |
|---|---:|---:|---:|---:|
| R2 site money | 16 | 73 | 11 | 62 |
| R3 sales spine | 11 | 62 | 22 | 40 |
| R4 accounting | 13 | 93 | 41 | 52 |
| R5 portal and design | 10 | 56 | 29 | 27 |
| R6 mobile and offline | 11 | 48 | 11 | 37 |
| **New work, R2 to R6 (computed)** | **61** | **332** | **114** | **218** |

Start-now and Hold columns are computed from the tables below.

## Release 1 leftovers (from STATUS.md)

| Item | Points | State | Work |
|---|---:|---|---|
| KAFF-802 hold release | 8 | REJECTED | Add the approval step. Rebuild, re-verify |
| KAFF-803 snag resolution | 5 | REJECTED | Add the debit-note posting. Rebuild, re-verify |
| KAFF-115 project team tab | 8 | REJECTED | Build from assignment rows. The working tree already has uncommitted edits to the tab and to `Domain/Closure` |
| KAFF-215 sidebar | 5 | BUILT (2026-11-22) | Verify only |
| Design-system stories | 106 | BUILT, no Verifier | Verify only, grouped by screen |

Rework to build (802 + 803 + 115, computed): 21 points. Verification-only points (215 + design system, computed): 111. Other BUILT slice 2 (9) and slice 8 (5) points and 5 NOT-BUILT slice 2 points are on the board but outside this list.

## Release 2: site money (73 pts, 16 stories)

| Story | Pts | Status | Waits on |
|---|---:|---|---|
| KAFF-609a junior drafts a عهدة | 3 | Hold | R2-Q6 |
| KAFF-609b supervisor submits or returns | 3 | Start now | none |
| KAFF-311 Finance pays a عهدة | 8 | Hold | R2-Q3, Q6 |
| KAFF-610 one open at a time, settle | 5 | Hold | R2-Q1, Q6 |
| KAFF-313 safe refusal prompts injection | 3 | Start now (server refusal exists) | R2-Q7 for who may inject |
| KAFF-312 owner current account | 5 | Hold | R2-Q7 |
| KAFF-310 firm advance and cap | 8 | Hold | R2-Q5 |
| KAFF-608 Accounts confirms expense draft | 5 | Hold | R2-Q1, Q3, Q9, Q12 (PendingQ91) |
| R2-09 Owner waiting list | 3 | Hold | R2-Q3 |
| R2-16 a subcontract on a project | 5 | Hold | R2-Q2 |
| KAFF-510a prepare and certify sub extract | 5 | Hold | R2-Q2 |
| KAFF-510b Technical Office gate, deductions | 5 | Hold | R2-Q2, Q11 |
| KAFF-510c Owner approval, post, pay | 5 | Hold | R2-Q3 |
| KAFF-318 withholding on sub and supplier | 5 | Hold | R2-Q4 |
| KAFF-309r تشوينات outstanding | 3 | Start now (core ACs) | R2-Q58, Q8 for two ACs |
| KAFF-612r weekly QC report | 2 | Start now | R2-Q10 for one AC |

Start now: 3 + 3 + 3 + 2 = 11 (computed). The stories file says to build "R2-05 and R2-06" first, but no such ids exist in it. The safe flows are KAFF-313 and KAFF-312, so read it that way.

## Release 3: sales spine (62 pts, 11 stories)

| Story | Pts | Status | Waits on |
|---|---:|---|---|
| R3-01 open and list opportunities | 5 | Start now | none |
| R3-02 stages, hold, stage rules | 3 | Start now on current code | R3-Q3 for skip and return rules |
| R3-03 inactivity alerts, Stalled | 3 | Start now (on-screen list) | R3-Q5 for channel |
| R3-04 close lost, reopen | 3 | Start now | R3-Q10 for reason list |
| R3-05 site-visit deposit | 8 | Hold | R3-Q1, Q2 |
| R3-06 pre-contract expenses | 5 | Hold | R3-Q4 |
| R3-07 estimate and margin view | 8 | Hold | R3-Q6, Q7 |
| R3-08 print quotation | 5 | Hold | R3-Q7 |
| R3-09 convert to project | 8 | Hold | R3-Q1, Q7, Q11 |
| R3-10 screens: pipeline, new, opportunity | 8 | Start now | none |
| R3-11 screens: deposit, expenses, quotation, convert | 6 | Hold | after R3-05 to R3-09 |

Start now: 5 + 3 + 3 + 3 + 8 = 22 (computed). The stories file says R3-01 to R3-04 can start now while three of those titles say "waits"; each question has a default, so this table follows the defaults.

## Release 4: accounting (93 pts, 13 stories; 88 without loans)

| Story | Pts | Status | Waits on |
|---|---:|---|---|
| KAFF-700 asset register, depreciation | 13 | Start now (depreciation); the purchase posting hits R4-A2 | R4-Q7 (default straight-line) |
| KAFF-701 accruals and prepayments | 8 | Start now | none |
| KAFF-702 month-end close | 8 | Hold | R4-A1, Q3 |
| KAFF-703 who performs the close | 2 | Hold | R4-Q3 |
| KAFF-704 trial balance and export | 5 | Start now | R4-Q10 for format (default CSV) |
| KAFF-705 equity, opening balances, year close | 8 | Hold | R4-Q2, Q4, A1 |
| KAFF-706 revenue recognition | 13 | Hold | R4-Q1, Q12 |
| KAFF-707 expense tagging | 5 | Start now | R4-Q11, A3 |
| KAFF-708 overhead spread report | 5 | Start now | R4-Q9 (low cost) |
| KAFF-709 budget baseline | 5 | Start now (10% tolerance is a placeholder) | R4-Q6 |
| KAFF-710 budget alerts on committed money | 8 | Hold | R4-Q6; "committed" has no source until R2 builds sub extracts |
| KAFF-711 loans | 5 | Hold | R4-Q8 (dropped if no) |
| KAFF-712 balance sheet, the gate | 8 | Hold | R4-A2 (nine pending account pairs) |

Start now: 13 + 8 + 5 + 5 + 5 + 5 = 41 (computed). The backlog priced KAFF-700 to 711 at 61; the draft is 93 because screens and KAFF-712 are counted.

## Release 5: portal and design (56 pts, 10 stories)

| Story | Pts | Status | Waits on |
|---|---:|---|---|
| R5-A client account and sign-in | 3 | Start now | R5-Q7 (default: Owner hands over password) |
| KAFF-810 portal surface | 8 | Start now on default | R5-Q1 deployment (Nabil and Architect) |
| KAFF-811 reflection test, the gate | 5 | Start now | none |
| R5-B portal reads | 8 | Start now on defaults | R5-Q8, Q9 |
| R5-C publish a photo | 3 | Start now | R5-Q12 |
| KAFF-812 client approves, change order half | 2 | Start now | R5-Q3 |
| KAFF-812 client approves, design half | 3 | Hold | R5-D, R5-Q2 |
| R5-D design phase module | 13 | Hold | R5-Q2, Q4, Q5 |
| KAFF-813 watermark until paid | 3 | Hold | R5-Q6, file store from R5-D |
| KAFF-808 design closure | 5 | Hold | R5-Q10 |
| KAFF-809 furnishing child project | 3 | Hold | R5-Q11 |

Start now: 3 + 8 + 5 + 8 + 3 + 2 = 29 (computed). The release has 10 stories because KAFF-812 is one story in two halves. Not scoped here: KAFF-814 closure marketing assets and referral (3 pts; spec section 11 requires it). Add it or rule it out.

## Release 6: mobile and offline (48 pts, 11 stories)

| Story | Pts | Status | Waits on |
|---|---:|---|---|
| S9-1 local store and queue | 5 | Hold | R6-Q1 |
| S9-2 send queue in order, safe to repeat | 5 | Hold | R6-Q2, Q3 |
| S9-3 offline daily log entries | 5 | Hold | R6-Q2 |
| S9-4 check-in and out offline | 3 | Hold | R6-Q4 |
| S9-5 photos offline | 5 | Start now | none |
| S9-6 gate: offline cannot move money | 3 | Start now (proof, last in order) | none |
| S9-7 expense and عهدة draft offline | 10 | Hold (the expense half, 5, can follow S9-1) | R6-Q5, Q6; R2 KAFF-609 and 610 for the عهدة half |
| S9-8 sending list screen | 3 | Start now | none |
| S9-9 late-arrival day rule | 3 | Hold | R6-Q2, Q3 |
| S9-10 app shell and install | 3 | Hold | R6-Q1 |
| S9-11 session end with unsent work | 3 | Hold | R6-Q8 |

Start now: 5 + 3 + 3 = 11 (computed). Backlog said 47; the re-cut is 48.

## Cross-release dependencies

1. **R2 builds the sources R4 needs.** R4 budget alerts (KAFF-710) count "committed" money. Nothing in `src/Domain` models a purchase order or supplier invoice today; approved subcontractor extracts (R2 KAFF-510c) are the only source R2 adds. KAFF-710 cannot finish before R2, and Karim must say what else counts (R4-Q6).
2. **Account pairs (PendingQ91) hit R2 and R4.** R2 KAFF-608 (`SiteExpensePayment`) and R4 KAFF-712, 705, 700 (`OpeningBalance`, `AssetPurchase`, `PeriodCloseTransfer`, `YearEndProfitTransfer`, `LoanDrawdown`, cheque types) stop at the same file, `src/Domain/Treasury/PostingAccountLegality.cs`. R2-Q12 and R4-A2 are one question. One ruling from Nabil and the Architect unblocks both; do it before R2 starts.
3. **R2 safe flows come before every other R2 payment.** KAFF-312 and 313 first, because عهدة, firm advance, subcontractor payments and withholding all pay through the safe.
4. **R6 عهدة offline needs R2 عهدة online.** S9-7 (عهدة half) waits on KAFF-609 and 610. The expense-draft half does not. R6 invoice photos (R6-Q6) also need the answer to R2-Q1.
5. **R5 design closure needs the R5 design module.** KAFF-812 design half, 813 and 808 wait on R5-D, a 13-point module with no backlog story; refine it first.
6. **R3 and R5 share the "who creates a project" rule.** R3-Q11 (who converts) and R5-Q11 (who creates furnishing) both lean on D-052. Answer once.
7. **R5 client change-order approval (R5-Q3) retires the engineer's record-approval endpoint from Release 1.** KAFF-811, the reflection test, must exist before any portal read ships.
8. **R3-09 credits a deposit to a ledger that is not ruled** (R3-Q1). R4's trial balance must show the same account.
9. **R3-Q7 can retire part of Release 1 KAFF-417** (quotation as an unsigned-project BOQ). Answer before R3 starts.
10. **R4-A1 (profit into `CurrentYearProfit`) shapes KAFF-702 and 705.** If chosen wrongly it cannot be edited out, because postings are append-only.
11. **Photo publishing is one question across R5 and R6** (R5-Q12 and R6-Q9).

## Recommended build order

0. **Finish Release 1 and clear its leftovers.** Rework 802, 803, 115 (21 pts, computed); verify 215 and the design system (111 pts, computed). Reason: Release 2 pays through treasury and the extract chain that Release 1 verifies, and the staged-release rule is one accepted release before the next opens.
1. **Rule the account pairs (PendingQ91) and R4-A1 before R2 and R4 start.** Reason: two releases stop at it, and a wrong pair means reversing postings.
2. **Release 2, site money, safe first.** Order: KAFF-313, 312, then 609b, 609a, 311, 610, 310, 608, then R2-16 and 510a to c, 318, the residuals. Reason: every flow pays through the safe, and subcontractor extracts feed R4's budget alerts.
3. **Release 3, sales spine.** Reason: it needs nothing from R2 except the deposit account, 22 of its 62 points start now, and it can run beside R2 screens if lanes allow. Answer R3-Q1 and Q7 first.
4. **Release 4, accounting.** Start 700, 701, 704, 707 to 709 after step 1. Finish 702, 705, 706, 710, 712 after R2 and the Karim answers. Reason: the balance-sheet gate depends on every earlier posting type.
5. **Release 5, portal and design.** Build 810, 811 and R5-A first (the gate), then reads, then the design module, then closure. Reason: portal reads need real extracts and change orders from Releases 1 and 2, and the design module is the largest unknown.
6. **Release 6, mobile and offline.** Reason: its gate ("offline cannot move money") proves every money surface built before it. S9-5 and S9-8 hold no money and can start early.

The plan reviews the order of Releases 2 to 6 with Nabil at each release boundary. This order follows the dependencies above; it is a recommendation.

## Points remaining

| Bucket | Points |
|---|---:|
| New work, R2 to R6 (computed) | 332 |
| Release 1 rework: KAFF-802, 803, 115 (computed) | 21 |
| **Build points remaining (computed)** | **353** |
| Verification only: KAFF-215 and design system (computed; already built) | 111 |
| Not scoped: KAFF-814 closure marketing assets | 3 |

The 332 uses the drafts' own points, which are estimates. R4 includes 5 points for loans that drop out if R4-Q8 is no (327 then, computed). No new estimates were added. The R2 draft is 73 against 57 in the backlog (plus 8 for KAFF-309); the R4 draft is 93 against 61. Slice 2 BUILT and NOT-BUILT points (14) and the deferred KAFF-104 and 129 are not counted.
