# Verification — slice 5 extract chain and its money (KAFF-500, 316, 317, 508, 509, 511, 512) — 2026-11-20

**Verifier:** fresh session, did not write the code. **Model:** Opus 5.5 (`claude-opus-5-5`), the strongest model; no downgrade.
**Tree:** `main` at `363d5ee`, working tree clean at start. Commits in scope `33ff701..363d5ee`.
**Sources read:** `CLAUDE.md`; `STATUS.md` top section; the seven story files; `spec.md` §5.1, §6.1–§6.9, §7, §13, §15; `decisions.md` D-238 to D-249 (by index offset).

Findings are written as they are reached. Sections below are in the order the pass reached them.

## Gates

(pending)

Order followed (`.claude/skills/run-kaff-erp/SKILL.md`): the 5080 listener and every `Kaff.Api`, `Kaff.Api.Tests`,
`Kaff.Domain.Tests` host stopped first; then build; tests only after a clean build.

| Gate | Result | Exit |
|---|---|---|
| `dotnet build KaffErp.sln -c Release` | 0 warnings, 0 errors | 0 |
| Domain.Tests | total 801, succeeded 801, failed 0, skipped 0 | 0 |
| Api.Tests (`Kaff.Api.Tests.exe`, 22m 56s) | total 1282, succeeded 1280, failed 0, skipped 2 (the two `ClosureEndpointTests` skips STATUS.md already names) | 0 |
| `ng test --watch=false` | 64 files, 591 / 591 passed | 0 |
| `ng build --configuration production` | bundle generation complete | 0 |

## Findings, in the order reached

### F-1 · KAFF-500 AC-500-E · NOT MET · the held remainder can never be released

`spec.md` §7: "the held portion releases when the NCR closes". `Extract.ReleaseHeldPortion`
(`src/Domain/Extracts/Extract.cs:415`) exists and its only callers are three domain tests
(`tests/Domain.Tests/ExtractChainTests.cs:370-381`). No route calls it, no NCR entity exists anywhere in `src/`
(`HeldForNcrId` is a bare GUID the Technical Office types), so nothing can close an NCR and nothing can release a
remainder. The story's own stage-2 build notes list `ReleaseHeldPortion` as not done, and nothing since has done it.
The first half of the criterion is met: a partial Technical Office approval over HTTP puts the extract in
`PartiallyApproved` and the approved portion proceeds (D-246 also records that the E4 screen is not built). The
release half is absent from the running system, which makes this NOT MET rather than UNTESTED. How the released
remainder is billed is an assumption (`RulingSet` `ExtractPartialApprovalScope`, status Assumed); recording the release
does not depend on that answer.

### F-2 · KAFF-317 AC-317-E · NOT MET · `HoldReleaseCarriesWithholding` is still read by nothing

D-245 item 2 says the released-hold collection "closes KAFF-317-E". It closes the missing route that round 2 found
(`POST /api/projects/{id}/hold-release/collect`, tested in
`ClosureThroughExtractChainTests.The_released_hold_is_collected_in_parts_with_withholding_and_a_cheque_that_is_cash_only_at_clearance`).
It does not close the criterion. AC-317-E says: under `HoldReleaseCarriesWithholding` true withholding is written, and
under false it is not. `grep -rn HoldReleaseCarriesWithholding src` finds only the record declaration and its default
(`src/Domain/Rulings/WithholdingBase.cs:22,24,30`). `ReleasedHoldPayments` (`src/Domain/Closure/ReleasedHoldPayments.cs`)
and `CollectReleasedHold/Handler.cs` never read it, so flipping the ruling to false changes nothing: a typed withheld
amount on the release is accepted and posted either way. The only test that sets the flag
(`ExtractEndpointTests.The_withheld_amount_follows_the_ruled_base_with_no_handler_change`) sets it to true and tests an
extract, not a release. This is the same defect round 2 named (`qa/slice-3/verification-2026-11-18-r2.md`, defect 1,
first half).

Note for the Scrum Master: since D-238 the withheld amount is typed by Finance from what arrived, not computed. If the
ruling's false case is meant as "refuse a withheld amount on a release", that is a two-line check. If D-238 is meant to
make the flag moot, the criterion and the ruling need rewording by Nabil. Either way the criterion as written is not met.

### Mutations — can the tests fail?

Each mutation was applied to one file, the solution rebuilt (0 errors), one test class run, and the file restored
with `git checkout -- <file>`. After the last one the solution was rebuilt from the clean tree (exit 0, 0 warnings)
before the API was started, so no mutated binary served the live probes.

| # | Rule | Mutation | Class run | Result |
|---|---|---|---|---|
| M1 | The hold only grows: a cancelled original's hold accrual is never reversed (D-244 §1) | removed the `if (IsHoldLeg(leg)) continue;` skip in `Extract.CancelForRevision` (`src/Domain/Extracts/Extract.cs`) | `ExtractCancellationTests` | **killed**, 1 of 8 red: `A_cancellation_keeps_the_hold_accrual_standing_and_mirrors_every_other_accrual_leg` |
| M2 | Nobody creates and approves the same movement | `Extract.EnsureMayDecide` made to ignore every prior actor | `ExtractChainTests` | **killed**, 2 of 32 red: `The_preparer_may_not_decide_and_a_screen_can_know_it_before_any_attempt`, `Nobody_holds_two_gates_on_one_extract_in_one_cycle` |
| M3 | No adjustment touches the hold | the hold check in `Adjustment.RaiseCreditNote` made never true (`src/Domain/Adjustments/Adjustment.cs:188`) | `AdjustmentTests` | **killed**, 1 of 41 red: `An_adjustment_that_names_the_hold_is_refused_and_the_hold_balance_is_unchanged`. (A first attempt, `false && A \|\| B`, left the `LedgerKind` half live and survived; that was the mutation's fault, not the test's.) |
| M4 | AC-317-E: the release withholds only if the ruling says so | `WithholdingBaseRule.Default` flipped to `HoldReleaseCarriesWithholding: false` (`src/Domain/Rulings/WithholdingBase.cs:24`) | `ClosureThroughExtractChainTests` (Api) | **survived**, 10 of 10 green. The released-hold collection still posts 600.00 of withholding with the ruling false. This is the evidence for F-2: the flag decides nothing |

### Live probes — role and assignment, direct to the routes

API restarted on http://localhost:5080 (Development, clean rebuild). `/api/health`: healthy, guards installed,
`leastPrivilege: true`, `excessPrivileges: []`. Five seeded users signed in (`docs/uat-manual.md` §1), each with its own
cookie, every call a real `POST` with an empty body. Project `PRJ-CE500CBD` (`01a1f08e-3f29-78e6-b185-d5d8c1187fd8`,
the round-2 project, every user assigned), extract 1 `01a1f08e-fbd9-7573-890c-a385a6ccf6d4` (Collected). A 403 is the
permission refusal; a 404, 409 or 400 means the caller passed role and assignment and was stopped by the record or
the body.

| Route | Owner | Finance | Technical Office | Supervisor | Junior engineer |
|---|---|---|---|---|---|
| `extracts/{id}/qc-report` | 403 | 403 | 403 | 400 `material_values_required` | 403 |
| `extracts/{id}/technical-approval` | 403 | 403 | 409 `illegal_transition` | 403 | 403 |
| `extracts/{id}/deductions` | 403 | 409 | 403 | 403 | 403 |
| `extracts/{id}/owner-approval` | 409 | 403 | 403 | 403 | 403 |
| `extracts/{id}/issue` | 409 | 403 | 403 | 403 | 403 |
| `extracts/{id}/collect` | 403 | 400 `method_required` | 403 | 403 | 403 |
| `extracts/{id}/dispute` | 403 | 409 | 403 | 403 | 403 |
| `adjustments/{id}/approval` | 404 | 403 | 403 | 403 | 403 |
| `change-orders/{id}/owner-approval` | 404 | 403 | 403 | 403 | 403 |
| `cheques/{id}/deposit` | 403 | 404 | 403 | 403 | 403 |
| `hold-release/collect` | 403 | 400 `method_required` | 403 | 403 | 403 |

Each route admits exactly the one role the catalogue names; the junior engineer is refused on every one. Finance is
refused the Owner's approvals (AC-509-E, AC-512-G's role half), the Technical Office is refused the deductions (AC-500-I).

Assignment, same role on an assigned and an unassigned project:

| User | Route | Unassigned | Assigned |
|---|---|---|---|
| Technical Office | `extracts/{id}/technical-approval` | 403 (`PRJ-0761FB3D`) | 404 `extract.not_found` (`PRJ-377EC1A2`) |
| Technical Office | `change-orders` (create) | 403 | 400 `change_order.title_required` |
| Supervisor | `extracts/{id}/qc-report` | 403 (`PRJ-47274FC5`) | 404 `extract.not_found` |
| Supervisor | `change-orders/{id}/client-approval` | 403 | 404 `change_order.not_found` |
| Junior engineer | `extracts` (prepare) | 403 | 400 `extract.period_invalid` |

An unassigned Finance or Owner user could not be probed live: both Finance users are assigned to every project
automatically, and the Owner is not project-scoped. Both are covered by
`EndpointPermissionCoverageTests` and the per-route permission theories (`Each_extract_route_requires_the_permission_D_189_rules_and_the_route_project`,
`Each_adjustment_route_requires_...`, `Each_change_order_route_requires_...`).

`driver.mjs smoke` after the restart: all checks passed, exit 0 (API 200, guards installed, least privilege, SPA
mounted, `dir=rtl`, Arabic text).

## The money and permission rules, this pass

- **No float near money.** No `double` or `float` in `src/Domain/Extracts`, `src/Api/Features/{Extracts,Cheques,ChangeOrders,Adjustments}`,
  `src/Domain/{Closure,Adjustments,ChangeOrders,Collections}`; none in `src/Web/src/app/features/extracts` arithmetic. Every
  money column in the model snapshot is `numeric(18,4)` (`KaffDbContextModelSnapshot.cs`); the eleven `numeric(18,6)` columns
  are rates. The new `extract_quantity_lines` quantities are `numeric(18,4)`.
- **No stored balance.** The collection position, the released-hold position and the hold to date are summed from postings
  on every read. No balance column was added in `33ff701..363d5ee`.
- **No posting update or delete path.** No `Remove`, `RemoveRange`, `ExecuteDelete`, `ExecuteUpdate` or posting `Update` in the
  diff. `extract_quantity_lines` is insert-only for `kaff_app` (D-249 §1).
- **The hold is never debited before handover.** A cancelled original's hold accrual is skipped, not reversed
  (`Extract.cs`, `CancelForRevision`; M1 killed). A rejection that would reverse a hold accrual is refused whole
  (`A_rejection_that_would_reverse_a_hold_accrual_is_refused_and_writes_nothing`). An adjustment naming the hold is refused
  in the domain (M3 killed), by `Posting.Create` and by the `KAFF_HOLD_DEBIT` / `KAFF_HOLD_REVERSAL` triggers
  (`001_guards.sql:118-134`).
- **The five ledgers never net.** A credit note moves only receivable to contract revenue (`A_credit_note_may_target_only_the_projects_own_contract_revenue_account`);
  a collection moves only receivable to safe and tax withheld (`Cash_reaches_the_safe_at_Collected_and_not_at_Issued_and_no_other_ledger_moves`).
- **Role and assignment, server-side, on every route.** Every endpoint in the four feature folders and the two closure
  collection routes carries `RequirePermission(..., ProjectScope.FromRoute())`. Probed live above.
- **Nobody creates and approves the same movement.** Extract gates (`EnsureMayDecide`, M2 killed), change-order gates
  (`ChangeOrder.EnsureMayDecide`), adjustments (`Adjustment.EnsureMayDecide`, which also bars whoever raised or resolved the
  dispute). Collections have no approval step (Q97 unruled), so the rule has nothing to bind there.
- **Audit on every state change, rejection with a stored reason.** `AuditCoverageTests` green;
  `Disputing_raising_and_deciding_write_audit_records_with_who_and_why`; every reject route refuses a blank reason.

### F-3 · KAFF-512 AC-512-C and KAFF-511 AC-511-B · NOT MET as written · a credit note is a new posting, not a reversal

AC-512-C: "reversing postings reference the originals through `ReversesId`". The built credit note is **one new
`CreditNote` posting** from the receivable to contract revenue, `ReversesId` null, asserted so by
`The_credit_note_posts_only_at_the_owners_approval_as_one_new_posting_and_the_hold_never_moves`
(`ExtractEndpointTests.cs:1720`). D-202 records why: a reversal mirrors the whole original and would have to reverse
the `HoldAccrual` leg, which §5.1 forbids, and a credit note is usually partial. The reason is sound, and `spec.md` §6.9
says only "it posts like any other movement". What the criterion protects is met: no posting row is updated or
deleted (every earlier posting is byte-for-byte unchanged in the same test), and correcting an approved credit note is
a true `ReversesId` mirror (`Reversing_an_approved_credit_note_writes_a_mirror_through_ReversesId_and_never_edits_the_original`).
The story text was never amended to carry D-202, and no ruling by Nabil ratifies the shape. AC-511-B's second clause
("any correction is a reversing posting referencing the original") has the same gap on the credit-note route; its
revised-extract route does reverse through `ReversesId`, hold leg excepted (D-244). Scored as a condition on both
stories, not as a rejection, the same way round 2 scored AC-303-D.

### F-4 · KAFF-500 AC-500-A · MET with a condition · the §15 chain is not fed from daily logs in any test

The chain reproduces every §15 figure: all three extracts through the aggregate to `Collected`
(`Section15ThroughExtractChainTests`), extracts 1 and 2 through the HTTP routes
(`Section_15_extract_one_runs_through_the_routes_and_the_printed_column_foots`,
`The_next_extract_certifies_from_where_the_last_one_stopped_and_only_after_it_is_issued`). Two clauses are not literally
met. (1) "executed quantities recorded through daily logs": every chain test seeds `QuantityExecuted` on the BOQ line
directly; the daily-log-to-BOQ path is `KAFF-600`'s, verified there, but no test joins the two. (2)
"`Section15WorkedExampleTests` no longer needs to build any posting by hand": it still does, on purpose, as the
independent KAFF-300 fixture D-034 requires. Neither is a money defect; both are wording for the Scrum Master.

### Observations, not scored

- O-1. `ChangeOrderEndpointTests.cs:418-427`: two `<summary>` blocks are stacked, so the AC-508-A/C/F summary documents
  the waiting-line test, and the billing test at line 453 has none. Cosmetic.
- O-2. Carried from round 2 and still open: collections always land in the Safe, never a bank account, against §6.5
  "client collections default to bank"; a cleared cheque still reads `Cleared` after its collection is reversed (D-239).
- O-3. D-245 "Open": a revision's hold-to-date memo shows the sum of both holds rather than the larger one (display
  only); no reversal of a released-hold collection exists; a revision of a revision carries payments one level only.
- O-4. D-249 §7: a period quantity can be negative when a daily log lowers a line's executed quantity; shown as it
  falls, no rule. Waiting on Nabil.

## Verdicts per story

### KAFF-500 — the extract chain — 9 MET, 1 MET with condition (A), 1 NOT MET (E) — recommend `BUILT verdict=REJECTED`

| AC | Verdict | Evidence |
|---|---|---|
| A §15 end to end | MET, conditional | F-4 |
| B §15 invariants | MET | `Section15ThroughExtractChainTests`: hold 200,000 = 20%, advance 0, تشوينات in = recovered, client cash = contract value, safe never negative |
| C Owner gate cannot be skipped | MET | the state machine has no `DeductionsApplied -> Issued`; `Owner_approval_is_the_owners_alone`, `Issue_is_the_owners_alone_for_now`; live: every non-Owner role 403 on `owner-approval` and `issue` |
| D nobody approves their own | MET | `Someone_who_acted_earlier_in_the_cycle_is_refused_and_the_read_model_leaves_the_control_out`; M2 killed; web gate controls are `@if` on server flags, `[disabled]` only on cancel buttons while busy |
| E splittable gate, NCR releases the remainder | **NOT MET** | F-1 |
| F rejection to Draft with reason | MET | `A_rejection_needs_a_reason_returns_to_draft_and_stores_the_reason`, `Each_rejection_route_is_gated_like_the_gate_it_rejects_at`; D-242 §2 rejection kinds |
| G rejection after posting reverses | MET | `A_rejection_after_the_ruled_accrual_writes_reversing_postings_and_deletes_nothing`; with a hold leg the rejection is refused whole (`A_rejection_that_would_reverse_a_hold_accrual_is_refused_and_writes_nothing`): the hold rule wins |
| H stopped project issues nothing | MET | `A_stopped_project_issues_nothing`; `PostingProjectStatePolicy` in the domain |
| I Technical Office moves no money | MET | `Only_finance_applies_deductions_and_the_technical_office_and_owner_are_refused`; live 403 |
| J six figures, column foots | MET | `Section_15_extract_one_runs_through_the_routes_and_the_printed_column_foots` (kind and derivation on every figure) |
| K atomic commit | MET | `A_posting_that_would_breach_a_guard_writes_none_and_names_the_balance` |

### KAFF-316 — collections and cheques — 10 of 10 MET — recommend `VERIFIED verdict=CONDITIONAL`

Re-checked against D-243 and D-245, which touched collection reversal, the cheque read and released-hold cheques.

| AC | Verdict | Evidence |
|---|---|---|
| A method, date, reference | MET | `A_collection_missing_method_date_or_reference_is_refused_and_nothing_changes`; live 400 `collection.method_required` |
| B role and assignment | MET | `Collect_is_for_finance_alone_and_needs_an_assignment_to_the_project`; live: every non-Finance role 403 on `collect` and `cheques/{id}/deposit` |
| C cheque transitions | MET | `A_cheque_collection_starts_a_cheque_that_only_finance_moves_and_only_by_the_legal_transitions`, `CollectionTests` |
| D extract settles (D-235, D-238) | MET | `CollectionPositionTests`; the round-2 live chain |
| E shortfall, no balancing entry | MET | part payments post what arrived; overpayment refused (`collection.exceeds_expected`) |
| F reversed, never edited | MET | `Reversing_a_collection_is_finance_alone_needs_a_reason_and_mirrors_the_postings`; `Reversing_a_revisions_collection_also_reverses_the_payment_carried_from_the_original` |
| G bounced cheque | MET | `A_deposited_cheque_can_bounce_and_the_extract_stays_issued` |
| H cheque postings | MET | cash only at clearance (D-238 §2) |
| I project-state policy decides | MET | `EnsureMayRecordCollection` reads `RulingSet.PostingProjectStates`; `PostingProjectStateLegalityTests` |
| J ledgers do not net | MET | `Cash_reaches_the_safe_at_Collected_and_not_at_Issued_and_no_other_ledger_moves` |

Conditions: O-2.

### KAFF-317 — withholding at source — 7 MET, 1 NOT MET (E), 2 HELD (C, G) — recommend `BUILT verdict=REJECTED`

| AC | Verdict | Evidence |
|---|---|---|
| A corporate client withholds, cash reconciles | MET | `A_corporate_client_withholds_and_cash_is_the_net_less_the_withheld_amount_posted_only_at_Collected` |
| B individual withholds nothing | MET | `An_individual_client_withholds_nothing_so_no_tax_component_and_no_withholding_posting` |
| C different amount than the rate | HELD | Q99 |
| D base follows the ruling | MET | theory `The_withheld_amount_follows_the_ruled_base_with_no_handler_change` |
| E release withholds only if ruled | **NOT MET** | F-2; M4 survived |
| F no category, no withholding | MET | `A_corporate_contract_with_no_category_withholds_nothing` |
| G rate changes after first extract | HELD | Q30 |
| H two accounts, two fields | MET | unchanged since round 2 |
| I atomic commit | MET | `A_guard_refusing_the_withholding_leg_leaves_no_partial_collection` (closes round 2's UNTESTED) |
| J role and assignment | MET | same routes as 316-B; live |

### KAFF-508 — change orders on the extract — 6 MET, 1 HELD (C, built on the Q102 default) — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence |
|---|---|---|
| A own section, own price | MET | `An_approved_change_order_is_billed_in_its_own_section_and_the_figures_foot` |
| B unapproved never billed | MET | `A_change_order_before_approval_is_never_billed` |
| C hold and recovery on change-order value | HELD | Q102; runs on the `ChangeOrderBillingTreatment` default (`Section15ThroughCalculatorTests`) |
| D billed once | MET | `An_approved_change_order_is_billed_on_one_extract_only` (the database refuses a second billing). A change order is billed whole, so "the unbilled remainder" is always nothing; partial billing is not built |
| E Cost Plus and Design stay out | MET | prepare is Lump Sum only (D-247 §1); `A_cost_plus_change_order_only_logs_carries_no_price_and_posts_nothing` |
| F column foots | MET | same test as A |
| G §15 unchanged | MET | the §15 tests above are green with change orders built |

Conditions: C held; O-1.

### KAFF-509 — the change-order chain — 8 MET, 2 HELD (B, D) — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence |
|---|---|---|
| A every legal and illegal transition | MET | `ChangeOrderTests` table tests; `An_illegal_step_is_refused_with_the_state_machine_error_and_moves_nothing` |
| B line pricing | HELD | Q104 |
| C approval raises the derived value | MET | `An_approved_change_order_raises_the_contract_value_and_leaves_the_signed_boq_exactly_as_signed`; the live project list carries `approvedChangeOrderTotal` |
| D client approval | HELD | Q103; built per D-241 §4 and D-243 §7 (the supervisor records it); live: supervisor unassigned 403, assigned 404 |
| E only the Owner approves | MET | `Finance_and_the_technical_office_cannot_give_the_owner_approval`; live 403 for Finance and Technical Office |
| F nobody approves their own | MET | `The_creator_is_refused_at_a_later_gate_and_the_read_model_leaves_the_control_out` |
| G rejection with stored reason | MET | `A_rejection_needs_a_reason_at_every_gate_and_the_reason_is_stored_and_audited` |
| H Cost Plus and Design only log | MET | `A_cost_plus_or_design_change_order_ends_at_Logged_unpriced_and_cannot_enter_the_chain`; the Api test for Cost Plus |
| I role and assignment | MET | `Each_gate_is_held_by_its_own_permission_and_a_role_without_assignment_is_refused`; live 403 unassigned |
| J signed BOQ untouched | MET | same test as C |

### KAFF-511 — a disputed extract — 5 MET, 1 NOT MET as written (B, scored as a condition), 2 HELD (C, D) — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence |
|---|---|---|
| A only an Issued extract is disputed | MET | `A_dispute_is_raised_on_an_issued_extract_and_on_no_other_state`; `A_dispute_needs_an_issued_extract_a_reason_and_a_resolution_needs_a_disputed_one` |
| B postings never edited | NOT MET as written | F-3. No row updated or deleted (met); the credit-note correction is not a `ReversesId` reversal |
| C credit note | HELD | Q106, Q107; built per D-202 and D-239 |
| D revised extract | HELD | Q106; built per D-241 §2, D-243 §8, D-244 |
| E hold untouched | MET | `A_dispute_changes_no_posting_and_the_hold_balance_not_at_the_raise_and_not_at_either_resolution`; `A_revision_carries_the_original_hold_accrues_only_the_difference_and_the_hold_never_decreases`; M1 killed |
| F nobody approves their own | MET | `Nobody_approves_their_own_adjustment_and_nobody_who_handled_the_dispute_does_either` |
| G role and assignment | MET | `Only_assigned_finance_raises_or_resolves_a_dispute_and_nobody_else_reaches_either_route`; live 403 for four roles |
| H audit with both reasons | MET | `Disputing_raising_and_deciding_write_audit_records_with_who_and_why` |

### KAFF-512 — the adjustment object — 7 MET, 1 NOT MET as written (C, scored as a condition), 1 HELD (B), 1 BLOCKED (J) — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence |
|---|---|---|
| A four fields | MET | `An_adjustment_missing_a_field_or_of_a_type_not_built_is_refused_and_nothing_is_stored` |
| B credit and debit note accounts | HELD | Q107; the debit note and the design credit are refused as not built (`Every_type_but_the_client_credit_note_is_refused_and_a_termination_settlement_by_its_own_error`) |
| C credit note reverses | NOT MET as written | F-3 |
| D nothing debits the hold | MET | `An_adjustment_that_names_the_hold_is_refused_in_the_domain_and_the_hold_balance_is_unchanged`; M3 killed |
| E ledgers do not net | MET | the credit-note pair is receivable to contract revenue only (`A_credit_note_may_target_only_the_projects_own_contract_revenue_account`) |
| F project-state table decides | MET | theory `The_project_state_table_decides_whether_a_credit_note_is_accepted` |
| G nobody approves their own | MET | `Nobody_approves_their_own_adjustment_and_nobody_who_handled_the_dispute_does_either`; live: Finance 403 on `approval` |
| H role and assignment | MET | `Only_the_owner_decides_an_adjustment_and_only_assigned_finance_raises_one`; live |
| I audit | MET | `Disputing_raising_and_deciding_write_audit_records_with_who_and_why` |
| J termination settlement | BLOCKED | Q108; refused by its own error |

## Not reached

- No Playwright or `driver.mjs layout` pass on the extract screens; D-246 reports 46 of 46 layout PASS and this pass did
  not re-measure. The screens carry no criterion in these seven stories.
- No live walk of a revised extract or a released-hold collection; both rest on their Api tests.
- Unassigned Finance and Owner probes (see the live probes section).
- `dotnet format --verify-no-changes` was not run.

## Corrections to the brief

- The brief says `KAFF-317` was rejected "on AC-317-E and AC-317-I, built since in D-245". AC-317-I is now met. AC-317-E is
  not: D-245 built the missing route but not the ruling's false case.
- AC-500-A says "both of §15's extracts"; §15 has three. The tests run all three.

## Trailers moved by this pass

KAFF-500 and KAFF-317 to `BUILT verdict=REJECTED`; KAFF-316, KAFF-508, KAFF-509, KAFF-511 and KAFF-512 to
`VERIFIED verdict=CONDITIONAL`, all `at=363d5ee on=2026-11-20`. Nothing committed.
