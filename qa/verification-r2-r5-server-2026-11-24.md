# Verification: Releases 2 to 5, server side - 2026-11-24

Verifier: separate session, model Opus 5.5 (claude-opus-5-5), the strongest model.
Scope: commits merged to main between 70fc958 and a32b42b (33ae992 R3, df5a97b R2, 3b3f7be R4) plus d683c98 (R5 portal). Server side only; no screens were built.
Judged against: the story files in plans/release-2..5, D-266, D-267 and the spec.md sections they cite.

Findings are written as they are reached.

## Caveat on the test binary

The Release test binary in tests/Api.Tests/bin/Release/net10.0 was built at 11:11 on 2026-11-24; the R4 merge commit a32b42b is timestamped 11:44. The binary contains the R2, R3, R4 and R5 test classes, so it was built from the lane content, but it was not rebuilt from HEAD (662c88c) by this pass. Test runs below are evidence for that build.

## Release 2, site money

Tests run: PettyCashRequestEndpointTests, MaterialAdvanceReadTests: 9 of 9 passed.

### KAFF-609b. Supervisor submits or returns a draft - recommend VERIFIED verdict=CONDITIONAL

| AC | Verdict | Evidence |
|---|---|---|
| AC1 submit; amount not editable | MET | `PettyCashRequest.Submit` (src/Domain/PettyCash/PettyCashRequest.cs); no edit method or route exists at all. Test `The_supervisor_submits_a_draft_and_it_is_audited_and_posts_nothing`. |
| AC2 return with stored reason, back to junior | MET (server half) | `Return` stores `ReturnReason`, blank refused 400 `ReasonRequired`. "Back to the junior" means state Returned; the junior's edit and resubmit path is KAFF-609a, not built, so a Returned request is terminal today. |
| AC3 junior refused | MET | 403 for junior, Finance, Owner, unassigned supervisor; test `A_junior_a_finance_user_the_owner_and_an_unassigned_engineer_are_refused_and_nothing_changes`. |
| AC4 audit | MET | Opt-out `AuditSaveChangesInterceptor`; test asserts actor and BeforeJson on the Status change and a ReturnReason audit. |

Never-break rules: creator is not the decider (`SeparationOfDuties.EnsureDifferentActor`, tested 403 `SameActorCreatedAndApproved`); role and assignment through `RequirePermission(DraftSubmit, ProjectScope.FromRoute())`; project row locked `FOR UPDATE`; Amount is `Money`, precision (18,4) from the context convention; nothing posts (tested).

Condition: no API route creates a draft (KAFF-609a is not built), so the flow cannot be driven end to end; the tests seed drafts in the database.

### KAFF-309r. تشوينات outstanding and reconciliation - recommend VERIFIED verdict=CONDITIONAL

| AC | Verdict | Evidence |
|---|---|---|
| AC1 supervisor sees outstanding at QC | MET (read only) | `GET /api/projects/{id}/material-advance`, `ProjectMoneyRead` + route project. The supervisor reads 200 in test. No screen. |
| AC2 recovery never exceeds outstanding | Not claimed in this pass | Pre-existing `MaterialAdvanceRecoveryRule`; not re-verified. |
| AC3 in equals recovered, read shows difference | MET | `Reconciled` = no open extract and issued == recovered; tests for 55,000 outstanding and fully reconciled. Sums from postings, reversal subtracts, matching `ProofOfNumbers`. |
| AC4, AC5 | HELD (Q58, Q8) | Not judged. |

Finding F-R2-1 (minor, test gap): MaterialAdvanceReadTests has no unassigned site engineer case, and the test named `The_junior_hr_and_an_unassigned_owner_style_caller_are_refused` asserts the Owner gets 200; the name does not match what it proves. The assignment refusal relies on the shared ProjectScope gate only. Also no test proves that a reversal subtracts.

### KAFF-313 and KAFF-612r - recorded defaults only

D-266 item 1 states these were already built; this pass added the RulingNotes `RefusedPaymentAfterInjection`, `QcReportScope` and `StockpileMarker` (src/Domain/Rulings/SiteMoneyRulings.cs), all marked Assumed. No new behaviour to verify. Recommend no trailer change from this pass.

## Release 3, sales

Tests run: Api `OpportunityPipelineTests` 12 of 12, Domain `OpportunityPipelineTests` 21 of 21 passed.

All routes carry `RequirePermission(Permission.OpportunityManage)` (company-wide, Owner and Marketing; PermissionCatalogue.cs:403). An opportunity has no project, so there is no assignment to check; this matches the catalogue scope. Audit comes from the opt-out interceptor. No money moves in R3-01 to R3-04.

### R3-01 Open and list opportunities - recommend VERIFIED verdict=PASS

| AC | Verdict | Evidence |
|---|---|---|
| A create on existing client, Lead, Active | MET | CreateOpportunity/Handler.cs; unknown client refused. Tests `Marketing_opens_an_opportunity_as_a_Lead_with_a_generated_code`, `Create_is_refused_for_an_unknown_client_and_a_blank_title`. |
| B generated code | MET | `O-{n}` from `opportunity_code_seq`; typed code ignored (test). |
| C OpportunityManage, others 403 | MET | Test `Every_other_role_is_refused_on_every_opportunity_endpoint` covers Finance, Technical Office, HR, Client on all seven routes. Site engineer is not in the list (minor gap; the gate is the shared catalogue). |
| D filter by stage and status, search name or phone | MET | ListOpportunities/Handler.cs; unknown filter refused, not read as all. |
| E audit with before and after | MET | Test `Create_and_stage_change_write_audit_records_with_before_and_after`. |
| F client through client endpoints | MET | No client is created here; existence check only. |

### R3-02 Stage moves and hold - recommend VERIFIED verdict=CONDITIONAL

| AC | Verdict | Evidence |
|---|---|---|
| A forward one stage from Lead to Negotiation | MET, with F-R3-1 | `Opportunity.AdvanceStage` (src/Domain/Sales/Opportunity.cs). |
| B hold and resume to the same stage | MET | `PutOnHold` leaves Stage; test `Hold_then_resume_returns_to_the_same_stage_and_a_held_opportunity_cannot_move`. |
| C move resets activity, revives Stalled | MET | `RecordActivity` inside `AdvanceStage`; tests at Api and Domain level. |
| D skip and go back | HELD (R3-Q3) | Not judged. |

Finding F-R3-1 (question for Nabil, not a defect yet): `AdvanceStage` moves Negotiation to Contract (`Stage++` up to `OpportunityStage.Contract`), and the tests `Advance_moves_one_stage_and_stops_at_Contract` assert it. The AC reads "Lead to Negotiation". spec.md section 3 lists Contract as a stage, while section 13 ends the chain at ClosedWon, which is the conversion of R3-09. Today an opportunity can sit at stage Contract with status Active and no project. Decide whether the last move belongs only to conversion.

### R3-03 Inactivity - recommend VERIFIED verdict=PASS

| AC | Verdict | Evidence |
|---|---|---|
| A day 2, day 4 flags, day 7 Stalled; counts are settings | MET | `OpportunityInactivity` (Assumed RulingNote, R3-Q5); Domain theory test over the day counts. |
| B activity revives at same stage | MET | Domain test `Activity_revives_a_stalled_opportunity_at_the_same_stage`. |
| C derived on read, not stored | MET | `InactivityAt` and `StatusAt`; the list filters by `LastActivityAt` thresholds. Nothing writes Status = Stalled. |
| D follow-up filter | MET | `followUp=true`; test `Quiet_days_flag_on_the_list_and_the_follow_up_filter_lists_only_flagged_live_rows`. |
| E OnHold never flags | MET | `IsLive` guard; Domain test `An_opportunity_on_hold_or_closed_never_flags_or_stalls`. |

### R3-04 Close lost and reopen - recommend VERIFIED verdict=PASS

| AC | Verdict | Evidence |
|---|---|---|
| A reason required and stored | MET | `CloseLost`; Api and Domain tests. |
| B reopen on the same client, no new client | MET | `Reopen` keeps ClientId and Stage; test counts clients. |
| C audit with reason | MET | The close audit holds `ClosedLostReason`; `Reopen` clears the column and the earlier audit keeps the reason (documented in code). Test `Close_lost_is_refused_without_a_reason_and_stores_it_with_the_audit_reason`. |
| D free text or list | HELD (R3-Q10) | Free text by default. |

Minor: transitions take no row lock or concurrency token (OpportunitySupport.cs). Two simultaneous advances could both read the same stage and both write Stage+1, landing one step instead of two. No money is involved; noted only.

## Release 4, accounting

Tests run: Api `AccountingEndpointTests` 34 of 34, Domain `AccountingTests` 35 of 35 passed.

Rulings checked: D-267 item 1 (Owner approves every accounting entry) and item 2 (baseline read by Owner and Finance only).

Never-break rules on the new code:
- Money: every new money column is numeric(18,4) (migration 20261124084136_AccountingLayer.cs lines 60, 110, 148, 179); rates and tolerance are numeric(18,6). No float or double found. `AccrualPayload.Amount` is a bare `decimal` inside the stored JSON payload (src/Api/Features/Accounting/AccountingEntries.cs, record `AccrualPayload`), wrapped to `Money` when built; a minor deviation from "Money is a value object".
- No stored balance: depreciation and amortisation progress are summed from non-reversed postings (`ScheduleLedger.Active`); the asset holds no accumulated figure; the trial balance and the overhead report read postings only.
- Append-only: no update or delete path on postings. The new tables `budget_baselines` and `prepayment_schedules` are not in the app role's updatable list (005_app_role.sql), so they are insert-only at the database too.
- Owner approval: Finance's request is a dry run that adds nothing; the postings are built at approval (ApproveAccountingEntry/Endpoint.cs). The requester can never decide (`AccountingRequest.EnsureMayDecide` with `SeparationOfDuties`); the request row is locked `FOR UPDATE`. A rejection stores a reason (test `The_owner_returns_a_request_with_a_reason_nothing_posts_and_finance_may_ask_again`). MET.
- Role and assignment: company routes use `TreasuryPostCompany` / `TreasuryReadCompany`; project routes use the project-scoped permission with `ProjectScope.FromRoute()` (test `Finance_without_an_assignment_to_the_project_is_refused_on_every_project_route`). Approval is `AccountingEntryApprove` (Owner, company-wide).
- Audit: opt-out interceptor; the approval sets an audit reason.

### Finding F-R4-1 (defect, money): an accrual accepts a Prepayment account as its accrued side

`Accrual.Create` calls `Accrual.CheckAccounts` (src/Domain/Accounting/Accrual.cs:83), which passes when the first account is `AccruedExpense` **or `Prepayment`** (Accrual.cs:154). The check is shared with `PrepaymentSchedule.Create`, which adds its own `prepayment.Type != AccountType.Prepayment` test (PrepaymentSchedule.cs:86); the accrual path adds no `AccruedExpense` test. `PostingAccountLegality` does not restrict `ExpenseAccrual`. So Finance can ask, and the Owner can approve, an `ExpenseAccrual` posting that credits the Prepayment asset instead of the `AccruedExpense` liability. That reduces a prepayment which its amortisation schedule still expects to release. KAFF-701 AC1 says the accrual is written "to `AccruedExpense`". No test covers a wrong accrued-side account type (AC_701_D varies only the expense side). Fix: require `accruedExpense.Type == AccountType.AccruedExpense` in `Accrual.Create`, with a test.

### Finding F-R4-2 (defect, low likelihood): month idempotency and the single release rely on application reads under a per-request lock

Approval locks only its own `accounting_requests` row. Finance may file two requests for the same depreciation month, the same prepayment month, or the same accrual release; the dry run does not refuse a second waiting request. Approved one after the other, the second writes nothing (tested). Approved concurrently, both re-read the same postings before either commits, and both post: double depreciation or amortisation, or two `AccrualRelease` postings for one accrual. No unique index on postings by source document and month exists, and `accruals` has no concurrency token (AccountingConfigurations.cs has no `IsRowVersion` or `IsConcurrencyToken`). Fix options: refuse a second waiting request of the same kind and key at submit, and lock the asset, schedule or accrual rows (or take one advisory lock per kind) inside the approval.

### KAFF-700 Asset register and depreciation - recommend VERIFIED verdict=CONDITIONAL

| AC | Verdict | Evidence |
|---|---|---|
| AC1 12,000 computer shows 50%, Finance changes per asset | MET (register only) | `DepreciationDefaults.RateFor`; `ChangeAssetRate` is locked once depreciation is posted. The purchase posting is not built (R4-A2, per D-266), so "a purchase is saved" is the register row only. |
| AC2 Depreciation posting AccumulatedDepreciation to DepreciationExpense; book value = cost less accumulated | MET | `AssetRegisterEntry.Depreciate`; test `AC_700_A_..._depreciates_500_a_month_on_approval_with_book_value`. Posts only at Owner approval. |
| AC3 fully depreciated writes nothing | MET | Charge capped at `Cost - accumulated`; Domain test `A_fully_depreciated_asset_writes_no_charge_and_the_last_charge_never_overshoots_cost`. |
| AC4 asset dated in a closed month refused | MET | RegisterAsset handler `ClosedPeriodAsync`; test `AC_700_D`. |
| AC5 second run for a month writes nothing | MET when approvals are sequential; see F-R4-2 for concurrent approvals | Test `AC_700_E`. |
| AC6 Owner, HR, Technical Office refused on write | MET | Test `AC_700_F_owner_hr_and_technical_office_cannot_write_accounting_entries`. |
| AC7 method, start month | ASSUMED (`AssetDepreciationRule`, R4-Q7) | Not judged. |

Condition: F-R4-2; no purchase posting yet.

### KAFF-701 Accruals and prepayments - recommend BUILT verdict=REJECTED

| AC | Verdict | Evidence |
|---|---|---|
| AC1 accrual of 5,000 company posts ExpenseAccrual to AccruedExpense, open list shows it | NOT MET | The happy path works (test `AC_701_A`), but the rule that the accrual lands on `AccruedExpense` is not enforced: F-R4-1. |
| AC2 12,000 over 12 months, 1,000 a month, ends at exactly zero | MET | `PrepaymentSchedule.CumulativeThrough` gives the last month the remainder; Api test `AC_701_B`, Domain rounding test. |
| AC3 release clears it, no double cost | MET (mirror pair, whole release) | `Accrual.Release`; test `AC_701_C`. The link to the real cost posting is a PROPOSAL and is not built. Concurrent double release: F-R4-2. |
| AC4 no tag or both refused | MET | The tag is the route (company or project); the expense side must be CompanyExpense or ProjectCost to match; the posting re-checks the project tag. Test `AC_701_D`. |

### KAFF-704 Trial balance and export - recommend VERIFIED verdict=PASS

| AC | Verdict | Evidence |
|---|---|---|
| AC1 debit or credit column, contra in credit, totals equal | MET | `TrialBalance.Build` places each account by the sign of its net, not by its normal balance. For an account at its normal balance that is the same column; an abnormal balance shows on the side it really sits. The contra account lands in credit (test `AC_704_A`). |
| AC2 five ledgers are their own rows, no netting | MET | One row per account; test `AC_704_B`. |
| AC3 CSV with Arabic readable in Excel | MET | UTF-8 with BOM, formula-injection guard on text cells; test `AC_704_C`. Format is `TrialBalanceExport` ASSUMED (R4-Q10). Notes: the CSV header row and the word "Total" are hard-coded English (`ToCsv` in GetTrialBalance/Endpoint.cs); figures are rounded to two decimals per row, so the printed rows can differ from the printed total by a rounding cent. |
| AC4 balances from postings | MET | Test `AC_704_D_the_trial_balance_reads_postings_never_a_stored_balance`. |

### KAFF-707 Expense tagging - recommend no trailer move (partial)

Only AC5 (gross and net margin per project use the tag) is built, inside the overhead report; test `AC_707_E`. AC1 to AC4 are not built (D-266: the project-tagged expense payment waits on R4-A2, the categories on R4-Q11). Not judged as defects.

### KAFF-708 Overhead spread report - recommend VERIFIED verdict=PASS

| AC | Verdict | Evidence |
|---|---|---|
| AC1 spread in proportion to size, shares total the overhead | MET | `OverheadSpread.Spread`; size is the contract value, `OverheadSpreadBasis` ASSUMED (R4-Q9). Test `AC_708_A`. |
| AC2 no posting, project cost unchanged | MET | Read-only GET; the test counts postings. |
| AC3 leftover to the largest project | MET | Domain test `A_rounding_leftover_lands_on_the_largest_project`. |

### KAFF-709 Budget baseline - recommend VERIFIED verdict=CONDITIONAL

| AC | Verdict | Evidence |
|---|---|---|
| AC1 baseline = signed BOQ cost, Owner sets tolerance | MET | `BudgetBaseline.Set` needs a signed project and a frozen BOQ; `BudgetBaselineSet` is Owner only. The 10% default is a placeholder (`BudgetTolerance`, R4-Q6). |
| AC2 rises by an approved change order | HELD (R4-Q6) | Not built, per D-266. |
| AC3 no other edit | MET | Unique index `ux_budget_baselines_project`; no edit or delete route; the table is not updatable by the app role. Test `AC_709_C`. |
| AC4 copy, not reference | MET | `SourceBoqId` is a plain column with no FK (AccountingConfigurations.cs); test `AC_709_D_a_later_price_change_does_not_reach_the_stored_baseline`. |
| D-267 item 2: read by Owner and Finance only | MET | `BudgetBaselineRead` [owner, finance]; test `Owner_and_assigned_finance_read_the_baseline_and_an_assigned_site_engineer_is_refused`. |

Condition: AC2 is held, so the baseline cannot yet follow change orders.

## Release 5, portal (d683c98)

Tests run: `PortalEndpointTests`, `PortalSignInTests`, `PortalReflectionGateTests`: 26 of 26 passed.

Ruling checked: D-267 item 3 (another client's project answers 403, not the 404 that AC-810-D and AC-811-D name). The tests assert 403 and that the body names nothing of client B.

Never-break rules on the new code:
- Role and assignment: project routes use `RequirePermission(PortalRead | PortalApprove, ProjectScope.FromRoute())`; both permissions are held by `Role.Client` only (PermissionCatalogue.cs:690-691), and the scope runs `ProjectAccessPolicy.ClientAccessAsync`, which admits only a project whose `ClientId` is the caller's client. The project list uses `RequirePortalSession` and filters by the caller's `ClientId`. Photo publishing is `SitePhotoPublish` on the route project (Technical Office and supervising engineer, Assumed R5-Q12); an unassigned supervisor is refused (test).
- Creator is not the approver: the client approval runs `ChangeOrder.RecordClientApproval`, whose `Decide` refuses anyone who acted on the change order before. The contract value moves only at the Owner's approval (test).
- No cost leaks: every portal response type lives under `Kaff.Api.Features.Portal`, carries no `Money` or Domain type, and money leaves as two-decimal strings (`PortalFormat`). The reflection gate walks every `/api/portal/*` route's declared type and fails on a non-portal type or a banned property name; its own proof types show it goes red for `CostPrice`, a nested `MarginPercent`, an entity and a `Money`.
- Gate limit (note, not a defect): the gate checks the type a route declares with `Produces<T>()`, not what the handler actually returns. A handler that declared a portal type and returned another one would pass. The handlers read in this pass all return the declared type.
- Audit: the interceptor covers the photo publish and the change-order decision with the client as actor (tests); sign-in and password change are audited (test).
- No posting is written by any portal route.

### Finding F-R5-1 (defect, money shown to the client): "paid" leaves out the advance, so "remaining" never reaches zero

`GetProject/Handler.cs` sets paid to the `ClientCollection` cash on the project's extracts only (`ExtractMoneyQueries.ReceivedAndWithheldAsync`), and remaining to the revised contract value less that. The advance the client pays at signing (`ClientAdvanceReceipt`) is not counted, and nor is the hold the client pays at handover unless it is collected under an extract. On the spec.md section 15 worked example after extract 2 the client has paid 250,000 + 240,000 + 120,000 = 610,000, and remaining should be 390,000. The portal shows paid 360,000 and remaining 640,000. At the end of the project the client has paid 1,000,000 ("Total client cash equals contract value exactly"), yet the portal would still show a remaining balance. The test `The_home_shows_progress_paid_and_remaining_as_two_decimal_strings_and_no_cost_field` seeds a 250,000 `ClientAdvanceReceipt` and asserts `paid` "0.00" and `remaining` "1000000.00", so it locks in the wrong figure. The `PortalExtractVisibility` note (src/Domain/Rulings/PortalRulings.cs:48-51) records this as the assumed formula, but R5-B AC-B-3 defines paid as "the client's cash received" and the assumption contradicts spec.md section 15. The formula for remaining is the open part of R5-Q8; what counts as paid is not. Fix: count all client cash on the project (advance receipt, extract collections, hold release collection, net of reversals), and assert the section 15 figures in the test. Raise with Nabil if the intent really was "paid on extracts only", because a client reading a remaining balance on a fully paid contract is the outcome.

### Finding F-R5-2 (defect, leak): a published photo is streamed with its original file metadata

`Portal/GetPhoto/Handler.cs` streams the stored bytes unchanged, and `DailyLogs/UploadPhoto/Handler.cs` stores the uploaded bytes unchanged (it only sniffs the type). No code anywhere in `src` strips EXIF or similar metadata (searched for "exif" and "gps"). A phone photo normally carries GPS position, device and time in its EXIF block, so the client receives the site location and possibly the author device. R5-C AC-C-4 says no location and no author travel with the photo. The JSON list meets it (`PortalPhoto` has id, caption and taken-at only, tested); the file does not. Fix: re-encode or strip metadata at upload or on the portal read path, with a test that uploads a JPEG with a GPS tag and checks the portal bytes carry none.

Minor note: the photo `Caption` is free text typed by site staff and is shown to the client as is. spec.md section 12 forbids internal notes. Not a defect on its own; worth naming for R5-Q12.

### KAFF-810 Portal surface - recommend VERIFIED verdict=CONDITIONAL

| AC | Verdict | Evidence |
|---|---|---|
| A portal host answers only /api/portal | MET (when configured) | `PortalHostMiddleware`; test `On_the_portal_host_only_the_portal_routes_answer`. With `Kaff:PortalHost` unset (the R5-Q1 same-host default) it passes everything through, by design. The portal's static files are not served yet. |
| B staff token refused on portal routes | MET | Tests `Every_project_route_of_the_portal_refuses_every_staff_role`, `The_list_of_projects_refuses_staff`, `A_staff_session_opens_no_portal_route`. |
| C portal token refused on staff routes | MET | Test `The_portal_cookie_opens_no_staff_route`. |
| D own project succeeds, other client's refused | MET as ruled by D-267 item 3 (403) | Test `One_client_calling_every_route_with_the_other_clients_ids_is_refused_and_learns_nothing`. |
| E own cookie and CORS | HELD (R5-Q1) | The portal session uses the same cookie mechanism as staff (`StaffSessionMinter.IssuePortal`). |
| F unshared response types | MET | Reflection gate namespace rule. |
| G audit with the client as actor | MET | Approve test counts audit records with the client as actor; sign-in audit test. |

Condition: E is held; F-R5-1 and F-R5-2 sit on routes this surface carries.

### KAFF-811 The gate - recommend VERIFIED verdict=CONDITIONAL

| AC | Verdict | Evidence |
|---|---|---|
| A fails on a Domain entity or staff type | MET | `A_staff_response_type_a_domain_entity_and_a_money_value_are_all_caught`. |
| B banned names | MET | Banned list as proposed; allow-list empty. |
| C a deliberate costPrice fails | MET | `A_cost_price_added_to_a_portal_response_is_caught`. The test proves itself red with the leaky types. |
| D A with B's ids refused, no body names B | MET (403 per D-267) | Test as above. |
| E the seed has a portal Client user | NOT MET | No seeder or tool creates a `Role.Client` user (src/Infrastructure/Persistence/Seeding has none; tools has none). Low weight while no screen exists, but it is not built. |
| F two decimals | MET | `PortalFormat`; the test regex checks every money field. |

### R5-A Client account and portal sign-in - recommend VERIFIED verdict=PASS

| AC | Verdict | Evidence |
|---|---|---|
| A-1 Owner creates a client user | Pre-existing (CreateUser); not re-verified. |
| A-2 client credential at the staff door is 401 | MET | Test `A_client_credential_at_the_staff_door_is_refused`. |
| A-3 forced password change first | MET | `ListProjects` refuses while `MustChangePassword`; the permission gate refuses the project routes; tests `A_first_sign_in_succeeds_and_reads_are_refused_until_the_client_sets_his_own_password`, `A_client_user_with_a_password_to_change_is_refused_every_read_until_he_does`. |
| A-4 five wrong passwords lock out | MET | Test `Five_wrong_passwords_lock_a_client_as_they_lock_staff`. |
| A-5 first password and reset | HELD (R5-Q7; `ClientCredentialHandover` Assumed) | Not judged. |

Staff account, subcontractor and wrong password all get the same 401 at the portal door (test).

### R5-B Portal reads - recommend BUILT verdict=REJECTED

| AC | Verdict | Evidence |
|---|---|---|
| B-1 two projects listed, other client's absent | MET | Test `A_client_lists_his_own_projects_and_never_the_others` (one project per client in the fixture; filter is by ClientId). |
| B-2 home: section progress, paid, remaining, pending, no cost | MET for shape | Test checks fields and banned words. The paid and remaining values are wrong: B-3. |
| B-3 paid = client cash received, from postings (section 15) | NOT MET | F-R5-1. |
| B-4 only Issued or Paid extracts | MET | `ListExtracts` filters Issued and Collected; test `An_extract_shows_only_once_it_is_issued`. |
| B-5 Cost Plus statement is a summary | MET by construction, UNTESTED | Cost Plus home has no sections or completion; the portal extract type has no line items. No Cost Plus portal test. |
| B-6 only published photos | MET | Test publishes and lists. |
| B-7 time plan | HELD (R5-Q9) | Not judged. |

### R5-C Publish a photo - recommend BUILT verdict=REJECTED

| AC | Verdict | Evidence |
|---|---|---|
| C-1 supervising engineer or Technical Office publishes, audited, shows on portal | MET | `PublishPhoto` under `SitePhotoPublish`; test checks the audit record and the portal list. |
| C-2 junior refused | MET | The junior (`_prep`), Finance, Owner, HR and the client are refused (test). |
| C-3 unpublish | HELD (R5-Q12) | Not built. |
| C-4 no location, author or log reference travels with it | NOT MET | JSON half met; the file bytes keep their metadata. F-R5-2. |

### KAFF-812 Client approves, change-order half - recommend VERIFIED verdict=PASS (change-order half only)

| AC | Verdict | Evidence |
|---|---|---|
| A ClientApproval to OwnerApproval, actor is the client, audited | MET | Test `The_client_approves_and_the_actor_is_the_client_user_and_the_contract_value_does_not_move`; row locked `FOR UPDATE`. |
| B reject needs a reason, stored | MET | Test `A_rejection_needs_a_reason_and_ends_the_change_order_with_the_reason_stored`. |
| C other states refused | MET | Draft answers 404 (invisible), a second approval 409; reject checks the gate explicitly. |
| D Cost Plus or Design shows no approve action | MET by construction, UNTESTED | `IsPriced` is false for Cost Plus and Design (ChangeOrder.cs:164-165), and `IsVisible` requires it. No portal test. |
| E contract value unchanged before the Owner | MET | Same test reads the home. |
| F client of X on Y's change order refused | MET | 403 on another client's project, 404 for an order not on his project (test). |
| J the client approves what he did not create | MET | `Decide` refuses a prior actor; D-035 keeps client users out of staff. |
| G to I design half | Not in scope (R5-D). | |

The trailer cannot be VERIFIED for the whole story while the design half is unbuilt; the Scrum Master decides how to record a half.

## Can the tests fail? Mutation checks

Run in a throwaway git worktree of HEAD (662c88c) under the session scratchpad, Domain tests only, built with warnings not treated as errors so the mutants compile. The worktree was removed afterwards; nothing in D:\ERP was touched.

| Mutation | Test that went red |
|---|---|
| `AccountingRequest.EnsureMayDecide` no longer calls `SeparationOfDuties.EnsureDifferentActor` | `AccountingTests.The_user_who_asked_can_never_decide_and_the_owner_can` |
| `PettyCashRequest.EnsureMayDecide` no longer calls `SeparationOfDuties.EnsureDifferentActor` | `PettyCashRequestTests.The_user_who_drafted_a_request_cannot_submit_or_return_it` |
| `Opportunity.InactivityAt` lets an OnHold opportunity flag | `OpportunityPipelineTests.An_opportunity_on_hold_or_closed_never_flags_or_stalls` |
| Probe for F-R4-1: `Accrual.Create` with a Prepayment account as the accrued side | A probe test expecting a refusal failed: the accrual was created. Confirms F-R4-1. The probe was not kept. |

## Verdicts

| Story | Recommended trailer |
|---|---|
| KAFF-609b | VERIFIED verdict=CONDITIONAL (609a, the draft route, not built) |
| KAFF-309r | VERIFIED verdict=CONDITIONAL (AC4, AC5 held; F-R2-1 test gap) |
| KAFF-313, KAFF-612r | No change from this pass (only RulingNotes added) |
| R3-01 | VERIFIED verdict=PASS |
| R3-02 | VERIFIED verdict=CONDITIONAL (F-R3-1 question; AC-D held) |
| R3-03 | VERIFIED verdict=PASS |
| R3-04 | VERIFIED verdict=PASS |
| KAFF-700 | VERIFIED verdict=CONDITIONAL (F-R4-2; no purchase posting) |
| KAFF-701 | BUILT verdict=REJECTED (AC1 NOT MET: F-R4-1) |
| KAFF-704 | VERIFIED verdict=PASS |
| KAFF-707 | No change (only AC5 built) |
| KAFF-708 | VERIFIED verdict=PASS |
| KAFF-709 | VERIFIED verdict=CONDITIONAL (AC2 held) |
| KAFF-810 | VERIFIED verdict=CONDITIONAL (AC-E held) |
| KAFF-811 | VERIFIED verdict=CONDITIONAL (AC-E NOT MET, low weight) |
| R5-A | VERIFIED verdict=PASS |
| R5-B | BUILT verdict=REJECTED (AC-B-3 NOT MET: F-R5-1) |
| R5-C | BUILT verdict=REJECTED (AC-C-4 NOT MET: F-R5-2) |
| KAFF-812 change-order half | VERIFIED verdict=PASS for the half; design half not built |

## Not reached

- Screens: out of scope; none built.
- KAFF-313 AC1 and AC4, KAFF-612r, KAFF-309r AC2 and R5-A AC-A-1: pre-existing behaviour, not re-verified.
- No full suite run (by instruction); no rebuild of the shared Release binaries, so the Api test evidence is from the 11:11 build (see the caveat at the top).
- Concurrency findings (F-R4-2, the R3 lost update) were reasoned from the code, not reproduced.
- Staging: not checked.

## Re-verification at 95780b4 - 2026-11-24

Verifier: separate session, model Opus 5.5 (claude-opus-5-5), the strongest model. Did not write the fixes in 87d8ba0.
Scope: only F-R4-1 (KAFF-701 AC1), F-R5-1 (R5-B AC-B-3, against spec.md section 15), F-R5-2 (R5-C AC-C-4), F-R4-2, F-R2-1, and F-R3-1 per Nabil's ruling of 2026-11-24 (next stage stops at Negotiation; Contract only by conversion).
Binaries: tests/*/bin/Release built at 12:16-12:17 from HEAD 95780b4.

Baseline, targeted classes only: Domain `AccountingTests` + `OpportunityPipelineTests` 57 of 57 passed; Api `AccountingEndpointTests`, `PortalEndpointTests`, `MaterialAdvanceReadTests`, `OpportunityPipelineTests` and `DailyLogTests.An_uploaded_photo_is_stored_and_served_without_its_metadata` 67 of 67 passed.

Mutations run in a throwaway git worktree of 95780b4 under the session scratchpad; it is removed at the end. Nothing in D:\ERP was mutated.

### Mutation round 1 (all six went red)

| Mutation | Test that went red |
|---|---|
| `Accrual.Create` skips the new `AccountType.AccruedExpense` check | Domain `AccountingTests.An_accrual_cannot_credit_a_prepayment_account` |
| `Opportunity.AdvanceStage` stops at `Contract` again | Domain `OpportunityPipelineTests.The_stage_moves_forward_one_step_at_a_time_up_to_Negotiation_and_no_further`; Api `OpportunityPipelineTests.Advance_moves_one_stage_and_stops_at_Negotiation` (5th advance answered 204, expected 409) |
| Portal paid counts `ClientCollection` only, not `ClientAdvanceReceipt` | `PortalEndpointTests.The_home_shows_progress_paid_and_remaining_as_two_decimal_strings_and_no_cost_field` and `Paid_follows_the_section_15_worked_example_after_extract_2` |
| `UploadPhoto` stores the raw bytes (no `SitePhotoMetadata.Strip`) | `DailyLogTests.An_uploaded_photo_is_stored_and_served_without_its_metadata` |
| تشوينات read ignores the reversal sign | `MaterialAdvanceReadTests.A_reversal_of_the_issue_subtracts_from_what_was_issued` |
| The application check for a second waiting accounting request is skipped | `AccountingEndpointTests.F_R4_2_...` (second POST answered 500 from the unique index, expected 409). `AC_700_A` also went red as a knock-on: the failed test left a request waiting in the shared class database. |

### Mutation rounds 2 and 3

| Mutation | Result |
|---|---|
| Remove the `CreateIndex` for `ux_accounting_requests_waiting_subject` from the migration only | No test went red: `F_R4_2_...` passed and `FreshDatabaseBootstrapTests` 8 of 8 passed. Test databases are built by `EnsureCreated` from the model (PostgresDatabase.cs:168), so the test proves the model's index, not the migration's. The migration was read and creates the same index and filter; the bootstrap test applies it cleanly at HEAD (8 of 8). |
| Remove the `HasIndex(...).IsUnique().HasFilter(...)` from `AccountingConfigurations` | `F_R4_2_...` went red: "Expected a DbUpdateException ... but no exception was thrown." |
| Drop `ProjectScope.FromRoute()` from `GetMaterialAdvance` | Fails closed: every caller got 403, so the four positive tests went red and the stranger test stayed green. Not a proof for the stranger test. |
| `ProjectAccessPolicy.AssignedAccessAsync` admits a user with no assignment | `MaterialAdvanceReadTests.A_site_engineer_not_assigned_to_the_project_is_refused` went red (200, expected 403). |
| Portal paid ignores the reversal sign (`leg.Amount` for every leg) | **No test went red.** "Net of reversals" on the portal paid figure is untested. |
| PNG strip keeps `tEXt` chunks | `DailyLogTests.An_uploaded_photo_is_stored_and_served_without_its_metadata` went red. |

### Probes of the photo stripper (throwaway test class, not kept)

`SitePhotoMetadata.Strip` was called directly on hand-built files.

| Probe | Result |
|---|---|
| JPEG: APP0, then an APP1 holding XMP with a GPS tag | Stripped. |
| WebP: VP8X, then an `EXIF` chunk (correctly padded) | Stripped. |
| JPEG with a second image appended after the first EOI (the MPF / Ultra HDR layout: secondary images follow the primary EOI), the second carrying an APP1 Exif GPS segment | **GPS kept.** The parser stops at the first SOS and copies everything up to the last EOI, so a secondary image keeps its own metadata. |
| JPEG with one 0xFF fill byte before the APP1 Exif segment (fill bytes are legal before a marker, ITU T.81 B.1.1.2) | **GPS kept.** The loop reads marker 0xFF, the length check fails, and the unparsed tail is copied unchanged. |

The stripper fails open by design ("A file that does not parse to the end keeps its unread tail unchanged"). Photos stored before 87d8ba0 were never stripped and the portal streams stored bytes as they are; no backfill exists.

### Verdicts per finding

**F-R4-1, KAFF-701 AC1: MET.** `Accrual.Create` refuses any accrued side that is not `AccountType.AccruedExpense` (src/Domain/Accounting/Accrual.cs:83-86). `Accrual.Create` is the only producer of `PostingType.ExpenseAccrual`. The new Domain test goes red when the check is removed. KAFF-701 now has AC1 to AC4 MET; the F-R4-2 note on AC3 (concurrent double release) is closed below. Recommend **VERIFIED verdict=PASS**. The link from a release to the real cost posting is still a PROPOSAL and not built; it is not an AC.

**F-R5-1, R5-B AC-B-3: MET.** Paid is now every `ClientCollection` and `ClientAdvanceReceipt` posting on the project, with a reversal (same type, `ReversesId` set, confirmed in `Posting.Reverse`) subtracted (src/Api/Features/Portal/GetProject/Handler.cs:39-46). This is the same set `ProofOfNumbers` uses for client cash in. The hold collected at handover is posted as `ClientCollection` (Closure/CollectReleasedHold), so it is counted. Test `Paid_follows_the_section_15_worked_example_after_extract_2` asserts 610,000 paid and 390,000 remaining, the spec.md section 15 figures, and goes red when the advance is left out. Gaps, not blockers: (1) no test proves "net of reversals" on this figure (mutation stayed green); (2) `TaxWithheldAtSource` is not counted as paid, so on a contract with withholding the remaining figure never reaches zero. The AC says "cash received", so this is a question for R5-Q8 (the remaining formula), not a defect. R5-B now: B-1, B-2, B-3, B-4, B-6 MET; B-5 MET by construction, UNTESTED; B-7 HELD. Recommend **VERIFIED verdict=CONDITIONAL** (B-5 untested, B-7 held, reversal test gap).

**F-R5-2, R5-C AC-C-4: MET for the ordinary phone file, with proven residual leak paths.** Every upload now passes through `SitePhotoMetadata.Strip` before it is stored (UploadPhoto/Handler.cs:72); the portal streams the stored file, and the upload handler is the only writer to `ISitePhotoStore`. JPEG APP1 (EXIF and XMP), other APPn except APP0, APP14 and the ICC profile, COM, the PNG text, time and eXIf chunks, and the WebP EXIF and XMP chunks are removed. The test fails when the strip is removed or when PNG keeps `tEXt`. Not covered: a JPEG with appended secondary images, and a JPEG with fill bytes before a metadata segment, both proven to keep GPS (probes above); photos uploaded before 87d8ba0; WebP has no test. Recommend **VERIFIED verdict=CONDITIONAL** (C-3 held; condition: the multi-image JPEG leak is closed or Nabil accepts it, and stored pre-fix photos are stripped or confirmed absent). If multi-image JPEGs from site phones are expected, treat AC-C-4 as NOT MET and return R5-C.

**F-R4-2: closed for duplicate waiting requests.** A second waiting request of the same kind and subject (`depreciate:{month}`, `amortise:{project}:{month}`, `release:{accrual}`) answers 409 `errors.accounting.request_already_waiting`, and the partial unique index `ux_accounting_requests_waiting_subject` (filter `status = 'Requested' AND subject_key IS NOT NULL`) refuses it at the database. With only one request per subject waiting, two concurrent approvals for the same month or release cannot both happen. Both halves are proven by mutation. Notes: (1) the test proves the model's index, not the migration's (test databases come from `EnsureCreated`); the migration was read and matches, and applies cleanly in `FreshDatabaseBootstrapTests`; (2) two simultaneous submits lose the race at the index and the loser gets 500, not 409; nothing posts, so this is cosmetic. Accrual creation has no subject key, which is correct: each accrual is new. KAFF-700's F-R4-2 condition is lifted; its "no purchase posting" condition stands.

**F-R2-1: closed.** `A_site_engineer_not_assigned_to_the_project_is_refused` goes red when the shared assignment gate admits an unassigned user; `A_reversal_of_the_issue_subtracts_from_what_was_issued` goes red when the reversal sign is dropped. The misnamed `The_junior_hr_and_an_unassigned_owner_style_caller_are_refused` (asserts the Owner gets 200) was not renamed; cosmetic. KAFF-309r stays **VERIFIED verdict=CONDITIONAL** for AC4 and AC5 held, the test gap is gone.

**F-R3-1, R3-02 AC-A: MET per Nabil's ruling of 2026-11-24.** `AdvanceStage` refuses at `Negotiation` with `AlreadyAtLastStage` (409); the Domain and Api tests assert four moves and a refused fifth and go red when the stop is moved back to `Contract`. Nothing in src now sets `OpportunityStage.Contract`; conversion (R3-09) is not built, so Contract is unreachable today, which matches the ruling. Minor finding F-RV-1: an opportunity saved at `Contract` before this fix (on staging, through the old rule) is not refused by the new check, because the check is `Stage == Negotiation`; `Stage++` would move it to the undefined value 7. `Stage >= OpportunityStage.Negotiation` would close it. Reasoned from the code, not reproduced. R3-02 recommend **VERIFIED verdict=CONDITIONAL** (AC-D held; F-RV-1 minor). The ruling is not yet recorded in decisions-index.md (no entry found for it).

Never-break rules on 87d8ba0: no float or double; paid is summed from postings, no stored figure; no posting update or delete path; the new `subject_key` column is not a balance; separation of duties on accounting requests is unchanged; no hold debit. Clean.

### Recommended trailers after this pass

| Story | Recommended trailer |
|---|---|
| KAFF-701 | VERIFIED verdict=PASS |
| R5-B | VERIFIED verdict=CONDITIONAL (B-5 untested, B-7 held, reversal test gap on paid) |
| R5-C | VERIFIED verdict=CONDITIONAL (C-3 held; multi-image JPEG and fill-byte leak paths, pre-fix photos) |
| KAFF-700 | VERIFIED verdict=CONDITIONAL (no purchase posting; F-R4-2 lifted) |
| KAFF-309r | VERIFIED verdict=CONDITIONAL (AC4, AC5 held) |
| R3-02 | VERIFIED verdict=CONDITIONAL (AC-D held; F-RV-1 minor) |

Not reached in this pass: staging; WebP and real camera files through the upload route; a concurrent-submit reproduction for F-R4-2; the trial balance CSV change in 87d8ba0 (out of scope, not judged).
