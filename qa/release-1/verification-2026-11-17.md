# Verification, release 1 changes since 9fb1c83 — 2026-11-17

Verifier: fresh session that wrote none of this code. Model: Opus 5.5 (claude-opus-5-5), the strongest model; not downgraded.
HEAD verified: `91cdcb3db210377bc5bc121ba0c0b9a21dce9061`. Range: `9fb1c83..91cdcb3` (25 commits).

Findings are written as they are reached. A section that ends abruptly is where the pass stopped.

## Gates (measured in this pass)

- `dotnet build KaffErp.sln -c Release`: exit 0, 0 warnings, 0 errors.
- `Kaff.Domain.Tests` (Release, `--no-build`): exit 0, **total: 738, failed: 0, succeeded: 738, skipped: 0**.

## End-to-end chains, as real requests against the running API (Release build of HEAD, port 5080)

Driven by a scratch Node script that signs in through `POST /api/auth/sign-in` and keeps the cookie, one session per
person. Demo accounts used: `tech_office_demo`, `owner_demo`, `site_eng_demo` (Junior), `site_sup_demo`, `accounts_demo`.
No demo account was changed. New rows were added (one client, three projects, five employees); nothing was deleted.
An attempt to create a throw-away HR user for chain C was refused by the session's permission layer, so HR's half of
chain C rests on the Api suite, not on a live request.

### Chain A, Lump Sum from creation to removal: ran clean at the API

| Step | Request | Result |
|---|---|---|
| Quick-add client as Technical Office | `POST /api/clients/quick` | 201 |
| Create Lump Sum | `POST /api/projects` | 201, `Setup` |
| Creator reads his own project | `GET /api/projects/{id}` | 200, stage `Boq`; team read lists him with origin `Creator` (D-227 item 1 holds) |
| Catalogue line, custom line | `POST .../boq/lines` x2 | 201, 201 |
| Sign without the recovery rate | `POST .../sign` hold + advance | 400 `errors.project.advance_recovery_rate_required` |
| Sign without the hold | `POST .../sign` advance + recovery | 400 `errors.project.signing_terms_required` |
| After both refusals | `GET` | still `Setup`, not signed |
| Sign with 15% / 12.5 / 0.3 | `POST .../sign` | 200, contract value 25,156.25 |
| Owner overview | `GET` | `Active`, stage `Execution`, `holdRate 0.15`, `advanceRate 0.125`, `advanceRecoveryRate 0.3`: stored exactly as typed, no default substituted; no `workStarted` key (false is omitted) |
| Engineer before assignment | `GET` | 403 |
| Owner adds engineer | `POST .../assignments` | 201 |
| Engineer list and read | `GET /api/projects`, `GET /{id}` | 200, present; no money key in either body |
| Check-in | `POST .../daily-logs/check-ins` | 201 |
| `workStarted` after the check-in alone | `GET` as Owner | `true`. D-228 item 4 says «جاري العمل» "from its first daily log entry"; a check-in is a `LogEntry`, so this reading is defensible, but it means a check-in with no work recorded flips the word. Recorded as a question, not a defect |
| Quantity, material (`bag`), photo (raw-body upload then record), crew hours 8 | four `POST`s | 201 each |
| Owner review of the day | `GET .../daily-logs?date=today` | 200, the engineer's log with the quantity |
| Owner, another day | `GET .../daily-logs?date=yesterday` | 200, `[]` |
| Owner write | `POST .../daily-logs/quantities` | 403 (read-only holds) |
| Owner crew read on the engineer route | `GET .../day-labour/crew` | 403. The web crew screen reads the reviewer's day through `listDayLogs`, not this route, so this is not a break |
| Owner removes engineer | `POST .../assignments/{id}/revoke` | 204 |
| Engineer after removal | `GET /{id}`, `GET /api/projects` | 403; project absent from his list |
| Added back | `POST .../assignments` | 201 (D-228 item 1, "can be added back") |

### Chain B, Design project: ran clean at the API

| Step | Result |
|---|---|
| Create Design as Technical Office | 201 `Setup` |
| Unsigned read | tabs `boq:false`, `dailyLog:false`, **`extracts:true`**; `conversionOffered:false`; `designRatePerSquareMetre "450"` to the Technical Office (a money role, see hard check 1) |
| `GET .../boq` | 404 `boq.not_found` |
| Convert before signing | 409 `errors.project.design_not_signed` |
| Sign with a hold | 400 `terms_do_not_match_contract_type` |
| Sign without area | 400 `errors.project.area_required` |
| Sign with area 120 and a smuggled `designRatePerSquareMetre: "1"` | 200, contract value **54,000 = 120 x 450**: the client cannot set the rate; the server's rate is used |
| Owner read after signing | `Active`, `conversionOffered:true`, rate `450.0000` |
| Convert | 201, execution project in `Setup` linked to the design |
| Convert again | 409 `errors.project.already_converted` |
| Execution team | converter (`Creator`) and both active Finance users (`Automatic`): D-227 and D-228's Finance defect are closed |
| Finance reads the execution project | 200 |
| Design after convert | `conversionOffered:false` |

The server sends `tabs.extracts: true` on an unsigned Design project, and after signing too. `spec.md` §5.3 says a Design project
has "no extract". The web frame hides the tab before signing; the server still offers the route. Preparing an extract on a
signed Design project as the Technical Office is refused 403 (no `ExtractPrepare`), and `PrepareExtract` would refuse
`boq.not_found` for any caller because a Design project has no BOQ. Low, pre-existing, not moved by these commits.

### Chain C, people: ran clean at the API for the Owner; HR not driven live

| Step | Result |
|---|---|
| Owner registers a salaried employee | 201 with department, job title, hired-on |
| Owner registers a worker | 201 |
| Second salaried on an **active** salaried phone, with and without acknowledgement | 409 `master.employee_phone_taken` both times (refuse) |
| Second worker on the same worker phone | 409 `duplicate_phone_not_acknowledged` (warn); phone-check names the match |
| Salaried list row | job title, department, hired-on; `timesEngaged` and `averageRating` null; no money member |
| Worker list row | باب names, `timesEngaged 0`, `averageRating null`; no rate member (D-232 item 4) |
| Archive, default list | 204, row gone from the list |
| Unarchive, unarchive again | 204, then 409 `master.not_archived`; row back on the list |
| Unarchive while another active salaried record now holds the phone | 409 `master.employee_phone_taken` |
| Site engineer: `GET /api/employees`, `?kind=DayLabour`, `/{id}`, `POST .../unarchive` | 403 on all four |
| Site engineer bodies | pool: no phone, no rate, no employee code; project read and list: no rate, value or total key; crew: worker id, name and trade only |

The site engineer's project read and project list still carry the **project** `code` (`PRJ-...`). It is not drawn on his
screens (see hard check 10); it is on the wire. The team read sends every caller each member's `userId` and `assignedAt`
(the type's own comment says `UserId` "is never shown"). Neither is a rate, a phone or an employee code.
- `Kaff.Api.Tests` (Release, `--no-build`, full suite): exit 0, **total: 1147, failed: 0, succeeded: 1145, skipped: 2**, 29 min 14 s. The two skips are the known `ClosureEndpointTests` product gaps (AC-802-M and the closed-project snag edits).
- `ng test --watch=false`: exit 0, **Test Files 56 passed (56), Tests 510 passed (510)**.
- `ng build --configuration production`: exit 0.

### Chains on the running app (Release API on 5080, dev server on 4200), 390 px RTL

`driver.mjs layout` PASS at 390 (and 1440 where run) with `overflowPx 0`, no small targets, no raw keys, no ids, on:
Owner `/projects`, the chain-A overview, BOQ, daily log, `/employees`, `/workers`; site engineer `/projects`, overview,
daily log, crew, team tab. Screens read or screenshotted:

- **Signed, no log yet (Owner)**: «لم تبدأ», stage line at التنفيذ, notice «لم يُسنَد مهندس الموقع بعد», three rates shown
  with «نسبة الخصم من كل مستخلص», «إضافة شخص» in the bottom bar beside «القائمة». Technical Office sees the same notice and
  no primary. Matches D-229 item 1.
- **«إضافة شخص»** opens the team tab with the add sheet; pressing «إضافة» with nobody chosen marks the field «اختر شخصاً»,
  the button is not greyed, the bar is hidden under the sheet.
- **Team tab (Owner)**: creator «أنشأ المشروع» with «إزالة», Finance «تلقائي» with no remove control, engineer with level
  «مبتدئ» and «إزالة». Matches `.design/v2/team-a/project-team.html` apart from the stage line (5 stages in the mock, 3
  built; the built line is what KAFF-420 AC-2 asks for).
- **Sign sheet (Technical Office)**: pressing «توقيع المقايسة» with a no-quantity line focuses and marks the line; with
  quantities complete it opens the sheet with three empty rate fields; pressing confirm empty marks all three «مطلوب»;
  the D-229 item 4 sentence is kept. Matches `boq.html` sign views.
- **Owner اليومية**: read-only, day switch plus «يوم آخر», the engineer's check-in (04:21 Cairo), quantity, material, photo
  and crew; the crew row reads «⁨1⁩ عمال · ⁨8⁩ ساعة». «أمس» then the crew row lands on the crew screen showing Monday 16
  November: the crew link keeps the day.
- **Status word**: chain A reads «جاري العمل» after the engineer's first entries. The **signed Design project** reads
  «جاري العمل» with no daily log at all: `shownStatus()` (`project-status-pill.ts`) exempts Design, citing the mock
  (`project.html` `ds` view), whose comment says "a design project has no daily log". D-228 item 4 has no such exception
  and the server sends the signed Design project `tabs.dailyLog: true`. The approved mock and the ruling disagree; a
  question for Nabil, not a build defect.
- **Unsigned execution project made by conversion**: «لم تبدأ», design link and area shown, BOQ and team tabs.
- Unsigned Design: overview and team only (no BOQ tab), «توقيع التعاقد» (from the mock; the frame hides extracts before signing).

Chains A, B and C therefore ran clean end to end on the running app for the parts driven. Not driven on screen: the
engineer pressing check-in with a real geolocation (the driver has none; done by API), and HR's screens.

## Hard checks

### 1. Permissions per role, by direct request on the chain-A project

| Caller | team | candidates | assign | revoke | employees | unarchive | project read |
|---|---|---|---|---|---|---|---|
| Owner (no row) | 200 | 200 | 201 | 204; Finance refused `identity.finance_cannot_be_removed` | 200 | 204 / 409 | 200 |
| Technical Office (creator) | 200 | 403 | 403 | 403 | 403 | 403 | 200 |
| Finance (`accounts_demo`, automatic) | 200 | 403 | 403 | 403 | 403 | 403 | 200 |
| Junior site engineer (assigned) | 200 | 403 | 403 | 403 | 403 | 403 | 200 |
| Supervisor site engineer, **not assigned** | 403 | 403 | 403 | 403 | 403 | 403 | 403 |
| HR | Api suite only: `AssignUserToProjectTests.Hr_cannot_staff_a_project_and_the_refusal_leaves_no_row`, `RevokeProjectAssignmentTests.Nobody_but_the_owner_can_revoke_an_assignment`, `PermissionMechanismTests.Only_the_owner_staffs_a_project_and_needs_no_assignment_to_do_it` |

Project read fields: `contractValue`, the three rates and `designRatePerSquareMetre` reach the Owner, Technical Office and
Finance and not the junior engineer (`ProjectMoneyRead`). `workStarted` and `conversionOffered` reach every `ProjectRead`
holder, junior included. Neither is money, and the brief's expectation that they go "only to money roles" is not in any
story or ruling; I record it as a wrong premise in the brief, not a defect. Photo routes: no file under
`src/Api/Features/DailyLogs` changed in the range.

### 3. State

- `Project.Sign` moves `Setup` to `Active` for all three types (`Project.cs:441`); a project already `Active` keeps its
  start date. Live: chain A and chain B both went `Setup>Active` with the audit row's before/after `Status` filled.
- Two refused signatures left chain A in `Setup`, unsigned.
- Postings newly reachable on a signed project: `PostingProjectStatePolicy` differs between `Setup` and `Active` only in
  `ExtractDerived`. A signed Lump Sum project can now have an extract prepared before any daily log; `spec.md` §5.1, §7 and
  §13 do not forbid that (an extract with no work certifies nothing). A Design project cannot reach an extract: no BOQ, so
  `PrepareExtract` refuses `boq.not_found`. Nothing `spec.md` forbids before work starts became reachable.

### 4. Audit rows, read from `audit_records` for the chain projects

Create: `Project Created` plus three `ProjectAssignment Created` (creator and two Finance) in the same second. Sign:
`Project Modified` with `AdvanceRate, AdvanceRecoveryRate, ContractValue, HoldRate, SignedOn, StartedOn, Status`,
before `Setup` after `Active` (Lump Sum) and `AreaSquareMetres, ContractValue, DesignRatePerSquareMetre, ...` (Design).
Assign: `Created`, actor Owner. Revoke: `Modified [RevokedAt, RevokedByUserId]`, actor Owner. Convert: the execution
project's `Created` plus three `ProjectAssignment Created`. Archive and unarchive: `Employee Modified [IsActive]` each.
All present.

### 10. Codes drawn on screen (reported, not judged)

The project list draws no code (`project-list-page.html:57`). Codes are still drawn by: the landing page
`refCodeAndName` (`landing-page.html:47, 70`, D-100); `proof-page.html:22`; client list and form; subcontractor and
supplier list and form; catalogue list, panel and import; bab tree and panel; the worker pool
(`worker-pool-page.html:26`); and the **site engineer's** worker-register duplicate-phone warning, where
`kaff-duplicate-phone-warning` keeps its default `showCode = true` (`worker-register-page.html:68`), so the engineer sees
the matching worker's `E-…` code. The people panel and quick-add client pass `[showCode]="false"`. On the wire, the
register-from-site `201` body returns the new worker's `code` to the engineer.

### 12. D-228 item 2 interim

`ConvertToExecution` refuses `!design.IsSigned` (409 `design_not_signed`) and nothing else was widened; `conversionOffered`
is `Design && IsSigned && no execution project && not archived`. Live: refused before signing, offered and allowed after,
refused a second time. Exactly as recorded.

### 7. `primary-action-contract.spec.ts`

The overlay exclusion is sound **for the templates as they stand**: every `kaff-detail-panel` in `features/` binds `[open]` to
a signal that starts closed (bab, catalogue create and item, BOQ start, add, custom and sign sheets, daily-log picker, crew
sheet, people panel, quick-add client, team add sheet). The hole is structural: the regex strips any
`<kaff-detail-panel>…</kaff-detail-panel>` whatever its `[open]` binding, so a screen that puts its real primary in a panel
bound `[open]="true"` (or one that is open on arrival) passes the contract. A test that fails on a constant-true `[open]`
would close it. Low.

The allowlist has 17 entries and each still draws a primary outside templates and overlays (checked with the spec's own
regex). Not all are page-body primaries in the sense of the contract:
- `babs/bab-move-dialog/bab-move-dialog.html` is a native `<dialog>`, an overlay's own action; it is listed because the
  exclusion covers only the two kit overlays.
- Several entries are inline-form submits rather than the screen's one primary: `closure-snag-submit`,
  `closure-resolve-submit`, `closure-callback-submit`, `revise-submit`, `department-create-save`,
  `workspace-convert-submit`, `catalogue-reimport-confirm-button`.
- Real un-migrated page primaries: audit apply, change-password submit, bab create, BOQ sign-ask and start, catalogue
  create, client/subcontractor/supplier/user submit, project create submit, project list empty-state create, the extract
  detail acts, closure hold release.
The allowlist is a delivery list, not a business rule (AC-936-I), so this is information for the migration, not a failure.

### 8. KAFF-936 and D-229 item 3 (primary never disabled for a fixable gap)

| Screen | Primary | Disabled when | Verdict |
|---|---|---|---|
| BOQ sign-ask | «توقيع المقايسة» | never; a missing quantity is focused and marked (seen live) | holds |
| BOQ sign confirm | «توقيع المقايسة» | never; three empty rates marked «مطلوب» (seen live) | holds |
| Daily log save | `daily-log-save` | `saveBlocked()`: quantity kind with no BOQ lines or a failed BOQ load (`daily-log-page.ts:297-302`); check-in while locating | holds: neither is fixable on the screen |
| Crew save / add | `crew-*` | never (`crew-page.html:3-12`) | holds |
| Team add confirm | «إضافة» | never; empty choice marked (seen live) | holds |
| People panel submit | `employee-submit` | `isEdit() && !dirty()` (`employee-panel.html:189`) | holds: nothing to fix, nothing changed |
| **Worker register (site)** | `worker-submit` | **`!canSubmit()`, i.e. while the form is invalid** (`worker-register-page.html:3`, `worker-register-page.ts:106`) | **breaks D-229 item 3**: an empty name or باب is a gap the engineer can fix on that screen, and the save is greyed instead of taking him to it. `5a4de1c` says "a save is never greyed for a fixable gap" and fixed the daily log and crew only |

KAFF-936 criteria, read against the code and the running app:
- A MET: the slot is `HeaderActionsService`; the migrated screens declare through it.
- B MET: the 1440 px runs show the action in the page header (`+ مشروع جديد`, «تسجيل موظف», «تسجيل عامل» are the first text in `<main>` at 1440, absent at 390).
- C MET: «إضافة شخص» sits in the bottom bar beside «القائمة» at 390 (screenshot).
- D MET: `.menu:only-child { flex: 1 }` (`app.css`); the BOQ and daily-log screenshots show «القائمة» filling the bar.
- E MET as written (a session that may not use the action gets no template; the Technical Office's signed overview has no primary and a full-width menu). The worker-register greying above is a D-229 item 3 breach, not an AC-936-E one: AC-936-E speaks of a user who may not use the action.
- F HELD (Q117), not judged.
- G MET: `PrimaryActionVariant` excludes `danger*` at compile time; `set()` throws at runtime (`header-actions.service.ts`); `header-actions.service.spec.ts` covers the throw.
- H MET: `OverlayService` counts open `kaff-detail-panel` and `kaff-confirm-dialog`; `data-overlay` hides `.abar`. Seen live: the bar is gone under the sign sheet and the team add sheet.
- I MET with the Low hole above.
- J MET: both screens declare through the slot, `.footer` markup and rules are gone, neither is allowlisted, `worker-submit` and `daily-log-save` render; the cancel link stays (`worker-register-page.html:128`).
- K MET: `layout` PASS at 390, `rtl`, overflow 0 on every screen run; the new CSS (`.menu:only-child`, `data-overlay`) has no physical property.
- L MET: no new user-facing key in the slot or bar; `ng test` and the Domain translation gate are green.

### 11. Formatting

`dotnet format KaffErp.sln --verify-no-changes --no-restore`: exit 2, 9,689 findings, **every one `ENDOFLINE`**. No other
finding.

## Story verdicts

### Slice 1

**KAFF-115 · project team panel** (trailer `DEFERRED`; now built as the team tab, D-227 item 2, D-228 item 1, D-233 item 3).
Judged against its criteria as written.

| AC | Verdict | Evidence |
|---|---|---|
| A Owner not on every team | MET | The team read is `ProjectAssignments` rows only (`GetProjectTeam/Handler.cs`); `CreatorStaffing` gives the Owner no row; `ProjectTeamTabTests.The_team_read_says_how_each_member_came_and_what_the_caller_may_do` asserts the Owner absent. "Exactly one member" can no longer be produced through the create route because Finance is staffed automatically (D-217 item 36) |
| B nor HR | UNTESTED | Same rows-only read, so HR is absent unless assigned, but no test reads the panel with an HR user who reached the project. HR no longer assigns (D-233 item 3), so the story's given ("HR who assigned everyone") cannot happen any more. The candidates read offers HR users as assignable; an Owner who adds one makes HR a member, which the story does not forbid |
| C seniority per project | MET | The member's `level` is the row's; `AssignUserToProjectTests.The_same_engineer_is_supervisor_on_one_project_and_junior_on_another` plus the team-read test asserting `level` |
| D revoked gone | MET | `ProjectTeamTabTests.A_removed_member_leaves_the_read_and_a_re_added_one_reads_as_added`; live in chain A |
| E leaver gone the same way | UNTESTED | Deactivation revokes rows (KAFF-111), and the read filters `RevokedAt == null`, but no test deactivates a member and then reads the team and the trail |
| F explicit empty state `team.empty` | **NOT MET** | `project-team-tab.html:28-43` renders an empty `<ul>` when there are no members; there is no empty state and no `team.empty` key. A project created by the Owner when no Finance user is active has no rows at all |
| G client refused | MET | The staff door (`MayHoldStaffSession`) refuses a `Role.Client` session before any route; unchanged |
| H HR reads the team on its own screen, `[RefCode] Name`, no money, no `ProjectRead` | **NOT MET** | No route uses `ProjectTeamRead` (grep of `src/Api`); HR has no Project Team screen. The HR half of `ProjectRead` (none) holds. D-233 item 3 says "HR cannot open a project and never reached the team tab", which describes the gap rather than ruling the HR screen out |
| I two distinct types, team type limited to name, code, member name, role, level | **NOT MET** | There is one team type, `TeamResponse` / `TeamMember`, and it carries `assignmentId`, `userId`, `origin`, `canRemove`, `assignedAt`, `canManage` beyond the allowed set; there is no reflection test |
| J Arabic RTL 390 | MET | `layout` PASS on `?tab=team` as Owner and engineer; names `dir="auto"` |

Where the story and the rulings differ: the story's surface is a read-only panel plus a separate HR screen (rule 5, D-051);
D-227 item 2 and D-229 item 1 made it an Owner-managed tab with add and remove, D-228 item 1 added the Finance and
creator rules, and D-233 item 3 took management from HR. Nothing rules the HR Project Team screen (rule 5, AC-H, AC-I,
rule 8) out, and Nabil's no-codes rule conflicts with rule 8's `[RefCode] Project Name`. The story needs rewriting
against D-227 to D-233 before it can be certified; as written it is **`BUILT verdict=REJECTED`** (F, H, I NOT MET).

**KAFF-113 · assign** (`VERIFIED verdict=PASS`, code moved: permission row narrowed, candidates route added).
AC-113-A ("HR staffs a project it was never assigned to... the assignment is created") **now contradicts D-233 item 3**:
HR is refused 403 and the tests assert the refusal. AC-113-C, E, F, H, I name HR as the actor; the rule each one
protects holds with the Owner as the actor (tests rewritten to `_owner`, all green). AC-113-B (HR still cannot open
the project) holds. AC-113-D holds. AC-113-G holds and now includes HR among the refused. Recommendation: the criteria
need restating with the Owner as actor; as a behaviour check every rule still holds, so `VERIFIED verdict=CONDITIONAL`
with the condition "restate A, C, E, F, H, I for D-233".

**KAFF-114 · revoke** (`VERIFIED verdict=PASS`, code moved: Finance cannot be removed, HR refused).
AC-114-A and AC-114-C name "HR revokes"/"HR assigns", which **contradicts D-233 item 3**; the behaviour holds with the
Owner (chain A: the engineer is refused on the next request, 403). AC-114-B, D, E, F hold (E now includes HR). New
behaviour outside the story: revoking a Finance row is refused (`identity.finance_cannot_be_removed`, D-228 item 1).
`VERIFIED verdict=CONDITIONAL`: restate A and C for D-233.

### Slice 2 (code moved under all four: two screens instead of one list, list fields, unarchive, duplicate-phone wording, pool figures shared)

**KAFF-207 · employee register** (`VERIFIED verdict=CONDITIONAL`). A MET (live C1). B, C, D unchanged and green in the
suite. E MET: the list row has no money member (`ListEmployees/Response.cs`), `ListEmployeesTests.The_raw_body_read_as_hr_carries_no_rate_and_no_money`,
live C5. F MET: archive removes the row from the default list (live C6), no delete route; unarchive is new and
audited. G MET: the salaried panel draws name, phone, national ID, job title, department, hired-on and nothing else
(`employee-panel.html:112-168`); code generated. H MET: site engineer 403 on list, read and unarchive (live). I MET: the
interceptor writes `Modified [IsActive]` on archive and unarchive. J MET: `layout` PASS 390 and 1440. The old cap
(`V-38-L`, no E2E suite for these screens) still stands. **Verdict holds: `VERIFIED verdict=CONDITIONAL`.**

**KAFF-208 · two populations** (`CONDITIONAL`). A, C, D unchanged (`EmployeeTests` allow-list gained `Unarchive`, which
sets `IsActive` only: a legitimate widening, see hard check 6). E: archived-match warns (live U3/U3b), and unarchive now
refuses when an active salaried record holds the phone (live U4, `UnarchiveEmployeeTests.A_salaried_employee_whose_phone_an_active_salaried_record_now_holds_stays_archived`).
F MET. G: the two screens read «الموظفون» and «عمالة اليومية» (D-232 item 2). **Verdict holds.**

**KAFF-209 · register from site** (`CONDITIONAL`). A, B, C, E, F, G, H unchanged in behaviour (handler change is the
error key only). D: the duplicate refusal now carries `errors.master.employee_duplicate_phone_not_acknowledged`, code
unchanged. I: `layout` not run on `/projects/{id}/day-labour/new` in this pass. K unchanged. The worker-register save is
greyed while the form is invalid, which breaks D-229 item 3 (hard check 8); that is a kit ruling, not a KAFF-209
criterion. The engineer sees the matching worker's code in the duplicate warning (hard check 10). **Verdict holds:
`VERIFIED verdict=CONDITIONAL`**, with the D-229 item 3 defect added to the conditions.

**KAFF-210 · engagement history** (`CONDITIONAL`). The pool figures moved into `EngagementFigures.ReadAsync`, read by the
pool and the register; still derived on read (B). The worker panel shows times engaged and rating only, no rate
(D-232 item 4). G: a worker with no engagements reads `timesEngaged 0` in the register body and «لم يُشغَّل من قبل» on
the list; the pool's empty state is unchanged. Other criteria not moved. **Verdict holds.**

### Slice 4 (code moved under all six)

**KAFF-400** (`CONDITIONAL`). A to H unchanged in substance; create now staffs the creator and Finance through
`NewProjectStaffing` in the same save (live audit rows). The earlier F-1 list leak is closed: the site engineer's list
and read carry no value, rate or total key (live C9d, C9g). AC-400-B's HR deviation is unchanged. **Holds,
CONDITIONAL** (HR deviation only).

**KAFF-415 · design to execution** (`CONDITIONAL`). A: "a design project offers convert" now only after signing (D-228
item 2 interim, hard check 12). B MET and widened by the not-signed refusal (live B3, B6b). C, D, E, F not driven live
(I did not sign the execution project); `DesignToExecutionTests` covers them. G MET (live: area 120 and the design link
on the execution project). H MET: converter and Finance staffed, audit rows written; role and assignment checked
(`ProjectCreate`, project-scoped). I MET (both links on screen). **Holds, CONDITIONAL.**

**KAFF-417 · quotation-first** (`CONDITIONAL`). **AC-417-B** ("an omitted value keeps the project's current term, the 20%
/ 25% defaults") and **AC-417-D** ("hold 20% and advance 25% pre-filled") contradict D-217 item 29 and D-227 item 3:
signing now refuses an omitted hold, advance or recovery rate, and the sheet starts empty. The build follows the
rulings. AC-417-K: the area is taken at signing and the rate comes from the server (a smuggled rate was ignored, live
B4). **Holds, CONDITIONAL**, with B and D to be restated.

**KAFF-418 · BOQ builder** (`CONDITIONAL`). AC-9 now covers three rates, all required and empty (live A6, A6b; screenshot).
F-2 from 2026-11-15 is closed (D-228 item 3 label). F-3 (no screen prepares a base) still open: the web calls only
`GET api/boq-bases` (`boqs.api.ts:137`). AC-6 on screen: a missing quantity is focused and marked when «توقيع
المقايسة» is pressed (screenshot), no longer a greyed button. **Holds, CONDITIONAL** (F-3).

**KAFF-419 · project create** (`CONDITIONAL`, condition: quick-add sheet at 390 not seen). The quick-add sheet was opened
in the browser at 390 in this pass: a bottom sheet with name, phone, kind (فرد / شركة), «حفظ العميل» and «إلغاء».
AC-5 holds live (both Finance users on every new project). **Recommend `VERIFIED verdict=PASS`.**

**KAFF-420 · frame and overview** (`PASS`). AC-2 says the stage line shows المقايسة, التنفيذ and التسليم only; a Design
project now shows «التعاقد · التصميم · التسليم» (D-229 item 2). The story text and the ruling differ; the build follows
the ruling for Design and the criterion for Lump Sum. AC-6: an unsigned project's main action opens the BOQ (Lump Sum)
and is «توقيع التعاقد» for Design (mock). AC-5 holds live. AC-7: no code on the frame. **Holds, PASS**; restate AC-2 for
Design.

### Slice 6 (web code moved in `5a4de1c` and `91cdcb3`; no server file under `DailyLogs` or `DayLabour` routes changed except the pool refactor)

**KAFF-600, 613, 614, 615.** The changes: the crew link carries `?date=` (seen live: «أمس» then the crew row opens
Monday 16 November), counts follow the rows, the save is no longer greyed on the daily log and crew, the project list
drops the code. Chain A drove check-in, quantity, material, photo and crew hours through the routes these stories own,
and the Owner's review read them back. KAFF-615 AC-6 («نفس عمالة أمس» today only) is `showSame` with `isToday()`
(`crew-page.ts:273-275`). The builder-reported departures, judged:

| Departure | Judgement |
|---|---|
| Crew read view lists only men with hours (`visibleRows`, `crew-page.ts:258`) | Condition on KAFF-615 AC-10 (mock): no criterion requires zero-hour men on a read-only day |
| «يوم آخر» on the crew screen for reviewers | Condition on KAFF-615 AC-10: an extra control, no criterion broken |
| No population badge on a duplicate-phone match (people panel) | Condition: the people mock is D-233's, no story criterion names the badge |
| Date control wraps on the phone | Condition: `layout` PASS, no overflow |
| Lump Sum stage line 3 stages, mock 5 | MET against KAFF-420 AC-2, which asks for three |

Copy: the crew row reads «⁨1⁩ عمال · ⁨8⁩ ساعة» (`91cdcb3` chose counts "that need no agreement"); «1 عمال» still
disagrees in number. Low, for the copy reviewer. **All four verdicts hold** (614 stays CONDITIONAL on AC-12, the real
phone check).

### KAFF-936 · primary action slot (`BUILT verdict=REJECTED` at `9fb1c83` on D, E, G, H, I, J)

All six re-judged MET (hard check 8). F is HELD. **Recommend `VERIFIED verdict=CONDITIONAL`**: the conditions are the
`[open]="true"` hole in the contract test (Low) and the worker-register greyed save, which breaks the kit rule D-229
item 3 that `5a4de1c` claimed to apply everywhere.

### 2. Money

- Three rates stored as typed: chain A signed 0.15 / 12.5 / 0.3 and read back `0.150000`, `0.125000`, `0.300000`. The
  recovery rate is a `Percentage` on the wire (fraction), the advance a decimal in percent units, as recorded in D-228.
- No silent default at signing: `SignProject/Handler.cs` refuses a Lump Sum signature without `AdvanceRecoveryRate`
  (`errors.project.advance_recovery_rate_required`, live A6). Mutation A8 below proves the test catches a default.
  `CreateProject` still stores 20% / 25% defaults on create (A2 response); signing overwrites them with typed values.
- `spec.md` §15: `Section15ThroughCalculatorTests` changed by one line in the range (a manual `TransitionTo(Active)`
  replaced by an assertion that signing activated the project); no expected figure changed. The suite is green.
- Precision: `Project.AdvanceRecoveryRate` and `DesignRatePerSquareMetre` go through the `Percentage` (18,6) and `Money`
  (18,4) conventions; no new `double` or `float` near money in the range (`git diff 9fb1c83 HEAD -- src | grep -E "double|float"`
  finds only the existing check-in coordinates). `EngagementFigure.AverageRating` is a bare `decimal?`, a rating, not money.
- Design rate typed as money: `Money? DesignRatePerSquareMetre` on the read, stored at signing; a client-sent rate is
  ignored (live B4: 120 m² x 450 = 54,000 although `"1"` was sent).
- No rate in the employees list body: live C5/C5b and `ListEmployeesTests.The_raw_body_read_as_hr_carries_no_rate_and_no_money`.
- No stored balance, no posting update or delete path, no hold debit and no same-user create-and-approve were added in
  the range (no server file under `Treasury`, `Postings`, `Extracts`, `Adjustments`, `Closure` or `src/Infrastructure` changed; the web closure page changed only its status word).

### 5. Tests that could not fail: mutations

Each mutation was applied to production code, the named test classes were built and run in Debug, and the file was
restored with `git checkout --`. `git status` was clean after each, and the solution was rebuilt clean afterwards.

| # | Mutation | Tests run | Result |
|---|---|---|---|
| D1 | `Project.Sign` never activates | `BoqTests` | **red**, 3 of 31 (`D_230_signing_a_project_in_setup_makes_it_active...` x3) |
| D2 | `Project.Sign` always transitions, even from Active | `BoqTests` | **red**, `D_230_signing_a_project_that_is_already_active_keeps_it_active...` |
| D3 | activate before the contract-value check | `BoqTests` | **red**, `D_230_a_refused_signature_leaves_the_project_in_setup` |
| D4 | `Employee.Unarchive` leaves `IsActive` false | `EmployeeTests` | **red**, `Unarchiving_reactivates_and_an_active_employee_is_refused` |
| A1 | employee list sends `timesEngaged` to salaried rows | `ListEmployeesTests` | **red**, `A_worker_row_names_its_bab_and_carries_times_engaged...` |
| A2 | employee list drops `jobTitle` | `ListEmployeesTests` | **red**, `A_salaried_row_carries_job_title_department_and_hire_date...` |
| A3 | unarchive skips the salaried phone pre-check | `UnarchiveEmployeeTests` | **green, 7 of 7**. The `ux_employees_salaried_phone` unique index raises, and the handler's `catch` maps it to the same `employee_phone_taken`. The test pins the outcome, not the pre-check; the rule is still guarded by the database. Not a weak test, a redundant pre-check |
| A4 | Design projects get the BOQ tab | `ProjectFrameTests` | **red**, both `A_design_project_is_sent_no_boq_tab...` cases |
| A5 | design rate sent without `ProjectMoneyRead` | `ProjectFrameTests` | **red**, `The_design_rate_is_withheld_from_a_caller_without_project_money...` |
| A6 | employee create sends the client duplicate-phone key | phone-match, create, kind-invariant classes | **red**, 5 of 20 |
| A7 | conversion allowed before signing | `DesignToExecutionTests` | **red**, `D_228_item_2_an_unsigned_design_project_is_refused...` |
| A8 | signing falls back to 25% when the recovery rate is missing | BOQ sign, builder, activation classes | **red**, `D_227_a_lump_sum_signature_without_the_advance_recovery_rate_is_refused_and_no_default_is_applied` |
| A9 | the creator is not staffed | create and conversion classes | **red**, 6 of 27 |

Every `e6c1f51` item named in the brief that I mutated (list fields, worker figures, unarchive, duplicate-phone key,
no BOQ tab for Design, design rate) has a test that goes red, except the unarchive phone pre-check (A3), which is
backed by the index. All three `8c6756a` `BoqTests` can fail.

### 6. Tests changed to fit new behaviour

- `ProjectFrameTests` status `Setup` to `Active` and stage `Boq` to `Execution`: legitimate; the fixture signs the project,
  and D-230 makes a signed project Active. The unsigned project still asserts the BOQ stage
  (`ProjectWorkDisplayTests.An_unsigned_project_stays_on_the_boq_stage`).
- Helpers that called `TransitionTo(Active)` after signing (extract, closure, change order, daily log, §15 harness,
  retention): each now **asserts** `Status == Active` after signing, which is stronger than before. The Domain fixtures
  that need a `Setup` project now leave it unsigned, since a signed `Setup` project can no longer exist: legitimate.
- `AssignUserToProjectTests`, `RevokeProjectAssignmentTests`, `PermissionMechanismTests`: HR swapped for the Owner as
  the actor, HR added to the refused list in both "nobody but" tests, and the HR-reach probes moved to a
  `ProjectTeamRead` probe route so HR's global reach (still real for that permission) and its `ProjectRead` refusal are
  both still asserted one line apart. The HR audit `GrantPath HrGlobal` assertion was dropped because HR can no longer
  assign; the Owner's `OwnerGlobal` assertion remains. Legitimate, not weakened.
- `EmployeeTests` allow-list gained `Unarchive`: legitimate; it sets `IsActive` only, and `Kind` stays without a setter
  (AC-208-D's point).
- `EmployeePhoneMatchTests`, `CreateEmployeeTests`, `EmployeeKindInvariantTests`, `RegisterDayLabourerFromSiteTests`:
  the expected message key changed to `errors.master.employee_duplicate_phone_not_acknowledged`, the code unchanged:
  legitimate (D-233 item 1 wording), and A6 shows they still fail on the old key.
- Sign calls in extract and closure tests gained `advanceRecoveryRate = "0.25"`: legitimate (D-227 item 3).

## Recommended trailers

| Story | Code moved since its verdict | Recommended | NOT MET or condition |
|---|---|---|---|
| KAFF-400 | yes | `VERIFIED verdict=CONDITIONAL` (holds) | AC-400-B HR deviation, unchanged |
| KAFF-415 | yes | `VERIFIED verdict=CONDITIONAL` (holds) | AC-415-A narrowed by the D-228 item 2 interim; C to F not driven live |
| KAFF-417 | yes | `VERIFIED verdict=CONDITIONAL` (holds) | AC-417-B and AC-417-D contradict D-217 item 29 and D-227 item 3; restate |
| KAFF-418 | yes | `VERIFIED verdict=CONDITIONAL` (holds) | F-3, no screen prepares a base |
| KAFF-419 | yes | **`VERIFIED verdict=PASS`** (was CONDITIONAL) | quick-add sheet seen at 390 |
| KAFF-420 | yes | `VERIFIED verdict=PASS` (holds) | restate AC-2 for the Design stage line (D-229 item 2) |
| KAFF-600 | web only | `VERIFIED verdict=PASS` (holds) | |
| KAFF-613 | web only | `VERIFIED verdict=PASS` (holds) | |
| KAFF-614 | web only | `VERIFIED verdict=CONDITIONAL` (holds) | AC-12 real-phone check |
| KAFF-615 | web only | `VERIFIED verdict=PASS` (holds) | mock departures recorded as conditions of no criterion |
| KAFF-936 | yes | **`VERIFIED verdict=CONDITIONAL`** (was REJECTED) | worker-register greyed save (D-229 item 3); `[open]="true"` hole in the contract test |
| KAFF-115 | built | **`BUILT verdict=REJECTED`** | F, H, I NOT MET; B, E UNTESTED; story needs restating against D-227 to D-233 |
| KAFF-113 | yes | `VERIFIED verdict=CONDITIONAL` (was PASS) | AC-113-A contradicts D-233 item 3; C, E, F, H, I name HR as actor |
| KAFF-114 | yes | `VERIFIED verdict=CONDITIONAL` (was PASS) | AC-114-A and C name HR as actor, contradict D-233 item 3 |
| KAFF-207 | yes | `VERIFIED verdict=CONDITIONAL` (holds) | V-38-L |
| KAFF-208 | yes | `VERIFIED verdict=CONDITIONAL` (holds) | V-38-L |
| KAFF-209 | yes | `VERIFIED verdict=CONDITIONAL` (holds) | V-38-L; worker-register greyed save; code shown in the duplicate warning |
| KAFF-210 | yes | `VERIFIED verdict=CONDITIONAL` (holds) | V-38-L, V-39-C/D |

## Defects, by severity

1. **Medium** · `worker-register-page.html:3` with `worker-register-page.ts:106`: the site engineer's register save is
   disabled while the form is invalid, a fixable gap, against D-229 item 3. `5a4de1c` applied the rule to the daily log
   and crew only.
2. **Medium (story)** · KAFF-115 AC-F: `project-team-tab.html:28-43` has no empty state for a team with no rows.
3. **Medium (story)** · KAFF-115 AC-H and AC-I: no HR Project Team surface exists and the one team type carries more
   than the story allows; needs a ruling to drop or rebuild.
4. **Low** · `primary-action-contract.spec.ts`: the overlay exclusion ignores a panel bound `[open]="true"`.
5. **Low** · Design projects: the server sends `tabs.extracts: true` before and after signing although `spec.md` §5.3
   says no extract; unreachable in practice (no BOQ). Pre-existing.
6. **Low** · `worker-register-page.html:68`: the duplicate warning shows the matching worker's `E-…` code to the site
   engineer (`showCode` defaults to true).
7. **Low (copy)** · crew row «⁨1⁩ عمال».
8. **Question** · a signed Design project reads «جاري العمل» with no log (`project-status-pill.ts`, mock `ds` view)
   while D-228 item 4 has no Design exception; and `workStarted` turns true on a check-in alone.

## Not reached in this pass

- HR live on screen and by request: creating a throw-away HR user was refused by this session's permission layer, and
  the brief forbade touching `hend_hr_demo`. HR rests on the Api suite.
- KAFF-415 C to F live (signing the execution project and the design credit).
- `.design/v2/team-a/daily-log-review.html` compared image to image; the review page was read as text and one 390 px
  screenshot.
- The worker-register screen measured with `layout` at 390.
- Real-device check-in geolocation (driver has none; check-in driven by API).

## Data left behind in the development database

Projects «مشروع تحقق 035897» (signed, one daily log), «تصميم تحقق 132761» and its execution project «تنفيذ 132761»,
«مقايسة للتوقيع 745736» (unsigned), «موقع بلا يومية 745736» (signed), «تصميم غير موقع 426309»; client «عميل تحقق
035897»; employees E-10015 to E-10019 (E-10016 archived then restored, E-10018 archived). `site_eng_demo` was assigned to «مشروع تحقق
035897», removed and re-assigned; he is still on it. Nothing was deleted.

## Close-out

Debug and Release solution builds after the mutations: exit 0, 0 warnings, 0 errors. `git status` shows only this report.
The API was restarted from the clean Release build on 5080; the dev server on 4200 was left running throughout.
`node .claude/skills/run-kaff-erp/driver.mjs smoke`: all checks passed. The stack is left running.
