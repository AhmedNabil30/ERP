# Verification — 2026-09-07

**Verifier, fresh session.** `CLAUDE.md`: *"If you wrote the code, you do not certify it."* I wrote
none of the code in scope and none of the commits in scope.

**Scope:** the 12 points with no standing verdict — **KAFF-128** (`BUILT` 2026-09-07),
**KAFF-125** (`VERIFIED`/`LAPSED`), **KAFF-118** (`BUILT` 2026-09-05), **KAFF-101b**
(`BUILT` 2026-09-02).

**Brief:** written by the coordinating session that briefed every builder and pushed every commit in
scope. Every claim in it is a claim to check, the gate figures included. §1a records where it was
right and where it was wrong.

**This file was created before any other action** and is written finding-by-finding, committed as it
goes. A section still marked `pending` at the end is a section I did not reach, and that is itself a
finding — see the last section.

---

## 0. Progress of this report

Everything is `pending` until reached. Nothing is marked done on an author's evidence.

| # | Item | State |
|---|---|---|
| 1 | Opening gate — `HEAD`, `git status`, stranded hosts, stack alive | pending |
| 1a | Corrections to the brief | pending |
| 2 | Gate figures re-measured by me | pending |
| 3 | KAFF-128 — the audit trail screen, and the strictest permission in the system | pending |
| 4 | KAFF-128 — `AC-128-B`'s Technical Office half: is the "cannot be built" claim true? | pending |
| 5 | KAFF-128 — `TC-1-304`, `ActorRole` as at the time of the event | pending |
| 6 | KAFF-125 — does `V-34-H`'s lapse stand? `git diff` on the three commits | pending |
| 7 | KAFF-125 — is *"criterion defect, not a lapse"* the right ruling for `AC-125-C`? | pending |
| 8 | KAFF-125 — rules 6 and 9 have no acceptance criterion | pending |
| 9 | KAFF-118 — whitelist-shaped audit coverage, and its positive controls | pending |
| 10 | KAFF-101b — the sign-in screen, never independently checked | pending |
| 11 | `F-1` re-run, and what that class of evidence still cannot see | pending |
| 12 | The first-strong exposure on the client and user lists | pending |
| 13 | The 43 unobserved-red QA cases | pending |
| 14 | Closing gate, and citations | pending |
| 15 | Verdict per story | pending |
| 16 | What I did not reach | pending |

### Findings index

*(populated as findings are made)*

---

## 1. Opening gate

**Recorded before anything was measured or mutated.**

| Gate | Value |
|---|---|
| `git rev-parse --short HEAD` | **`0359b8d`** — matches the brief |
| `git status --porcelain` | clean at start; then `?? qa/slice-1/verification-2026-09-07.md`, this report |
| `docker ps` | `kaff-db  Up 2 weeks (healthy)` |
| API `GET /api/health` | `{"status":"healthy","databaseReachable":true,"guardsInstalled":true,"missingGuards":[]}` — **`kaff_demo`, confirmed by me** |
| SPA `GET http://localhost:4200/` | **`200`**, 697 bytes — **the server is genuinely up**, so an E2E result from this session is not a silent skip |
| Live hosts | `dotnet run … Kaff.Api --configuration Release --no-build` (wrapper PID 5176, host PID **10656**) · `ng serve` (PID 15424) · `npm start` (PID 7732) — one of each, no strandings |

### `V-35-A` — the brief's own MSB3021 warning fires immediately, and it blocks the gate order

`dotnet build KaffErp.sln -c Release -warnaserror` against the stack as handed to me returns
**24 errors, 0 warnings** — every one `MSB3021`/`MSB3026`/`MSB3027`, all naming
`The file is locked by: "Kaff.Api (10656)"`. **This is not a code error**, exactly as `STATUS.md`
warns, and it is not a finding against the code. It is recorded because it fixes the order of this
pass: **every live-stack check must run before the build gates**, since measuring the build requires
stopping the API that the live checks need. I ran the live work first and stopped the host once.

⚠️ Also: the brief and `STATUS.md` both say `dotnet build Kaff.sln`. **There is no `Kaff.sln`** —
the solution is **`KaffErp.sln`**, and `Kaff.sln` returns `MSB1009: Project file does not exist`,
which reads as a broken repository and is not. Severity **INFO**; noted so the next session does not
lose the same two minutes.

---

## 3. KAFF-128 — the strictest permission in the system

**What should be true** comes from the story, not the code: *"strictly limited to the Owner
(Global) … completely hidden from all other roles, **even for their own projects**"* — `AC-128-B`,
and D-049 ruling 1.

### 3.1 The role census against `GET /api/audit`, driven live

Every row below is a real sign-in against the running API on `kaff_demo`, the forced password change
cleared where present, then `GET /api/audit` with that session's cookie. **`permissions` is what
`GET /api/auth/me` returned for that session**, not what a catalogue file says.

| Account | Role | Permissions returned | `GET /api/audit` |
|---|---|---|---|
| `owner_demo` | Owner | `… AuditRead` | **`200`** — records returned |
| `v35_techoffice` *(created by me, see §4)* | TechnicalOffice | `ProjectCreate, CatalogueManage, BabManage, SubcontractorManage` | **`403`** |
| `sara_finance_demo` | Finance | `SupplierManage, TreasuryPostCompany, AccountManage, PeriodClose` | **`403`** |
| `hend_hr_demo` | Hr | `UserRead, EmployeeManage` | **`403`** |
| `karim_sales_demo` | MarketingSales | `ClientManage, OpportunityManage` | **`403`** |
| `portal_client_demo` | Client (portal) | — | **`401` at sign-in**, `auth.invalid_credentials` — D-065's identical-message rule holds |
| *(no session)* | — | — | **`401`** |

**The server half of `AC-128-B` holds, and `AuditRead` is held by exactly one role.** This is the
part of the criterion that can be discharged today, and it is discharged.

---

## 4. `V-35-B` — the builder's *"cannot be built"* is half wrong, and the half that is right is a real hole

**MEDIUM.**

The KAFF-128 building agent reported `AC-128-B` **partially discharged** on two grounds: *no such
seeded account*, and *`POST /api/projects` returns 404 so no assignment could be made*. **I tested
both.**

| Claim | Verdict | Evidence |
|---|---|---|
| *"no such seeded account"* | **true but not load-bearing** | `deploy/DEMO.md` §4.4 seeds Owner, Hr, Finance, MarketingSales and a portal Client. No TechnicalOffice. |
| *"a TechnicalOffice account cannot be built"* | ⛔ **FALSE** | `POST /api/users` as the Owner with `role=TechnicalOffice, department=Operations, operationsSubDepartment=Technical` returns **`201`**, and that account signs in (`204`), clears its forced password change (`204`), and is refused `GET /api/audit` with **`403`**. It took one request. |
| *"`POST /api/projects` returns 404"* | **true, re-confirmed live** | `POST /api/projects` → **`404`**. So does `GET /api/projects`. `deploy/DEMO.md` §1 is still accurate. |
| *"so no assignment could be made"* | **true** | `POST /api/projects/{fabricated-guid}/assignments` as the Owner → **`403` `auth.forbidden`**. There is no route to a `Project` row, so there is no route to an assignment. |

**Two corrections follow, and they point in opposite directions.**

1. **The first attempt failed for a reason the builder appears to have taken as proof.** My own
   attempt 1 sent `operationsSubDepartment: "TechnicalOffice"` and got a bare `400`; attempts with
   the value omitted got `identity.operations_requires_sub_department`. The legal values are
   `Technical` / `Financial` / `Administrative` (`OperationsSubDepartment`). **A `400` on the first
   payload shape is not a demonstration that the account cannot exist** — and the difference between
   the two readings is one retry.
2. **The clause that makes this criterion unusual is still unproven, and cannot be proven today.**
   `AC-128-B` says *"Technical Office **on a project they are assigned to**"*. `projects` came back
   `[]` for every non-Owner account I drove, because no `Project` row can be minted. So what is
   proven is *"a TechnicalOffice user with no assignment is refused"* — which is what `role × assignment`
   would predict anyway. **The one clause that distinguishes this permission from every other
   permission in the system has no evidence, in any test, at any level.**

⛔ **Record this as a coverage hole on the strictest gate in the system, not as a discharged
criterion.** `AC-128-B` is **partially discharged** — the builder's own word — and the residue is
the *whole point of the criterion*. It stays open until a `Project` can exist; it belongs with
sprint 6 item 5, and it should be attached to whatever story creates `POST /api/projects` as an
acceptance criterion of that story, so it cannot be forgotten when the blocker clears.

**Data I added to `kaff_demo`, declared:** one user, `v35_techoffice`, created through the real
endpoint (`201`), and forced-password-change cleared on it and on the three seeded staff accounts
(their passwords are now the temporary one with `New` appended, per `deploy/DEMO.md` §4.4's own
convention). **No `src/` change, no SQL, no `DbContext` write.** Re-run `scripts/seed-demo.ps1`
against a dropped/recreated `kaff_demo` to restore the documented state.


