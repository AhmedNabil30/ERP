# Client demo — setup and script

Written 2026-09-03, QA/Backend session, after repairing `tests/E2E.Tests/SmokeTests.cs`. What Nabil
asked for: a clean database, known credentials, and a script that works every time — not three
leftover `kaff_verify` rows (`karim`, `sara_finance`, `hend_hr`) whose passwords nobody recorded
(decisions.md D-104's own "not cleaned up" note).

**Read this whole file before standing in front of a client.** §1 is the finding that decides what you
can show.

> ### 📌 Re-verified 2026-09-09, QA session, ahead of Nabil running this file as `process/agile.md` §4's
> acceptance script for the first time ever
>
> **The single most out-of-date claim in the 2026-09-03 text was §2's "the client flow has no
> screen."** Since then `KAFF-126` (client screens), `KAFF-127` (user-management screens) and
> `KAFF-128` (audit trail screen) all shipped, and this session drove every one of them live —
> sign-in through to a rendered screen, on a freshly dropped-and-recreated `kaff_demo`, not read off
> source. **Three real, working screens exist that this file used to say did not: a user list with
> create/edit/deactivate/reactivate, a client list with create/edit/archive and a *live* duplicate-phone
> warning in the form itself, and an Owner-only audit trail.** §2 and §5 below are corrected
> accordingly, not rewritten wholesale — struck text stays visible, the way this file's own §1
> already does it.
>
> **Everything else was re-checked, not assumed.** §1's `POST /api/projects` finding — still 404
> [Verified: 2026-09-09 — `src/Api/Features/` has no `Projects` folder; live probe below]. §3/§4's
> runbook — followed end to end against a freshly dropped `kaff_demo`, reported in §4.3. An acceptance
> checklist is new, at the end (§9) — one line per story, so Nabil finishes it knowing exactly what he
> just accepted and what he did not.

---

## 1. Can a project be created through the API? No. **A client, yes — as of 2026-09-04.**

> ### ⚠️ Half of this section went stale in one day, and it is corrected rather than rewritten
>
> **`src/Api/Features/Clients/` now exists.** KAFF-119 shipped on 2026-09-04 (`86cc8b0` + `01c7b3a`)
> with `POST /api/clients` and `POST /api/clients/phone-check`, and **the seed registers two clients
> through them** — §4.3. Everything below about *projects* is still true and was re-checked the same
> day: the probe still answers **404**.
>
> Left in place because this file's own last paragraph named the signal to come back on, and the
> signal fired for the other half of the sentence. That is worth seeing.

Checked directly, not assumed: `src/Api/Features/` has ~~no `Projects` folder and no `Clients`
folder~~ **no `Projects` folder** — only `Health`, `Setup`, `Auth`, `Users`, `Assignments`
[Verified 2026-09-03 — directory listing of `src/Api/Features/`] **and, since 2026-09-04, `Clients`**.
`Kaff.Domain.Projects.Project` has a
full `Create` factory and a working state machine (`src/Domain/Projects/Project.cs`), and
`POST /api/projects/{projectId}/assignments` (KAFF-113) exists to staff a project — but nothing in this
codebase can mint the `Project` row that route's `{projectId}` names. `scripts/seed-demo.ps1` proves
this live, not just by reading the source: its last step `POST`s to `/api/projects` and gets **404**,
every run, because the route simply is not mapped.

~~There is also no `Clients` endpoint, and~~ `Project.Create` requires a `ClientId` — so even a
hypothetical raw-SQL project insert would need a fabricated client row underneath it, compounding the
same problem CLAUDE.md and this brief both warn about: **data that did not arrive through a real
endpoint proves nothing and can violate an invariant silently.** This runbook does not do that, and
**the two clients it now seeds arrive through `POST /api/clients` like everything else here.** No
project exists in the seeded demo database, on purpose, and every landing screen below shows the
honest empty state that follows from that.

**What this means for the demo:** you cannot show a project, a team roster, a staffed or unstaffed
site, an extract, or any money. You can show sign-in, the forced-password-change flow, each role's
real landing shell rendering its real (currently project-less) data, **and the client registration
flow end to end, including the duplicate-phone decision** — which is the only part of this demo where
a business rule visibly decides something rather than a form saving a row. See §5 for exactly what
each screen shows.

**If a `POST /api/projects`-shaped endpoint ships later**, re-run `scripts/seed-demo.ps1` and update
this section — its own final step will start returning something other than 404, which is the signal
to come back and rewrite this file rather than let it go stale.

**Re-checked 2026-09-09, QA session, against `ce7f37f`:** `src/Api/Features/` still carries no
`Projects` folder, and the seed's own final step still gets **404** — this time on a freshly
dropped-and-recreated `kaff_demo`, not a database that had already been proving it for days. The
finding stands, five days after the last re-check, and it is still the single fact that shapes this
whole runbook.

---

## 2. What the demo can show, and what it cannot

> ### 📌 Corrected 2026-09-09 — this whole section was written before three screens existed
>
> **`KAFF-126`, `KAFF-127` and `KAFF-128` all shipped after 2026-09-04** and this session drove every
> one of them live against a fresh `kaff_demo`. The old text below said the Owner and MarketingSales
> landings were an honest "not built yet" placeholder, that a client screen did not exist, and that
> editing/archiving/searching a client was unbuilt. **None of that is true today.** Struck rather than
> deleted, per this file's own house rule.

**Can show:**
- Sign-in, in Arabic, RTL, at phone width (390px).
- A forced password change on first sign-in (`mustChangePassword`), for every account except the
  Owner.
- Four real landing shells, each reflecting what `GET /api/auth/me` actually returns for that role
  today (D-103, D-104):
  - **Owner** — ~~the honest "not built yet" surface where the user list (S-006) will go~~ **lands on
    `/users` (S-006) and redirects there automatically — a real, working user list**, all five
    seeded accounts shown by name, role, department, username and phone, the Owner's own account
    included [Verified: 2026-09-09, live, driven end to end at 390px against a freshly seeded
    `kaff_demo` — `src/Web/src/app/core/navigation/landing.ts` -> `landingFor`, `RULED_LANDINGS`].
    `KAFF-127`.
  - **Hr** — the project-team landing (D-051 Q32), currently showing "لا توجد مشاريع بعد" (no projects
    yet) because none exist. **Unchanged** — re-verified live 2026-09-09.
  - **Finance** — the profile-and-my-projects landing (S-005), showing "لست مُسنداً إلى أي مشروع حتى
    الآن" (not assigned to any project yet) for the same reason. **Unchanged** — re-verified live
    2026-09-09.
  - **MarketingSales** — ~~the honest "not built yet" surface where the client list (S-011) will go~~
    **lands on `/clients` (S-011) and redirects there automatically — a real, working client list**,
    both seeded clients shown with code, kind and phone, search box and the three status chips
    (الكل / الحاليون / المؤرشفون) all live against the server [Verified: 2026-09-09, live, driven end
    to end at 390px — same landing table as above]. `KAFF-126`.
- The sign-out flow, and that `localStorage`/`sessionStorage` stay empty throughout (D-050's
  no-client-side-token rule, verified live below).
- **A user-management screen, not only the endpoints (`KAFF-127`).** From `/users`: create a user
  (role, department, sub-department, temporary password), open any user's own file at `/users/:id`,
  change their role (with a confirm step naming every project it would revoke — none exist yet, so
  the count is honestly zero), move them between departments, and deactivate/reactivate with a
  reason box that is stored in the audit trail
  [Verified: 2026-09-09 @ `src/Web/src/app/features/users/user-form/user-form-page.html` — the
  role-change, deactivate and reactivate confirms all render and all carry `data-testid`s the E2E
  suite drives]. **`AC-127-A`'s search box and filter chips have no server parameter behind them
  yet** — a known, open finding (`F-127-1`), not a regression this session introduced.
- **A client-management screen, not only the endpoints (`KAFF-126`).** From `/clients`: create a
  client, search by name/code/phone, filter by الحاليون/المؤرشفون/الكل, open a client's own file at
  `/clients/:id`, edit its contact details, and archive it behind a two-step confirm
  [Verified: 2026-09-09 @ `src/Web/src/app/features/clients/client-form/client-form-page.html` —
  `client-archive`, `client-archive-confirm`, `client-archive-cancel` test ids]. `KAFF-121`,
  `KAFF-123`, `KAFF-124`.
- **The duplicate-phone warning, live in the form itself — not only in the seed's printed output.**
  Typing a phone that already matches a client renders the warning banner
  (`client-duplicate-warning`) inline, names the matching client and its code, and requires an
  explicit acknowledgement checkbox before the save is allowed through
  [Verified: 2026-09-09 @ `client-form-page.html` lines 53-77, and `client-form-page.ts` line 216 —
  `problem.code === 'master.duplicate_phone_not_acknowledged'`]. **This is new since 2026-09-03**: the
  form used to not exist at all, so this whole interaction could only be narrated from the seed
  script. It can now be driven live, a second time, with a phone number typed by hand.
- **An Owner-only audit trail (`KAFF-128`), reached by typing `/audit`.** No nav item points at it —
  that is deliberate, not a gap (see §5) — but the screen is real: a date-range filter, a list of
  every audited event with actor, role-at-the-time and action, and a detail panel per row showing
  before/after values, the request path, the correlation id and — where the event carries one — the
  IP address. Confirmed live: signing in as Owner and calling `GET /api/audit` after the seed returns
  real rows (`SignedIn`, `Modified`, `DuplicatePhoneAcknowledged`, …)
  [Verified: 2026-09-09, live — `GET /api/audit` returned populated rows against `kaff_demo`]. A
  non-Owner calling the same endpoint is refused with **403** — driven live as `karim_sales_demo`
  [Verified: 2026-09-09, live — `403`, `errors.auth.forbidden`]. **`AC-128-B`'s narrower half — "even
  a Technical Office lead assigned to their own project is refused" — cannot be demonstrated today**:
  it needs a project to assign someone to, and §1 stands. This is `V-35-B`'s own finding, not new.
- **Client registration, and the duplicate-phone decision** (KAFF-119, added 2026-09-04). The seed
  walks it end to end and the four responses are the demo — **read them out, they are the story,**
  and the same four steps can now be re-driven by hand through the form above rather than only read
  off the seed's console output:
  1. A corporate client on `01001234567` → **201**, `C-10001`. **The code is generated; nobody typed
     it and nobody can edit it** (spec.md §2's amendment).
  2. `phone-check` on `+20 100 123 4567` → **200** naming `C-10001`. **Same number, different
     format, one match** — `AC-119-C`.
  3. The company's owner as an individual on the same number in **Arabic-Indic digits** `٠١٠٠١٢٣٤٥٦٧`,
     not acknowledged → **409**. *A third spelling of the same number, and the system still knows.*
  4. The same request acknowledged → **201**, `C-10002`. **The warning does not block the save**
     (`AC-119-D`) **and the decision is in the audit trail** (`AC-119-E`) — as a
     `DuplicatePhoneAcknowledged` event whose subject is the **matched** client, `C-10001`, not the
     one just created. Verified in the database on 2026-09-04, not inferred.

  **`C-10001` and `C-10002` run consecutively here only because nothing failed.** A failed save burns
  a number and leaves a gap — `Q57`, open with Karim. If a client asks about the numbering, that is
  the honest answer, not "they are always consecutive."

**Cannot show, and say so before a client asks:**
- Any project — creation, detail, status, or team roster with a nonzero team size. §1.
- ~~A client *screen*. The endpoints exist; the Angular form does not — `AC-119-L` is held and is
  Frontend's. The client flow demos through the seed script's output, not through the UI.~~
  **Stale as of `KAFF-126`, shipped and re-verified live above.** The form exists; §2's "Can show"
  list above is where this went.
- Any opportunity, quotation, or BOQ.
- ~~Editing, archiving or searching a client — KAFF-121, 123 and 124 are `Ready` and unbuilt.~~
  **Stale — `STATUS.md` carries all three as `VERIFIED PASS`, and this session drove the search box,
  the archive confirm and an edit save live.** Moved to "Can show" above.
- **A search that actually filters, or a sort order that is ruled, on the *user* list** — `KAFF-127`'s
  screen renders every user with no working search or filter control server-side (`F-127-1`), and
  the list's own order is unruled pending `Q59` (does alif with and without hamza collate together).
  Neither is a defect to apologise for; both are open findings, not silent gaps — name them if asked.
- **A project-scoped audit trail, or the refusal of an assigned Technical Office lead reading their
  own project's trail** — `AC-128-B`'s distinguishing clause needs a project to test against, and §1
  stands. What *is* demonstrable is the company-wide refusal: every non-Owner role gets **403**.
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
Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess -Unique |
    ForEach-Object { Stop-Process -Id $_ -Force }
```

> **⚠️ Narrowed 2026-09-08.** This block used to match `Win32_Process` on the **command line**
> containing `Kaff.Api`, which is any process that merely *mentions* the string — an editor with
> `Kaff.Api.csproj` open matches it, and that killed an unrelated process twice on this board
> (`decisions.md` D-122 §8). The form above takes the **owning PID of the listener on 5080** and
> nothing else, which is exactly the process holding the DLLs open, whichever way it was launched.
> If nothing is listening it stops nothing and says nothing — that is correct, not a silent failure.

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
session), ~~three `POST /api/users` calls (Hr, Finance, MarketingSales), four client calls~~
**three staff `POST /api/users` calls (Hr, Finance, MarketingSales), one `POST /api/clients` call,
then a fourth `POST /api/users` call the 2026-09-03 text never listed — the portal client account,
`Role.Client`, scoped to the client just created (`V-33-E`, added 2026-09-05, see §4.4) — and only
then** the remaining three client calls (`POST /api/clients/phone-check`, then the same
`POST /api/clients` twice, unacknowledged and acknowledged — §2). Last is the `POST /api/projects`
probe from §1. **No raw SQL. No direct `DbContext` writes.** Every account and every client exists
because a real endpoint accepted a real request — the same doors a real user goes through.

**The script asserts its own story rather than only printing it.** It throws if the corporate client
is not `201`, if the acknowledged duplicate is not `201`, and warns loudly if the unacknowledged one
is not `409` or if `phone-check` finds nothing — because a seed that quietly stopped demonstrating the
rule would still look like a successful run. Verified end to end against a fresh database on
2026-09-04, **and re-verified end to end on 2026-09-09 against a freshly dropped-and-recreated
`kaff_demo` — every status code matched exactly, and the two clients' Arabic names and phone digits
came back uncorrupted in the database** (`octet_length` sanity not needed this time — read directly:
`شركة النيل للتطوير العقاري` / `01001234567` and `أحمد محمود عبد الرحمن` / `٠١٠٠١٢٣٤٥٦٧`, both
normalising to the same `01001234567`). Full transcript is this session's own log, not reproduced
here.

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
| **Client (portal)** | `portal_client_demo` | `Demo#Portal1` | **Yes** — and it never gets that far; see below |

**⚠️ The portal account exists to be REFUSED, and its password above is the correct one.** Added
2026-09-05 for `V-33-E`: until then the seed created no `Role.Client` user at all, so spec.md §12's
client-portal boundary had **no UI-level evidence anywhere in the repository**. A `Role.Client`
cannot hold a staff session (`StaffSessionRules.MayHoldStaffSession`), so signing in with these
credentials on the staff host is turned away with **exactly the message a wrong password produces** —
D-065's ruling, because a message that said "this account cannot sign in here" would confirm to an
attacker that the username exists. `UserScreenTests` drives it and asserts the two texts are equal.
It is scoped to `C-10001`; there is no portal host to sign it in to yet.

**`mustChangePassword: true` is a demo step, not a bug.** Signing in with any of the three staff
accounts above lands on the forced-change screen first — walk through it live, or use
`scripts/screenshot-demo.mjs` beforehand to see what is on the other side. The current password to
enter there is the temporary one in the table; pick any new password at least 8 characters (no
complexity rule, D-049 ruling 3) if demoing by hand — the screenshot script's own choice is the
temporary password with `New` appended, e.g. `Demo#Hr123New`.

### 4.5 The clients the seed leaves behind

| Code | Name | Kind | Phone, as typed |
|---|---|---|---|
| `C-10001` | شركة النيل للتطوير العقاري | Corporate | `01001234567` |
| `C-10002` | أحمد محمود عبد الرحمن | Individual | `٠١٠٠١٢٣٤٥٦٧` |

**They share a phone number on purpose** — one company and its owner on one line, which D-049 ruling 8
says is normal and is why the warning asks rather than refuses. `C-10002` exists only because the
acknowledgement was sent, and that acknowledgement is a row in `audit_records`:

```powershell
docker exec kaff-db psql -U kaff -d kaff_demo -c "SELECT event_type, entity_type, entity_id, actor_display_name FROM audit_records WHERE event_type = 'DuplicatePhoneAcknowledged'"
```

**Check the `entity_id` against `C-10001`'s id, not `C-10002`'s.** The trail records *which client was
already there*, because that is the fact somebody needs later — D-107 §3.

---

## 5. Screenshots — taken, and looked at

> ### 📌 Rebuilt 2026-09-09 — the Owner and MarketingSales descriptions below were the placeholder text,
> and the placeholder is gone
>
> `KAFF-126` and `KAFF-127` shipped after the 2026-09-03 run this section used to describe. Re-run
> 2026-09-09 against a freshly dropped-and-recreated `kaff_demo`, same command, same technique, same
> four accounts — and two of the four landings now render a real screen instead of "not built yet."
> Old text struck, not deleted.

```powershell
node scripts\screenshot-demo.mjs <outDir>
```

Signs in as each of the four accounts through the real sign-in form (native value-setter + `input`
event — the technique decisions.md D-104 already verified against this exact signal-forms stack),
clears the forced password change where present, and screenshots the resulting landing at 390×844,
Arabic, RTL. ~~Run 2026-09-03 against a freshly seeded `kaff_demo`; all four observed directly, not
inferred from source~~ **re-run 2026-09-09 against a freshly dropped-and-recreated `kaff_demo`; all
four observed directly again, both from the script's own printed `body text ->` dump and from reading
the four PNGs it wrote:**

- **Owner** — ~~dark theme, "كف" title top-left, hamburger toggle top-right (RTL inline-start —
  correct, matches D-104's own computed-style check), signed-in name "ناصر الشريف" and a sign-out
  button. Heading "قائمة المستخدمين" (user list) with "لم يُبنَ هذا الجزء من النظام بعد." (this part
  of the system has not been built yet) underneath — the honest placeholder, not an invented table.~~
  **Redirects to `/users` and renders the real user list (`KAFF-127`).** Same chrome (dark theme,
  "كف" top-left, hamburger top-right, signed-in name and sign-out button — unchanged), but the
  heading "المستخدمون" is now followed by all five seeded accounts as real rows: **سارة محمود** ·
  المالية, **كريم فؤاد** · التسويق والمبيعات, **محمود صاحب الشركة** · عميل (the portal account),
  **ناصر الشريف** · المالك (the Owner's own account — S-006 puts the Owner in their own list, by
  design), **هند علي** · الموارد البشرية — each with its username and phone in a `<bdi>` isolate, and
  a "+ مستخدم جديد" (new user) action at the foot. [Verified: 2026-09-09, live at 390×844, both from
  `screenshot-demo.mjs`'s body-text dump and the PNG.]
- **Hr** — heading "المشاريع" (Projects) with "لا توجد مشاريع بعد." (no projects yet). **Unchanged** —
  this is D-100's team-size indicator working correctly on an empty set; there is genuinely nothing
  to staff, per §1. Re-verified live 2026-09-09, identical output.
- **Finance** — a "الملف الشخصي" (Profile) panel showing name/role/department correctly, then
  "مشاريعي" (My projects) with "لست مُسنداً إلى أي مشروع حتى الآن." (not assigned to any project yet).
  **Unchanged.** Re-verified live 2026-09-09, identical output.
- **MarketingSales** — ~~same "not built yet" shape as Owner, headed "قائمة العملاء" (client list)~~
  **Redirects to `/clients` and renders the real client list (`KAFF-126`).** Heading "العملاء", a
  search box ("ابحث بالاسم أو الكود أو رقم الهاتف"), three filter chips with الحاليون (active)
  selected by default, then both seeded clients as real rows: **شركة النيل للتطوير العقاري** ·
  `C-10001` · شركة · `01001234567`, and **أحمد محمود عبد الرحمن** · `C-10002` · فرد ·
  `٠١٠٠١٢٣٤٥٦٧` (rendered in its original Arabic-Indic digits, not silently converted — the code and
  the phone both isolated in `<bdi>` and neither one reorders against the Arabic name beside it), and
  a "+ عميل جديد" (new client) action at the foot. [Verified: 2026-09-09, live at 390×844.]

**A fifth screen exists that this script does not screenshot, because it has no nav item and no
account whose *landing* it is:** the audit trail at `/audit`, Owner-only, reached by typing the URL.
Confirmed live 2026-09-09 by calling `GET /api/audit` as the signed-in Owner (real rows came back —
`SignedIn`, `Modified`, `DuplicatePhoneAcknowledged`) and as `karim_sales_demo` (refused, **403**).
Add it to a manual walkthrough (§6) rather than to this script — screenshotting it would mean driving
a fifth account through a URL the landing table never routes anyone to, which is a different kind of
check from what this script does for the other four.

**All four**: no horizontal scroll at 390px, Arabic text right-aligned, locale switch and account menu
laid out correctly for `dir="rtl"`. `localStorage` and `sessionStorage` were read before sign-in and
after landing on every run and were **empty in both directions, every time** — D-050's rule holds
under an actual browser, not just by code reading.

Screenshots are not checked into this repository (binary artefacts, and they go stale the moment a
screen changes) — regenerate them with the command above before a demo, on the machine that will
present it, against the database that will be shown.

---

## 6. Live in front of a client

> **📌 Corrected 2026-09-09 — steps 4 and 5 used to undersell what is here.** `karim_sales_demo` is
> not "useful if there's time," it is the client list; and the client flow does not need the seed
> script's console to tell its own story any more. Renumbered and rewritten below.

1. Sections 3–4 above, done ahead of time — not while someone is watching.
2. Open `http://localhost:4200`, sign in as `owner_demo` — lands on the **real user list** (`/users`,
   `KAFF-127`), all five seeded accounts, the Owner's own included. Open one, walk through a
   department move or a role change (the confirm names every project it would revoke — honestly zero,
   per §1) without saving, so nothing here needs undoing afterwards.
3. Sign out, sign in as `hend_hr_demo` — show the forced password change, then the Hr landing
   ("لا توجد مشاريع بعد.").
4. Sign in as `sara_finance_demo` — the forced password change again, then the profile landing and
   its own honest empty state ("لست مُسنداً إلى أي مشروع حتى الآن.").
5. Sign in as `karim_sales_demo` — the forced password change, then lands on the **real client list**
   (`/clients`, `KAFF-126`), both seeded clients. Open "+ عميل جديد" and type a phone that matches one
   of them (`01001234567` or its Arabic-Indic twin) — the duplicate warning renders inline, names the
   match, and the acknowledgement checkbox is what unblocks the save. **This is the moment worth
   pausing on**, live, not read off a script: §2's four-step reading of the seed's own console output
   is the same rule, already proven once before the demo started; this is the second, human proof of
   it.
6. Sign back in as `owner_demo` (or use the tab already signed in as them) and type `/audit` in the
   address bar — there is no nav item, by design (§5), so typing it is the only way there. Point out
   the `SignedIn` rows from steps 2–5 above and the `DuplicatePhoneAcknowledged` row from step 5, then
   open one to show the before/after panel. If useful, sign in as any other role first and show the
   same URL refused with **403** — the strictest permission in the system, per KAFF-128's own story
   text.
7. Say §2's "cannot show" list out loud before anyone asks. It costs one sentence and avoids the
   client discovering the gap themselves mid-demo. **No project exists, and nothing above creates
   one — §1 stands through every step above.**

---

## 7. Seeding staging

**Everything above is local.** Staging is the Oracle Cloud VPS described in `deploy/README.md` — three
containers, deployed by `.github/workflows/deploy-staging.yml` on every push to `main`.

```powershell
.\scripts\seed-demo.ps1 -Base https://<the name Caddy serves>
```

**Pass the site root and nothing else.** nginx proxies `/api/` to the API container, which is
`expose`d and **never published to the host**, so there is no separate API port to aim at — the site
URL *is* the API base. It is the same value as the `STAGING_URL` repository variable.

> **⚠️ `https://` and the name, not `http://` and an IP — changed 2026-09-04 (D-115).** Caddy holds
> 80 and 443 and terminates TLS; nginx moved to 8080 bound to `127.0.0.1`, so there is nothing on
> port 80 to seed against and nothing reachable on 8080 from off the box. A bare IP has no
> certificate.

### Check this first, or step 1 throws

```powershell
Invoke-WebRequest https://<the name Caddy serves>/api/setup -UseBasicParsing   # want {"available":true}
```

**`POST /api/setup` succeeds exactly once per database (KAFF-100).** If staging already has an Owner
this answers `{"available":false}` and the script stops at step 1 with `SetupErrors.AlreadyCompleted`.

**Reseeding staging means dropping its database, and that is not a step to take casually:**
`deploy/README.md` records that **`kaff-staging-db` is the only copy and there are no backups.** The
compose project is `kaff-staging` and the service is `db`, so it is `docker compose -f
docker-compose.staging.yml exec db psql …` from `STAGING_DEPLOY_TARGET` on the host — **and it is
Nabil's call, not a runbook step.** Nothing in this repository does it for you, deliberately.

### Two things that are different on staging, and one that is not

- **The `Secure`-cookie workaround still applies and still works.** §4.3's note explains why the
  script replays `Set-Cookie` by hand — .NET's `CookieContainer` will not attach a `Secure` cookie
  over plain `http://`. Over `https://` it would, but the script does not depend on which: it turns
  automatic cookie handling off and replays the header either way.
- **A real browser only started working on staging with TLS.** The same `Secure` attribute a scripted
  client works around is one a browser enforces: on `http://<ip>` it discards the cookie, so sign-in
  appeared to succeed and the next request was a `401`. **That is fixed by Caddy, not by anything in
  this runbook.**
- **The demo passwords are weak and known** (`Demo#Owner1`, `Demo#Hr123`, …) and staging is on the
  public internet. TLS means nobody reads them off the wire; it does **not** mean nobody can use
  them. Acceptable for a walkthrough; **not acceptable to leave sitting there afterwards.** Seed it
  before the demo, and plan what happens to it after.
- **The project probe still 404s there too.** §1 is a property of the codebase, not of the machine.

---

## 8. What this session did not do

- **Did not build a project-creation endpoint.** That is a scope decision for Nabil/the Architect, not
  something to invent under a demo brief — CLAUDE.md and `agents.md` both name inventing a missing
  capability as the expensive failure mode this project keeps naming and re-naming.
- **Did not seed `kaff_verify`.** Its existing state (D-104) is a separate concern from this
  runbook's; nothing here touches it.
- **Did not check screenshots into the repository.** §5 explains why.
- **Did not test this runbook against staging** (`deploy/README.md`). The same script works there —
  point `-Base` at `$STAGING_URL` and `KAFF_WEB` at the staging origin — but staging still has no
  backups, so seeding demo accounts onto it is a separate decision from running this locally.
  ⚠️ **And nothing in §7 has been run against staging since it moved behind Caddy on 2026-09-04**;
  the URLs there are corrected on paper, not confirmed on the box.

### Amended 2026-09-09 — QA session, ahead of Nabil's first acceptance run

- **Did not touch the `kaff` database, at any point.** Every command in this pass ran against
  `kaff_demo`, dropped and recreated first per §3. `V-31-A`'s probe row is exactly as `STATUS.md`
  sprint-6 item 9 left it.
- **Did not re-run `scripts/seed-demo.ps1` against a database that already had data.** `kaff_demo`
  existed from an earlier session; it was dropped and recreated before this session's own run, per
  this brief's own instruction and §3.
- **Did not screenshot `/audit`.** `scripts/screenshot-demo.mjs` drives four accounts to their
  *landing*; the audit trail is nobody's landing (§5), so confirming it meant a live CDP session of
  its own rather than an addition to that script. Confirmed instead by signed-in `GET`/`403` calls,
  reported in §2 and §5 with what each returned.
- **Did not resolve `Q59`** (list sort order — does alif with and without hamza collate together).
  Still open with Karim, still blocks nothing built, named in §2 as an open finding on the user list.
- **Did not touch any story file, `STATUS.md`, or a trailer.** State is Nabil's to move, not this
  session's — §9 below is a script for him to run, not a verdict this session is recording on his
  behalf.
- **Did not test §7 against staging.** Same gap as the paragraph above; unchanged by this pass.

---

## 9. Acceptance checklist — for Nabil, one line per story

`process/agile.md` §4: this is the demo script, and running it is what moves a story from
`VERIFIED` to `ACCEPTED`. **Nothing on this list is ticked by this session — that is yours to do,
live, while running §6.** Tick a box only once you have actually seen the thing named, not because
the box is here.

### Demonstrable today — walk through §6, then tick what you saw

| ☐ | Story | Pts | What you just watched |
|---|---|---:|---|
| ☐ | **KAFF-100** | 5 | The Owner bootstrap — watch `POST /api/setup` return `201` in §4.3's own console output. No screen renders this; it is the one story on this list accepted from a transcript, not a click |
| ☐ | **KAFF-101a** / **KAFF-101b** | 5 + 3 | Sign-in, Arabic, RTL, 390px, correct landing per role |
| ☐ | **KAFF-102** | 2 | Sign-out, between any two accounts in §6 |
| ☐ | **KAFF-103** | 5 | The forced password change, on `hend_hr_demo` / `sara_finance_demo` / `karim_sales_demo` |
| ☐ | **KAFF-105a** | 2 | `GET /api/auth/me` is what every landing's identity comes from — implicit in every screen above |
| ☐ | **KAFF-106** | 5 | Create a user from `/users` → "+ مستخدم جديد" |
| ☐ | **KAFF-108** | 3 | Move a user between departments, from their own `/users/:id` file |
| ☐ | **KAFF-109** | 5 | Change a user's role — the confirm names every project it would revoke (honestly zero, §1) |
| ☐ | **KAFF-110** | 5 | Deactivate a user, with a stored reason |
| ☐ | **KAFF-112** | 3 | Reactivate the user you just deactivated |
| ☐ | **KAFF-117** | 5 | Read the audit trail as Owner at `/audit`; get refused (**403**) as any other role |
| ☐ | **KAFF-118** | 3 | Every action in this walkthrough left exactly one audit row — spot-check two or three in the panel |
| ☐ | **KAFF-119** | 5 | Register a client and see the duplicate-phone warning live in the form (§6 step 5) |
| ☐ | **KAFF-120** | 2 | Switch a client's kind to "فرد" (Individual) in the form and watch the tax-registration field disappear |
| ☐ | **KAFF-121** | 3 | Edit a client's contact details from `/clients/:id` |
| ☐ | **KAFF-123** | 2 | Archive a client — two-step confirm, then it's still there, just marked "مؤرشف" |
| ☐ | **KAFF-124** | 2 | Search the client list by name, code or phone, and filter by الحاليون/المؤرشفون |
| ☐ | **KAFF-126** | 8 | The client screens as a whole — list, create, edit, archive, search, all in one form |
| ☐ | **KAFF-127** | 8 | The user-management screens as a whole — list, create, edit, role change, department move, deactivate, reactivate. **Known gap, not a defect to be surprised by:** the search box and filter chips render but have no server behaviour yet (`F-127-1`), and the list's own order is unruled (`Q59`) |
| ☐ | **KAFF-128** | 3 | The audit trail screen — same click as KAFF-117 above, this row is the *screen*, that one is the *permission* |

**These 21 stories, 84 points, all ask you to watch something happen on screen. `process/agile.md`
§4 is run, not read — if a row above didn't happen in front of you, don't tick it.**

### Cannot be reached this pass — not accepted, and here is why

| Story | Pts | Why not |
|---|---:|---|
| **KAFF-105b** | 5 | Carries the project list on `/api/auth/me` — correctly returns *empty*, because §1 holds. The mechanism is exercised (every landing reads it); the non-empty case cannot be, without a project |
| **KAFF-111** | 3 | Deactivating a user revokes their project assignments — there are none to revoke. The screen honestly shows "0 من المشاريع" (zero), which proves the *count* renders, not that a real revocation happened |
| **KAFF-113** | 5 | Assign a user to a project — no project exists to assign anyone to (§1) |
| **KAFF-114** | 3 | Revoke a project assignment — same blocker as KAFF-113 |
| **KAFF-116** | 3 | Every audit record says how access was granted — the `grantPath` field exists and is rendered when present, but nothing in this seed carries one, because no project-scoped grant exists to record |
| **KAFF-125** | 3 | The staff shell works, live, exactly as described in §5 — but its verdict is `VERIFIED / LAPSED`, and `STATUS.md`'s sprint-6 item 3 records that **`AC-125-C`'s rewrite is still awaiting your ratification**, not a Verifier's or a BA's. Accepting the shell you watched work is not the same act as ratifying that criterion text; do the second one separately, in writing, if you're satisfied with it |
| **KAFF-104**, **KAFF-115**, **KAFF-129** | 5 + 8 + 8 | `DEFERRED` — never built, carried to `slice 1b`. Nothing to demo; §1 and `STATUS.md` both already say so |
| Any project, extract, ledger, posting, or balance | — | §1. No `POST /api/projects`-shaped endpoint exists in this codebase today, re-confirmed live 2026-09-09 |
| Slice 2's masters (catalogue, أبواب, employees, workers, subcontractors, suppliers) | — | None of it is built. `STATUS.md`: every slice-2 story is `NOT-BUILT` |

**KAFF-107** (2 pts, `FOLDED`) and **KAFF-122** (0 pts, `SUPERSEDED`) carry no state this checklist can
move either way, and `STATUS.md`'s own slice-1 total of 127 already excludes both — nothing here needs
to account for them a second time.

**Total: 84 points demonstrable and listed above for acceptance in this pass; 22 points cannot be
reached this pass for the reasons given; 21 points `DEFERRED`. 84 + 22 + 21 = the slice's 127** —
every point is named somewhere on this list, on purpose, so nothing is accepted by silence and
nothing is left unaccounted either.
