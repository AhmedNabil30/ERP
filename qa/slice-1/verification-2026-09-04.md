# Verification — 2026-09-04

**Verifier, fresh session, machine to itself.** `CLAUDE.md`: *"If you wrote the code, you do not
certify it."* I wrote none of the four commits in scope.

**This closes sprint 2**, and one of these commits goes in front of a client.

`V-31-E`'s remedy is applied here for the first time: **`git status` and the `HEAD` sha are a named
step of both the opening and the closing gate** (§1, §11). The previous pass caught a foreign tree by
luck with six minutes of margin; this one checks on purpose.

---

## 0. Progress of this report

Everything is `pending` until reached. Nothing is marked done on an author's evidence.

| # | Item | State |
|---|---|---|
| 1 | Opening gate — `git status`, `HEAD`, stranded hosts, baseline re-measured | **done** — §1 |
| 1a | Corrections to the brief | **done** — §1a |
| 2 | The projection — what `/api/auth/me` leaks, and whether team size is live | pending |
| 3 | `ProjectTeamRead`'s grants — mutated, watched red | pending |
| 4 | SM-33's rename — paid correctly and completely? | pending |
| 5 | KAFF-125's code-reviewed half, and `AC-125-B` attacked | pending |
| 6 | `AC-125-C` — judging a deliberate deviation | pending |
| 7 | The E2E repair — all six watched failing | pending |
| 8 | The demo seed — no raw SQL, and the `POST /api/projects` 404 | pending |
| 9 | The deleted status page — references and i18n keys | pending |
| 10 | Verdicts per commit | pending |
| 11 | Closing gate — `git status`, `HEAD`, suites re-run | pending |
| 12 | What I did not do, as a count | pending |
| 13 | Fit to put in front of a client? | pending |
| 14 | The one thing Nabil should know | pending |
| 15 | For the cleanup that follows — a list, not a repair | pending |

### Findings index

| ID | Severity | Subject |
|---|---|---|
| *(populated as reached)* | | |

---

## 1. Opening gate — `V-31-E`'s remedy, applied as a named step

**Recorded before anything was built, measured, or mutated.**

| Gate | Value |
|---|---|
| `git rev-parse HEAD` | **`440e4bd9f91de2d2be8ca51bfa5c438c4f213eb2`** |
| `git status --porcelain` | **empty — tree clean** |
| `docker ps` | `kaff-db  Up 12 days (healthy)` |
| **Stranded hosts** | **THREE FOUND — see below** |

### The opening gate earned its place on its first run

`Get-CimInstance Win32_Process` matched on **command line** (the corrected form) and found a live API
host left over from the previous session:

```
8708   powershell.exe   … ConnectionStrings__KaffDatabase=…Database=kaff_verify… dotnet run --project src\Api\Kaff.Api.csproj
24720  dotnet.exe       … run --project src\Api\Kaff.Api.csproj --configuration Release --no-build
3728   Kaff.Api.exe     D:\ERP\src\Api\bin\Release\net10.0\Kaff.Api.exe
```

All three killed by PID before the first build. **Note both launch forms were present at once** —
`dotnet run` *and* the apphost `Kaff.Api.exe` — which is exactly the pair the skill's 2026-08-30
amendment says a name-based check misses half of.

Had I built without this step, `Kaff.Api.dll` was held open and the build would have hit the
`MSB3026` path the brief warns about. **The gate is not ceremony; it fired on its first use.**

### Baseline, re-measured

| Gate | Brief claimed | Measured |
|---|---|---|
| Build, `-c Release --no-incremental` | 0 / 0 | **0 warnings, 0 errors** |
| **`MSB3026`** | — | **absent** |
| `Kaff.Api.Tests.dll` written | — | **yes** — named in the build output, so the copy is evidenced, not assumed |
| `dotnet format --verify-no-changes` | exit 0 | **exit 0** |
| Domain suite | 111/111 | **111/111** |
| Api suite | 241/241 | **241/241, 0 skipped** |
| E2E suite | 6/6 | pending — §7 |
| Citations | 1104 / 0 / 0 | **1110 checked / 0 broken / 0 legacy** — see §1a |

---

## 1a. Corrections to the brief

*(populated as reached)*

**The citation count.** The brief's `1104` is D-105's *opening* baseline, taken before D-105 wrote its
own `decisions.md` entry. At `HEAD` the sweep reports **1110 / 0 broken / 0 legacy**. The
load-bearing halves — `0 broken`, `0 legacy` — are exact; only the total moved, and it moved for the
expected reason.
