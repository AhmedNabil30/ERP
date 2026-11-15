# Verification — slice 6 site day (KAFF-600, KAFF-613, KAFF-614, KAFF-615) and re-verify KAFF-420 · 2026-11-15

**Verifier:** fresh session, did not write the code. **Model:** Opus 5.5 (claude-opus-5-5).
**Tree:** HEAD `8f8898a123a2a034dc6c5c666d5ec3afad72d288`, working tree clean at start.
**Read:** `CLAUDE.md`, top of `STATUS.md`, the five story files, `spec.md` §8 and §9 (head), D-212 to D-219, D-221, D-223,
the KAFF-420 part and F-1, F-2, F-5 of `qa/slice-4/verification-2026-11-15.md`.

Findings are written as they are reached.

## Finding S-1 (Medium) — the day of an entry follows the client's clock offset, not the site zone

**Item 1 of the brief.** `DailyLog.DayOf(occurredAt)` is `DateOnly.FromDateTime(occurredAt.DateTime)`
(`src/Domain/DailyLogs/DailyLog.cs:43`): the wall-clock date in whatever offset the client sent. Every production path
uses it: `DailyLogAccess.OpenAsync` (`src/Api/Features/DailyLogs/DailyLogAccess.cs`, `DateOnly day = DailyLog.DayOf(occurredAt)`,
and `DailyLog.Open`) and `GateAsync` (the check-in rule compares `DayOf(occurredAt)` with `rule.SiteDate(now)`). "Today" is
derived in the site zone (`DailyLogEntryRule.SiteDate`), the day of the entry is not. The two only agree when the client sends
the Cairo offset.

**Evidence, a probe test run against the test database and then deleted.** Server clock 2026-09-25T22:30Z
(2026-09-26 01:30 in Cairo, summer time):

- check-in with `occurredAt = "2026-09-25T22:30:00+00:00"`: **409 `errors.daily_log.check_in_only_today`**;
- the same instant sent as `"2026-09-26T01:30:00+03:00"`: 201, log dated 2026-09-26;
- a quantity at `"2026-09-25T22:35:00+00:00"`: **201, stored on a second log dated 2026-09-25**, i.e. work done at 01:35
  Cairo time on the 26th lands on the 25th and will read as back-dated.

The web client sends the device's own offset (`localIso`, `src/Web/src/app/shared/kaff-day-switch/site-days.ts:33`), so a phone
set to Cairo time is not affected today. Any client sending UTC (`toISOString()`, a future mobile app, a phone in the wrong zone,
an offline queue) is affected between 22:00 and 24:00 UTC. AC-600-I asks that an entry "belongs to the day it occurred on"; with
the client's offset trusted, which day that is depends on the client. The test fixtures all send `+00:00` at a noon UTC clock
(`DailyLogTests.cs:50, 607`), which is why no test sees this. Fix: derive the day with `rulings.DailyLogEntry.SiteDate(occurredAt)`
in `OpenAsync`, `GateAsync` and `DailyLog.Open`/`EntryOnAnotherDay`.

## Audit rows inspected (KAFF-614 AC-13, KAFF-613 AC-J, KAFF-615 AC-11)

Probe, same run: check-in, a quantity, a quantity correction, a crew entry, a crew correction and a material, then the
`audit_records` rows read back by entity id. Every one has a `Created` row with actor, project and the full after image
(the correction rows show `Quantity: -10.0000`, `CorrectsEntryId`, `CorrectedByUserId`; the crew correction `Hours: -2.0000`).
The BOQ line has one `Modified` row per move (`QuantityExecuted` 40, then 30) with before and after. The `DailyLog` has a
`Created` row. No `Modified` or `Deleted` row on any entry. The mechanism is the shared interceptor
(`AuditSaveChangesInterceptor.cs:82-99`, opt-out by `IAuditExempt`, which only `AuditRecord` implements). **MET.**

## Tests that can fail (mutations, reverted with `git checkout`)

- `DailyLogAccess.GateAsync` changed to always succeed: the six `AC_614_1_no_entry_of_any_kind...` cases went red.
- `CorrectionAccess.ForCrew` with the owner case removed: `AC_615_5_the_owner_edits_the_crew...` went red.
  `canCorrect_on_a_crew_line_equals_what_the_correct_endpoint_does_for_every_reader` stayed green, correctly: it asserts
  the read and the endpoint agree, and both moved together, which is the property item 3 asks about.

---

**Interruption.** The pass stopped on a usage limit at this point. Everything above this line (S-1, the audit rows, the
mutations) was judged and written before the interruption. Everything below was written after it. Some evidence below
(the KAFF-420 money mutation, the code reads of the frame and the daily-log screen) was gathered before the interruption
but not yet written; it is marked "(gathered before the interruption)" where it appears. Tree re-checked after the restart:
HEAD still `8f8898a`, no change under `src/`, the probe test file deleted.

## KAFF-420 · re-verify (judged after the interruption; the mutation was run before it)

Fixes since the rejection: `b3af13d`, `1a0ba86`, `d4c791e`, `7ac3924`.

| AC | Verdict | Evidence |
|---|---|---|
| AC-1 one frame above overview, BOQ, daily log, crew, extracts; one kit part | **MET** | `<kaff-project-frame` appears in `project-workspace-page.html`, `boq-page.html`, `daily-log-page.html:17`, `crew-page.html`, `extract-list-page.html` and `extract-detail-page.html`. There is one component, `shared/kaff-project-frame`, and no copy. For the junior the frame leaves out the client and type line (`kaff-project-frame.ts` `showMeta`). The mock does the same (`.design/v2/team-a/project.html:439`, `SITE()?''`). Screen check at 390 px below. |
| AC-2 three stages, no sentence, no lock | MET | Unchanged since the 2026-11-15 pass. `STAGES` has three values in `kaff-project-frame.ts` and the template draws only a check mark (`kaff-project-frame.html`). |
| AC-3 tabs by role, absent, server refuses | MET | Unchanged. `GetProject/Handler.cs` `TabsAsync` reads the same permissions that gate the routes. The 403 tests are as listed in the previous report. |
| AC-4 owner and supervisor: percent, أبواب, three money figures, آخر يومية | MET | `Owner_supervisor_finance_and_technical_office_receive_the_three_money_figures`, `Money_roles_receive_every_contract_figure_including_the_advance_recovery_rate`. Screen check below. |
| AC-5 junior: no money figure; the API does not send him money | **MET** | `GetProject/Handler.cs`: `seesMoney` (`ProjectMoneyRead`, route-scoped) gates `ContractValue`, `HoldRate`, `AdvanceRate`, `OriginalContractValue`, `ApprovedChangeOrderTotal`, `AdvanceRecoveryRate` and `Money`. `Response.cs` uses `JsonIgnore WhenWritingNull`, so the keys are absent. `ListProjects/Handler.cs` evaluates `ProjectMoneyRead` per row. Test `The_junior_and_marketing_never_receive_the_money_block` now asserts that no contract-figure key is present. **It can fail:** with `seesMoney` changed to test `ProjectRead`, both cases (junior and marketing) went red. The change was reverted with `git checkout` (gathered before the interruption). Same test for the list: `The_junior_and_marketing_never_receive_a_contract_figure_in_the_project_list`. F-5 is closed: the Design branch draws `p.contractValue` only inside `@if (money())` (`project-workspace-page.html:240-244`). |
| AC-6 unsigned: overview, BOQ, team; main action opens the BOQ | MET | Unchanged. `notReached` in `kaff-project-frame.ts` hides the daily log and extracts until signing. |
| AC-7 no code or id on screen | MET (screen check below) | The frame draws no code. `QuantityLine.ItemCode` is still in the daily-log read body, but neither screen renders it. |

F-2 (the signing advance labelled as the recovery rate) is closed by `1a0ba86`. `advance_signing` «نسبة الدفعة المقدمة» is now on `p.advanceRate`, and `advance_rate` «نسبة استرداد الدفعة المقدمة» is on `p.advanceRecoveryRate`. F-4 is closed for the three keys it named (now «التعاقد»).

## KAFF-613 · code movement since `553309c` (judged after the interruption)

`git diff 553309c HEAD` on its paths:
- `RecordCheckIn/Handler.cs` now passes `EntryGate.CheckIn`, so a check-in must be dated today (D-217 item 10).
- The two stale comments are corrected (F-613-2 closed).
- `DailyLog.cs`: `RecordField` refuses a check-in or check-out, which must go through `RecordCheckIn`.
- `GetMyLog/Response.cs` moved to lists and `canCorrect`. `CheckInLine` is unchanged.
- The screen was rebuilt. Check-in is now the one primary action. Check-out is in `kaff-attendance-strip`. The far flag is drawn through `[far]`.
- F-613-1 is closed: `daily-log-page.ts:304` takes `localIso(new Date())` before it awaits `currentPlace()`.

The location refusal, the 500 m flag and the audit row are unchanged in code. The audit row was seen again in this pass's probe.

Conditions on the verdict:
- F-613-3 is still open. No test asserts the scalar `checkIn` after a new-route check-in. Low.
- New: S-1 touches AC-613-G. A check-in sent with a UTC offset between 22:00 and 24:00 UTC is refused as "not today".

Recommendation: stays `VERIFIED verdict=CONDITIONAL`.

## Finding S-2 (Medium) — the owner's اليومية tab leads to «غير مصرح», and the owner cannot reach the crew screen by navigation

`GET /api/projects/{id}` sends the owner `tabs.dailyLog: true`, because `TabsAsync` counts `DailyLogReview` as well as
`DailyLogWrite`. The raw body for `owner_demo` is `"tabs":{"overview":true,"boq":true,"dailyLog":true,"extracts":true,"team":true}`.
The route `projects/:projectId/daily-log` is guarded by `dailyLogWriteGuard` (`app.routes.ts:277-279`). `GET .../daily-logs/mine`
needs `DailyLogWrite`, which the owner does not hold (`PermissionCatalogue.cs:409`).

Driven at 390 px: the owner opens the overview, clicks اليومية, and lands on `/forbidden` with «غير مصرح بالدخول». The crew screen is
reached only from the daily log's «العمالة» row, so the owner can open it only by typing the URL. When he does, the edit works:
`/day-labour` at 390 px shows «تعديل».

- KAFF-420 AC-3 ("A tab a role may not open is absent"): **NOT MET** for the owner.
- KAFF-615 AC-5: MET on the server, and the screen works at its URL. It is a condition, because nothing on screen leads there.

## Finding S-3 (Medium) — the primary action is drawn at the top of the page on a phone, not in the bottom bar

The daily log and the crew screen put their one primary in `HeaderActionsService`. The shell renders that slot as `.page-actions`
at the top of `<main>`, above the project frame (`app.html:217-221`). The phone `abar` holds only the menu button
(`app.html:231-243`).

The 390 px screenshots of the junior's daily log and crew screen show «حفظ» and «تعديل» above «المشاريع» and the frame. The bottom
bar carries «القائمة» alone. The approved mocks draw the primary in the bottom bar beside the menu
(`.design/v2/team-a/daily-log.html:166-168, 454`). The comment at the head of both templates ("The shell places this in the bottom
bar on a phone") is false.

`KAFF-936` is `BUILT`, and its AC-936-C asks for exactly that placement. At HEAD that placement does not hold for these screens,
so KAFF-936's trailer should be re-checked. Because KAFF-936 is built, `AC-600-K2` is no longer held and can be judged: it is
**NOT MET**, because the save is not in the bottom bar.

My reading for item 10: KAFF-614 AC-11 and KAFF-615 AC-10 ("follows the approved mock") are **NOT MET** on placement.

## Finding S-4 (Medium) — the photo kind is missing from the daily log screen

The approved mock has five kinds, including «صورة» (`.design/v2/team-a/daily-log.html:402`, with the capture button at 493).
`KIND_OPTIONS` (`daily-log-page.ts:46-51`) has four, and the class comment says "Not built: the photo kind (no upload endpoint
exists, only a file reference)". `POST .../photos` takes only a `fileReference` string, and no upload route exists.

- KAFF-614 AC-11 ("the entry kinds"): **NOT MET**.
- KAFF-614 AC-9 holds on the server only: no engineer can record a photo from the screen.
- KAFF-600's story text ("records ... photos") has no screen path either.

## Finding S-5 (Medium) — Accounts has no way to confirm a site expense draft

AC-600-H says the expense "is stored as a draft, no posting is written, and it is Accounts that confirms and posts it". The draft
and the absent posting are tested (`AC_600_H_an_engineers_expense_is_stored_as_a_draft_and_writes_no_posting`).

`Permission.SiteExpenseConfirm` exists in the catalogue, but **no endpoint uses it**. A grep of `src/Api` finds it only in a
comment. A draft can never become money, so the third clause is **NOT MET**. Either a story is needed for the confirm path, or
the clause should be retired.

## Finding S-6 (Low-Medium) — the material unit is stored in the UI language

`unitOptions` uses `this.i18n.t(key)` as the **value** as well as the label (`daily-log-page.ts:138-140`), and the request sends
`unit: this.materialUnit()` (`:412`). An Arabic session therefore stores «كيس» and an English session stores "Bag" for the same
unit. The server keeps the string as it arrives: `MaterialEntry.Unit` is free text in the audit after-image. Anything that later
sums materials by unit splits on language.

Fix: send the key or a stable code, and translate on read. Severity is low today, because the UI is almost always Arabic and the
list is not yet confirmed (D-217 item 17). This is a data defect, not a copy defect.

## Finding S-7 (Low) — «الكمية المقاسة» is shown on an estimate

When a line has no survey, `lineTotal` falls back to `quantityEstimated`. The figure pair and the picker still label it
`daily_log.quantity.measured` = «الكمية المقاسة» (`ar.json:1787`). D-217 item 21 says the quantity "counts as estimated until a
survey is recorded", so an unsurveyed line is shown as measured. The read sends both fields, so the screen can choose the label.

## Finding S-8 (Low, question) — a crew line entered by mistake cannot be removed

`DailyLogEntryRule.HoursAllowed` requires hours above 0, and a correction obeys it
(`AC_615_11_a_crew_edit_obeys_the_hours_rule...`). The screen floors a saved row at 0.5 (`crew-page.ts` `onStep`). KAFF-615,
D-217 and D-218 say nothing about zero; "above 0" comes from the builder's ruling note. The story does not forbid this, so it is a
question for Nabil, not a violation.

Client edge in the same area: an edit sends `first.hours + (value - saved)` against the first of a man's lines. Suppose a reviewer
sees two lines for one man, from two engineers' logs, and lowers the total by more than the first line holds. The server then
refuses the edit.

## Finding S-9 (Low) — corrections need no check-in today (KAFF-614 AC-1 says "of any kind")

A correction is a new entry row (D-219 item 1), and neither Correct handler runs the check-in gate. The existing test
`AC_614_5_an_earlier_open_day_is_corrected_by_the_supervising_engineer...` corrects as a supervisor who never checked in. D-219
already records this as open: the owner cannot check in, and D-218 item 16 lets him edit crew. Reported as a condition on AC-1,
not as NOT MET, because two rulings conflict here and the conflict is already raised.

## Finding S-10 (Low) — the AC-600-I test does not test what it names

`AC_600_I_an_entry_made_at_2350_belongs_to_the_day_it_occurred_even_when_it_syncs_after_midnight` (`DailyLogTests.cs:334-353`)
sends `2026-09-25T23:50:00+02:00`. In September Cairo is +03:00, so that instant is 00:50 on the 26th at the site. The server
clock is fixed at 12:00Z on the 25th (`DailyLogTests.cs:50`), so the entry does not sync after midnight. The comment "The API clock
is the real now" is stale. The test passes only because S-1 lets the client's offset pick the day. Once S-1 is fixed, rewrite the
test with a Cairo offset and a clock past midnight.

## Raw bodies, read on the running stack, 2026-11-15

**As the junior (`site_eng_demo`).** None of these reads carries a key matching rate, amount, cost, price, value, balance, phone
or money:
- `GET /api/projects`: one row, no contract figure.
- `GET /api/projects/{id}`: no money key; tabs are overview, dailyLog and team.
- `GET .../daily-logs/mine`, `GET .../daily-logs/boq-lines`, `GET .../day-labour/crew` (200), `GET .../progress`.

`itemCode` is in the two daily-log reads, and neither screen draws it. The review read `GET .../daily-logs?date=` returns 403.
The junior's own read has no `backDated` key.

**As the supervisor.** The review read for the 14th has `backDated: true` on nine lines.

**As the owner.** 403 on `day-labour/crew` and on `daily-logs/mine`, 200 on the review read.

**Low.** `ListPool` still sends `phone` and `code` to every holder of `DayLabourSiteManage`. The add-a-man sheet does not draw them,
and KAFF-615 AC-7 asks only about the sheet.

## Screens at 360 and 390 px (driver `layout`, Arabic)

- Junior: the overview, the daily log and the crew screen PASS at 360 and 390: dir rtl, overflow 0, no small targets, no raw
  keys, no ids.
- Owner at 390: the overview and the crew screen PASS.
- A grep of the daily-log, crew, frame, day-switch and attendance-strip CSS finds no physical left or right property.
- A grep of both templates finds no literal text outside `i18n.t`.

## Gates re-run for this pass

- Release build: 0 warnings, 0 errors.
- Api.Tests, `Kaff.Api.Tests.Features.DailyLogs.*` plus `ProjectFrameTests`: 125/125.
- Domain.Tests, `*DailyLog*`: 52/52.
- Not re-run: `ng test` and the full suites. Since the brief's figures, only docs changed.

## Verdicts per criterion

**KAFF-600**: 13 judged (K is retired; K2 can be judged now that KAFF-936 is BUILT).
- MET: A, B, C, D, E, F, G, J.
- MET with condition: I (S-1, S-10). K1: the save is visible, nothing overflows and the CSS uses logical properties; there is no
  footer, and the save sits at the top.
- **NOT MET**: H (S-5), K2 (S-3).

**KAFF-613** (re-check only): no regression. F-613-1 and F-613-2 are closed, F-613-3 is still open, and S-1 is a new condition on AC-G.

**KAFF-614**: 13 criteria.
- MET: 2, 3, 4, 6, 7, 8, 10, 13 (audit rows inspected). 5 is MET: the owner is refused, as the story says, and owner correction is
  not ruled. 9 is MET on the server.
- MET with condition: 1 (S-9).
- **NOT MET**: 11 (S-3 placement, S-4 photo kind).
- UNTESTED: 12. No on-screen keyboard was emulated. No primary covers the field any more, but the fixed menu bar still might.

**KAFF-615**: 11 criteria.
- MET: 1, 2, 3, 4, 6 (`showSame` requires `isToday`), 7, 8, 9 (`counted` leaves out a refused row), 11 (the probe saw the correction
  row `-2.0000` naming its entry, with an audit row).
- MET with condition: 5 (S-2).
- **NOT MET**: 10 (S-3).

**KAFF-420** (re-verify): 7 criteria.
- MET: 1, 2, 4, 5, 6, 7.
- **NOT MET**: 3 (S-2: the owner's اليومية tab opens «غير مصرح»).

## Recommended trailers

| Story | Recommendation | Why |
|---|---|---|
| KAFF-600 | `BUILT verdict=REJECTED` | H (no Accounts confirm path) and K2 (primary not in the bottom bar) are NOT MET |
| KAFF-613 | stays `VERIFIED verdict=CONDITIONAL` | No regression. F-613-3 and S-1 are open |
| KAFF-614 | `BUILT verdict=REJECTED` | AC-11 is NOT MET (photo kind, placement). AC-12 is untested |
| KAFF-615 | `BUILT verdict=REJECTED` | AC-10 is NOT MET (placement). S-2 is a condition on AC-5 |
| KAFF-420 | `BUILT verdict=REJECTED` | AC-1 and AC-5 are now MET. AC-3 is NOT MET for the owner (S-2) |

If the Scrum Master rules that S-3 is a shell defect owned by KAFF-936 and not by these screens, KAFF-615 becomes
`VERIFIED verdict=CONDITIONAL`. KAFF-614 still fails on the photo kind.

## Not reached

- The on-screen keyboard at 360 and 390 px (KAFF-614 AC-12).
- `ng test` and the full Api suite (not re-run).
- A full owner crew edit, driven on screen.
- The supervisor's screens: checked through the API only, not screenshot.
- KAFF-936's own trailer: flagged here, not verified.
