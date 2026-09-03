# Client demo — setup and script

Written 2026-09-03, QA/Backend session, after repairing `tests/E2E.Tests/SmokeTests.cs`. What Nabil
asked for: a clean database, known credentials, and a script that works every time — not three
leftover `kaff_verify` rows (`karim`, `sara_finance`, `hend_hr`) whose passwords nobody recorded
(decisions.md D-104's own "not cleaned up" note).

**Read this whole file before standing in front of a client.** §1 is the finding that decides what you
can show.

---

## 1. Can a project be created through the API? No.

Checked directly, not assumed: `src/Api/Features/` has no `Projects` folder and no `Clients` folder —
only `Health`, `Setup`, `Auth`, `Users`, `Assignments`
[Verified 2026-09-03 — directory listing of `src/Api/Features/`]. `Kaff.Domain.Projects.Project` has a
full `Create` factory and a working state machine (`src/Domain/Projects/Project.cs`), and
`POST /api/projects/{projectId}/assignments` (KAFF-113) exists to staff a project — but nothing in this
codebase can mint the `Project` row that route's `{projectId}` names. `scripts/seed-demo.ps1` proves
this live, not just by reading the source: its last step `POST`s to `/api/projects` and gets **404**,
every run, because the route simply is not mapped.

There is also no `Clients` endpoint, and `Project.Create` requires a `ClientId` — so even a
hypothetical raw-SQL project insert would need a fabricated client row underneath it, compounding the
same problem CLAUDE.md and this brief both warn about: **data that did not arrive through a real
endpoint proves nothing and can violate an invariant silently.** This runbook does not do that. No
project exists in the seeded demo database, on purpose, and every landing screen below shows the
honest empty state that follows from that.

**What this means for the demo:** you cannot show a project, a team roster, a staffed or unstaffed
site, an extract, or any money. You can show sign-in, the forced-password-change flow, and each role's
real landing shell rendering its real (currently project-less) data. See §5 for exactly what each
screen shows.

**If a `POST /api/projects`-shaped endpoint ships later**, re-run `scripts/seed-demo.ps1` and update
this section — its own final step will start returning something other than 404, which is the signal
to come back and rewrite this file rather than let it go stale.

---

## 2. What the demo can show, and what it cannot

**Can show:**
- Sign-in, in Arabic, RTL, at phone width (390px).
- A forced password change on first sign-in (`mustChangePassword`), for every account except the
  Owner.
- Four real landing shells, each reflecting what `GET /api/auth/me` actually returns for that role
  today (D-103, D-104):
  - **Owner** — the honest "not built yet" surface where the user list (S-006) will go.
  - **Hr** — the project-team landing (D-051 Q32), currently showing "لا توجد مشاريع بعد" (no projects
    yet) because none exist.
  - **Finance** — the profile-and-my-projects landing (S-005), showing "لست مُسنداً إلى أي مشروع حتى
    الآن" (not assigned to any project yet) for the same reason.
  - **MarketingSales** — the honest "not built yet" surface where the client list (S-011) will go.
- The sign-out flow, and that `localStorage`/`sessionStorage` stay empty throughout (D-050's
  no-client-side-token rule, verified live below).

**Cannot show, and say so before a client asks:**
- Any project — creation, detail, status, or team roster with a nonzero team size. §1.
- Any client, opportunity, quotation, or BOQ.
- Any extract, change order, or the §15 worked money example.
- Any of the five ledgers, any posting, any balance.
- Slice 2 onward's masters (catalogue, أبواب, employees, workers, subcontractors, suppliers) — none of
  it exists yet.

---

## 3. One-time prerequisites

Same stack as everywhere else in this repo — see `.claude/skills/run-kaff-erp/SKILL.md` for the full
detail. In short: Docker Desktop running, `.NET SDK 10.0.400`, Node, and the solution built in Release
(`dotnet build KaffErp.sln --configuration Release`).

**Do not seed into `kaff`.** It carries `V-31-A`'s probe row and will not boot — CLAUDE.md forbids the
surgery to fix it and Nabil has not authorised it. **Do not seed into `kaff_verify`** either for a
demo you need to be repeatable: it already has an Owner and three accounts with unknown current
passwords, so `POST /api/setup` there returns "already completed" rather than a clean slate. This
runbook provisions its own database instead, so the seed can be re-run from nothing as many times as
needed:

```powershell
docker exec kaff-db psql -U kaff -d postgres -c "DROP DATABASE IF EXISTS kaff_demo"
docker exec kaff-db psql -U kaff -d postgres -c "CREATE DATABASE kaff_demo OWNER kaff"
```

---

## 4. The script

### 4.1 Start the API against `kaff_demo`

Stop any running `Kaff.Api` first (SKILL.md's gotcha — a stale one locks the DLLs the next build
needs):

```powershell
Get-CimInstance Win32_Process |
    Where-Object { $_.CommandLine -match 'Kaff\.Api' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
```

Then start it pointed at the fresh database — `Development` so it auto-migrates and applies the
guard scripts on boot:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5080'
$env:ConnectionStrings__KaffDatabase = 'Host=localhost;Port=5432;Database=kaff_demo;Username=kaff;Password=kaff'
dotnet run --project src\Api\Kaff.Api.csproj --configuration Release --no-build
```

Confirm it is up and the database really is empty before seeding:

```powershell
node .claude\skills\run-kaff-erp\driver.mjs health
Invoke-WebRequest http://localhost:5080/api/setup -UseBasicParsing
# expect {"available":true} — if it says false, the DROP/CREATE above did not run against this API
```

### 4.2 Start the SPA

```powershell
cd src\Web
npm start
```

### 4.3 Seed through the real endpoints

```powershell
powershell -NoProfile -File scripts\seed-demo.ps1
```

This runs, in order: `POST /api/setup` (the Owner), `POST /api/auth/sign-in` (as the Owner, to get a
session), three `POST /api/users` calls (Hr, Finance, MarketingSales), and the `POST /api/projects`
probe from §1. **No raw SQL. No direct `DbContext` writes.** Every account exists because a real
endpoint accepted a real request — the same doors a real user goes through.

**Not idempotent, by design.** `POST /api/setup` can succeed exactly once per database (KAFF-100), so
re-running this script against an already-seeded `kaff_demo` fails at the first step with
`SetupErrors.AlreadyCompleted`. To seed again, drop and recreate the database first (§3) and restart
the API (§4.1) — the API caches nothing that survives a restart, but it does hold an open connection
pool that goes stale across a `docker` container recreate, so restarting it after the drop/create is
what actually matters, not superstition.

**Two PowerShell 5.1 traps this script works around, in case you extend it:**
1. `Invoke-WebRequest`'s own charset guessing corrupts the Arabic full names on their way through —
   confirmed by checking `octet_length` vs `length` of the stored value directly in Postgres, which
   showed a doubled byte count (mojibake, not a display artefact). The script avoids `Invoke-WebRequest`
   entirely and sends `System.Net.Http.HttpClient` requests built from raw UTF-8 bytes read off the
   `payload-*.json` files in `scripts/seed-demo/`, and decodes every response the same explicit way.
2. The auth cookie is `Secure` (D-050, `StaffSessionMinter.CookieAttributes`), and .NET's
   `CookieContainer` — which backs `Invoke-WebRequest -WebSession` and `HttpClientHandler`'s default
   cookie handling — refuses to attach a `Secure` cookie to a plain `http://` request, even to
   `localhost`. A real browser exempts `localhost` from that rule; a scripted client does not. The
   script sets `UseCookies = $false` and replays the `Set-Cookie` value by hand as a literal `Cookie`
   header on every authenticated call. **This is a scripting workaround only — nothing about the
   server's cookie security is relaxed by it**, and it is why the demo itself, driven through a real
   browser (§5), never hits this at all.

### 4.4 Credentials

| Role | Username | Password | `mustChangePassword` |
|---|---|---|---|
| Owner | `owner_demo` | `Demo#Owner1` | No — the Owner sets their own password at setup (rule 7) |
| Hr | `hend_hr_demo` | `Demo#Hr123` | **Yes** — sign-in redirects straight to `/change-password` |
| Finance | `sara_finance_demo` | `Demo#Fin123` | **Yes** |
| MarketingSales | `karim_sales_demo` | `Demo#Sales123` | **Yes** |

**`mustChangePassword: true` is a demo step, not a bug.** Signing in with any of the three staff
accounts above lands on the forced-change screen first — walk through it live, or use
`scripts/screenshot-demo.mjs` beforehand to see what is on the other side. The current password to
enter there is the temporary one in the table; pick any new password at least 8 characters (no
complexity rule, D-049 ruling 3) if demoing by hand — the screenshot script's own choice is the
temporary password with `New` appended, e.g. `Demo#Hr123New`.

---

## 5. Screenshots — taken, and looked at

```powershell
node scripts\screenshot-demo.mjs <outDir>
```

Signs in as each of the four accounts through the real sign-in form (native value-setter + `input`
event — the technique decisions.md D-104 already verified against this exact signal-forms stack),
clears the forced password change where present, and screenshots the resulting landing at 390×844,
Arabic, RTL. Run 2026-09-03 against a freshly seeded `kaff_demo`; all four observed directly, not
inferred from source:

- **Owner** — dark theme, "كف" title top-left, hamburger toggle top-right (RTL inline-start — correct,
  matches D-104's own computed-style check), signed-in name "ناصر الشريف" and a sign-out button.
  Heading "قائمة المستخدمين" (user list) with "لم يُبنَ هذا الجزء من النظام بعد." (this part of the
  system has not been built yet) underneath — the honest placeholder, not an invented table.
- **Hr** — heading "المشاريع" (Projects) with "لا توجد مشاريع بعد." (no projects yet). This is D-100's
  team-size indicator working correctly on an empty set — there is genuinely nothing to staff, per §1.
- **Finance** — a "الملف الشخصي" (Profile) panel showing name/role/department correctly, then
  "مشاريعي" (My projects) with "لست مُسنداً إلى أي مشروع حتى الآن." (not assigned to any project yet).
- **MarketingSales** — same "not built yet" shape as Owner, headed "قائمة العملاء" (client list).

**All four**: no horizontal scroll at 390px, Arabic text right-aligned, locale switch and account menu
laid out correctly for `dir="rtl"`. `localStorage` and `sessionStorage` were read before sign-in and
after landing on every run and were **empty in both directions, every time** — D-050's rule holds
under an actual browser, not just by code reading.

Screenshots are not checked into this repository (binary artefacts, and they go stale the moment a
screen changes) — regenerate them with the command above before a demo, on the machine that will
present it, against the database that will be shown.

---

## 6. Live in front of a client

1. Sections 3–4 above, done ahead of time — not while someone is watching.
2. Open `http://localhost:4200`, sign in as `owner_demo`.
3. Sign out, sign in as `hend_hr_demo` — show the forced password change, then the Hr landing.
4. Repeat for `sara_finance_demo` and `karim_sales_demo` if useful; they demonstrate the same
   mechanism from two more angles (a "not built yet" role and a project-list role) rather than
   anything new.
5. Say §2's "cannot show" list out loud before anyone asks. It costs one sentence and avoids the
   client discovering the gap themselves mid-demo.

---

## 7. What this session did not do

- **Did not build a project-creation endpoint.** That is a scope decision for Nabil/the Architect, not
  something to invent under a demo brief — CLAUDE.md and `agents.md` both name inventing a missing
  capability as the expensive failure mode this project keeps naming and re-naming.
- **Did not seed `kaff_verify`.** Its existing state (D-104) is a separate concern from this
  runbook's; nothing here touches it.
- **Did not check screenshots into the repository.** §5 explains why.
- **Did not test this runbook against staging** (`deploy/README.md`). The same script works there —
  point `-Base` at `$STAGING_URL` and `KAFF_WEB` at the staging origin — but staging has no TLS and no
  backups today, so seeding demo accounts onto it is a separate decision from running this locally.
