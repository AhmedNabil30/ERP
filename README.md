# Kaff ERP

ERP for Kaff, an Egyptian construction and finishing contractor. A production system with real money
in it.

**Read in this order before changing anything:**

1. [`CLAUDE.md`](CLAUDE.md) — the rules and conventions. Prohibitions, not preferences.
2. [`spec.md`](spec.md) — the business truth. If code and `spec.md` disagree, `spec.md` wins.
3. [`decisions.md`](decisions.md) — why things are the way they are, and the open questions.
4. [`architecture.md`](architecture.md) — what is where.
5. [`agents.md`](agents.md) — who does what, and in which order.
6. [`process/agile.md`](process/agile.md) — how the work moves: sprints, refinement, Ready and Done.

Then, for the sprint you are on: [`stories/`](stories/) (what to build and why),
[`ux/`](ux/) (what it looks like), [`qa/`](qa/) (how it will be checked), and
[`meetings/`](meetings/) (what was asked and answered).

---

## Stack

.NET 10 · ASP.NET Core minimal APIs · EF Core + PostgreSQL 16 · Angular 22 · xUnit · Playwright.

Pinned. Do not substitute. A new dependency needs an entry in `decisions.md` explaining why the
framework cannot already do it.

---

## Prerequisites

| | |
|---|---|
| .NET SDK | 10.0.100 or later (`global.json` pins the band) |
| Node | 22.22.3+, 24.15+ or 26+ — the Angular 22 CLI refuses anything lower |
| PostgreSQL | 16 (15 is the floor — the guards use `NULLS NOT DISTINCT`) |
| Docker | optional, for the local database |

---

## First run

```bash
docker compose up -d db

dotnet run --project src/Api/Kaff.Api.csproj
```

The API listens on `http://localhost:5080`. In Development it applies migrations, installs the
database guards and seeds the company-level accounts on start-up.

```bash
cd src/Web
npm ci
npm start          # http://localhost:4200, proxying /api to the API
```

Open `http://localhost:4200`. You should see an Arabic, right-to-left page reporting that the API,
the database and the guards are all healthy. If the guards report as missing, the database is not
enforcing the append-only and non-negative-balance rules — stop and find out why.

---

## Tests

**Run the test executables directly. Not `dotnet test`.** See the note below — this is the part
that surprises people.

```bash
dotnet build KaffErp.sln --configuration Release

# Pure domain rules. No database.
./tests/Domain.Tests/bin/Release/net10.0/Kaff.Domain.Tests

# Schema invariants, the database guards, the permission gate. Needs a real PostgreSQL:
# the rules being checked live there, and a fake provider would report safety that does not exist.
KAFF_TEST_DB="Host=localhost;Port=5432;Database=kaff;Username=kaff;Password=kaff" \
  ./tests/Api.Tests/bin/Release/net10.0/Kaff.Api.Tests

# Playwright smoke suite. Skipped unless the application is actually running.
KAFF_E2E_BASE_URL=http://localhost:4200 \
  ./tests/E2E.Tests/bin/Release/net10.0/Kaff.E2E.Tests
```

On Windows add `.exe`. For a TRX report append `--report-trx --report-trx-filename name.trx`.

The Api.Tests fixture creates a uniquely-named database per run and drops it afterwards. It never
touches an existing one.

Playwright needs its browsers once:

```bash
dotnet build tests/E2E.Tests/Kaff.E2E.Tests.csproj
pwsh tests/E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

**Test runner.** These projects run on Microsoft.Testing.Platform. Each test project is an
executable that hosts itself — there is no `Microsoft.NET.Test.Sdk` and no VSTest.

Two settings are needed, and they are not the same setting:

| | |
|---|---|
| `global.json` → `"test": { "runner": … }` | what `dotnet test` *speaks* to the executable |
| `tests/Directory.Build.props` → `UseMicrosoftTestingPlatformRunner` | what the executable *is* |

**`dotnet test` does not work on this stack.** SDK 10.0.400 launches the host with
`--server dotnettestcli`, the handshake yields nothing, and it reports `Zero tests ran` with exit
code 5 after about 200ms — a *green-looking* command that ran nothing, except that the non-zero exit
gives it away. Run the executables directly, as CI does. See `decisions.md` D-046 and D-037.

The Api suite needs a PostgreSQL 15+ server whose user may `CREATE DATABASE`; it creates a
uniquely-named database per run and drops it afterwards, so it never touches an existing one. If
`KAFF_TEST_DB` is wrong or unset the suite fails with an actionable message rather than silently
falling back to an in-memory provider — the rules it checks live in the database.

---

## Configuration

| Setting | Environment variable | Notes |
|---|---|---|
| `ConnectionStrings:KaffDatabase` | `ConnectionStrings__KaffDatabase` | Required. |
| `Jwt:Issuer` | `Jwt__Issuer` | Required. |
| `Jwt:Audience` | `Jwt__Audience` | Required. |
| `Jwt:SigningKey` | `Jwt__SigningKey` | Required, at least 32 characters. **Never commit a real one.** |
| `Kaff:ApplyMigrationsOnStartup` | `Kaff__ApplyMigrationsOnStartup` | Defaults to true in Development only. |
| `Kaff:AllowedOrigins` | `Kaff__AllowedOrigins__0` | CORS origins for the SPA. |

`appsettings.Development.json` carries a placeholder signing key for local work only. Every other
environment supplies its own through the environment or a secret store.

Outside Development the application **refuses to start** when the database guards are missing. That
is deliberate — see `decisions.md` D-033.

---

## Before you commit

```bash
dotnet format KaffErp.sln --verify-no-changes
dotnet build KaffErp.sln --configuration Release
```

Warnings are errors. `.editorconfig` is enforced, not advisory. CI runs both on every push.

---

## The rules that get broken first

From `CLAUDE.md`, repeated because they are the ones that cost money:

- **Never store a balance.** They are derived from the `account_balances` view. Always.
- **Never update or delete a posting.** Corrections are reversing postings. The database will refuse
  you, including from psql.
- **Money is `decimal(18,4)`**, configured by convention so it cannot be forgotten. Never `float`,
  never `double`.
- **The five ledgers never net.** Client advance, hold, firm advance, عهدة, owner current account.
- **The hold only grows** until handover.
- **Every endpoint checks role and assignment**, server-side.
- **Nobody creates and approves the same movement.**
- **No hardcoded user-facing strings**, in either language.

---

## Open questions

`decisions.md` ends with a table of questions for Nabil, ordered by what they block. Several block
slice 1. An agent that answers one of them itself has made the most expensive kind of mistake
available on this project — raise it instead.
