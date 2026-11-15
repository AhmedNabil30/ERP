# Verification — slice 6 re-verify (KAFF-600, KAFF-613, KAFF-614, KAFF-615), KAFF-420 re-verify, KAFF-936 first verify · 2026-11-16

**Verifier:** fresh session, did not write the code. **Model:** Opus 5.5 (claude-opus-5-5).
**Tree:** HEAD `9fb1c83`, working tree clean at start. Previous pass at `8f8898a` (`qa/slice-6/verification-2026-11-15.md`).
**Read:** `CLAUDE.md`, top of `STATUS.md`, the six story files, D-224, D-225, D-226, the previous report.

Findings are written as they are reached.

## Note on the clock

The pass started at 22:14 UTC on 2026-11-15, which is 00:14 on 2026-11-16 in Cairo (+02:00 in November). The running stack's
real clock was therefore inside the 22:00-24:00 UTC window that S-1 is about, and every probe below up to about 23:00 UTC was
run inside it. No clock was faked on the running stack.

## S-1 (site day) — closed, proven on the running stack inside the window

Server clock 2026-11-15T22:15:44Z. As `site_eng_demo` and `site_sup_demo`, `POST .../daily-logs/check-ins` with
`occurredAt = "2026-11-15T22:15:44+00:00"` (UTC offset): **201** for both (at `8f8898a` the same shape was 409
`check_in_only_today`). The logs were stored with `log_date = 2026-11-16` (read from `daily_logs`), the Cairo day. A quantity
sent at `2026-11-15T22:16:25+00:00` landed on the same 2026-11-16 log (`logId 01a1e147-d98b-...`), not on a second log for
the 15th. `DailyLog.DayOf` now takes the rule and calls `DailyLogEntryRule.SiteDate` (`src/Domain/DailyLogs/DailyLog.cs:43-47`),
and every `Record*` method takes the rule as a parameter. A grep of `src/Domain/DailyLogs`, `src/Domain/DayLabour`,
`src/Api/Features/DailyLogs` and `src/Api/Features/DayLabour` for `DateOnly.FromDateTime`, `.DateTime`, `.UtcDateTime` and
`.Offset` finds only `DailyLogEntryRule.SiteDate` itself. Engagement open and close now use `SiteDate(now)`.

Still open, outside these stories and already recorded in D-224 "Not decided here": `ExtractSupport.Today`,
`ClosureSupport.Today`, `AdjustmentSupport.Today` and `SignProject/Handler.cs` take the UTC day.

The scalar `checkIn` of `GET .../daily-logs/mine?date=2026-11-16` was `"2026-11-15T22:15:44+00:00"` after a check-in through
the new route, so the behaviour F-613-3 asks about holds on the stack (whether a test asserts it is checked below).

## Photos — probed with real requests on the running stack (item 1)

Project `فيلا 47` (`01a0de49-...`). Files built in the scratchpad: a 104-byte JPEG header, a JPEG header followed by HTML and a
script (polyglot), plain text named `.jpg`, an SVG named `.png`, a 10 MiB + 4 byte JPEG, a JPEG of exactly 10 MiB.

Upload, `POST .../daily-logs/photos/upload`:
- junior and supervisor: 201 with a server-generated reference; owner, Accounts, Technical Office: **403**;
- the junior on a project he is not assigned to (`01a1caa3-...`): **403** (correct role, no assignment);
- text and SVG: **400** `daily_log.photo_not_an_image` whatever the content type header said; the polyglot: 201 (it starts with
  JPEG bytes) and is served back as `image/jpeg` with `nosniff`, so a browser will not run it;
- 10 MiB + 4: **413** `daily_log.photo_too_large`; exactly 10 MiB: 201. Sent chunked with no length: 413 from Kestrel's body
  limit (a bare problem body with no `code`, so the screen cannot show the specific message; Low).

Record, `POST .../daily-logs/photos`: a reference wrapped in braces is refused 400 `file_reference_invalid`. The path and
other-project cases are covered by `Recording_a_photo_refuses_a_reference_that_is_not_one_this_project_holds` (seven inputs) and
`..._uploaded_to_another_project`.

Download, `GET .../daily-logs/photos/{reference}`:

| Caller | junior's recorded photo | uploaded but never recorded | supervisor's photo |
|---|---|---|---|
| junior (author) | 200 `image/jpeg`, `X-Content-Type-Options: nosniff` | 404 | **404** |
| supervisor | 200, nosniff | 404 | 200 |
| owner | 200, nosniff | 404 | 200 |
| Accounts (Finance) | 403 | 403 | — |
| Technical Office | 403 | 403 | — |
| anonymous | 401 | | |

The owner fetching the same reference under another project's route: 404. A non-Guid reference (`..%2F..%2Fappsettings.json`,
`{ref}.jpg`): 404 by the route constraint. The files sit in `src/Api/bin/Release/net10.0/site-photos/{project:N}/{photo:N}.jpg`;
the API has no static file middleware and `GET /site-photos/{project:N}/{photo:N}.jpg` returns 401 from the API and 404 from the
dev server. No guessable path serves a file.

Audit: the `PhotoEntry` rows written by the probe each have a `Created` audit row with actor, role, project and caption
(`audit_records`). The upload itself writes no audit row; it writes no database row either, only a file, and the entry that
makes it visible is audited. Not a defect against `CLAUDE.md`, which asks for audit on state changes.

`DailyLogRead` is used by exactly one endpoint, `GetPhoto` (grep of `src`), and by no web code. It widens nothing else.

### Finding P-1 (Low) — a reference can be re-recorded by another engineer on the same project

`RecordPhoto` checks only that the project holds a file with that id (`RecordPhoto/Handler.cs:52-62`); it does not check who
uploaded it, and two entries may name the same file. Probe: the supervisor uploaded and recorded a photo; the junior then
recorded the same reference on his own log (201), after which the junior can download it. The junior must already know a random
version 4 Guid that no read sends him (the junior's reads carry only his own log), so the exposure is theoretical. A fix would
record the uploader with the file, or refuse a reference already named by an entry.

### Finding P-2 (Low) — an upload is accepted without a check-in and is never cleaned up

`UploadPhoto` is gated by `DailyLogWrite` only; the check-in gate runs on `RecordPhoto`. An assigned engineer may therefore
write any number of 10 MiB files to disk without checking in and without recording them. The handler's own `ponytail:` note
names the missing cleanup. Disk use, not a confidentiality issue.

## `canCorrect` against the correct routes, per role and day (item 4) — MET on the stack

Server clock about 22:20 UTC (00:20 on the 16th in Cairo). The junior recorded on the 16th (today) a quantity `9af80768` and a
crew line `45d69f79`, and on the 15th (one day back) a quantity `f53d6059` and a crew line `1567d395`. The flags read from
`GET .../daily-logs/mine` (junior) and `GET .../daily-logs?date=` (supervisor, owner), then the real `POST .../corrections`:

| Caller | qty today | crew today | qty 15th | crew 15th |
|---|---|---|---|---|
| junior (author) | true / 201 | true / 201 | false / 403 | false / 403 |
| supervisor | false / 403 | false / 403 | true / 201 | true / 201 |
| owner | false / 403 | true / 201 | false / 403 | true / 201 |
| Accounts, Technical Office | — / 403 | — / 403 | | |

Twelve flags, twelve matching answers. Both come from `DailyLogAccess.CorrectionRightsAsync`. `backDated` is absent from the
junior's body and `true` on the 15th for the supervisor and the owner (KAFF-614 AC-3, KAFF-615 AC-8).

## Crew read and pool read (item 4)

`GET .../day-labour/crew`: junior 200, supervisor 200, keys `workerId`, `name`, `trade` only; owner, Technical Office, Accounts
403 (the crew screen treats the owner's 403 as an empty roster and builds from the review read, `crew-page.ts:484-494`).

`GET .../day-labour` (pool): junior and supervisor 200 with keys `averageRating babId frequency fullName id isActive specialty`,
**no `phone`, no `code`**; owner 200 with `phone` and `code` (he holds `EmployeeManage`); Technical Office and Accounts 403.
The earlier Low note on `ListPool` is closed.

The unlinked page `/projects/:id/day-labour/pool` still binds `worker.code` and `worker.phone`
(`worker-pool-page.html:16-78`), including in its `data-testid`s. See the screen check below for what a junior sees there.

## Material unit (item 5) — MET

On the stack, `POST .../daily-logs/materials` with `unit` = «كيس» 400 `daily_log.unit_not_allowed`, `"Bag"` 400, `"../x"` 400,
`"bag"` 201, `" m3 "` 201 (trimmed to `m3`). The audit after-image of the new row has `"Unit": "bag"`. The key list is
`Kaff.Domain.DailyLogs.MaterialUnits.Keys` and the web list `core/daily-log/material-units.ts` matches it. A legacy row is
readable: `materialUnitLabel` shows a stored non-key as stored, and both are tested
(`DailyLogUnderD217Tests.cs:472-501` sets a row to «كيس» in SQL and reads it back; `daily-log-page.spec.ts:681`). S-6 is closed.

## Owner's review read body (item 3, server half)

`GET .../daily-logs?date=2026-11-16`: owner 200, supervisor 200, junior 403, Technical Office 403, Accounts 403. Every key in the
owner's and the supervisor's bodies: `accuracyMetres babName backDated boqLineId canCorrect caption checkIn checkIns checkOut
corrected correctedAt correctedByName correctedByUserId dayLabour descriptionAr deviceTime distanceFromSiteMetres engineerId
engineerName entryId expenseDrafts fileReference flagReason id isPublished isStockpile itemCode kind latitude log logDate
longitude materialType materials notes occurredAt photos previousQuantity quantities quantity serverTime stoppageReasons unit url`.
No rate, amount, cost, price, value, balance or phone. `ExpenseDraftLine` carries a description and a time, no amount.

**`itemCode` is in the body** (for example `MAS-3004`), together with entity ids. Neither page renders it (grep of both
templates), so KAFF-420 AC-7 ("no code or id on the screen") holds. If the brief's "no code in any body" is the bar, this read
does not meet it; it is the same `QuantityLine` the previous pass noted. Low.

`tabs.dailyLogMode`: owner `review`, supervisor `write`, junior `write`, Technical Office `null` (`dailyLog: false`), Accounts
`null`.

## Owner's daily log on screen (item 3) — S-2 closed

Driven with `driver.mjs signed-shot owner_demo ... /projects/{id}` and the step `click:اليومية`, at 390 px: the owner lands on the
read-only page (`daily-log-review-page`), not on `/forbidden`. The page shows the four-day switch, «يوم آخر», the crew row
linking to `/projects/{id}/day-labour`, and one block per engineer with attendance («الحضور 00:15», «بعيد عن الموقع · 16,571 متر»,
«لم يُسجَّل الانصراف»), quantities, materials, notes and photos. `layout` at 360, 390 and 1440: PASS (rtl, overflow 0, no small
targets, no raw keys, no ids).

Routing: `app.routes.ts` now has three routes on `projects/:projectId/daily-log`, matched by `dailyLogModeMatch('write')`,
`dailyLogModeMatch('review')`, and a fallback redirect to `/forbidden`. The guard reads `tabs.dailyLogMode` from the server, not a
role name. As `tech_office_demo` and `accounts_demo` typing `/projects/{id}/daily-log`: «غير مصرح بالدخول» (`signed-text`).
No seeded Marketing user exists; Marketing is covered only by
`Technical_office_marketing_and_finance_read_no_daily_log_and_no_photo(MarketingSales)`.

No write route opens to the owner on the server: as `owner_demo`, `POST` to `quantities`, `materials`, `photos`, `fields`,
`day-labour`, `expense-drafts`, `check-ins`, `photos/upload`, and `GET mine`, `GET boq-lines`: all **403**. The review page
template has no write control (no `kaff-button` other than retry; the crew link leads to the crew screen, where D-218 item 16
lets him edit).

The three departures the builder lists, against `.design/v2/team-a/daily-log-review.html` r2 (screenshot of the mock at 390 px
taken in this pass):
1. **Date control on its own row on a phone.** The mock draws «يوم آخر» beside the chips on one row. Built: it wraps below. D-226
   item 4 says "one date control beside the four-day switch". At 390 the four chips fill the row. My reading: a layout departure,
   not a behaviour one; the control exists and works. Condition, not NOT MET.
2. **Day chips without the day number.** The mock shows «اليوم 10», «أمس 9». Built: weekday names only. The chosen day is printed
   in full under the switch («الاثنين، 16 نوفمبر»), so the date is never ambiguous. Condition.
3. **Switch visible while loading.** Not visible in a static screenshot; the template shows the switch above the `@if (loading())`
   block. Harmless. Condition.

No story criterion is written against this page except KAFF-420 AC-3 (the tab opens) and KAFF-615 AC-5 (the crew link). All three
departures should go to the designer for a yes or no, per the rule that an approved mock is not hand-edited.

## Shell bottom bar (item 6, KAFF-936)

At 360 and 390 px (junior, `site_eng_demo`): the daily log shows «حفظ» in the bottom bar beside «القائمة», and the crew screen
shows «تعديل» there. Nothing is drawn at the top any more (`app.html` hides `.page-actions` when `phone() && session()`). At 1440
the save is in the page header and not in a bar. The client list at 390 puts «+ عميل جديد» in the bar and keeps its search field
on the page. `layout` PASS at 360 and 390 for both screens. S-3 is closed for the daily log and the crew screen.

The daily log's save is drawn **greyed** in the bar when nothing is entered (screenshot `dl-360`). That is form validity, not a
permission, so AC-936-E does not apply; noted only because the mock does not draw a disabled save.

**KAFF-614 AC-12 (field visible above the bar while typing).** A desktop Chromium with an emulated viewport raises no on-screen
keyboard, so it cannot prove this. `index.html` sets `interactive-widget=resizes-content` and `.app` is `100dvh` with the bar in
flow (`app.css:17-24`), which on Android Chrome shrinks the layout above the keyboard; iOS Safari ignores `interactive-widget`.
**A real phone is needed, one Android and one iPhone.** Stays UNTESTED.

The photo kind is on the screen: the kind grid has ملاحظة, كمية, خامات, صورة, سبب التوقف (five, as the mock). S-4 is closed on the
screen; the upload path is above.

## Earlier findings — status at `9fb1c83`

| Finding | Status | Evidence |
|---|---|---|
| S-1 site day | **Closed** | Live probe above; `DailyLogSiteDayTests` (four cases, a clock past 22:00Z) |
| S-2 owner's اليومية tab | **Closed** | Owner clicks اليومية and reaches the review page; `dailyLogMode` |
| S-3 primary at the top on a phone | **Closed** for the daily log and the crew screen | Screenshots at 360 and 390 |
| S-4 photo kind missing | **Closed** | Kind present; upload and download probed |
| S-5 no Accounts confirm path | **Retired by ruling** | D-225 item 3 moved the confirm half of AC-600-H to release 2; the story text was amended |
| S-6 material unit in UI language | **Closed** | Key on the wire, non-key refused, legacy row readable |
| S-7 «الكمية المقاسة» on an estimate | **Open, Low** | `daily-log-page.html:114` still labels `lineTotal` as measured; `lineTotal` falls back to `quantityEstimated` (`daily-log-page.ts:362-363`) |
| S-8 a crew line cannot go to zero | **Open, question for Nabil** | Unchanged |
| S-9 corrections need no check-in | **Open, Low** (two rulings conflict) | Unchanged; the owner corrected crew in this pass without any check-in |
| S-10 AC-600-I test | **Closed** | `DailyLogTests.cs:341-362` now uses `+03:00`, a clock at 21:30Z (00:30 Cairo) and asserts the log dates `2026-09-25,2026-09-26` |
| F-613-3 scalar `checkIn` untested | **Closed, and was already covered at `8f8898a`** | `AC_600_E_the_later_timestamp_wins_for_the_check_in...` (`DailyLogTests.cs:191-199`, from `bb00dce`) checks in through `/check-ins` and reads the scalar `checkIn`; a null would throw. Also `DailyLogOwnerReviewReadTests.cs:114`. The previous pass missed it |
| Pool sends phone and code to site engineers | **Closed** | Live keys above |

## KAFF-936 · first verification (the story was BUILT and never verified)

The trailer says `BUILT` while the story body still says "Status: BLOCKED" on a Definition-of-Ready box (no QA scenarios). The
mechanism is `HeaderActionsService` (one `TemplateRef` signal), rendered by `app.html` in `.page-actions` on desktop and in
`.abar-action` beside the menu at `max-width: 820px` (`app.ts` `phone`). Tests: `app.spec.ts`, three cases under
"App shell primary-action slot (KAFF-936)".

| AC | Verdict | Evidence |
|---|---|---|
| A one declaration mechanism | MET | The daily log and the crew screen put their primary inside an `<ng-template #primaryAction>` handed to `HeaderActionsService`; only the shell's outlets render it. Read literally, the screen's template file still contains a `kaff-button variant="primary"`, inside the `ng-template`; the story leaves the mechanism to the builder, and a `TemplateRef` mechanism cannot avoid that. |
| B desktop placement | MET | 1440 px: «حفظ» in the page header, `.abar` not rendered; `app.spec.ts` "at desktop width ...". |
| C phone placement | MET | 360 and 390 px: «حفظ» and «تعديل» in `.abar` beside «القائمة», not at the top; `--kaff-button-size: 60px` (`app.css` `.abar-action`), the bar's padding adds `env(safe-area-inset-bottom)`. |
| D zero actions: menu expands | **NOT MET** | `.menu` has `min-inline-size: 116px` and no `flex: 1` (`app.css:184-196`). The owner's review page (no declared action) at 390 shows the menu at its 116 px width with the rest of the bar empty. The spec case "with no declared action the bar holds the menu alone" asserts only that no slot exists, so it passes with the menu not expanded. |
| E absent, never disabled | **NOT MET** | The absent half holds for the screens checked (the owner's review page declares nothing). The criterion also requires "the bar behaves as AC-936-D", which fails. |
| F two or more | HELD on Q117 | Not judged. |
| G destructive refused loudly | **NOT MET** | Not built. `HeaderActionsService.set` takes any `TemplateRef`; no type or runtime check. |
| H overlay hides the bar | **NOT MET** | Not built. Only the nav sheet hides the bar (`.app[data-sheet='1'] .abar`); `kaff-confirm-dialog` and `kaff-detail-panel` do not. |
| I a violating template fails a test | **NOT MET** | Not built. No allowlist test exists (grep of `src/Web/src` for an allowlist finds none related). |
| J the two screens move onto the contract | **NOT MET** | The daily log moved (no `.footer`, `daily-log-save` still reaches the button). `worker-register-page.html:119-120` still renders its private `.footer` with `kaff-button variant="primary" testId="worker-submit"`, and its CSS keeps `.footer` (`worker-register-page.css:127-138`). |
| K RTL, logical properties | MET | The added CSS uses `gap`, `min-inline-size`, `inline-size` only; `layout` PASS at 360 and 390 on the daily log, crew and client list. |
| L strings | MET (vacuous) | The change adds no string (the bar's text is the screen's own key). The commit `66f0508` does not say so, which the criterion asks. |

**KAFF-936: 11 judged, 5 MET, 6 NOT MET (D, E, G, H, I, J), 1 HELD (F).** Recommendation: `BUILT verdict=REJECTED`. The part
the slice-6 stories need (B, C) works.

## Tests that can fail (mutations, reverted with `git checkout`, rebuilt clean)

- `GetPhoto/Handler.cs`: the clause `(readsAll || log.EngineerId == caller)` removed, so a junior could read every photo on the
  project. Together with the next mutation, in one build:
- `DailyLog.DayOf`: changed back to the wall-clock date of the offset the client wrote (`DateOnly.FromDateTime(occurredAt.DateTime)`).
- Run of `DailyLogPhotosAndOwnerReadTests`, `DailyLogSiteDayTests`, `DailyLogTests` (133 tests): **7 failed**:
  `Download_follows_the_log_read_rules_a_junior_sees_his_own...` (the photo mutation), and
  `A_check_in_sent_in_utc_after_2200_is_accepted_as_today...`, `A_quantity_sent_in_utc_after_2200_lands_on_the_cairo_day`,
  `The_same_instant_gives_the_same_answer_whatever_offset...` and the three cases of `The_three_day_window_is_counted_in_site_days`
  (the day mutation). `AC_600_I_...` stayed green under the day mutation, correctly: it writes the Cairo offset, so the wall clock
  and the site day agree; the UTC cases are the ones that guard S-1.
- `app.html`: the `.abar-action` block removed. `ng test --include src/app/app.spec.ts`: "on a phone the declared action is in the
  bottom bar beside the menu" **failed**, the other two passed.
- Tree after: `git status` shows only this report. Release build after the revert: 0 warnings, 0 errors.

## Prohibitions on the changed code (item 7)

- **Append-only corrections:** a grep of the diff `8f8898a..HEAD` under `src` for `ExecuteUpdate`, `ExecuteDelete`, `.Remove(` and
  `RemoveRange` finds none. The corrections made in this pass each produced a new 201 entry; no entry row was changed.
- **Deltas, not totals:** unchanged; corrections compute `currentFigure` and store the signed difference
  (`CorrectQuantity/Handler.cs:75-95`).
- **Money:** no `float` or `double` near money in the diff (the one `double` is latitude and longitude). No balance column. No
  posting path touched. The hold is not touched.
- **Role and assignment:** every new endpoint (`UploadPhoto`, `GetPhoto`) uses `RequirePermission(..., ProjectScope.FromRoute())`;
  correct-role-not-assigned refused live (upload) and in tests (download).
- **Create and approve:** no approval flow in the changed code.
- **Audit:** the new `PhotoEntry`, `MaterialEntry`, `ExecutedQuantityEntry` rows of this pass each have a `Created` audit row.
  The upload writes a file and no row (see P-2).
- **No hardcoded strings:** a scan of the changed templates and components (`daily-log-review-page.*`, `daily-log-page.*`,
  `app.html`, `kaff-day-switch.ts`, `kaff-attendance-strip.ts`) for Arabic text outside comments finds none.
- **RTL:** the added CSS in the diff has no physical `left`, `right`, `top`, `bottom`, `margin-*`, `padding-*`, `width` or `height`.

## Formatting (item 8)

`dotnet format KaffErp.sln --verify-no-changes --no-restore`: exit 2, **7877 findings, every one `ENDOFLINE`, 0 others.**

## Gates run in this pass, at `9fb1c83`

- Release build: 0 warnings, 0 errors (before the probes, and again after the mutations were reverted).
- Api.Tests, full suite: `total: 1099`, `failed: 0`, `succeeded: 1097`, `skipped: 2`, exit code **0**.
- Domain.Tests: 732 / 732, exit code 0.
- `ng test --watch=false`: 51 files, **444 / 444**, exit code **0**.
- Not run: `ng build --configuration production`, the E2E suite.

## Verdicts per criterion

**KAFF-600**: 12 judged (A to J, K1, K2; K retired). **All 12 MET.** H is judged as amended by D-225 item 3 (a draft, no posting;
`AC_600_H_an_engineers_expense_is_stored_as_a_draft_and_writes_no_posting`). I is MET now that S-1 and S-10 are closed. K1: RTL,
overflow 0, logical properties, the save visible (now in the bar). K2: the save is declared to the shell and drawn beside the menu,
the private `.footer` is gone from markup and CSS, `daily-log-save` still reaches the button.

**KAFF-613** (re-check): no regression found. F-613-3 is closed (and was covered at `8f8898a`), the S-1 condition on AC-G is closed.

**KAFF-614**: 13 criteria. MET: 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13. MET with condition: 1 (S-9). **UNTESTED: 12** (needs a real
phone). Low: S-7, P-1, P-2.

**KAFF-615**: 11 criteria. **All MET.** AC-5's condition (S-2) is closed: the owner reaches the crew screen from the review page's
«العمالة» row and his crew corrections returned 201. AC-10 is MET on placement; the rest of the mock comparison is the
previous pass's. S-8 stays a question for Nabil.

**KAFF-420**: 7 criteria. **All MET.** AC-3: the owner's and supervisor's tabs are complete and the owner's اليومية opens; the
junior has no `boq` and no `extracts`; Technical Office and Accounts get `dailyLog: false` and are refused the route.

**KAFF-936**: 11 judged, 5 MET (A, B, C, K, L), **6 NOT MET (D, E, G, H, I, J)**, F HELD.

## Recommended trailers

| Story | Recommendation | Why |
|---|---|---|
| KAFF-600 | `VERIFIED verdict=PASS` | Every criterion MET |
| KAFF-613 | `VERIFIED verdict=PASS` | Both open conditions closed; no regression |
| KAFF-614 | `VERIFIED verdict=CONDITIONAL` | AC-12 UNTESTED until a real Android and iPhone check; S-9, S-7, P-1, P-2 Low |
| KAFF-615 | `VERIFIED verdict=PASS` | Every criterion MET; S-8 is a question, not a violation |
| KAFF-420 | `VERIFIED verdict=PASS` | AC-3 now MET. The owner's review page departs from the D-226 mock in three layout points (above); they belong to no KAFF-420 criterion and should go to the designer |
| KAFF-936 | `BUILT verdict=REJECTED` | D, E, G, H, I, J NOT MET |

## Not reached, and what could not be done

- The on-screen keyboard (KAFF-614 AC-12): not possible in an emulated desktop viewport. Needs a real phone.
- The server clock was not faked on the running stack; the real clock happened to be inside the 22:00-24:00 UTC window. Times
  before 22:00 UTC on a Cairo day boundary in summer (+03:00, 21:00-24:00 UTC) were covered only by the tests.
- No seeded Marketing user: Marketing's refusal is proven only by tests.
- A user with the Site Engineer role and no assignment to the probed project was not available on the stack; the junior on an
  unassigned project stood in for it (upload 403).
- `ng build --configuration production` and E2E: not run.
- The crew screen against its mock beyond the bar placement: not re-compared.
- The owner's review page with `kaff-confirm-dialog` or a picker sheet open (AC-936-H): read from code only.

## Left behind

- Probe rows on the local dev database (project `فيلا 47`): check-ins, quantities, materials (two with `?????` types from a shell
  encoding), crew lines and their corrections, five photo entries, for 2026-11-15 and 2026-11-16. They are entries, append-only,
  and were not removed.
- Photo files under `src/Api/bin/Release/net10.0/site-photos/` (build output, ignored by git).
- Two more `kaff_test_*` databases from the Api runs.
- No change under `src/` or `tests/`; the API and dev server started for this pass were stopped.
