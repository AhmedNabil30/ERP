# Verification — slice 2, second batch (2026-09-12)

**Verifier:** fresh session, `opus`. Wrote none of this code.
**Scope:** `7117704..35b0550` — `KAFF-200`, `201`, `207`, `208`, `209`, `210`, `211`, `212`.
**Rulings in force:** D-135, D-136, D-139, D-140, D-141, D-142, D-144, D-145, D-146, D-147, D-148, D-149.
**Measured at:** `610bf9e` (working tree clean before this report was written). `610bf9e` is the
Board's trailer-move commit on top of `35b0550`; the code under test is identical.

> This file is written section by section as the pass runs. If it ends without a
> **"Pass complete"** line at the bottom, the pass was cut short and everything below the last
> completed section is **unreached**, not passed.

---

## 1. Gates — measured in this session

Order per `run-kaff-erp` "the gate order": stranded hosts killed first, then build, then suites.

| Gate | Command | Result (measured here) |
|---|---|---|
| Build | `dotnet build KaffErp.sln -c Release --nologo` | **exit 0 · 0 Warning(s) · 0 Error(s)** |
| Format | `dotnet format KaffErp.sln --verify-no-changes --no-restore` | **exit 0**, no diff |
| Domain.Tests | `tests\Domain.Tests\bin\Release\net10.0\Kaff.Domain.Tests.exe` | **total 229 · failed 1 · succeeded 228 · skipped 0** — exit 2 |
| Api.Tests | `tests\Api.Tests\bin\Release\net10.0\Kaff.Api.Tests.exe` | **total 539 · failed 0 · succeeded 538 · skipped 1** — exit 0 |
| `npm run build` | `npm run build` in `src\Web` | **exit 0**, bundle generated, **1 warning** (CSS budget) |
| `npm test` | `npm test` in `src\Web` | **15 files · 96 tests · 96 passed** — exit 0 |

Every one of these numbers was measured in this session against the binaries produced by the build
above. None is copied from a story, from `STATUS.md`, or from a builder report.

**The Domain suite is red at `HEAD`.** See `V-38-A`. Every builder figure in `STATUS.md` for this
batch reports Domain green (`229/229` at step 5i); that figure does not reproduce.

The one Api skip is `CreateEmployeeTests.A_day_labourer_cannot_be_registered_again_as_salaried_with_the_same_phone`
— the `Q80` hold. That is a HELD criterion that is **visibly** held, which is the correct shape.

The `npm run build` warning is `catalogue-list-page.css` over its 4.00 kB budget by 305 bytes. It is
outside this batch's scope (`KAFF-203`'s screen) but it means `npm run build` is **not** warning-free,
and the project's warnings-as-errors posture does not extend to the Angular budget. Recorded as
`V-38-B` (LOW) so that no later reader reports "npm build clean" off this page.

---

*(pass in progress)*
