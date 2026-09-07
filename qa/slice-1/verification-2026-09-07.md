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
| 1 | Opening gate — `HEAD`, `git status`, stranded hosts, stack alive | **done** — §1 |
| 2 | Gate figures re-measured by me | **done** — §2, and §2.1 is a finding |
| 3 | KAFF-128 — the role census against `GET /api/audit` | **done** — §3 |
| 4 | KAFF-128 — `AC-128-B`: is the "cannot be built" claim true? | **done** — §4, **half of it is false** |
| 5 | KAFF-128 — `TC-1-304`, `ActorRole` as at the time of the event | **done** — §5, **PASS, driven** |
| 6 | KAFF-125 — does `V-34-H`'s lapse stand? | **done** — §6, **confirmed on diffs** |
| 7 | KAFF-125 — is *"criterion defect, not a lapse"* right? | **done** — §7, **right on D-096, wrong on authority** |
| 8 | KAFF-125 — the rewritten `AC-125-C` | **done** — §8, **it cannot pass** |
| 9 | KAFF-125 — rules 6 and 9 have no criterion | **done** — §9, **I disagree with QA on rule 6** |
| 10 | The first-strong exposure on the client and user lists | **done** — §10, **it is a non-defect** |
| 11 | Why the `dir="ltr"` fix really works | **done** — §11 |
| 12 | `F-1` re-run with its positive control | **done** — §12 |
| 13 | KAFF-101b — the sign-in screen, never independently checked | **done** — §13, **all eight criteria** |
| 14 | KAFF-118 — the whitelist, and the claim driven end to end | **done** — §14 |
| 15 | **E2E and CI** — added mid-pass by the coordinator | **done** — §15, **reproduced, `V-35-K`** |
| 16 | Closing gate, and the state the machine is left in | **done** — §16 |
| 17 | Verdict per story | **done** — §17 |
| 18 | The four questions the brief asked directly | **done** — §18 |
| 19 | What I did not reach | **done** — §19, **eight items** |
| 20 | Findings index | **done** — §20 |
| 21 | Corrections to the brief | **done** — §21, **five** |

**Nothing in this pass is marked done on an author's evidence.** Every figure was re-measured and
every claim re-driven.

### Findings index

**See §20** — it is at the end because it was written last, from findings made in order.
**One HIGH (`V-35-K`), seven MEDIUM, one LOW, two INFO, one recorded PASS.**

⛔ **The headline: no defective shipped behaviour was found in any of the four stories, and the
worst finding is about a gate rather than about the code.** The E2E suite that covers every screen
in slice 1 has never once run in CI against a database it could pass against.

---

## 2. The gate figures, re-measured by me

**Every figure below I ran in this session.** The brief's column is what it claimed. `STATUS.md`
says *"Re-measure rather than quote — every one of these has been wrong once."*

| Gate | Brief / `STATUS.md` claimed | **Measured by me** | |
|---|---|---|---|
| Build, **Release**, `-warnaserror` | 0 / 0 | **0 warnings, 0 errors** | ✔ *(only after stopping the API — `V-35-A`)* |
| Build, **Debug**, `-warnaserror` | 0 / 0 | **0 warnings, 0 errors** | ✔ |
| `dotnet format --verify-no-changes` | clean | **clean, exit 0** | ✔ |
| Domain.Tests | 127 / 127 | **total 127 · failed 0 · succeeded 127 · skipped 0** | ✔ |
| Api.Tests | 317 / 317 | *§2.1* | |
| Citations | 1183 / 0 / 0 *(brief)* · 1184 *(`STATUS.md`)* | *§2.1* | ⚠️ the two sources disagree by one before I start |
| SPA build, `strictTemplates` | clean *(Frontend's own figure)* | **clean** — production build, 12 lazy chunks emitted | ✔ **re-measured** |
| Web unit tests | 8 / 8 *(Frontend's own figure)* | **8 passed, 8 total — in exactly 1 test file** | ✔ figure right, *§2.2* |
| E2E | 25 / 25 *(Frontend's own figure)* | *§15* | ⛔ **and see `V-35-K`** |

### 2.1 `V-35-L` — ⛔ **the citation gate is RED at `HEAD`, and both sources report it green**

**MEDIUM. The brief names this gate as a deliverable condition — *"must stay at 0 broken and 0
legacy"* — and it was already at 2 broken when I arrived.**

```
scripts/check-citations.ps1        exit code 1
SM-31 identifier citations checked: 1183
  broken (identifier absent):        2
  legacy line-number citations:      0

BROKEN - a cited identifier does not exist:
  verification-2026-08-23.md:120  'audit.grant.portal_client' is absent from ar.json
  verification-2026-08-23.md:121  'audit.grant.portal_client' is absent from en.json
```

| Source | Claim |
|---|---|
| The brief | citations **1183 / 0 / 0** |
| `STATUS.md` "Gates, as last measured" | citations **1184 / 0 / 0** at `a21892e` |
| **Measured, `HEAD` `0359b8d`** | **1183 / 2 / 0 — exit 1** |

**It is not my doing, and I checked rather than assumed:** my report contains **zero** `Verified:`
citations, and `git status` shows the working tree modified in exactly one file — this report.
`verification-2026-08-23.md` is untouched by me.

**The cause is traceable to one commit, and it is in this pass's own scope.**
`git log -S 'audit.grant.portal_client' -- …/ar.json …/en.json` returns **`5dc1ebf`, 2026-09-07,
*"KAFF-128 built"***. **KAFF-128 rule 8 required those four orphan keys to be used or deleted; the
builder deleted them — correctly, §14.4 — and the deletion broke two citations in a historical QA
report that had cited one of them as present.**

**This is the D-096 problem in a new place: a record that was true on its date, invalidated by a
later change, with a gate that notices.** The gate worked. Nobody ran it.

⚠️ **`HEAD` is literally titled *"Citation gate back to 0 legacy: I wrote the retired form while
describing it."*** That commit repaired the **legacy** count and the **broken** count stayed at 2
through it — which is what a partial re-measurement looks like. **Three of the brief's own gate
figures have now been checked; this is the one that was wrong.**

**The fix is not mine and is one line of the 2026-08-23 report** — a dated record citing a key that
has since been deliberately removed. `SM-29`'s strike-don't-delete convention and D-096's "history
never claims a present tense" both bear on how, and it is the Scrum Master's file to touch, not
mine.

### 2.2 The web unit figure is right and much smaller than it sounds

`8 / 8` is true. **It is 8 tests in one file** — `src/Web/src/app/core/auth/guards.spec.ts`, added
2026-09-05 at `8ea9258`. `Test Files 1 passed (1)`.

**No component in the SPA has a unit test**: not the sign-in screen, not the landing, not either
list, not the audit trail. The whole frontend unit gate is the route guards. That is not a defect —
`agents.md` puts screen behaviour in E2E — but *"vitest 8/8"* on the board reads as broader coverage
than it is, and §15 is about to show that the suite which *does* cover the screens is not running
where the board thinks it is.

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
convention). Later, §5, its role was changed to `SiteEngineer`. **No `src/` change, no SQL, no
`DbContext` write.** Re-run `scripts/seed-demo.ps1` against a dropped/recreated `kaff_demo` to
restore the documented state.

### 4.1 The UI half of `AC-128-B`, driven — and it holds

Chromium, one browser per account, real sign-in through `POST /api/auth/sign-in`, then a **hard
load** of `http://localhost:4200/audit` (`Page.navigate`, not an in-app `pushState` — that
distinction is `TC-1-301`'s whole point).

| Account | Role | `location.pathname` after the hard load | Surface |
|---|---|---|---|
| `v35_techoffice` | TechnicalOffice | **`/forbidden`** | `ليس لديك صلاحية لهذا الإجراء.` + chrome + sign-out + back-to-start |
| `sara_finance_demo` | Finance | **`/forbidden`** | same |
| `hend_hr_demo` | Hr | **`/forbidden`** | same |
| `karim_sales_demo` | MarketingSales | **`/forbidden`** | same |
| `owner_demo` | Owner | **`/audit`** | the trail renders, 51 records |

`dir="rtl"` and `lang="ar"` on every one; `localStorage` and `sessionStorage` **empty in both
directions on all five** (D-050 holds under a real browser, re-confirmed rather than quoted).

**`AC-128-C` discharged** — the Owner reaches the screen on a hard load, not only on an in-app
navigation, so the guard does not depend on its position in `canActivate`.

**`AC-128-D` discharged, by enumeration rather than by a list of forbidden controls** — which is what
`TC-1-302` demands. The complete interactive set on the rendered trail is: the chrome (hamburger,
two locale buttons, sign-out, one nav link), **two `input[type=date]` filters, one apply button, and
one button per row that opens the detail panel.** Nothing edits, deletes or corrects. **No `form`
posts anything at an audit record.**

⚠️ **What is still not discharged is `TC-1-300`'s own stated condition**, and the case says so in
its own words: *"An unassigned user is refused by the assignment check whatever the permission says,
so a case seeded without the assignment stays green against a project-scoped trail. **Seed the
assignment or the case proves nothing.**"* I could not seed it — §4. **`TC-1-300` is therefore
executed-but-inconclusive, not passed.** It is the only case in this pass with that status and it is
the P1 on the strictest gate in the system.

---

## 5. `V-35-C` — `TC-1-304` decided by driving it, not by reading the component: **it passes**

**Result: PASS. No finding against the code.** Recorded as a numbered item because the builder left
it open and because the method is the point.

The builder's argument was *"the component renders `entry.actorRole` and joins against nothing, so
the case can only fail if a role changes between act and read"* — **an argument from the code, which
is exactly what `agents.md` §7 says a verdict may not rest on.** So I made the role change.

1. `v35_techoffice` acted six times as **`TechnicalOffice`** (five `SignedIn`, one `Modified` on
   `/api/auth/change-password`). `GET /api/audit` showed all six with `actorRole: "TechnicalOffice"`.
2. `PUT /api/users/{id}/role` with `{"role":"SiteEngineer"}` → **`200`**,
   `{"role":"SiteEngineer","revokedProjectIds":[]}`. `GET /api/users` confirms the account is now
   `SiteEngineer`.
3. `GET /api/audit` again: **all six earlier records still read `actorRole: "TechnicalOffice"`.**
4. The **screen** re-opened as the Owner after the change: **six rows render `· المكتب الفني`, and
   zero rows anywhere on the trail render the site-engineer role.**

**`TC-1-304` is green on evidence that could have been red** — the Given it names ("that user's role
is later changed") was actually performed, so this is not a fixture that is green against both
designs. The `AC-118-J` half — *"an actor since deactivated is still named"* — I did **not** drive;
see §16.

**Incidental, and not a defect (1):** `eventType` is `null` on **27 of 51** records. Every one of them
has `action` = `Modified` or `Created` with a populated `changedProperties`; `eventType` is
populated only on `action = Occurred` (`SignedIn`, `SignInFailed`, `DuplicatePhoneAcknowledged`).
That is the two-axis design working, and the screen renders `حدث · تسجيل دخول` for the one and the
changed-property list for the other. Recorded so the next reader does not mistake it for a hole.

---

## 6. `V-35-D` — `V-34-H`'s lapse is **confirmed**, on diffs rather than a file list

**Confirmation of a standing finding, not a new one.** The brief asked for `git diff`, because
`D-122 §2` records *"a file list is not a diff"* as this board's own error. Here are the diffs.

| Commit | Date | What it did to `landingFor()` |
|---|---|---|
| **`e0fd5cf`** | 2026-09-04 | `MarketingSales` moved from `{ kind: 'pending', titleKey: 'landing.pending.marketing_sales.title' }` to **`{ kind: 'clients' }`**. `'clients'` added to the `Landing` union. **`navPathFor()` created**, sending MarketingSales to `/clients` instead of `/` |
| **`b5c9e46`** | 2026-09-04 | **Does not touch `landing.ts`.** It rewrote `client-manage.guard.ts` (+9/−2) and added the whole `forbidden-page` component and its route |
| **`8ea9258`** | 2026-09-05 | `Owner` moved from `{ kind: 'pending', … }` to **`{ kind: 'users' }`**; **the `pending` variant was deleted from the `Landing` union** along with its `navLabelKeyFor` case; `navPathFor()` rewritten to a three-way switch |

**Two of nine roles changed landing, and a variant of the type was deleted, after the 2026-09-04
verdict.** `AC-125-C` is *"the four profile-only roles land on S-005"* and the story's rule 6 is
role-based landing; **the landing dispatch is KAFF-125's own subject matter.** Under D-096 §1 this
is a commit that changed behaviour the story's criteria assert, made after the verdict.

⛔ **`V-34-H` stands. KAFF-125's `LAPSED` verdict is correct, and this pass re-verifies it below
rather than reinstating it on the old evidence.**

**One correction to `V-34-H` itself:** it names three commits. **`b5c9e46` did not change
`landingFor()`** — its `src/` diff is `app.routes.ts`, `client-manage.guard.ts` and the new
forbidden page. It is still a legitimate part of the lapse basis (it changed how a refused route
behaves, and `AC-125-B` is about guards), but the sentence *"`landingFor()` changed at `e0fd5cf` /
`b5c9e46` / `8ea9258`"* is true of two of the three. Severity **INFO**; the verdict is unaffected.

---

## 7. `V-35-E` — the *"criterion defect, not a lapse"* ruling is **right on D-096 and wrong on who was allowed to make it**

**MEDIUM.**

**The brief asks whether the Scrum Master's ruling is correct. Split answer: yes on the lapse, no on
the authority.**

### 7.1 On D-096, the ruling is correct, and its facts check out

I re-ran the amendment's own evidence rather than accepting it:

* `git log --oneline -S "session.projects" -- src/Web/src/app/features/landing/landing-page.html`
  returns **exactly one commit, `7461332`**, 2026-09-03, *"KAFF-125: the staff shell"* — **KAFF-125's
  own build commit.**
* `git show 7461332:…/landing-page.html` carries `data-testid="profile-projects"`,
  `class="project-level"`, `class="project-path"` **and** `data-testid="profile-projects-empty"`.
  Every element the old clause forbade was there from the first line of this story's code.
* Only three commits have ever touched that file: `7461332`, `e0fd5cf`, `8ea9258`.

**So no commit after the verdict changed the behaviour the old `AC-125-C` asserted.** D-096 §1
requires exactly that for a lapse. **A second lapse would have been wrong, and the Scrum Master was
right to refuse it.**

### 7.2 On authority, the ruling took a call a Verifier had reserved to Nabil, in writing

`qa/slice-1/verification-2026-09-04.md` §6 is unambiguous, and it is worth quoting because the
amendment does not quote it:

> *"**This is Nabil's criterion and Nabil's call. I am not resolving it** … **One line closes this**,
> and only Nabil can write it: either `AC-125-C` is amended and S-005 stands, or the criterion holds
> and the projects section comes off the profile landing. **It should not close by a Verifier**"*

And its verdict block: *"**`AC-125-C` is not accepted as satisfied.** It is deliberately unmet."*

**The 2026-09-07 amendment chose the first of the two branches** — *"`AC-125-C` is amended and S-005
stands"* — and it was written by the **BA**, three days later, with no record anywhere that Nabil
was asked or answered. `verification-2026-09-05.md` still carried it as *"Nabil's, an unperformed
check"*.

**Nothing in the amendment's reasoning is wrong.** S-005 is the older statement, the deviation is
defensible, D-104 recorded it honestly, and I would have advised the same outcome. **The defect is
that a decision explicitly escalated to Nabil was closed inside the agent layer**, which is the
precise failure `CLAUDE.md` names — *"An invented rule is always plausible, which is why it survives
review and surfaces months later during acceptance."* This one is more than plausible; it is
probably right; **and it still has not been asked.**

⛔ **Recommendation: leave the amended text in place and raise the amendment itself to Nabil as a
one-line ratification.** Do not revert it — reverting would restore a criterion known to be false.
The cost of closing this properly is the one message §6 of the 2026-09-04 report said it was.

---

## 8. `V-35-F` — the rewritten `AC-125-C` **cannot pass today**, which is the fault it was written to cure

**MEDIUM.**

The amended `AC-125-C` opens:

> *Given an active user of Finance, TechnicalOffice, SiteEngineer or HeadOfDesign, freshly signed
> in, **holding two active project assignments at two different levels***

**No `Project` row can be created in this system** — `POST /api/projects` is **`404`**, confirmed
live in §4, and `deploy/DEMO.md` §1 has said so since 2026-09-03. Therefore **no user can hold one
active assignment, let alone two at two levels.** The criterion is marked *(fails if the rule is
broken)* and it **cannot be executed at all**, by anyone, until slice 4.

`agents.md` §3c, quoted inside this very story and inside `landing.ts`'s own comments: **"a criterion
that cannot pass is as bad as one that cannot fail."** The old `AC-125-C` was false. The new one is
unexecutable. **The story swapped one §3c fault for the other**, and the amendment does not
acknowledge that it did.

**What would fix it** is not mine to write, but the shape is visible: the only part of `AC-125-C`
that can be discharged today is its **third** clause — the zero-assignment empty state — and that
one **I did discharge**: Finance, TechnicalOffice, SiteEngineer and HeadOfDesign all land on the
profile surface and, with no assignments, render `لست مُسنداً إلى أي مشروع حتى الآن.` as an explicit
empty state rather than an absent section (`ux/components.md` §9). **Splitting the criterion so the
empty-state half can be discharged now, and the two-assignment half is held against slice 4 with an
identifier, would leave nothing unfalsifiable and nothing unpassable.** Routed to the BA, not
decided here.

---

## 9. KAFF-125 rules 6 and 9 have no criterion — the brief asks whether I agree that rule 6 is unfalsifiable

**QA's finding is right that both rules are uncovered. I disagree with the reasoning offered for
rule 6, and the disagreement produces a finding.**

### 9.1 `V-35-G` — rule 6 is falsifiable **today**, and on its face the shipped code contradicts it

**MEDIUM.**

Rule 6, verbatim: *"Role-based routing sends each signed-in role to its ruled landing. **Built from
the permission set returned by `/api/auth/me`, never from `switch (role)`** — department and
per-project seniority are independent axes a role switch cannot see."*

`src/Web/src/app/core/navigation/landing.ts` -> `landingFor` is **literally `switch (role)`**, nine
cases. `navLabelKeyFor` and `navPathFor` — **which decide the one nav item a role sees and where it
points** — both switch on `landingFor(role).kind`, so the navigation is derived from the role switch
too.

**The argument that rule 6 is unfalsifiable before slice 4 confuses a prohibition with its
consequence.** The *consequence* — a user seeing the wrong item because department or per-project
seniority differ from what the role implies — is indeed unobservable in slice 1, and on that half QA
is right. But **rule 6 as written prohibits a mechanism**, and a mechanism prohibition is checkable
the moment the mechanism exists, exactly the way `CLAUDE.md`'s *"never use `float` … anywhere near
money"* is checkable before any money moves. A unit assertion over `landing.ts` — or a lint rule —
would decide it in one line, and it would be **red today**.

**The builder saw this and answered it in the file it governs.** `landing.ts`'s header comment says:
*"Branches on role, not a `switch (role)` menu built for permission convenience. Rule 6 forbids
building *navigation* from `switch (role)` … that rule is about which nav items a role sees as it
grows in later slices, not about which of two server-projected shapes this response carries."*

⛔ **That reading may well be right, and it is not the builder's to make.** Two things about it:

1. **It does not cover `navPathFor` and `navLabelKeyFor`.** The defence is that rule 6 governs
   *navigation*, not the landing dispatch — but those two functions **are** the navigation, and they
   are derived from the role switch. On the builder's own reading of the rule, the code still
   breaches it.
2. **Because rule 6 has no acceptance criterion, this interpretation is recorded nowhere a reviewer
   looks** — only in a doc comment inside the file that would fail the rule. That is the
   `SM-33`/`D-097 §2` shape: the artefact that states the rule and the artefact that decides it are
   the same artefact.

**My answer to the brief: no, I do not agree rule 6 is unfalsifiable before slice 4** — its
consequence is, its prohibition is not, and on the prohibition the code is in breach today. Whether
the prohibition means what the builder says it means is **UX's and the BA's to settle**, and it
needs a criterion either way.

### 9.2 Rule 9 — uncovered, and this pass is the first evidence it holds

Rule 9: *"The shell enforces no permission … a route a role should not see is reached, sent to the
server, and refused there. Hiding it is convenience only."*

**No criterion on KAFF-125 asserts it.** It is nonetheless **true today, and §3.1 and §4.1 of this
report are the proof**: the four non-Owner roles are refused at `/audit` by the **server** with a
real `403` (`GET /api/audit`, driven directly, no browser involved), and *separately* the shell
routes them to `/forbidden`. Server refusal and UI hiding are both present and independent, which is
what `CLAUDE.md` requires.

**That evidence exists only because this pass gathered it for KAFF-128.** No suite asserts rule 9
for KAFF-125's own routes, and the story has no criterion that would make one necessary. **Agreed
with QA: it is a genuine coverage gap.** Severity **LOW** — the behaviour is right; the assertion
is missing.

### 9.3 `V-34-I` is still present at `HEAD`, unrepaired

Not a new finding — recorded so it is not lost. `src/Web/src/app/core/navigation/landing.ts` line 51
still reads `âš ï¸` where line 39 reads `⚠️`. I can see it in the `e0fd5cf` diff and in the file at
`HEAD`. Still harmless (a comment), still the only occurrence.

### 9.4 `AC-125-C`'s empty-state clause, discharged live

Driven at **390px** in Arabic, real sign-ins, hard loads of `/`:

| Account | Role | Lands on | Projects section |
|---|---|---|---|
| `sara_finance_demo` | Finance | S-005 profile | **explicit empty state** `لست مُسنداً إلى أي مشروع حتى الآن.` — `[data-testid=profile-projects-empty]` present, `[data-testid=profile-projects]` **absent** |
| `v35_techoffice` | SiteEngineer *(after §5's role change)* | S-005 profile | same |
| `hend_hr_demo` | Hr | HR projects | `[data-testid=hr-projects-empty]`, `لا توجد مشاريع بعد.` |
| `karim_sales_demo` | MarketingSales | **`/clients`** | client list, 3 clients |
| `owner_demo` | Owner | **`/users`** | user list |

**No horizontal overflow on any of the five** — `scrollWidth 390 / clientWidth 390` on every one.
`ux/components.md` §9 is satisfied: an empty state, not an absent section, and no placeholder rows.

**A useful side effect of §5:** the role change to `SiteEngineer` is reflected on the landing
immediately (`الصفة: مهندس الموقع`), which independently confirms `landingFor()` is live and driven
by the session payload rather than anything cached.

**TechnicalOffice and HeadOfDesign were not both driven as profile roles** — the one TechnicalOffice
account I had became a SiteEngineer in §5, and **no HeadOfDesign account exists**. Two of the four
profile roles are observed; the other two are the same code path. Stated rather than glossed.

---

## 10. `V-35-H` — ⛔ **sprint 6 item 4 is a non-defect. The client and user lists do not reorder.**

**MEDIUM — because it is scheduled work that should not be done, on a mechanism that is wrong.**

`STATUS.md` sprint 6 item 4, and the brief's hint 2, both say:

> *"The first-strong exposure on the client and user lists — their phone `<bdi>`s carry no `dir` and
> are green only because a seeded number is one unbroken digit run. **`0100-123-4567` reorders**."*

**It does not. I registered one and looked at it.**

`POST /api/users` with `phone: "0100-123-4567"` → **`201`** (`v35_bidi`). On the shipped user list at
390px, that phone's `<bdi>`:

```
dir attribute = (none)      computed direction = ltr      first char x=252  last char x=341   → in order
```

**Every `<bdi>` on both lists resolves `ltr`** — 6 on the client list (including a `+20 100 555 0001`
I registered through `POST /api/clients`, `201`, `C-10003`) and 14 on the user list. **None is
reordered.** Codes, usernames, Latin phones, Arabic-Indic phones: all in order.

### Why — the recorded mechanism is wrong, and that is the reason the prediction failed

The premise in the brief, in `STATUS.md`, and **in a comment inside shipped source** is:

> *"A `<bdi>` defaults to `dir="auto"`, which is first-strong — and a timestamp contains no strong
> character at all … so first-strong finds nothing, **falls back to the paragraph's RTL**"*
> — `src/Web/src/app/features/audit/audit-trail-page.html`, the comment above `bdi.row-when`

**`dir="auto"` with no strong character falls back to `ltr`, not to the parent.** Measured in the
shipped page, inside `<html dir="rtl">`, with a raw `<bdi>` carrying no `dir`:

| Content | `<bdi>` no `dir` | plain `<span>`, no isolation |
|---|---|---|
| `0100-123-4567` | `ltr`, in order | `rtl`, in order |
| `::1` | **`ltr`, in order** | `rtl`, **REORDERED** |
| `/api/auth/sign-in` | **`ltr`, in order** | `rtl`, **REORDERED** |
| `07/09/2026, 23:26:25` | **`ltr`, in order** | `rtl`, **REORDERED** |
| `01a07d85-…-dcf7457bf240` | `ltr`, in order | `rtl`, in order |

**`<bdi>` alone already handles every one of them.** What reorders is an *unisolated* run — which is
what `<bdi>` exists to prevent.

---

## 11. `V-35-I` — the `dir="ltr"` fix is **right**, its stated reason is **wrong**, and the true reason is more dangerous

**MEDIUM.**

Having shown a bare `<bdi>` is enough for `::1` and a slash-separated date, I tested the shipped
element itself: **on the live audit trail, I removed `dir="ltr"` from the six shipped
`bdi[dir="ltr"]` elements at runtime and re-measured.**

```
with dir=ltr:  ltr / in order        without dir:  rtl / REORDERED     "07‏/09‏/2026، 23:34:20"
```

**All six reordered.** So the fix is load-bearing and must stay. But it reorders for a reason the
comment does not name. The timestamp's code points:

```
0030 0037 200f 002f 0030 0039 200f 002f 0032 0030 0032 0036 060c 0020 0032 0033 003a ...
   0    7  RLM    /    0    9  RLM    /    2    0    2    6    ،   sp   2    3    :
```

⛔ **`Intl.DateTimeFormat('ar-EG')` injects `U+200F RIGHT-TO-LEFT MARK` into the formatted string.**
`U+200F` **is a strong RTL character.** First-strong therefore *does* find one, immediately, and
resolves the `<bdi>` to RTL. Proven both ways in the same page:

| Probe | Resolved direction |
|---|---|
| `<bdi>` containing the real timestamp (with `U+200F`) | **`rtl`** |
| the same string with `[‎‏]` stripped | **`ltr`** |

**Why the difference matters more than the fix:**

1. **The recorded rule over-predicts.** It says any run with no strong character reorders — so it
   condemns the client and user lists, which are fine. That is exactly the false prediction §10
   found on the board as scheduled work.
2. **The recorded rule under-predicts the real hazard.** The true rule is: **any string that has
   passed through an Arabic-locale formatter carries `U+200F` and will reorder inside a `<bdi>`
   unless `dir="ltr"` pins it.** That is a property of the *formatter*, not of the character classes
   in the value, and no amount of looking at the digits and separators reveals it.
3. **It is written in three places** — the brief, `STATUS.md` sprint 6 item 4, and a comment in
   shipped source that a future session will read as settled. `qa/strategy.md`'s own logic applies:
   a wrong mechanism recorded confidently is worse than none.

**Where this bites next:** `I18nService.formatDate` has exactly one caller in the whole SPA —
`audit-trail-page.ts` -> `timestamp` — and that value is rendered in exactly **two** places
(`bdi.row-when` on the row, and the `fact-value` timestamp in the detail panel). **Both carry
`dir="ltr"`.** So there is **no second live instance of this defect**. `formatNumber` /
`formatMoney` has **no call site yet**; I checked `Intl.NumberFormat('ar-EG')` directly and it emits
Arabic-Indic digits with **no** `U+200F`, so money is not pre-exposed. **Slice 3 should re-check that
the day it formats its first amount**, because the rule to apply is "did a formatter touch it", not
"does it look like digits".

**Recommendation:** keep every `dir="ltr"`. **Correct the comment and `STATUS.md` item 4** rather
than acting on it. **Do not spend sprint 6 capacity on the client and user lists.**

---

## 12. `F-1` re-run, with its own positive control — and what that class of evidence still cannot see

**`F-1` is fixed. Independently reproduced, including the builder's exact numbers.**

`TC-1-271` / `TC-1-272` executed as written — **enumerated from the DOM**, never from a list of
selectors — at 390px in Arabic with data on every screen. For each `<bdi>`: box width vs a `Range`
over its own text node.

| Screen | `<bdi>` swept | As shipped | With `.row-link > bdi { justify-self: start }` neutralised |
|---|---:|---|---|
| Client list `/clients` | 6 | **0 stretched — PASS** | **3 stretched — RED** |
| User list `/users` | 14 | **0 stretched — PASS** | **14 stretched — RED** |
| Audit trail `/audit` | 167 | **0 stretched — PASS** | **69 stretched — RED** |

**The control fires on all three**, and the first stretched client-list row measures
`boxW 308.0 / textW 85.4` — **the builder's reported numbers to the decimal**, reproduced by a
different session with a different harness. That is as good as a mutation gets on this board.

**The positive control is a runtime style injection, not a file edit** — no `src/` change, and it
sidesteps D-120 §3 entirely (`git checkout` reverts the file, not the mutation) because there is
nothing to revert.

### What this class of evidence still cannot see — the brief's question

1. **Direction and visual order.** §10 and §11 are the proof: a reordered run occupies the **same
   box**, so every geometry assertion above is green whether `07‏/09‏/2026` reads correctly or
   backwards. The geometry sweep and the direction check are disjoint, and only one of them is
   written down as a case.
2. **A zero-width `<bdi>`.** My sweep skips `box.width === 0`, and so must any box-comparison check —
   a zero box trivially satisfies *"no wider than its text"*. **An element that fails to render at
   all passes `TC-1-271` and `TC-1-272` silently**, and it also passes the overflow check. That is a
   real hole in the case's shape, not in the code: nothing today renders a zero-width `<bdi>`, but
   nothing would notice if it did.
3. **Correctness of the value.** Geometry cannot tell `01001234567` from `01001234576`. `TC-1-272`
   is a shape check, and the board should not read it as coverage of what the rows say.
4. **Vertical placement and overlap.** Both cases measure the inline axis only.

---

## 13. KAFF-101b — the sign-in screen, driven end to end for the first time

**Every criterion that can be executed today was executed, in a real browser, against the live
stack. Nothing failed.**

| Criterion | Result | Evidence |
|---|---|---|
| **`AC-101b-A`** — a staff sign-in arrives at the staff shell | **PASS** *(via KAFF-125)* | §9.4 — Finance signs in and lands on S-005 with `displayName`, `role`, `department` from `GET /api/auth/me` |
| **`AC-101b-B`** — a client never sees the staff shell | **PASS** | `portal_client_demo` / `Demo#Portal1` submitted at the staff form: stays on `/sign-in`, refusal message, **the shell never renders**, `localStorage`/`sessionStorage` both empty. The API half is `401 auth.invalid_credentials` (§3.1) |
| **`AC-101b-C`** — the portal is not discoverable | **PASS** | Page **plus both JS bundles fetched and searched — 305,024 bytes**. `portal`, `client-portal`, `clientportal`, `عميل؟`, `are you a client`: **all absent** |
| **`AC-101b-D`** — HR lands on the team surface | **PASS on its first clause; second clause vacuous** | §9.4 — `hend_hr_demo` lands on `المشاريع` / `[data-testid=hr-projects-empty]`. ⚠️ *"no project dashboard route is reachable … and requesting one directly is refused by the server"* — **there is no project route in the SPA and no `/api/projects` on the server** (`404`, §4). The clause cannot fail today. Noted, not scored against the story |
| **`AC-101b-E`** — only what was ruled | **PASS** | `input[type=password]` carries `minlength=8` and `required` and **nothing else**; `meter`/`progress`/`[class*=strength]` — **none present**; `abcdefgh` leaves the submit button **enabled** and `validity.valid = true` |
| **`AC-101b-F`** — a forced change cannot be walked around | **PASS** | A **freshly created** Finance user (`v35_forced`, `mustChangePassword: true`) hard-loaded `/`, `/clients`, `/users`, `/audit` and `/change-password`. **Every one lands on `/change-password`**, four repeat runs, each watched for 5s at 200ms intervals — `/change-password` was the **only** path observed in all 25 samples of every run |
| **`AC-101b-G`** — one refusal for three causes | **PASS, on five causes** | wrong password ×2, unknown username, a **deactivated** account (`POST /api/users/{id}/deactivate` → `204`, then sign-in), and a `Role.Client` credential: **all five render the byte-identical string** `اسم المستخدم أو كلمة المرور غير صحيحة.` and all five stay on `/sign-in` |
| **`AC-101b-H`** — Arabic, RTL, mobile width | **PASS** | `dir="rtl"`, `scrollWidth 390 / clientWidth 390` — no horizontal overflow at 390px, no literal observed |

**`AC-101b-G` is the one I most expected to break** and it is solid: I added a *fourth* and *fifth*
cause the criterion does not name (a deactivated account, and a portal credential with a wrong
password) and the message did not vary.

### 13.1 `V-35-J` — a readiness heuristic this project's own harness shares can observe a pre-guard frame

**LOW — methodology, and it produced a false result in this very session before I caught it.**

My first `AC-101b-F` run reported `asked /users -> landed /users`, which reads as a forced-password
user walking around the guard. **It is not true.** Four repeat runs with a 5-second observation
window show `/change-password` and nothing else, and `sara_finance_demo` (no forced change, no
`UserManage`) correctly lands on `/forbidden`.

**The cause is the readiness rule, and it is copied from `.claude/skills/run-kaff-erp/driver.mjs`:**

```js
// Angular is zoneless and the first route is lazy: wait for real content, not just load.
while (Date.now() < deadline) { if (await evaluate('document.body.innerText.trim().length > 0')) break; … }
```

**The chrome renders before the lazy route chunk and its guards resolve**, so
`innerText.length > 0` is satisfied by a frame that is *inside* the navigation, not after it. The
body text I captured was ~50 characters — header and sign-out and nothing else.

⚠️ **This cuts the dangerous way too.** It gave me a false **red**; the identical heuristic can give
a false **green** — asserting that chrome exists, or that a page has no overflow, on a frame the
guard is about to navigate away from. **This is the same family as D-114 §5 and the brief's hint 5**:
the check ran, the run was green, and what it observed was not the state under test.

**Recommendation, routed not applied:** any assertion about *which route a guard settled on* should
poll to a stable path rather than read `location` once after "content appeared". I have not looked at
whether `tests/E2E.Tests` uses Playwright's own auto-waiting (which does not share this flaw) — see
§16.

---

## 14. KAFF-118 — the whitelist holds, and the claim is true when driven rather than read

### 14.1 The whitelist test, assessed against D-116

`tests/Api.Tests/AuditCoverageTests.cs` -> `Every_entity_is_audited_unless_it_is_a_named_exemption`.

**The shape is sound, and better than the brief's warning implies:**

* It is an **exact-set** assertion — `exempt.Select(t => t.Name).Should().BeEquivalentTo([nameof(AuditRecord)])`.
  It is not *"these are not exempt"*; it is *"exactly this one is"*. **Any new `IAuditExempt`
  fails it**, whatever it is named and whatever slice it lands in. That is the right direction.
* It carries its **own anti-vacuity guard** — `…Where(IsAssignableTo(typeof(Entity))).Should()
  .HaveCountGreaterThan(5, "if the model stops being enumerable this test passes by describing
  nothing")`. The D-041 shape is explicitly defended against.
* **Both negative tests have positive controls in the same method**, which is what D-116 asks for:
  `Ten_reads_write_no_audit_record` ends by creating a real client and asserting the counter moved;
  `A_refused_write_writes_no_audit_record` does the same after both refusals. **The comments state
  why, correctly** — *"'the count did not change' is satisfied by a counter that cannot change."*

**The one thing it cannot see, stated rather than implied:** the assertion reads the *model*, not the
*interceptor*. If `AuditSaveChangesInterceptor` were unregistered from the `DbContext` options, the
exempt set would still be `[AuditRecord]` and this test would stay **green**. It is the two negative
tests' positive controls that would catch that — so the file as a whole is covered, but **not by the
test whose name makes the coverage claim**. Worth knowing before anyone splits the file.

⛔ **I did not mutate `src/` to prove the whitelist goes red.** My brief forbids changes under
`src/`, and adding `IAuditExempt` to an entity is a `src/` change. **The mutation that would settle
it is one word** — `: IAuditExempt` on `Client` — and it belongs to whoever next has a mandate to
edit that tree. Recorded as unproven rather than assumed, and see §14.2 for the evidence I could
gather without it.

### 14.2 `AC-118-A` … `AC-118-J`, driven against the live stack

Rather than read the suite, I drove **every mutating endpoint slice 1 has**, one at a time, and asked
after each: *did the trail grow, and does the new record describe what just happened?* The trail was
re-read between every step and compared by record `id`.

| Act | HTTP | Records written | What the record says |
|---|---|---|---|
| `POST /api/users` | `201` | **+1** | `Created` `User` |
| `PUT /api/users/{id}/department` | `204` | **+1** | `Modified` `User` `[Department]` |
| `PUT /api/users/{id}/role` | `200` | **+1** | `Modified` `User` `[Role]` |
| `POST /api/users/{id}/deactivate` | `204` | **+1** | `Modified` `User` `[DeactivatedAt, IsActive, SecurityStamp]`, **`reason` stored verbatim in Arabic** |
| `POST /api/users/{id}/reactivate` | `204` | **+1** | `Modified` `User` `[DeactivatedAt, IsActive, MustChangePassword, PasswordHash, SecurityStamp]` |
| `POST /api/clients` | `201` | **+1** | `Created` `Client` |
| `PUT /api/clients/{id}` | `200` | **+1** | `Modified` `Client` `[Name]`, **`before`/`after` both populated** |
| `POST /api/clients/{id}/archive` | `204` | **+1** | `Modified` `Client` `[IsActive]` |
| `POST /api/auth/sign-in` (wrong password) | `401` | **+2** | `Modified` `User` `[FailedSignInAttempts]` **and** `Occurred/SignInFailed` — **sharing one `correlationId`** |
| `POST /api/auth/sign-out` | `204` | **+1** | `Occurred/SignedOut` `User` |
| `GET /api/clients?status=all` | `200` | **0** | — |
| `GET /api/users` | `200` | **0** | — |
| `POST /api/clients/phone-check` | `200` | **0** | — |
| `POST /api/clients` blank name | `400` | **0** | — |

**Nothing is missing and nothing is spurious.**

* **`AC-118-A` and `AC-118-B` — PASS**, every identity and client act observed writing its own record.
* **`AC-118-E` — PASS, live**: the failed sign-in wrote an entity change and an event **together,
  under one `correlationId`** (`…bf0ee5`). That is the criterion, observed rather than asserted.
* **`AC-118-G` — PASS**: `reason="سبب الاختبار من جلسة التحقق"` stored verbatim, Arabic intact, no
  mojibake.
* **`AC-118-H` — PASS**: three distinct reads, **zero** records. My positive control is the table
  itself — the same session's writes moved the counter every time.
* **`AC-118-I` — PASS**: a domain refusal (`400`) wrote nothing, and §3.1's permission refusals
  (`403`) are visible nowhere in the trail either.
* **`AC-118-F` — PASS, and checked harder than the criterion asks.** Seven records carry
  `PasswordHash` / `SecurityStamp` in `changedProperties`; **every one renders `"[redacted]"` in both
  `before` and `after`**. I searched the entire 200-record payload for a bcrypt/argon/pbkdf2-shaped
  string: **none**. And `MustChangePassword: true → false` is visible *unredacted* alongside them,
  which is right — it is a fact, not a secret.
* **`AC-118-J`** — the *role* half is `V-35-C` (PASS). The *deactivated actor* half I did not drive
  end to end; §16.

### 14.3 `TC-1-303` — executed, and it **passes**

I opened detail panels until I found a record carrying a redacted value (the `reactivate` record).
The changes table renders:

```
الحقل              قبل                                    بعد
DeactivatedAt      2026-09-07T20:42:16.082494+00:00       لا قيمة
IsActive           false                                  true
MustChangePassword true                                   false
PasswordHash       [محجوب]                                [محجوب]
SecurityStamp      [محجوب]                                [محجوب]
```

**Four `[محجوب]` (`audit.value.redacted`) and one `لا قيمة` (`audit.value.none`) in the same panel,
and no empty cell.** That is exactly what `TC-1-303` asks for, and it is the strongest possible form
of it: **a redacted value and a genuinely-absent value side by side in one table**, rendering
differently. Neither reads as blank. `::1` renders in order beside them.

### 14.4 `AC-128-E`'s orphans — resolved by deletion, verified

KAFF-128 rule 8 said the four `audit.grant.*` keys must be *used or deleted, not left orphaned a
third time*. **`audit.grant.` appears nowhere in `ar.json` or `en.json`.** The only surviving match
on "grant" is `audit.field.grant` (`"الصلاحية"` / `"Authority"`), a different key, present in both
catalogues. **Rule 8 discharged.**

### 14.5 An observation the criteria do not cover: the changes table is in English

`الحقل` / `قبل` / `بعد` are translated; **the field names inside the table are raw CLR property
names** — `DeactivatedAt`, `IsActive`, `MustChangePassword`, `PasswordHash`, `SecurityStamp` — shown
untranslated to an Arabic-reading Owner.

**I am not scoring this against KAFF-128.** These arrive as data in `changedProperties`, so this is
not a hardcoded string in a template and rule 5 is not breached; and **no criterion, and no line of
`S-015` I can cite, says what that column should contain.** But `CLAUDE.md` says *"The UI is
Arabic"*, and this is the one place in slice 1 where a screen shows the reader an English identifier
from the domain model. **Routed to UX as a question, not reported as a defect** — it will only grow,
because slice 3's records will name money fields.

---

## 15. `V-35-K` — ⛔ **the E2E gate has never run in CI against a seeded database, and 17/25 have been red there since 2026-09-04**

**HIGH — the first HIGH of this pass. It is a gate the board reports as passing that does not pass
where it is supposed to run.**

Raised mid-pass by the coordinating session from a CI run Nabil surfaced. **I did not take it on
report — I reproduced it on this machine, exactly, and then established which tests survive and
why.**

### 15.1 My own E2E figures, all three of them

Run **CI-shaped**: the **production SPA build** served by `ci/serve-e2e.mjs` on **4173** proxying
`/api` to the API on 5080 — not the `ng serve` dev server on 4200.

| # | Stack | Result |
|---|---|---|
| 1 | Seeded `kaff_demo` **after my own §4/§5 probes** | **20 / 25** — 5 failed |
| 2 | `kaff_demo` **dropped, re-created, re-seeded** by `scripts/seed-demo.ps1` | **25 / 25 — 0 failed** |
| 3 | **Empty database**, migrations applied on start, no seed — **CI's exact condition** | ⛔ **8 / 25 — 17 failed** |

**Run 1's five failures were mine**, and I say so plainly: §4 cleared `sara_finance_demo`'s forced
password change to `Demo#Fin123New`, and the suite knows only the seed password `Demo#Fin123` and
its own `Demo#Fin456`. Every failure read
`sara_finance_demo answered neither password — is this stack seeded by scripts/seed-demo.ps1?`.
**Not a defect — but it is the same illness as run 3, in miniature: one session touching one
password in a shared database turned the gate red, and nothing but the gate noticed.**

**Run 3 reproduces the CI figure exactly — 17 failed, 8 succeeded** — from the summary the
coordinator relayed. Same numbers, same machine as everything else in this report.

### 15.2 `.github/workflows/ci.yml` has no seeding step, and has not been touched since 2026-08-25

Read, not assumed. The `e2e` job: creates `kaff_e2e`, builds the API and the suite, installs
chromium, downloads `web-dist`, starts the API with `ASPNETCORE_ENVIRONMENT=Staging` and
`Kaff__ApplyMigrationsOnStartup=true`, serves the SPA on 4173, runs the suite. **Migrations create
the schema. Nothing creates `owner_demo`, `sara_finance_demo` or `portal_client_demo`.**

```
git log --format='%h %ad %s' --date=short -- .github/workflows/ci.yml
  1923ae0  2026-08-25  CI: upload the browser subdirectory as the web artifact
  8e5c962  2026-08-24  Kaff ERP: slice 0 complete, slice 1 in progress
```

**Untouched since 2026-08-25.** Against that, the suite it runs:

| Suite | Landed | E2E facts | Needs seeded accounts |
|---|---|---:|---|
| `SmokeTests` | 2026-08-24 / repaired `ad92638` | 5 | **no** |
| `SuiteConfigurationTests` | 2026-08-24 | 1 | **no** |
| `ClientScreenTests` | **`b5c9e46`, 2026-09-04** | 7 | **6 of 7** |
| `UserScreenTests` | **`8ea9258`/`a4496a8`, 2026-09-05** | 5 | **5 of 5** |
| `BidiGeometryTests` | **`a1c93ce`, 2026-09-07** | 2 | **2 of 2** |
| `AuditScreenTests` | **`5dc1ebf`, 2026-09-07** | 5 | **4 of 5** |

⛔ **The E2E job has been red in CI since `b5c9e46` on 2026-09-04** — the first commit to add a
screen test that needs an account — **and the board has reported "E2E 25/25" throughout.** Every
25/25 ever recorded on this board, the Frontend agent's and **my own run 2**, is a *local* figure
taken against a database somebody seeded by hand out of band.

**`STATUS.md` marks the E2E row *"⚠️ Frontend agent's own figure"*. That warning is correct and
insufficient** — the problem is not who measured it, it is that the measurement cannot be
reproduced by the only thing that runs unattended.

### 15.3 Which 8 pass, and **one of them is green for the wrong reason**

Established by subtraction from the 17 named failures against each file's fact count — arithmetic,
not inference:

| Passing on an unseeded database | Why |
|---|---|
| `SmokeTests` ×5 | **honestly seed-independent** — health, guards installed, the app mounts, RTL, Arabic |
| `SuiteConfigurationTests` ×1 | **honestly seed-independent** — it asserts the suite is configured and fails rather than skips |
| `ClientScreenTests.A_signed_out_visitor_asking_for_a_client_form_is_sent_to_sign_in` | **honestly seed-independent** — anonymous, needs no account |
| ⛔ `AuditScreenTests.A_portal_client_holds_no_session_to_reach_the_trail_with` | **green for the wrong reason** |

**That last one is the finding inside the finding.** Its two assertions are:

```csharp
(await SignInToApiAsync(client, PortalClientUser, PortalClientSeedPassword))
    .Should().BeNull("a Role.Client may not hold a staff session (spec.md §12, D-065)");
(await client.GetAsync("/api/audit")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
```

**A sign-in for an account that does not exist also returns null** — that is D-065's own ruling,
which this project chose deliberately so an attacker cannot tell a bad password from a bad username.
And an anonymous `GET /api/audit` is `401` on any stack at all. **Neither assertion can distinguish
*"the portal boundary holds"* from *"there is no portal account."*** On an empty database it passes
having tested nothing.

**This is the canonical shape the brief names** — `body.Should().Contain("portal_client_demo")`
staying green when the account was renamed — **recurring, on the same subject, in the file written
to close it.** It is the exact instance `V-33-E` was raised about.

**How it could fail honestly:** assert first that the account *exists* — the sibling
`UserScreenTests.AssertPortalAccountExistsAsync` does exactly that, and **it is one of the 17 that
correctly goes red**. The assertion this test needs is already written, twelve lines away, in
another file.

### 15.4 Two things I could not establish

1. **Whether the `e2e` job is a required check or advisory.** That is branch-protection
   configuration, which does not live in the repository and which I cannot read from here. **It
   decides how bad this is:** if it is required, `main` should have been unmergeable since
   2026-09-04 and was not, which would mean it is advisory in practice whatever it says.
2. **Whether CI has *ever* been green on the E2E job.** The evidence is strongly one way — the job
   has never had a seed step and the first seed-dependent test landed 2026-09-04 — but confirming
   it needs the Actions run history, which I cannot reach.

**I did not fix it and did not add a seed step**, as instructed.

### 15.5 A related fragility, recorded while it is in view

**The E2E suite mutates the seeded state it depends on.** `EnsureCanSignInAsync` moves
`sara_finance_demo` to `Demo#Fin456` and `portal_client_demo` to `Demo#Portal2` on first run, then
tries the changed password first so a second run survives. That is a sound design given the
constraint — but it means **after any E2E run this machine's `kaff_demo` no longer matches
`deploy/DEMO.md` §4.4's password table.** Anyone reading that table after a suite run gets the
wrong password for Finance and concludes the stack is broken. Worth one line in `DEMO.md`; it is not
mine to add.

---

## 16. Closing gate, and the state I leave the machine in

| | |
|---|---|
| `git status --porcelain` | clean but for this report and its commits |
| `src/` changed | **none.** No file under `src/` was created, edited or deleted by me at any point |
| API | up on 5080, `Development`, against **`kaff_demo` dropped, re-created and re-seeded by `scripts/seed-demo.ps1`** — `{"status":"healthy","guardsInstalled":true,"missingGuards":[]}` |
| SPA | `ng serve` on **4200** — `200`. `ci/serve-e2e.mjs` also left on **4173** — `200` |
| `kaff_e2e_v35` | the scratch database of §15.1 run 3, **dropped** |
| `kaff` (dev) | **untouched.** `V-31-A`/`V-33-F` is the Architect's and I did not go near it |
| `web-dist/` | refreshed from the production build for §15's CI-shaped runs. Gitignored, not a tracked change |

⚠️ **`kaff_demo` is at `deploy/DEMO.md` §4.4's documented passwords right now** — I re-seeded after
the last E2E run, so §15.5's password drift is *not* currently in effect. The next E2E run will
reintroduce it.

**Probe accounts from §4/§5/§13/§14 (`v35_techoffice`, `v35_bidi`, `v35_forced`, `v35_a####`) and
the probe clients (`C-10003`, the edits and the archive) are all gone** with the database they lived
in. Every observation in this report was recorded before it was dropped.

---

## 17. Verdicts

**These are verdicts on the code as it stands at `0359b8d`. §4 acceptance is Nabil's and remains 0
of 127.**

### KAFF-128 — the audit trail screen · **CONDITIONAL**

**Everything I could execute passed, and the one thing I could not execute is the criterion's whole
point.**

* `AC-128-A` **PASS** — 167 `<bdi>` swept at 390px, none stretched, no sideways scroll, direction
  correct (§12), and the timestamps genuinely isolated for the right reason (§11).
* `AC-128-B` ⚠️ **PARTIAL — the builder's own word, and it is the correct word.** Server and screen
  both refuse TechnicalOffice, Finance, Hr, MarketingSales, the portal `Role.Client` and anonymous
  (§3.1, §4.1). **The *"even for their own projects"* clause is unproven and unprovable today**, and
  `TC-1-300` says in its own text that without the assignment it proves nothing. `V-35-B`.
* `AC-128-C` **PASS** (§4.1) · `AC-128-D` **PASS**, by DOM enumeration (§4.1) · `AC-128-E`
  **PASS**, orphans deleted from both catalogues (§14.4) · `AC-128-F` **PASS** — the tests exist and
  ran, **but see `V-35-K`: they have never run in CI.**
* `TC-1-303` **PASS** (§14.3) · `TC-1-304` **PASS**, driven (§5).

**CONDITIONAL on:** `AC-128-B`'s assignment half being attached, as an acceptance criterion, to
whatever story creates `POST /api/projects` — so the strictest permission in the system is not left
with its distinguishing clause untested when the blocker clears.

### KAFF-125 — the staff shell · **LAPSED — the lapse is confirmed, and re-verification is blocked on a criterion defect**

* **`V-34-H` confirmed on diffs** (§6): `landingFor()` changed behaviour for two of nine roles and
  lost a variant of its type after the verdict. **The `LAPSED` verdict is right and stands.**
* **I cannot lift it**, and the reason is not the code — **the code is right**. `AC-125-C`, as
  amended on 2026-09-07, **cannot be executed by anyone** until a `Project` can exist (`V-35-F`).
  A criterion that cannot pass cannot be re-verified against.
* `AC-125-A`, `AC-125-B`, `AC-125-D`, `AC-125-E`, `AC-125-F` and `AC-125-C`'s **empty-state clause**
  all hold on evidence in §9.4 and §13.
* Two rules still carry no criterion, and one of them the code **breaches on its face**
  (`V-35-G`, rule 6).

**To clear it:** split `AC-125-C` (§8), settle rule 6's reading with UX (§9.1), and re-verify. That
is a short path and none of it is a code change.

### KAFF-118 — every slice-1 change is audited · **PASS**

Every mutating endpoint slice 1 has was driven individually and **every one wrote exactly one
record**; every read and every refusal wrote **none**; the correlation-id, verbatim-reason and
redaction criteria were all observed live rather than read (§14.2). The whitelist test's shape is
sound and its negative tests carry real positive controls (§14.1).

**One reservation, stated rather than buried:** I did not mutate `src/` to watch the whitelist go
red, because this brief forbids it. The behavioural evidence in §14.2 is stronger than that mutation
would have been, but it is not the same assertion.

### KAFF-101b — the sign-in screen · **PASS**

All eight criteria driven in a real browser against the live stack, **the first time any independent
session has looked at it** (§13). Nothing failed. `AC-101b-G` holds on five causes rather than the
three it names. `AC-101b-D`'s second clause is vacuous today (no project route exists anywhere) and
is noted, not scored.

---

## 18. Answers to the four questions the brief asked me directly

1. **Is the `AC-125-C` "criterion defect, not a lapse" ruling right?** **Right on D-096, wrong on
   authority.** No commit changed the behaviour the old criterion asserted — I re-ran the evidence
   and it holds — so a second lapse would have been wrong. But the 2026-09-04 Verifier reserved this
   call to Nabil **in writing**, and the BA closed it three days later. Keep the text; ask Nabil to
   ratify it. `V-35-E`, §7.
2. **Is rule 6 unfalsifiable before slice 4?** **No.** Its *consequence* is; its *prohibition* is
   checkable today and the code breaches it on its face — `navPathFor` and `navLabelKeyFor` derive
   the nav item from `switch (role)`. `V-35-G`, §9.1.
3. **Is the builder's "the Technical Office half cannot be built" true?** **Half of it.** The
   account takes one request and `201`; the *assignment* genuinely cannot be made. `V-35-B`, §4.
4. **`TC-1-304`?** **Passes, as a driven fact** — I changed the role and the old records held.
   `V-35-C`, §5.

---

## 19. What I did not reach

**Named, so the next session does not assume otherwise.**

1. **`AC-128-B`'s assignment clause** — blocked, not skipped. §4.
2. **The whitelist mutation** (`: IAuditExempt` on an entity) — forbidden by this brief. §14.1.
3. **`AC-118-J`'s deactivated-actor half** — I drove the *role*-change half (§5) and deactivated an
   account (§13), but did not then re-read the trail to confirm the deactivated actor is still
   named. `ReactivateUserTests` asserts it; I did not observe it.
4. **Whether the CI `e2e` job is a required check, and whether it has ever been green** — both need
   data outside the repository. §15.4.
5. **`Kaff:ForwardedProxyHops=2` against real staging** — unwitnessed, as it was before. Known-open.
6. **Whether `tests/E2E.Tests` shares the readiness flaw of `V-35-J`** — I found the flaw in the
   `driver.mjs` heuristic and my copy of it. The E2E suite uses Playwright's `WaitForURLAsync`, which
   does *not* share it, but I did not audit every assertion in the suite for the pattern.
7. **HeadOfDesign as a profile role** — no such account exists and I did not create one; two of the
   four profile roles were driven. §9.4.
8. **`ux/navigation.md`'s stale `mustChangePassword` paragraph** — KAFF-125's story flags it as
   uncorrected since 2026-09-01. I confirmed the code matches **D-072 §2** and not that paragraph
   (§13, `AC-101b-F`), but I did not sweep `ux/` for other staleness.

---

## 20. Findings index

| ID | Severity | Subject |
|---|---|---|
| `V-35-K` | **HIGH** | **The E2E gate has never run in CI against a seeded database.** `ci.yml` has no seeding step and is untouched since 2026-08-25; the first seed-dependent screen test landed 2026-09-04. Reproduced locally: **8/25 on an empty database, identical to CI's 17 failed**. Every 25/25 ever reported, including my own, is a local figure against a hand-seeded database. **And one of the 8 survivors is green for the wrong reason** — `A_portal_client_holds_no_session_to_reach_the_trail_with` passes when the portal account does not exist, because a missing account is refused identically to a `Role.Client` (D-065). §15 |
| `V-35-L` | **MEDIUM** | **The citation gate is RED at `HEAD` and both sources report it green.** Measured 1183 / **2 broken** / 0 legacy, exit 1. Caused by `5dc1ebf` deleting the four `audit.grant.*` orphans — which `AC-128-E` rule 8 correctly required — breaking two citations in `verification-2026-08-23.md`. Not caused by this report. §2.1 |
| `V-35-B` | **MEDIUM** | **"The Technical Office half cannot be built" is half wrong.** `POST /api/users` creates a TechnicalOffice account and returns **`201`**; it signs in and is refused `GET /api/audit` with `403`. What genuinely cannot be built is the **assignment**, and that is the clause `AC-128-B` turns on — a real coverage hole on the strictest gate in the system. §4 |
| `V-35-E` | **MEDIUM** | **The `AC-125-C` ruling is right on D-096 and wrong on authority.** The 2026-09-04 Verifier reserved the call to Nabil in writing — *"only Nabil can write it … It should not close by a Verifier"* — and the BA closed it on 2026-09-07 with no record that Nabil was asked. Keep the text, ask for ratification. §7 |
| `V-35-F` | **MEDIUM** | **The rewritten `AC-125-C` cannot pass.** It requires *"two active project assignments at two different levels"* and no `Project` can exist (`POST /api/projects` → `404`). One `agents.md` §3c fault — a criterion that cannot fail — was swapped for the other. §8 |
| `V-35-G` | **MEDIUM** | **KAFF-125 rule 6 is falsifiable today and the code breaches it on its face.** `landingFor` is `switch (role)`, and `navPathFor` / `navLabelKeyFor` — the navigation itself — derive from it. The builder's defence is recorded only in a doc comment inside the file the rule governs, and does not cover those two functions. §9.1 |
| `V-35-H` | **MEDIUM** | **Sprint 6 item 4 is a non-defect and should not be built.** I registered a real `0100-123-4567` through `POST /api/users`: it renders **in order**, `computed=ltr`. Every `<bdi>` on both lists resolves LTR. `dir="auto"` falls back to **ltr**, not to the parent's RTL. §10 |
| `V-35-I` | **MEDIUM** | **The `dir="ltr"` fix is right; its recorded reason is wrong.** Removing it at runtime *does* reorder the shipped timestamps — but because `Intl.DateTimeFormat('ar-EG')` injects `U+200F RLM`, a **strong** RTL character, not because "first-strong finds nothing." The wrong rule over-predicts (§10) and under-predicts the real hazard: *any string a locale formatter touched*. Written in three places including shipped source. §11 |
| `V-35-J` | **LOW** | **A readiness heuristic in `driver.mjs` can observe a pre-guard frame.** `innerText.length > 0` is satisfied by the chrome before the lazy route and its guards resolve. It gave me a false **red** on `AC-101b-F`; the same shape gives a false **green**. D-114 §5's family. §13.1 |
| `V-35-D` | **INFO** | `V-34-H` **confirmed on diffs, not a file list.** One correction: **`b5c9e46` did not touch `landingFor()`** — two of the three named commits did. The verdict is unaffected. §6 |
| `V-35-A` | **INFO** | The `MSB3021` file-lock fires immediately and fixes this pass's order. **There is no `Kaff.sln`** — the solution is `KaffErp.sln`, and the brief and `STATUS.md` both name the wrong file. §1 |
| `V-35-C` | **PASS** | `TC-1-304` decided by driving the role change rather than reading the component. Six records held their old role on the API and on the screen. §5 |

**By severity: one HIGH, seven MEDIUM, one LOW, two INFO, one recorded PASS.**

**No defective shipped behaviour was found in any of the four stories.** Every business rule I
attacked held. **Nine of the twelve findings are about the project's records, its gates and its
criteria rather than its code** — which is the same distribution the last three passes reported, and
the reason the process sections of these reports keep growing.

---

## 21. Corrections to the brief

The brief asked to be corrected in writing. Five places.

1. ⛔ **"Citations 1183/0/0."** It is **1183 / 2 / 0, exit 1**, and was before I started. `V-35-L`.
2. ⛔ **"`0100-123-4567` reorders."** It does not. Neither does `::1`, a route or a GUID inside a
   bare `<bdi>`. The stated mechanism — first-strong falling back to the paragraph's RTL — is wrong;
   the fallback is LTR. `V-35-H`, `V-35-I`.
3. ⛔ **"`POST /api/projects` returns 404 so no assignment could be made *and* there is no such
   seeded account"** — offered as one reason the TO half cannot be built. **The account half is
   false**; it takes one request. `V-35-B`.
4. ⚠️ **"Use `dotnet build Kaff.sln`"** (also in `STATUS.md`). **No such file.** `KaffErp.sln`.
   `V-35-A`.
5. ⚠️ **"E2E 25/25 … nobody has re-run them."** I re-ran them and got 25/25 — **and that figure is
   worth less than it looks.** The same suite is 8/25 where it actually runs. `V-35-K`.

**Where the brief was right, and it matters:** it was right that `TC-1-304` had to be decided rather
than argued; right that `AC-128-B` had to be tested rather than accepted; right that the E2E suite
skipping silently was a risk worth naming (it does not skip — it fails, correctly — but the risk it
pointed at was real and larger); and right that a green-only case proves nothing, which §15.3 found
a live instance of.


