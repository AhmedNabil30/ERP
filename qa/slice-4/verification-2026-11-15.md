# Verification — slice 4 project setup (KAFF-400, KAFF-418, KAFF-419, KAFF-420) · 2026-11-15

**Verifier:** fresh session, did not write the code. **Model:** Opus 5.5 (`claude-opus-5-5`), the strongest model, not a downgrade.
**Tree:** `ea26899` on `main`, clean at start.
**Method:** acceptance criteria read against `spec.md` §2, §4.4, §4.5, §5, §5.1, §9, §13, §15 and decisions D-217, D-218, D-220, D-221, D-222, D-223. Code read only to establish behaviour. Tests counted only where they could fail; mutation checks noted where run.

Findings are written as they are reached. Sections may be incomplete if the pass stops.

## Finding F-1 (High, money visibility) — the junior site engineer is sent the contract value

**Criterion:** KAFF-420 AC-5, "The API does not send him the money figures." D-217 item 37.

**Evidence, run on the live test database, not inferred.** A temporary probe test was appended to
`tests/Api.Tests/Features/Projects/ProjectFrameTests.cs`, run with `dotnet test --filter-method "*VerifierProbe*"`, then
removed with `git checkout`. The raw body of `GET /api/projects/{id}` for the junior site engineer (assignment level Junior) was:

```
"contractValue":"1000000.0000","holdRate":"0.200000","advanceRate":"0.250000", ...
"originalContractValue":"1000000.0000","approvedChangeOrderTotal":"0", ...
"tabs":{"overview":true,"boq":false,"dailyLog":true,"extracts":false,"team":true}
```

Only the `money` block (billed, collected, outstanding) is withheld. `src/Api/Features/Projects/GetProject/Handler.cs:86-92`
fills `ContractValue`, `HoldRate`, `AdvanceRate`, `OriginalContractValue` and `ApprovedChangeOrderTotal` for every
`ProjectRead` holder; only line 102 is gated by `ProjectMoneyRead`.

The code contradicts itself: `GetProgress/Handler.cs:84` (`Visible(...)`) and `GetProgress/Response.cs:37` treat
`ContractValue` as money and withhold it from the same caller. The overview screen also treats it as money: the hero
figure at `project-workspace-page.html:276-283` only renders inside `@if (money(); as m)`. So the screen hides it, and
the API sends it anyway, which is exactly what AC-5's second sentence forbids.

The same leak is on `GET /api/projects` (`ListProjects/Response.cs`, `ProjectSummary.ContractValue`,
`OriginalContractValue`, `ApprovedChangeOrderTotal`): every site engineer assigned to a project receives its contract value,
and `project-list-page.html:86-87` renders it.

**Why the tests did not catch it.** `ProjectFrameTests.The_junior_and_marketing_never_receive_the_money_block` asserts only
that the `money` key and the strings `billedToDate`, `collectedToDate`, `outstanding` are absent. It would stay green with the
contract value in the payload, and it does.

**Verdict on KAFF-420 AC-5: NOT MET** (API half). The screen half (no money figure drawn) holds for a signed Lump Sum project;
see F-5 for the Design and unsigned branches.

## Finding F-2 (Medium, wrong label on a money term) — the overview calls the signing advance the recovery rate

**Criterion:** D-223 item 6 ("the advance taken at signing and the advance recovered from each extract are two different
rates; the sign sheet must say which one it sets, and the server must hold both"), D-223 item 5. Brief known item (3).

- The server holds both: `Project.AdvanceRate` and `Project.AdvanceRecoveryRate` (`src/Domain/Projects/Project.cs:157,161`).
- The sign sheet's advance field is `boq.sign.field.advance` = «نسبة الدفعة المقدمة (%)» (`boq-page.html:290`). It is sent as
  `clientAdvance` (`boq-page.ts:527`) and lands on `AdvanceRate` (`SignProject/Handler.cs:84-99`); the recovery rate is
  left at its existing value or the 25% default (`SignProject/Handler.cs:99`). The label names the advance, not «عند التوقيع»;
  it does not say in words which of the two rates it sets. Acceptable reading, but D-223 item 6 asks for it to say so.
- **The contract terms on the overview are wrong.** `project-workspace-page.html` renders `p.advanceRate` (the signing
  advance, from `GET /api/projects/{id}` `advanceRate`) under `projects.workspace.vocab.fact.advance_rate` =
  «نسبة استرداد الدفعة المقدمة», the recovery rate. The recovery rate itself (`AdvanceRecoveryRate`) is not on
  `GetProject/Response.cs` at all, so no screen in this slice can show it.
- Net effect: a user who types 30 in the sign sheet sees «نسبة استرداد الدفعة المقدمة 30%» on the overview while every extract
  recovers at 25%.

Hold label: «نسبة الحجز» on both the sign sheet (`boq.sign.field.hold` = «نسبة الحجز (%)») and the terms
(`projects.workspace.vocab.fact.hold_rate`). D-223 item 5 is MET.

Minor: `clientAdvance` crosses the wire as a JSON number (`boq-page.ts:527`, `Number(...)`), and `SignProject.Request.ClientAdvance`
is a bare `decimal` in percent units while `HoldPercentage` is a `Percentage` fraction string (D-151). It is a rate, not money, so
it is not a breach of the money rule, but the two rate fields on one request use two units and two wire types.

---

## KAFF-400 · Projects API — create, list, read

| AC | Verdict | Evidence |
|---|---|---|
| AC-400-A vertical slices | MET | `Projects/CreateProject` has Endpoint, Handler, Request, Response, Validator. `GetProject`, `ListProjects` have no Request/Validator, the same as `GetClient`/`ListClients` (a GET with no body). No repository, no MediatR; handlers use `KaffDbContext` directly. |
| AC-400-B permission test per role | MET with a recorded deviation | Site engineer with no assignment: `GetProjectTests.A_site_engineer_with_no_assignment_is_refused` (403). HR half deviates from the story text: HR is refused (`ListProjectsTests.Hr_is_refused_and_must_use_its_own_project_dashboard`) instead of receiving a redacted list; HR's financial-free list is `GET /api/auth/me` (KAFF-105b, D-103). The build note flags this for Karim/Nabil; I found no decision that closes it. Refusing is safer than the story asked, so this is not a money or permission breach, but the criterion as written is not what was built. Create is refused to Marketing, Finance, HR and site engineers (`CreateProjectTests.Marketing_finance_hr_and_site_engineers_are_refused`). |
| AC-400-C account set opens with the project | MET | `CreateProject/Handler.cs` adds project and accounts before one `SaveChangesAsync`. `CreateProjectTests.Creating_a_lump_sum_project_opens_its_five_account_set` reads them back and fails if any is missing. |
| AC-400-D hold defaults to 20, precision | MET, letter differs | Default 20%: `CreateProjectTests.HoldPercentage_defaults_to_20_when_omitted` asserts wire `"0.2"` and stored 20 (would fail on 5% or none). Precision is explicit but is `decimal(18,6)` through the `Percentage` convention (`KaffDbContext.cs:166-168`), not `(18,4)`. Finer than asked and explicit, so no truncation risk. |
| AC-400-E money as JSON string | MET | `CreateProjectTests.Money_and_rate_fields_serialise_as_json_strings` checks `holdRate`, `advanceRate` raw kind. `contractValue` is null on create (derived), so the string assertion on money is carried by other suites; the probe in F-1 shows `contractValue` as `"1000000.0000"`, a string. |
| AC-400-F audit on create | MET | `CreateProjectTests.Creating_a_project_leaves_a_created_audit_record` asserts the `Created` record with actor and role. Written by `AuditSaveChangesInterceptor`, one mechanism. |
| AC-400-G build and suites green | MET (quoted) | Gates measured on `ce73181`+copy by `gate-runner`: build 0/0, Domain 727/727, Api 1024 pass 2 skip. Not rerun whole; the tests I rely on ran green in this pass where noted. |
| AC-400-H no migration | MET at the time; superseded | No migration in KAFF-400. Later stories added `BoqBases` migration and guard script `004_project_name.sql` with written reasons (D-220 item 7, D-221 item 1). |

**Contract value is never typed:** `CreateProject.Request` has no contract value member; `CreateProjectTests.A_typed_contract_value_is_ignored_and_a_new_lump_sum_project_has_none_until_it_is_signed` sends one and asserts it is null in the response and the row. MET.

**Not in the story, found here:** `ListProjects` sends `contractValue` to every assigned role including junior site engineers (see F-1).

---

## KAFF-418 · BOQ builder

Tests relied on, run in this pass (with `ProjectCreateRulesTests`, `ProjectFrameTests`, `BoqAndSignatureTests`,
`CreateProjectTests`, `ListProjectsTests`, `GetProjectTests`): 94 of 94 green, the only failure being my own probe from F-1,
since removed and the test project rebuilt (`grep -c VerifierProbe` on the dll = 0).

| AC | Verdict | Evidence |
|---|---|---|
| AC-1 named base, no rates/hold/advance | MET (API); screen absent | `BoqBase`/`BoqBaseLine` hold item key and quantity only (D-220 item 1). `BoqBuilderTests.AC_418_A_the_technical_office_prepares_renames_edits_and_archives_a_base_and_it_holds_no_rate`, `AC_418_A_a_base_needs_a_name...`. **No web screen calls `POST /api/boq-bases`**; the only web use is `GET api/boq-bases` (`core/boqs/boqs.api.ts:131`). D-217 "what this needs" names "a base-BOQ entity **and a screen to prepare one**". In the running app the Technical Office cannot prepare a base, so the start sheet's «مقايسات جاهزة» group is always empty. See F-3. |
| AC-2 everyone who may build lists bases | MET | `BoqBaseRead` = Owner, Technical Office (D-220 item 2). `AC_418_B_everyone_who_may_build_a_boq_lists_and_reads_bases_and_nobody_else_does` checks two TO users and the owner read, Finance and site engineer refused on all five routes. |
| AC-3 fill from base or earlier project, today's rates, no hold/advance | MET | `CopyBoq/Handler.cs`: base lines priced from today's catalogue; earlier project matched by item code; hold/advance never read. `AC_418_C_a_base_fills_an_empty_boq_with_todays_rates_and_leaves_the_hold_and_advance_alone` reprices before copy and asserts 1600 and unchanged rates; `AC_418_C_an_earlier_project_fills...`, `AC_418_C_a_source_project_the_caller_is_not_assigned_to_is_refused`, `..._already_has_lines_is_refused`, `..._archived_is_skipped_and_named`. |
| AC-4 several items in one request | MET | `POST .../boq/lines/batch`; `AC_418_D_several_catalogue_items_are_added_in_one_request`, `AC_418_D_one_bad_item_in_a_batch_stores_nothing`, `AC_418_D_the_batch_route_and_the_copy_route_refuse_the_wrong_role_and_the_unassigned`. |
| AC-5 one quantity, surveyed replaces estimated | MET | `AC_418_E_...` totals 4 estimated + 2 surveyed (not 10 estimated) = 10,350 and signs at that value; would fail if estimated were used. UI: one box per line, `boq-page.ts:349-358` writes to surveyed when present. |
| AC-6 no-quantity line blocks signing, refusal names it | MET | `AC_418_F_...` asserts 409, `lineId`, `position`, `descriptionAr`, nothing signed or frozen. UI disables the sign button and shows the missing count (`boq-page.ts:142`, `boq-page.html` `boq-missing`). |
| AC-7 custom item ordinary line, flagged, no catalogue write | MET | `AC_418_G_...` edits, removes, asserts `pendingCatalogueReview` true and no catalogue row. `BoqAndSignatureTests.A_custom_line_is_flagged_for_review_and_the_catalogue_is_not_written`. |
| AC-8 sign while custom item waits | MET | Same test signs with the flag still true. |
| AC-9 hold and advance required at signature, start empty | MET | Server: `AC_418_H_...` refuses `{}`, hold only, advance only with `errors.project.signing_terms_required`. UI: `hold`/`advance` signals start `''` and are reset on open (`boq-page.ts:212-213, 503-504`). |
| AC-10 archived BOQ read-only | MET | `AC_418_I_...` refuses add, batch, update, remove, copy after 31 days, BOQ still readable. UI `editable()` false when archived (`boq-page.ts:136-138`). |
| AC-11 one main button, one sheet, order | MET | `boq-page.html` empty state: one `boq-start` «ابدأ المقايسة»; sheet order blank, `boq.start.bases`, `boq.start.projects`. The earlier-projects list is every listed Lump Sum project, including ones with an empty BOQ, which the server then refuses with `CopySourceEmpty` (Low, F-6). |
| AC-12 remove without confirm, rate on every line, no condition/markup control | MET | `kaff-boq-line.html`: remove button emits directly; `rt` rate always rendered; no condition or markup input. `boq-page.ts:361-366`. |
| AC-13 «توقيع المقايسة» on button, sheet title, confirm; paper «عرض سعر» | MET | `boq.sign.title` and `boq.sign.confirm` both «توقيع المقايسة»; button uses `boq.sign.title`; `boq.print.title` «عرض سعر». |
| AC-14 no review mark on a custom line | MET | `kaff-boq-line` `pending` is the no-quantity state only (`kaff-boq-line.ts:46`); no custom/review tag in the template. |
| AC-15 project frame above | MET | `boq-page.html:2` wraps in `<kaff-project-frame current="boq">`. |
| AC-16 freeze by copy, no FK | MET | `BoqLine` has no catalogue key (`Domain/Boqs/BoqLine.cs:75-123`). `BoqAndSignatureTests.The_boq_lines_carry_no_foreign_key_to_the_catalogue_or_to_the_babs`, `Editing_the_catalogue_after_signing_leaves_the_signed_boq_unchanged`, database trigger test `The_database_refuses_to_change_insert_or_delete_a_frozen_line_whatever_the_path`. The copy test reprices after copy and asserts the line kept 1600. |

Measured quantity word: `boq.qty.surveyed` = «مقاسة» (D-223 item 7). MET.

Known item (2): `boq.sign.lead` text is verbatim in the approved mock `.design/v2/team-a/boq.html:560`. It is a warning before an
irreversible act. It does conflict in spirit with the "screens explain themselves" rule; a question for Nabil, not a defect.

Hold label in the mock is «نسبة المحجوز»; the build follows D-223 item 5 («نسبة الحجز»). Correct, the ruling wins.

### Finding F-3 (Medium, capability unreachable) — no screen to prepare a base BOQ

`POST/PUT /api/boq-bases` and archive exist and are tested, but nothing in `src/Web` calls them. D-217 item 19 and its "what this
needs" list ask for the screen. Either a story for it is missing from the board, or AC-1 was meant to include it. The Scrum Master
should decide which; as built, the start sheet can only offer a blank BOQ or an earlier project.

---

## KAFF-419 · Project create

| AC | Verdict | Evidence |
|---|---|---|
| AC-1 no type preselected, request without type refused | MET | `ContractType` has no 0 member; `Validator` refuses undefined with `errors.project.contract_type_required`. `ProjectCreateRulesTests.A_request_with_no_contract_type_is_refused_and_nothing_is_created` (asserts no row), `A_contract_type_that_is_not_one_of_the_three_is_refused`. Web: `contractType = signal('')` (`project-create-page.ts:69`); the 390 px screenshot shows three unselected radios with no hint. |
| AC-2 same name same client refused with the exact text; other client accepted | MET | Handler check plus unique index `ux_projects_client_name` (`004_project_name.sql`). Tests: trimmed and case variants refused 409 `errors.project.name_taken_for_client`; another client accepted; the database refuses directly (`The_database_itself_refuses...`). Arabic: `errors.project.name_taken_for_client` = «يوجد مشروع بهذا الاسم لهذا العميل», verbatim. |
| AC-3 quick-add under ProjectCreate; full client screen keeps ClientManage | MET | `POST /api/clients/quick` gated `ProjectCreate`; reuses `CreateClient.Handler`. Tests: owner and TO allowed; Marketing, Finance, site engineer refused on add and phone-check; TO refused on `POST /api/clients`. |
| AC-4 duplicate phone warns and allows | MET | `A_phone_that_belongs_to_another_client_warns_and_then_allows`: phone-check returns the match, unacknowledged 409, acknowledged 201. Web: `kaff-duplicate-phone-warning` with `[showCode]="false"`, save disabled until acknowledged. |
| AC-5 Finance assigned at creation, as a RulingNote | MET | `CreateProject/Handler.cs` adds a Standard assignment for each active Finance user in the same save. `Every_active_finance_user_is_assigned_to_a_new_project_and_an_inactive_one_is_not`, `The_finance_staffing_is_registered_as_an_assumed_ruling`. |
| AC-6 follows the mock, quick-add is a bottom sheet at 390 px | MET for the page; quick-add sheet UNTESTED | `driver.mjs layout` at 390 px on `/projects/new`: PASS, dir rtl, overflow 0, no small targets, no raw keys, no ids. Screenshot read: name, client combobox, three type cards, full-width submit. The quick-add sheet was not opened in the browser in this pass; it is a `kaff-detail-panel`, the same part the BOQ sheets use. |
| AC-7 audit on create and quick-add | MET | `Creating_a_project_and_quick_adding_a_client_each_write_an_audit_record_naming_the_actor`. |

Client picker (D-222): `GET /api/clients/picker` under `ProjectCreate`, id and name only, archived excluded, refused without
`ProjectCreate`. Tested.

---

## KAFF-420 · Project frame and overview

| AC | Verdict | Evidence |
|---|---|---|
| AC-1 one frame above overview, BOQ, daily log, crew, extracts | **NOT MET** | `kaff-project-frame` is one kit part (`shared/kaff-project-frame`), but only `project-workspace-page.html` and `boq-page.html` use it (grep of `<kaff-project-frame` in `src/Web`). Routes `projects/:projectId/day-labour`, `/daily-log` and `/extracts` (`app.routes.ts:250, 271, 278`) render without it, so the user leaves the project frame to reach three of the five tabs (D-218 item 1). The daily log and crew screens belong to KAFF-614/615, still `COMMITTED`; the extracts screen exists and has no frame. The Scrum Master can rescope the two to KAFF-614/615, but as written this criterion is not met. |
| AC-2 stage line has three stages; a stage not reached shows as coming, no sentence, no lock | MET | `Frame.cs` `ProjectStage` has three values. The frame template draws a check for passed stages and nothing else; the 390 px screenshot shows «التسليم» as an empty circle, no lock, no sentence. |
| AC-3 tabs by role, absent not hidden, server refuses | MET | `GetProject/Handler.cs` `TabsAsync` derives tabs from the permissions gating each route. `ProjectFrameTests.Each_role_is_sent_exactly_the_tabs_it_may_open` (six roles), `The_junior_is_refused_the_boq_and_the_extract_routes` (403 on `/boq`, `/extracts`, `/waiting-on`), `An_unassigned_engineer_is_refused_the_frame_and_the_boq`. Signed in as the junior (`site_eng_demo`) on the demo project, the frame shows نظرة عامة, اليومية, الفريق only. |
| AC-4 owner and supervisor overview: percent, أبواب, three money figures, آخر يومية | MET | Owner screenshot at 390 px shows percent, contract value, executed, certified, أبواب; the template renders billed, collected, outstanding (`workspace-money`) and the last log. The server sends the money block to owner and supervisor (`Owner_supervisor_finance_and_technical_office_receive_the_three_money_figures`). |
| AC-5 junior: percent, أبواب, آخر يومية, no money, no extract line, no closing group; the API does not send him money | **NOT MET** | Screen half holds: the signed-in junior's text at 390 px is percent, أبواب, آخر يومية, no money figure. API half fails, see F-1: `GET /api/projects/{id}` sends him `contractValue`, `originalContractValue`, `approvedChangeOrderTotal`, `holdRate`, `advanceRate`. |
| AC-6 unsigned: overview, BOQ, team; main action opens the BOQ | MET | `kaff-project-frame.ts:40-52` hides the daily log and extracts before signing. `project-workspace-page.spec.ts:399` asserts tabs `['overview','boq','team']` and the slot button `open_boq`. |
| AC-7 no code or id on screen | MET | `driver.mjs layout` at 390 px on the overview and the BOQ: `ids: []`; no code in the frame template or in the junior's text. Outside this story, the project list still shows the project code (`project-list-page.html:62`). |

Known item (1): `projects.frame.money.failed` «تعذّر تحميل بيانات المال.» exists in `ar.json` and no template uses it. The
approved mock draws a «فشل جزء المال» state (`.design/v2/team-a/project.html:343, 488`). In the build the money block arrives
inside `GET /api/projects/{id}`, so it cannot fail on its own: the state is unreachable, not missing. Dead key, and a mock
state the architecture made moot. Low.

### Finding F-4 (Low, copy) — «العقد» survives on three keys after D-223 item 4

D-223 item 4: "The screens say «التعاقد» everywhere." Still «العقد»: `projects.workspace.progress.done` «المنجز من العقد»
(on the overview hero for every role, including the junior), `projects.workspace.date.signed` «تاريخ توقيع العقد», and
`projects.frame.terms` «شروط العقد».

### Finding F-5 (Low, money on screen) — a signed Design project shows its contract value to every role

The Design branch of `project-workspace-page.html` (`@else if (p.contractType === 'Design')`) renders `p.contractValue`
outside any `money()` guard. Fixing F-1 on the server closes this too; noted so the fix covers it.

### Finding F-6 (Low) — the start sheet offers earlier projects with an empty BOQ

`boq-page.ts:427` lists every listed Lump Sum project except the current one. A project with no BOQ lines is offered, and the
server then refuses it with `CopySourceEmpty`.

---

## Prohibitions checked on every pass

| Rule | Result |
|---|---|
| No float or double near money | Holds. The only `double` in scope is `Project.SiteLatitude/SiteLongitude` (coordinates). Money is `Money` with the `(18,4)` convention; rates are `Percentage` `(18,6)`; bare decimals `(18,4)` (`KaffDbContext.cs:162-172`). The web sends the signing advance as a JS number (a rate in percent units, not money), see F-2. |
| No stored balance | Holds. Overview money is summed from extracts and collections on read (`GetProject/Handler.cs` `MoneyAsync`); the contract value is the BOQ total plus approved change orders, summed on every read. |
| No posting update or delete path | None added. No slice-4 handler read touches a posting. |
| Hold never debited before handover | Not touched by these stories. The hold rate is set at signing only. |
| Role and assignment server-side | Holds on every route read: project-scoped `RequirePermission(..., ProjectScope.FromRoute())` on BOQ, sign, read, progress and team; company-wide rows for create, list, picker, quick-add and bases (no project to scope to). Copying from an earlier project checks assignment on the source in the handler. The list filters by assignment for every non-owner. |
| Nobody creates and approves the same movement | Not exercised: no money movement is created or approved in these slices. Building and signing a BOQ by the same Technical Office user is a contract act, not a movement. |
| Audit on every state change | One interceptor; no entity in these slices is `IAuditExempt`. Tested for project create, quick-add client and signing (before and after). Bases, copy and batch add go through the same interceptor but have no dedicated test. |
| Signed BOQ frozen by copy | Holds, see KAFF-418 AC-16. |
| Contract value derived, never typed | Holds, see KAFF-400. |
| No hardcoded UI strings | Holds on the screens read; `layout` reported `rawKeys: []` on create, overview and BOQ at 390 px. |
| Arabic RTL at 390 px | `layout` PASS (dir rtl, overflow 0, no small targets) on `/projects/new`, the overview and the BOQ. Screenshots read. The tab strip scrolls inside itself at 390 px (the fourth tab is cut, «الم…»); that is the frame's design, not page overflow. |

## KAFF-415 and KAFF-417 since `553309c`

`git diff 553309c HEAD -- src/` touches code under them: `SignProject/Handler.cs` (+52; signing terms are now required for
Lump Sum, which an execution project made from a design meets at its own signature), `GetProject/*` (new frame fields), and
the archived guard on BOQ writes. `DesignToExecutionTests` and `FollowUpTests` were rerun in this pass: 19 of 19 green. No
reason found to reopen either.

## Verdicts recommended

| Story | Verdict | Conditions or reasons |
|---|---|---|
| KAFF-400 | VERIFIED verdict=CONDITIONAL | The AC-400-B HR half is built as a refusal, not the story's redacted list; Nabil or Karim must accept the deviation. The F-1 list leak (`ListProjects` sends the contract value to site engineers) should be fixed with KAFF-420. |
| KAFF-418 | VERIFIED verdict=CONDITIONAL | F-3: no screen to prepare a base; put it on the board or rule it out. F-2: the sign sheet label does not say which advance it sets (D-223 item 6). |
| KAFF-419 | VERIFIED verdict=CONDITIONAL | The quick-add bottom sheet at 390 px was not seen in the browser. |
| KAFF-420 | BUILT verdict=REJECTED | AC-5 NOT MET (F-1: the API sends the junior the contract value and rates). AC-1 NOT MET (the frame is on two of five screens). F-2: the overview labels the signing advance as the recovery rate. |

## Not reached in this pass

- The quick-add sheet opened in the browser at 390 px.
- The supervisor (`site_sup_demo`) signed in on the overview; the server side is covered by `ProjectFrameTests`.
- A full gate rerun; the gate figures are quoted from `gate-runner`.
- Mutating `src/` to prove the permission tests can fail: the brief forbids `src` edits. One probe in a test file was run and
  reverted (F-1).
