# Verification, round 2 — KAFF-303, KAFF-316 (collections), KAFF-317, KAFF-319 — 2026-11-18

**Verifier:** fresh session, did not write the code. **Model:** Opus 5.5 (claude-opus-5-5), the strongest model; not downgraded.
**HEAD:** `33ff701` (working tree clean at start). Re-verifies the rejections in `qa/slice-3/verification-2026-11-18.md`
against D-235, D-236, D-237 and D-238.

Findings are written as they are reached.

## Method

The dev API on http://localhost:5080 was left running. `/api/health` at start:
`leastPrivilege: true, excessPrivileges: []`. No test suite was run in full.

A fresh project was created for this pass so the demo project's extracts stay as the UAT manual expects:
`PRJ-CE500CBD` (`01a1f08e-3f29-78e6-b185-d5d8c1187fd8`), corporate client C-10011, Lump Sum, hold 20%, advance 0,
recovery 25%. Its Arabic name was mangled to question marks by the shell's encoding (cosmetic, on the dev database).
Assigned: `tech_office_demo`, `site_sup_demo` (Supervisor), `site_eng_demo` (Junior); both Finance users are assigned
automatically. Every step below is a real HTTP request with a real sign-in cookie. Balances were read with a
read-only `SELECT` as `kaff_app` summing `postings` per account (to minus from), because no API lists a project's
accounts (F-4 of round 1).

## Setting up the extract

- `PUT /api/projects/{id}/withholding` as Technical Office: **403** `auth.forbidden`. As Finance
  `{"category":"ContractingAndSupplies"}`: **200**. As Finance `{}`: **400** `project.withholding_category_required`.
- Sign, one daily-log quantity (500 m2 of MAS-3004), extract 1 prepared, one honest Technical Office rejection to
  refresh the evidence, then QC (supervisor), technical approval, deductions (Finance), owner approval and issue
  (Owner). Extract 1 `01a1f08e-fbd9-7573-890c-a385a6ccf6d4` issued: work value 205,275.0000, hold 41,055.0000,
  net payable 164,220.0000, expected withheld 1,642.2000 (1% of net payable, the default base).

## The chain, step by step

Balances are the project's receivable and hold, and the company Safe (`SAFE-MAIN`) and `TAX-WHT-RECEIVABLE`, which
read 0 before the chain.

| # | Step | Response | Extract | Safe | Receivable | Tax withheld | Hold |
|---|---|---|---|---|---|---|---|
| 0 | Issued | 200 | Issued | 0 | 164,220.0000 | 0 | 41,055.0000 |
| 1 | Cheque A received, 100,000, `CHQ-A-0001` | 200 | Issued, inCheques 100,000, remaining 64,220 | 0 | 164,220.0000 | 0 | 41,055.0000 |
| 2 | Clear cheque A while Received | 409 `cheque.illegal_transition` | unchanged | 0 | 164,220.0000 | 0 | 41,055.0000 |
| 3 | Bounce cheque A while Received | 409 `cheque.illegal_transition` | unchanged | | | | |
| 4 | Deposit cheque A | 200 `Deposited` | Issued | 0 | 164,220.0000 | 0 | 41,055.0000 |
| 5 | Bounce as the junior site engineer | 403 `auth.forbidden` | | | | | |
| 6 | Bounce with no reason | 400 `extract.reason_required` | | | | | |
| 7 | Bounce with reason "Insufficient funds (bank memo 7781)" | 200 `Bounced` | Issued; gate `ChequeBounce` / `Reversed` with the reason stored; inCheques 0, remaining 164,220 | 0 | 164,220.0000 | 0 | 41,055.0000 |
| 8 | Clear the bounced cheque | 409 `cheque.illegal_transition` | unchanged | 0 | | | |
| 9 | Cheque B received, 100,000, `CHQ-B-0002` | 200 | Issued, inCheques 100,000, remaining 64,220 | 0 | 164,220.0000 | 0 | 41,055.0000 |
| 10 | Deposit cheque B | 200 `Deposited` | Issued | 0 | 164,220.0000 | 0 | 41,055.0000 |
| 11 | Clear cheque B | 200 `Cleared` | Issued (part paid), collected 100,000, remaining 64,220 | 100,000.0000 | 64,220.0000 | 0 | 41,055.0000 |
| 12 | Transfer of 64,220.0001 | 409 `collection.exceeds_expected` | unchanged | | | | |
| 13 | Transfer with `withheldAtSource` -1 | 400 `collection.amount_invalid` | | | | | |
| 14 | Transfer with no `cashReceived` | 400 `collection.amount_required` | | | | | |
| 15 | Transfer 50,000 with 1,642.20 withheld, `TRF-0003` | 200 | Issued, collected 151,642.20, remaining 12,577.80 | 150,000.0000 | 12,577.8000 | 1,642.2000 | 41,055.0000 |
| 16 | Raise dispute as Technical Office | 403 | | | | | |
| 17 | Raise dispute as Finance | 200 | Disputed | | | | |
| 18 | Resolve as credit note 2,577.80, Finance (Owner gets 403) | 200 | Issued; adjustment 1 `Raised` | | | | |
| 19 | Collect while the credit note is Raised | 409 `extract.credit_note_open` | | | | | |
| 20 | Approve the credit note as Finance / as Owner | 403 / 200 `Approved` | Issued, expected 161,642.20, remaining 10,000 | 150,000.0000 | 10,000.0000 | 1,642.2000 | 41,055.0000 |
| 21 | Transfer of 10,000.01 | 409 `collection.exceeds_expected` | | | | | |
| 22 | Cash 10,000, `RCPT-0004` | 200 | **Collected**, remaining 0 | 160,000.0000 | 0.0000 | 1,642.2000 | 41,055.0000 |
| 23 | Cash 1 more | 409 `extract.illegal_transition` | Collected | | | | |

Reconciliation at step 22: 160,000.00 cash + 1,642.20 withheld + 2,577.80 credit note = 164,220.00 net payable. The hold
read 41,055.0000 at every step and the client advance read 0 throughout. The postings written, in order:
`RevenueRecognition` 205,275 and `HoldAccrual` 41,055 at issue; `ClientCollection` 100,000 at clearance (none at
receipt, deposit or bounce); `TaxWithheldAtSource` 1,642.20 and `ClientCollection` 50,000 for the transfer;
`CreditNote` 2,577.80 (source `Adjustment`); `ClientCollection` 10,000. Every collection leg carries source type
`Extract`.

### After the chain

| # | Step | Response |
|---|---|---|
| 24 | Generic reversal of the extract's `HoldAccrual` | **409** `treasury.hold_posting_not_reversible` |
| 25 | Generic reversal of a `ClientCollection` leg | **409** `treasury.flow_posting_not_reversible` |
| 26 | Generic reversal of `RevenueRecognition`, of `TaxWithheldAtSource`, of the `CreditNote` | **409** `treasury.flow_posting_not_reversible`, each |
| 27 | Generic reversal with `{}` | **400** `treasury.posting_date_required` |
| 28 | Generic reversal dated 2031-01-01, 1999-12-31, 2026-11-20 | **400** `treasury.posting_date_out_of_range`, each |
| 29 | `PostMovement` naming source type `Extract`, `Adjustment`, `ProjectHandover`, `SubcontractorRetentionRelease` (real extract id, real accounts) | **400** `treasury.flow_source_not_allowed`, each |
| 30 | Collection reversal with no reason / as Owner | 400 `extract.reason_required` / 403 |
| 31 | Collection reversal with a reason, Finance | 200; extract `Issued`; four mirror postings (100,000, 50,000, 10,000 cash, 1,642.20 withheld), each with `reverses_id`; Safe 0, receivable 161,642.20, tax 0, hold 41,055; the cleared cheque B still reads `Cleared` |
| 32 | Collect 160,000 + 1,642.20 withheld by transfer | 200, `Collected` |
| 33 | Practical completion (Owner), handover (Owner) | 200 `HandoverPending`, 200 `Handover` |
| 34 | `PostMovement` `HoldRelease` of 15,000 (partial) at Handover, source `Payment` and `OpeningBalance` | **409** `treasury.hold_release_in_full` with `messageKey` `errors.treasury.hold_release_in_full`, twice. Hold unchanged. This is round 1's F-2, now translated |
| 35 | `PostMovement` `HoldRelease` of 41,055 (full) at Handover, source `Payment` | **201**. Hold 0, receivable 41,055 |
| 36 | `POST /hold-release` afterwards, Finance / Owner | 409 `closure.hold_already_released` / 403 |
| 37 | Generic reversal of the generic hold release | **409** `treasury.hold_posting_not_reversible` |

Every error key met above exists in both `src/Web/public/locales/ar.json` and `en.json`.

## Observations from the chain

- **O-r2-1. The hold can be released by a generic movement instead of the release flow (step 35).** `PostMovement`
  refuses the flow-owned source *types* (D-238 item 1) but not the flow-owned posting *types*. A `HoldRelease` under
  source `Payment` released the whole hold at Handover; the release flow then answered "already released". The money
  rule holds (once, in full, at handover, and it cannot be reversed), so this is not scored against these stories.
  But the release is no longer tied to the handover document, and the same shape lets a `ClientCollection` posting
  under a non-flow source move money out of an extract's receivable without the extract's collection position
  seeing it (`ExtractMoneyQueries.CollectedAsync` counts only source type `Extract`). Not run live for the
  collection case.
- **O-r2-2. A cleared cheque's money is reversed with the rest by a collection reversal and the cheque still reads
  `Cleared` (step 31).** D-238 lists it as open for Nabil.
- **O-r2-3. The same Finance user raised the dispute and resolved it as a credit note (steps 17, 18).** The Owner
  approved the note, so no movement was created and approved by one person. KAFF-511, outside this pass.

### More live checks (resumed 2026-11-19 after a usage limit)

| # | Step | Response |
|---|---|---|
| 38 | `PostMovement` `ClientCollection` 1,000 from the project receivable to the Safe, source `Payment` | **201**. This is O-r2-1's collection case: money left the receivable and no extract or collection record moved |
| 39 | Generic reversal of it as the junior site engineer | 403 `auth.forbidden` |
| 40 | Generic reversal of it through the company route | 404 `treasury.reversal_target_not_found` |
| 41 | Generic reversal of it, project route, Finance | **201**, a mirror: Safe to receivable, 1,000.0000, same type, `reversesId` set |
| 42 | The same reversal again | 409 `treasury.posting_already_reversed` |
| 43 | Reversal of the reversal | 409 `treasury.reversal_of_reversal` |
| 44 | `GET /api/projects/{id}/cheques` as the Owner | 403 (D-239 item 4 rules that the Owner may read it; ruled, not built) |

State left behind on the dev database: project `PRJ-CE500CBD` in `Handover`, extract 1 `Collected`, hold released (0),
receivable 41,055 (the released hold, unpaid; no route collects it), Safe +160,000, tax withheld +1,642.20.

## Mutations (each reverted with `git checkout -- <file>`, then rebuilt)

Tests ran in Debug with `--filter-class` only. The API runs from the Release build and was not touched.
Baseline before mutating: Domain `PostingRuleTests` + `CollectionPositionTests` 36/36. Api `ExtractEndpointTests`
(which includes the partial class in `ExtractCollectionTests.cs`), `ReversePostingTests`, `TreasuryGuardTests` and
`DatabaseGuardTranslationTests` all green.

| # | Mutation | Result |
|---|---|---|
| M1 | `Posting.Reverse`: hold-ledger refusal made unreachable at run time | **red**, 3 `PostingRuleTests` (hold accrual, hold release, hold rule reported first) |
| M2 | `001_guards.sql` line 124: `KAFF_HOLD_REVERSAL` block disabled | **red**, `TreasuryGuardTests.A_reversal_of_a_hold_accrual_is_refused_by_the_database` and `..._hold_release_...` |
| M3 | `Posting.ReverseMovement`: flow-owned refusal disabled | **red**, 4 cases of `PostingRuleTests.A_posting_a_flow_wrote_cannot_be_reversed_as_a_movement` |
| M4 | `006_flow_reversal.sql`: `KAFF_FLOW_REVERSAL` never raised | **red**, `ExtractEndpointTests.The_database_refuses_a_reversal_of_a_flow_posting_that_its_flow_did_not_make` |
| M5 | `DatabaseGuardTranslation.Map`: `KAFF_HOLD_PARTIAL_RELEASE` entry deleted | **red**, `DatabaseGuardTranslationTests.Every_named_posting_guard_prefix_..._has_a_mapped_treasury_error` |
| M6 | `Extract.PostReceipt`: any payment completes the collection | **red**, 5 `ExtractEndpointTests` (part payment, collections chain, credit note cap, cheque list, read actions) |

`git status --short src tests` is empty after the last revert, and both test projects were rebuilt from clean source.

## Rulings that arrived during this pass (D-239, D-240): ruled, not built

These are not scored against the stories unless a criterion requires them.

- D-239 item 2: no cap on a credit note, and `adjustment.exceeds_extract_net` is to be removed. The guard is still in
  the code (M6 turned `A_credit_note_cannot_exceed_what_is_still_unpaid` red, so a test exists for a rule now withdrawn).
- D-239 item 3: a credit note on a Collected extract cannot be reversed. Not checked live.
- D-239 item 4: the Owner reads the cheque list. Live: 403 (step 44).
- D-240 item 3: no future posting dates. Live on 2026-11-18, a reversal dated 2026-11-20 was refused, but the code
  still allows the site day plus one (`TypedPostingDate`, D-236 item 2).

## Verdicts per story

### KAFF-303 — correct a mistake with a reversing posting — 5 MET, 1 NOT MET as written (D) — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence |
|---|---|---|
| A mirror, accounts swapped, `ReversesId` | MET | live step 41; `ReversePostingTests` |
| B second reversal refused | MET | live step 42, `posting_already_reversed` |
| C reversal of a reversal refused | MET | live step 43, `reversal_of_reversal` |
| D the hold accepts a reversal that debits it | **NOT MET as written; superseded** | live steps 24 and 37: 409 `hold_posting_not_reversible`. The criterion asks for the opposite of `spec.md` §5.1 ("Nothing may be taken out of it mid-project ... It releases once, in full, at handover") and of `CLAUDE.md`. Spec wins, so the behaviour is right and the criterion is wrong. D-236 item 1 made the change. The test is now `AC_303_D_A_reversal_that_would_debit_the_hold_is_refused_and_writes_nothing`, and M1 and M2 prove the rule's tests can fail |
| E closed period refused | MET | test `AC_303_E_...`, green in the baseline |
| F role without assignment | MET | live steps 39 and 40; round 1 showed 403 for unassigned Finance |

Round 1's money defect (F-5) is closed. No hold posting and no flow-owned posting can be reversed generically, in the
domain or in the database (M1 to M4). F-7 (missing date) is closed (step 27).

**Condition:** AC-303-D must be rewritten to match §5.1.

**Question for Nabil/Karim:** a hold accrual posted in error by hand (a generic `HoldAccrual` under a non-flow source)
now cannot be corrected at all. It stays in the hold until handover and is then released to the client receivable.
Spec does not say how such a mistake is corrected.

### KAFF-316 — collections and cheque states — all ten MET under D-235 — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence |
|---|---|---|
| A method, date, reference (and now amount) required | MET | live step 14, `amount_required`; test `A_collection_missing_method_date_or_reference_is_refused_and_nothing_changes` |
| B role and assignment | MET | live: junior engineer 403 on bounce (step 5), Owner 403 on collection reversal (step 30); test `Collect_is_for_finance_alone_and_needs_an_assignment_to_the_project` |
| C cheque transitions | MET | live steps 2, 3, 4, 7, 8, 10 and 11 |
| D the extract becomes settled | MET (D-235 item 2) | live: part payments left it Issued, and it became Collected exactly when received plus withheld reached the expected amount (step 22); M6 red. Spec §13 calls the end state `Paid` and the code calls it `Collected`, as round 1 accepted |
| E shortfall, no balancing entry | MET (D-235 item 2) | live steps 11 and 15: what arrived is posted, and nothing balances it. Overpayment is refused (`exceeds_expected`). D-239 item 2 says overpayment is not yet ruled, so that refusal is the lane's choice |
| F mistaken collection reversed, never edited | MET | live step 31: four mirror postings with `reverses_id`, the originals untouched |
| G bounced cheque | MET (D-235 item 1) | live step 7: no posting, extract stays Issued, reason stored on gate `ChequeBounce`; a bounce with no reason is refused (step 6) |
| H cheque postings | MET (D-235 item 1) | live: no posting at receipt, deposit or bounce; a `ClientCollection` from receivable to Safe at clearance (step 11) |
| I the project-state policy decides | MET | `EnsureMayRecordCollection` asks `RulingSet.PostingProjectStates`; tests as in round 1 |
| J the ledgers do not net | MET | client advance and hold unchanged through every step. The client advance was 0 on this project; round 1 showed it unchanged at 175,000 |

Conditions:
- O-2 of round 1: collections always go to the Safe, never a bank, against §6.5 "default to bank".
- O-r2-1: a generic `ClientCollection` or `HoldRelease` under a non-flow source bypasses the flow (steps 35 and 38).
- O-r2-2: a cleared cheque still reads `Cleared` after its money is reversed (open in D-239).

### KAFF-317 — withholding at source — 6 MET, 1 NOT MET (E), 1 UNTESTED (I), 2 HELD (C, G) — recommend `BUILT verdict=REJECTED`

| AC | Verdict | Evidence |
|---|---|---|
| A corporate client withholds, cash reconciles | MET | live: Finance set `ContractingAndSupplies` through `PUT /withholding` (Technical Office got 403). Step 15 posted 1,642.20 to `TAX-WHT-RECEIVABLE` and 50,000 cash. Step 32 posted 160,000 + 1,642.20 = 161,642.20, the expected amount. Round 1's "unreachable by request" is closed |
| B individual withholds nothing | MET | tests `An_individual_clients_contract_cannot_carry_a_withholding_category` and `A_withheld_amount_on_a_contract_that_withholds_nothing_is_refused` |
| C different amount than the rate | HELD | Q99, still open (D-239) |
| D the base follows the ruling | MET | test theory `The_withheld_amount_follows_the_ruled_base_with_no_handler_change` |
| E hold release withholds only if the ruling says so | **NOT MET** | `HoldReleaseCarriesWithholding` is still read by no production code (`src/Domain/Rulings/WithholdingBase.cs:22-24`; the only other readers are `RulingsTests.cs:143,155` and one test fixture). After the hold release (step 35) the receivable holds 41,055, and no route collects it, with or without withholding. Unchanged from round 1 |
| F no category, no withholding | MET | test `A_corporate_contract_with_no_category_withholds_nothing` |
| G rate changes after the first extract | HELD | Q30, still open |
| H two accounts, two fields | MET | accounts `TaxWithheldAtSource` and `TaxWithholdingPayable`; fields: the contract's category and the collection's `withheldAtSource` |
| I atomic commit | UNTESTED | one save by construction, but still no test makes the withholding leg breach a guard and asserts that neither leg landed |
| J role and assignment | MET | same routes as 316-B |

### KAFF-319 — a refused posting reads as a translated message — 4 of 4 MET — recommend `VERIFIED verdict=PASS`

| AC | Verdict | Evidence |
|---|---|---|
| A a guard firing without a domain refusal returns ProblemDetails, not 500 | MET | live step 34: the partial `HoldRelease` that was a bare 500 in round 1 is now 409 `treasury.hold_release_in_full` with its `messageKey`. Only the database checks this rule (`TreasuryErrors.cs:113`) |
| B every prefix mapped | MET | the test now derives the prefixes from every guard SQL file. It excludes only `KAFF_APPEND_ONLY` and `KAFF_ACCOUNT_IMMUTABLE`, which guard UPDATE paths that no request takes; M5 red |
| C same i18n mechanism | MET | every key met in this pass is in both `ar.json` and `en.json` |
| D unknown exception still 500 | MET | test `An_exception_that_names_no_guard_prefix_does_not_translate` |

## Money and permission rules, this pass

- No float near money in the code read. Amounts cross the wire as strings and post as `decimal(18,4)`.
- No stored balance: the collection position is derived on every read (`ExtractCollections.StateAsync`).
- No posting update or delete path. Every correction in this pass was a new row with `reverses_id`.
- The hold was never debited before handover. A generic reversal of a hold posting is refused in the domain and in
  the database.
- Role and assignment were checked server-side on every route probed.
- Create and approve: Finance raised the credit note and the Owner approved it. Finance was refused the approval
  (step 20).

## Defects

1. **KAFF-317 AC-317-E.** `src/Domain/Rulings/WithholdingBase.cs:22-24`: `HoldReleaseCarriesWithholding` is dead, and
   no route collects the released hold (`src/Api/Features/Closure/ReleaseHold` posts the release; nothing collects it).
2. **KAFF-317 AC-317-I.** No test proves that the collection and its withholding leg commit together.
3. **O-r2-1, not scored.** `src/Domain/Treasury/Posting.cs:141-153` (`CreateMovement`) refuses flow source types but
   not flow posting types, so `HoldRelease` and `ClientCollection` can be posted by hand around their flows.

## Not reached

- AC-303-E (closed period) was not re-run live; its test was green in the baseline.
- An unassigned Finance user was not probed live, because both Finance users are assigned to every project
  automatically.
- D-239 item 3 (reversing a credit note on a Collected extract) was not checked.
- Hold-release withholding under `HoldReleaseCarriesWithholding = false` could not be run, because nothing reads the flag.
