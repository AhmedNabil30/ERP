# Verification pass — 2026-09-13, the D-150 repair round

**Scope:** `35b0550..HEAD` (`54ff044`), 22 commits. The repair round after D-150's verdict, plus the
new build under D-151, D-152 and D-153. Stories touched: `KAFF-207`, `KAFF-210`, `KAFF-211`,
`KAFF-212`, `KAFF-200`, `KAFF-208`, `KAFF-209`.

**Verifier:** a fresh session on `opus`. I wrote none of this code. Findings are `V-39-A` onward.
**No verdict in this file is `ACCEPTED`** — the unwritten E2E suite (`V-38-L`) caps every slice-2
story at `CONDITIONAL` (D-138), and `ACCEPTED` is Nabil's word, not a Verifier's.

**Working tree at the start:** `STATUS.md` modified, nothing else. I changed no story, no trailer and
nothing under `src/`. The only file this pass writes is this one.

---

## 1. Gates — measured by me, on this machine, at `54ff044`

Every number below was produced by a command I ran in this session. Nothing is quoted from
`STATUS.md` or from a builder's report. Stranded hosts were killed before the build, per the
`run-kaff-erp` gate order.

| # | Gate | Command | Result |
|---|---|---|---|
| 1 | Build, Release, warnings as errors | `dotnet build KaffErp.sln -c Release --nologo` | **exit 0 · 0 Warning(s) · 0 Error(s)** ✅ |
| 2 | Format | `dotnet format KaffErp.sln --verify-no-changes --no-restore` | **exit 0, no output** ✅ |
| 3 | Domain.Tests | `tests\Domain.Tests\bin\Release\net10.0\Kaff.Domain.Tests.exe` | **total 240 · failed 0 · succeeded 240 · skipped 0, exit 0** ✅ |
| 4 | Api.Tests | `tests\Api.Tests\bin\Release\net10.0\Kaff.Api.Tests.exe` | **total 557 · failed 0 · succeeded 557 · skipped 0, exit 0** ✅ |
| 5 | SPA production build | `npm run build` (in `src\Web`) | **exit 0 · zero warning lines · budget clean** ✅ — `V-38-B` is repaired, see §5 |
| 6 | vitest | `npm test` (in `src\Web`) | **18 files · 106 tests passed, 0 failed, exit 0** ✅ |

Gate 5 is the one `V-38-B` was about. `ad19b7b` trimmed `catalogue-list-page.css` under its 4.00 kB
budget, and the build now emits **no** warning line at all — I grepped the full log for
`warning`/`Warning`/`budget` and it matched nothing outside chunk filenames.

### 1a. Api.Tests, and the skip that is gone

The run produced a `total:` line, so it is a result and not a truncated log:

```
  total: 557
  failed: 0
  succeeded: 557
  skipped: 0
```

**`skipped: 0` is the interesting number.** Every Backend report from `63a57e7` onward carried
"1 skipped" — the `Q80` case held pending Karim. D-152 §4 answered it and `427c8ad` rewrote the test
as warn-and-acknowledge; there is now **no `Skip` anywhere in `tests/`** except
`E2E.Tests/E2EEnvironment.cs`, which skips only when the application is not running. See §5.

### 1b. All six gates are GREEN

Build 0/0 · format clean · Domain 240/240 · Api 557/557 · SPA build 0 warnings · vitest 106/106.
**Nothing in the gate set blocks Nabil's authorised `git push`.** What caps the stories is the
missing E2E suite and the findings in §7, not a red gate.

---

## 2. `V-38-H` — the retention rate, driven live

**The money-shaped item, and the reason D-150 rejected nothing else this round.** D-151 rules that a
rate crosses the wire as a **fraction**, a JSON string, typed `Percentage` in both directions.

### How it was driven

A scratch database, **`kaff_verifier_v39`**, created in the `kaff-db` container — never `kaff`.
`GET /api/setup` answered `{"available":true}` against it before setup, which is only possible on a
database with no Owner, so the API was demonstrably not on the dev database. The API ran on 5080
against it and the SPA on 4200. One client row and one project row were inserted with SQL, because
there is no projects endpoint on this API and every `KAFF-209`/`210` route is under
`/api/projects/{projectId}`. Every other fixture was created through the API or through the SPA.

### The round trip

| Step | Request | Response |
|---|---|---|
| Create at 5% | `POST /api/subcontractors` with `"retentionRate": "0.05"` | `201`, `"retentionRate":"0.05"` |
| Read | `GET /api/subcontractors/{id}` | `200`, **`"retentionRate":"0.050000"`** |
| Write that response back **verbatim** | `PUT /api/subcontractors/{id}` with `"0.050000"` | `200`, `"0.050000"` |
| Read again | `GET /api/subcontractors/{id}` | `200`, **`"0.050000"`** |

**Byte-identical across the round trip**, and the stored column reads `0.050000` in SQL
(`select retention_rate from subcontractors`). **5% never became 0.05% and never became 500%.**
D-150's `V-38-H` — the defect that turned a read-and-write-back into 0.05% — **is closed.**

### The refusals D-151 §2 requires

| Case | Measured |
|---|---|
| `retentionRate` omitted from a `PUT` | **`400 master.retention_rate_required`**, `messageKey` `errors.master.retention_rate_required` ✅ |
| Body that does not parse (`{"name": "x", `) | **`400 wire.malformed_body`**, `messageKey` `errors.wire.malformed_body` ✅ |
| Negative rate (`"-0.05"`) | **`400 wire.malformed_body`** — `Percentage`'s own constructor refuses it before a handler sees it, and `PercentageJsonConverter` rethrows as `JsonException` so it lands as a `400` rather than a `500` ✅ |

### The SPA half (`d6e697f`)

Driven, not read. The subcontractor edit screen at `/subcontractors/{id}` was loaded in a 390px frame
with the Owner signed in, `fetch` was wrapped to record the outgoing request, and the form's own
submit button was clicked. What it sent:

```
PUT api/subcontractors/{id}
{"name":"VRF39 Sub","phone":"01011100039","tradeBabId":"…","retentionRate":"0.05","acknowledgedDuplicatePhone":false}  -> 200
```

**A fraction, as a string.** The field displayed `5` for a stored `0.050000`, and after the save the
column still reads `0.050000`.

**No `Number()` or `parseFloat` touches a rate anywhere in `src/Web`.** I grepped the whole app for
`Number(`, `parseFloat` and `parseInt`: every hit is a comment forbidding it, except three that are
not rates — `bab-form-page.ts` -> `sortOrder` (`Number.parseInt`, an integer ordering), and
`worker-pool-page.ts` -> `ratingDraft` (`Number`, a 1–5 rating, not money). Conversion runs through
`percent-wire.ts`'s string-shift helpers in both directions.

**Server-side the unit is carried by the type, not by a handler.**
`tests/Api.Tests/WireUnitTests.cs` -> `Every_rate_on_the_wire_is_typed_Percentage` reflects over every
`Request`/`Response` record under `Kaff.Api.Features.*` and fails on any `decimal`-typed
`*Rate`/`*Markup`/`*Percentage` member outside a documented allow-list, so the next rate added in the
wrong unit reddens a gate rather than a balance.

> **`V-39-A` · LOW** · `src/Api/Features/Subcontractors/CreateSubcontractor/Response.cs` and
> `GetSubcontractor/Response.cs`. The same rate serialises as **`"0.05"` from `POST`** and
> **`"0.050000"` from `GET`** — the create response echoes the submitted scale, the read carries the
> column's `numeric(18,6)` scale. Both are the same number and the D-151 round trip (GET → PUT → GET)
> is byte-stable, so this is **not** the `V-38-H` defect returning. It matters only for a client that
> byte-compares a create response against a later read. **What I did:** measured it, wrote it down,
> changed nothing.

> **`V-39-B` · LOW** · `src/Web/src/app/features/subcontractors/subcontractor-form/subcontractor-form-page.ts`
> -> `SubcontractorDraft.retentionPercent`. The doc comment still reads *"the wire's own unit here
> (AC-211-C), not a fraction"*, which is what the code did **before** `d6e697f` and is the opposite of
> what it does now. The field is the operator's percent and `payload()` converts it; the comment says
> the wire takes the percent. A stale comment on the exact member that carried `V-38-H` is worth one
> line to fix. **What I did:** named it. Changed nothing.

---

## 3. `KAFF-207` — the `V-38-C` repair, driven

D-150 rejected `KAFF-207` because HR could neither load nor save a day labourer whose باب was set:
the `<select>` bound `[value]` before its options existed, so a stored باب rendered as *بدون باب* and
the save then tripped `400 day_labour_requires_trade`. `3295c31` binds `[selected]` per option
instead.

**Driven on the real screen**, Arabic, 390px, Owner signed in, against a day labourer created through
the API with `babId` set to `CON` (أعمال خرسانة):

- **Load.** The باب `<select>` reads `value = 01a09b57-…` and the selected option's text is
  **أعمال خرسانة** — not *بدون باب*. The specialty, phone and name all carry their stored values.
- **Save.** Clicking the form's own submit button sent
  `PUT api/employees/{id}` with **`"babId":"01a09b57-…"`** and the server answered **`200`**. No
  `day_labour_requires_trade`, no silent null.
- **After the save** the row still holds its باب in SQL (`bab_id is not null` for `E-10001`).

**Both halves of `V-38-C` are repaired.** The screen also renders `نوع التكلفة · يومية` rather than
`enum.EmployeeKind.[object Object]`, so `V-37-D` has not returned either.

`KAFF-207`'s own `Department` field (`c5b3a24`, `2723fd8`) is present on the form and round-trips:
the salaried fixture was created with `department: "Finance"` and `GET /api/employees/{id}` returns
it. ⚠️ The Frontend session flagged that its asked-for **edit round-trip test** may not exist and only
a label test does; I did not audit `employee-form-page.spec.ts` case by case — recorded in §8 as not
reached.

---

## 4. `KAFF-210` against its own acceptance criteria

D-150 rejected it as *"about four criteria of eleven"*. D-152 answered `Q76`–`Q78` and D-153 §1 gave
the mechanism, and `224cf8a` / `6906ec3` / `f391f12` built the rest. **I drove all eleven.** Fixtures:
one day labourer with two engagements (rates `300.0000` and `487.6543`, ratings 4 and 2), one worker
never engaged, one project, and five role users — HR, two Site Engineers, Finance, Technical Office —
each signed in for himself.

| AC | Met? | What I measured |
|---|---|---|
| **A** — an engagement is recorded against a worker | ⚠️ **substantially** | `POST …/day-labour/engagements` → `201`; it appears in the history read and moves the pool figures. **But the dates are not the operator's** — see `V-39-C` |
| **B** — the three figures are derived, never stored | ✅ | `engagements` holds `id, worker_id, project_id, day_rate, opened_on, opened_at, closed_on, closed_at, rating, opened_by_user_id` — **no average, no count, no total, no cached rating**, on the engagement or on the employee. `ListPool` and `ListEngagements` both compute on every read |
| **C** — four decimals, never through a float | ✅ | `487.6543` submitted, `"487.6543"` returned, `487.6543` in the column. `Money` throughout; no `float`/`double` on the path |
| **D** — the average is the average of the rates paid | ✅ | Two engagements at `300.0000` and `487.6543` read back `averageDayRate "393.8272"`. A **third** engagement at `250.5000` moved it to `"346.0514"` on the next read **with nothing else written** |
| **E** — no posting, no account | ✅ | `select count(*) from postings` = **0** after every act. The 14 accounts in the database were all written at Owner bootstrap (15:16:33), minutes before the first engagement (15:20:34) |
| **F** — an engagement is a stretch, a rating is out of 5 | ✅ | `frequency` counts engagements. `rating: 7` and `rating: 0` both refused **`400 master.engagement_rating_out_of_range`**; 4, 2 and 5 accepted |
| **G** — never engaged reads as unengaged, not as zero | ✅ | API returns `averageDayRate: null`, `frequency: 0`, `averageRating: null`; the pool and the history screen both render **لم يُشغَّل من قبل** in all three places, never `0` and never a blank |
| **H** — a role without the permission reaches nothing | ✅ | See the table below |
| **I** — the engagement and the rating are audited | ✅ | Seven `Engagement` audit rows for seven acts: `Created` ×2, `Modified ["DayRate"]` ×2, `Modified ["ClosedAt","ClosedOn"]`, `Modified ["Rating"]` ×2 — each with actor, role, **and the route project** (`project_id` + `grant_path`), which is D-148 working |
| **J** — Arabic, RTL, 390px | ✅ | §6 |
| **K** — manual close only, and only within its own project | ✅ | Closing engagement `e2` through **another project's** route is refused `403`; nothing closes an engagement on its own — there is no timer, no hosted service and no `openedOn`-based expiry anywhere in the slice |

**Ten of eleven fully met, one (`A`) met except for the dates.**

### The permission surface, driven role by role

Every call below was made directly against the API with no browser, each role signed in as itself.

| Caller | pool (`GET …/day-labour`) | history (`GET …/engagements`) | day rate (`PUT …/day-rate`) | rating (`POST …/rate`) |
|---|---|---|---|---|
| Site Engineer, **not assigned** | `403` | `403` | `403` | `403` |
| Site Engineer 2, not assigned | `403` | `403` | `403` | `403` |
| Finance, not assigned | `403` | `403` | `403` | `403` |
| HR, not assigned | `403` | `403` | `403` | `403` |
| Technical Office, not assigned | `403` | `403` | `403` | `403` |
| Site Engineer, **assigned, not the opener** | `200` | `200` — **empty**, he opened none | `403 master.engagement_not_responsible_engineer` | `403 master.engagement_not_responsible_engineer` |
| Site Engineer, **assigned, on his own engagement** | `200` | `200` — his one engagement only | **`200`** | **`200`** |
| Finance, assigned | `403` (no `DayLabourSiteManage`) | `200` — all three engagements, `averageDayRate 346.0514` | `403` — Finance **reads** a rate, never writes one | `403 auth.forbidden` |
| **HR, assigned** | `403` | `403` | `403` | `403` |

This is exactly D-152 §2/§4 and D-153 §1: the rate is the Owner's, Finance's and **the responsible
engineer's**, nobody else's; HR is deliberately absent even when assigned; and the read is refused
for the same roles as the write (D-110 §2).

**`CloseEngagement` carries no opener check, by design, and I proved it:** Site Engineer 2 closed an
engagement **Site Engineer 1 had opened** and got **`200`**, while the same caller was refused
`403 master.engagement_not_responsible_engineer` on that engagement's day rate and on its rating.
That is D-152 §3 (an administrative act, so nothing dangles while somebody is away) set against
D-153 §1 point 4 (a rate and a judgement belong to the engineer who made them), and the code
expresses the difference in one shared guard — `src/Api/Features/DayLabour/EngagementResponsibility.cs`
-> `IsResponsibleOrOwnerAsync`, called by `SetEngagementDayRate` and `RateEngagement` and deliberately
not by `CloseEngagement`.

> **`V-39-C` · MEDIUM** · `src/Api/Features/DayLabour/OpenEngagement/Request.cs` -> `Request`;
> `stories/slice-2-masters/KAFF-210-worker-engagement-history.md` -> `AC-210-A`. **`AC-210-A` says an
> engagement is recorded *"with its project, its dates and its agreed day rate"*, and the request
> carries none of those but the worker.** `openedOn` comes from the clock, `closedOn` from the clock
> at the close, and the rate from a second call. An engagement that started last Tuesday therefore
> cannot be entered as having started last Tuesday, and a close entered on Monday for a man who left
> on Friday dates itself Monday. **A day rate multiplied by the wrong number of days is money**, and
> slice 6 is where the daily log will multiply it. I verified this is not merely the SPA's doing: no
> member for either date exists on the request record. **What I did:** measured it, named it, changed
> nothing. It is a BA question (was the clock intended?) before it is a code change.

> **`V-39-D` · LOW** · `stories/slice-2-masters/KAFF-210-worker-engagement-history.md` -> `AC-210-B`,
> `AC-210-D`, rule 2. Both criteria say *"the **pool** renders his average day rate"*. As shipped the
> pool (`ListPool`, `S-025`) carries **no money member at all** and the average day rate is on the
> rate-gated history read (`ListEngagements`, `S-027`) — which is **correct** under D-153 §1 point 5,
> because the pool sits behind the money-free `DayLabourSiteManage`. The code follows the ruling and
> the criterion follows the pre-ruling story. **Read literally, `AC-210-D` is unmet; read against
> D-153 it is met.** A criterion that only a decision outside the story can reconcile is the `V-35-N`
> shape. **What I did:** named it for the BA. Changed nothing.

---

## 5. The D-152 / D-153 mechanisms

### `Q83` — the salaried phone index is unique only among **active** salaried records

Read out of the running database, not out of a migration file:

```
CREATE UNIQUE INDEX ux_employees_salaried_phone ON public.employees USING btree (phone_normalised)
  WHERE (((kind)::text = 'Salaried'::text) AND is_active)
```

Both halves of D-153 §3's predicate are there. The other three phone indexes
(`ix_employees_phone`, `ix_subcontractors_phone`, `ix_suppliers_phone`, and `ix_users_phone_normalised`)
are **non-unique**, which is D-141. `tests/Api.Tests/EmployeePhoneMatchTests.cs` ->
`A_salaried_phone_belonging_to_an_archived_leaver_can_be_registered_again` drives the consequence:
archive the first salaried record, and the same phone is then a **warn-and-acknowledge**
(`409 duplicate_phone_not_acknowledged`, then `201` on acknowledgement), not a refusal.

### `Q80` — the test carries no `Skip` and asserts warn-and-acknowledge

`tests/Api.Tests/CreateEmployeeTests.cs` ->
`A_salaried_create_matching_an_active_day_labourer_warns_and_succeeds_once_acknowledged`. It is a bare
`[Fact]`, and it asserts `201 Created` with the reason *"D-153 §4 (Q80) — a cross-population phone
match warns, it does not refuse"*, having first asserted the unacknowledged attempt is refused.

**There is no `Skip` anywhere in `tests/`** except `E2E.Tests/E2EEnvironment.cs`, which skips only
when the application is not running — I grepped the whole tree for `Skip =` and `Skip(`. The Api run
reports **`skipped: 0`**, which is the same fact measured from the other end.

### `Q81` — the seeder skips the whole run when any باب exists. **Both branches driven.**

| Branch | What I did | What happened |
|---|---|---|
| **Empty** | Started the API against a brand-new database | **Eight trades inserted** — `CON` 0.150000, `MAS` 0.150000, `PLU` 0.200000, `ELE` 0.200000, `HVA` 0.200000, `FIN` 0.300000, `CAR` 0.250000, `MET` 0.250000, exactly D-145 §1's table, markups stored as fractions |
| **Not empty** | **Deleted seven of the eight** (left `CON` alone) and restarted the API | Log: *"Trades (أبواب) already exist; the seeder made no change (D-152 §8)."* — and `select count(*) from babs` still reads **1**. The seven missing seed codes were **not** re-inserted |

The second branch is the one that matters: a **per-code** guard would have re-inserted the seven and
quietly resurrected rows an operator had removed. The guard is the first statement of the internal
overload in `src/Infrastructure/Persistence/Seeding/BabSeeder.cs` -> `SeedAsync`, so every caller
passes through it, and the class contains no `SetDefaultMarkup` and no `Rename` — D-142 point 3's own
test of itself.

### `DayLabourRateManage = 64`, `TouchesMoney: true`, and the money-free row left alone

- `src/Domain/Authorization/Permission.cs` -> `DayLabourRateManage = 64`; `DayLabourSiteManage = 62`
  is untouched at its own number.
- `src/Domain/Authorization/PermissionCatalogue.cs` -> the `DayLabourRateManage` row is
  `ProjectScoped`, granted to **Owner, Finance and `engineerJunior`**, and carries
  **`TouchesMoney: true`**.
- `tests/Domain.Tests/CatalogueCompletenessTests.cs` ->
  `Only_the_owner_and_assigned_site_engineers_hold_DayLabourSiteManage_and_it_touches_no_money` and
  `Hr_holds_no_permission_that_touches_money` are **green and unedited**: `git show 224cf8a` on both
  test files is **additions only** — the new `DayLabourRateManage` tests were added beside the old
  ones, and not one existing assertion was changed to accommodate the new row. That is the difference
  between a row that fits the mechanism and a row the mechanism was bent around.
- Driven, not merely read: the pool read behind `DayLabourSiteManage` returns **no money member at
  all** (`frequency` and `averageRating` only), and Finance — who holds the rate row but not the site
  row — is refused `403` on the pool while reading the rate on the history route.

