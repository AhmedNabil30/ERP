---
name: run-kaff-erp
description: Build, run, screenshot, smoke-test and drive the Kaff ERP stack (ASP.NET Core API on 5080, Angular 22 SPA on 4200, PostgreSQL 16 in Docker). Use when asked to start, launch, serve, build, test, screenshot, or interact with the Kaff app, its API, or its web UI.
---

# Running Kaff ERP

Three processes: **PostgreSQL 16** in Docker, the **API** on `http://localhost:5080`, and the
**Angular 22 dev server** on `http://localhost:4200` which proxies `/api` to the API.

Agents drive it through **`.claude/skills/run-kaff-erp/driver.mjs`** — a zero-dependency Node script
that speaks CDP to the chromium Playwright already downloaded for the E2E project. It screenshots,
evaluates JavaScript, clicks, and smoke-checks the whole stack.

**All paths below are relative to the repo root (`d:\ERP`).** Commands are Windows PowerShell 5.1,
which is what this repository is developed on. Verified end to end on 2026-08-22.

---

## Prerequisites

Already present on this machine; listed so a fresh one can be brought up.

- **.NET SDK 10.0.400** (`global.json` pins 10.0.100 with `rollForward: latestFeature`)
- **Node 24.19.0** (CI uses 22; both build the app)
- **Docker Desktop**, running
- **Playwright chromium**, for the driver and the E2E suite:

```powershell
powershell -NoProfile -File tests\E2E.Tests\bin\Release\net10.0\playwright.ps1 install chromium
```

`pwsh` is **not** installed here — CI calls this script with `pwsh`, which fails locally with
`The term 'pwsh' is not recognized`. Use `powershell -NoProfile -File` as above. The script needs the
E2E project built first (see Build).

---

## Build

```powershell
docker compose up -d db
dotnet build KaffErp.sln --configuration Release
cd src\Web; npm ci; cd ..\..
```

`npm ci` is only needed on a fresh clone — `src\Web\node_modules` already exists here.

Optional gate, and what CI runs first:

```powershell
dotnet format KaffErp.sln --verify-no-changes --no-restore
```

---

## Run — agent path

### 1. Start the two servers

Each needs its own background process. **The API must be on port 5080**: `src\Web\proxy.conf.json`
hardcodes that target, and the SPA silently gets no data if the API is elsewhere.

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5080'
dotnet run --project src\Api\Kaff.Api.csproj --configuration Release --no-build
```

```powershell
cd src\Web; npm start
```

`Development` sets `ApplyMigrationsOnStartup: true`, so the API migrates the database on boot. It
also refuses to start at all if the PostgreSQL guards are missing — see Gotchas.

### 2. Drive it

```powershell
node .claude\skills\run-kaff-erp\driver.mjs smoke
```

Verified output:

```
PASS  API /api/health returns 200  — got 200
PASS  health reports healthy  — healthy
PASS  database reachable  — true
PASS  database guards installed  — []
PASS  SPA renders content  — 108 chars
PASS  document direction is RTL  — dir=rtl
PASS  page contains Arabic text  — كف …

title="كف" lang=ar dir=rtl

All checks passed.
```

Every driver command:

| Command | What it does |
|---|---|
| `driver.mjs health` | `GET /api/health`, prints status and body, exit 1 if not 200 |
| `driver.mjs api <METHOD> <path> [json]` | any API call |
| `driver.mjs shot <url> <out.png>` | full-page screenshot |
| `driver.mjs eval <url> "<js>"` | evaluate JavaScript in the page, print the JSON result |
| `driver.mjs flow <outDir>` | the language-switch flow: clicks, two screenshots, asserts direction flips |
| `driver.mjs smoke` | API + guards + SPA render + RTL + Arabic |

`KAFF_API` and `KAFF_WEB` override the two base URLs. `CHROME` overrides the browser binary.

Examples that were run:

```powershell
node .claude\skills\run-kaff-erp\driver.mjs health
node .claude\skills\run-kaff-erp\driver.mjs shot http://localhost:4200/ shots\status.png
node .claude\skills\run-kaff-erp\driver.mjs eval http://localhost:4200/ "Array.from(document.querySelectorAll('button')).map(b=>b.innerText.trim())"
node .claude\skills\run-kaff-erp\driver.mjs flow shots
```

`flow` writes `status-ar.png` and `status-en.png` and prints:

```
before  lang=ar dir=rtl  …\status-ar.png
after   lang=en dir=ltr  …\status-en.png

PASS  language switch flips direction
```

### 3. What the app actually is today

**One endpoint — `GET /api/health`.** Slice 1's endpoints (sign-in, users, clients) are not built
yet, so there is nothing to authenticate against and no login screen. `driver.mjs api …` is ready for
them; it has nothing else to call today.

**One page** — the status page at `/`, in Arabic RTL, with a language switch and a refresh button.

---

## Run — human path

Same two commands, then open `http://localhost:4200` in a browser. The dev server hot-reloads.
Ctrl-C each process to stop. Nothing here needs the driver — but nothing here is scriptable either.

---

## Test

```powershell
.\tests\Domain.Tests\bin\Release\net10.0\Kaff.Domain.Tests.exe
.\tests\Api.Tests\bin\Release\net10.0\Kaff.Api.Tests.exe
```

Verified: Domain **74/74**, Api **43/43**. The Api suite needs the Docker database; with
`KAFF_TEST_DB` unset it defaults to `kaff/kaff`, matching `docker-compose.yml`.

The E2E suite needs the full stack running:

```powershell
$env:KAFF_E2E_BASE_URL='http://localhost:4200'
.\tests\E2E.Tests\bin\Release\net10.0\Kaff.E2E.Tests.exe
```

Verified **5/5**. Without `KAFF_E2E_BASE_URL` the tests skip — except under `CI=true`, where an
unconfigured suite fails on purpose.

`dotnet test` also works and gives the same results:

```powershell
dotnet test tests\Domain.Tests\Kaff.Domain.Tests.csproj --no-build -c Release
```

**This contradicts `decisions.md` D-046 and the long comment in `.github/workflows/ci.yml`**, both of
which say `dotnet test` reports "Zero tests ran" with exit code 5. It does not, on SDK 10.0.400, as
of 2026-08-22 — `global.json` carries `"test": { "runner": "Microsoft.Testing.Platform" }`, which
routes around the VSTest bridge that was broken. The direct-executable invocation is still what CI
uses and still works; the *reason* recorded for it is stale. Flagged, not changed.

---

## Gotchas

- **Stop the API before building, or the build fails.** A running `Kaff.Api` holds
  `Kaff.Domain.dll` and `Kaff.Infrastructure.dll` open, and `dotnet build` fails with four
  `MSB3021` / `MSB3027` "file is locked by: Kaff.Api (pid)" errors. **Nothing is wrong with the
  code** — but the errors name the SDK's `Microsoft.Common.CurrentVersion.targets`, not your source,
  which reads like a toolchain problem on first sight. The stale binaries also still run, so the test
  suites report green against the previous build. Stop it first:

  ```powershell
  Get-Process -Name Kaff.Api -ErrorAction SilentlyContinue | Stop-Process -Force
  ```

- **⚠️ And a leftover `Kaff.Api.Tests` host is worse, because the build *succeeds*.** The gotcha above
  fails loudly. This one does not. A stranded **`Kaff.Api.Tests`** process locks `Kaff.Api.dll` in the
  test output directory; the build then emits **`MSB3026`** warnings, **copies nothing**, and reports
  **`Build succeeded`, 0 errors, exit code 0**.

  **Checking the build's exit code — which `decisions.md` D-046 exists to make you do — passes here,
  and the suite you run next executes the previous binary.** That is a green light with no evidence
  behind it, which is the failure D-046 was written about, arriving through the one door D-046 does
  not watch. Found 2026-08-24 by the Architect during A-04; recorded as D-069 §6.

  **Two rules, not one:** kill `Kaff.Api.Tests` as well as `Kaff.Api`, and **treat `MSB3026` on a

  > **⚠️ Amended 2026-08-25 by the Scrum Master, after hitting this live.** The paragraph above is
  > true **only for a bare `dotnet build`**. With the project standard **`-warnaserror`, MSB3026 is
  > promoted to an error and the build fails loudly** — verified 2026-08-25: **exit 1, 24 errors**,
  > naming `Kaff.Api (21724)` as the holder.
  >
  > **So the standard command protects you and a bare `dotnet build` does not.** What does *not*
  > protect you either way: **the test executables still run, and still report green off the stale
  > binary** — 75/75 and 106/106 on that same failed build. **Read the build result before the test
  > result, every time**; a green suite next to a failed build is the stale binary, not a passing one.
  >
  > **And `Stop-Process -Name` is not enough.** The holder survived two name-based kills and had to be
  > taken by PID, read out of the MSB3026 message itself. The error names the process and its pid —
  > use it.
  succeeded build as a failed build.**

  ```powershell
  Get-Process -Name Kaff.Api, Kaff.Api.Tests, Kaff.Domain.Tests -ErrorAction SilentlyContinue |
      Stop-Process -Force
  ```

- **`docker compose up -d db` recreates the container**, even when one is already running. The API's
  pooled Npgsql connections go stale and **the first request after the restart fails** — `smoke`
  reported 4 failures, then passed on a re-run with no other change. It self-heals; retry once. Use
  `docker start kaff-db` to avoid it entirely. Data survives either way (named volume `kaff-db-data`).
- **Port 5080 is not configurable in practice.** There is no `launchSettings.json`, so
  `ASPNETCORE_URLS` must be set explicitly, and `src\Web\proxy.conf.json` hardcodes
  `http://localhost:5080`. An API on any other port gives you an SPA that renders and shows nothing.
- **`Page.loadEventFired` is too early for a screenshot.** The first route is lazy and Angular is
  zoneless, so `load` fires before the chunk has rendered — a screenshot taken on it is blank. The
  driver polls for `document.body.innerText` to be non-empty, up to 20s.
- **Click by visible text, not by class or coordinates.** `CLAUDE.md` forbids hardcoded strings so
  labels come from i18n, and under RTL the visual and logical order of a row differ, which makes
  coordinate clicking wrong rather than merely fragile. The driver's `click` matches trimmed
  `innerText` first and falls back to a CSS selector.
- **`guardsInstalled` in the health response is load-bearing.** Per D-033 the API refuses to start
  when the PostgreSQL guards are absent, because the append-only and non-negative-balance rules live
  in the database. A stack that is "up" without them reports a safety it does not have — `smoke`
  asserts it for that reason, not for completeness.
- **`pwsh` is not installed.** Every CI line that calls it needs `powershell -NoProfile -File` here.
- **Editing files with PowerShell string replacement corrupts them.** PowerShell 5.1 reads BOM-less
  UTF-8 as Windows-1252, so `Get-Content -Raw` / `Set-Content` silently double-encodes every `§`,
  `—` and Arabic character — **and the result still compiles clean.** This repo is Arabic-facing. Use
  an editor tool. See `decisions.md` D-056 §5.
- **The driver launches a fresh chromium per command.** Each takes ~2-4s. That is deliberate — no
  state leaks between commands — but it means `smoke` is not fast, and a loop over `eval` is slow.

---

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `driver: fetch failed`, exit 1 | The API is not running, or not on `KAFF_API`. Start it, or set `KAFF_API`. |
| `smoke` fails the four API checks, SPA checks pass | Stale connection pool after a database restart. Re-run it once. |
| `No Playwright browser cache at …` | Run the `playwright.ps1 install chromium` line in Prerequisites, or set `CHROME` to any Chrome binary. |
| `The term 'pwsh' is not recognized` | Use `powershell -NoProfile -File` instead of `pwsh`. |
| `28P01: password authentication failed` from the Api suite | The database is not the compose one. `docker compose up -d db`, or set `KAFF_TEST_DB`. See D-054. |
| SPA renders but every status row is an error | The API is not on 5080. `proxy.conf.json` cannot reach it. |
| `chromium printed no DevTools endpoint in 30s` | Chromium could not start. The driver prints its stderr — usually a missing shared library on Linux. |
