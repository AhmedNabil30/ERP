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

