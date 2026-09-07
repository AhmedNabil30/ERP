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


