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
| 2 | The projection — what `/api/auth/me` leaks, and whether team size is live | **done** — §2 |
| 3 | `ProjectTeamRead`'s grants — mutated, watched red | **done** — §3 |
| 4 | SM-33's rename — paid correctly and completely? | **done** — §4 |
| 5 | KAFF-125's code-reviewed half, and `AC-125-B` attacked | **done** — §5 |
| 6 | `AC-125-C` — judging a deliberate deviation | **done** — §6 |
| 7 | The E2E repair — all six watched failing | **done** — §7 |
| 8 | The demo seed — no raw SQL, and the `POST /api/projects` 404 | **done** — §8 |
| 9 | The deleted status page — references and i18n keys | **done** — §9 |
| 10 | Verdicts per commit | pending |
| 11 | Closing gate — `git status`, `HEAD`, suites re-run | pending |
| 12 | What I did not do, as a count | pending |
| 13 | Fit to put in front of a client? | pending |
| 14 | The one thing Nabil should know | pending |
| 15 | For the cleanup that follows — a list, not a repair | pending |

### Findings index

| ID | Severity | Subject |
|---|---|---|
| `V-32-A` | **HIGH** | A money field added to the staff `ProjectEntry` reaches the wire on `/api/auth/me` with the whole suite green — the anti-leak test is a seven-word blocklist, and `Amount`, `Total`, `Price`, `Rate`, `Hold`, `Retention` and `Advance` are not on it |
| `V-32-B` | **MEDIUM** | SM-33's rename left **nine** records citing a test that does not exist, and the DoD gate written as *absolute* cannot see them: the checker resolves an identifier by plain substring, so the marked-amendment note SM-33 itself mandates keeps every stale citation green forever |
| `V-32-C` | **LOW** | `scripts/check-citations.ps1` reads `*.md` only. SM-30's citations live in `.cs` comments in a bare, backtick-less form, so neither the broken nor the legacy pattern can ever match them — including two drifted `file:line` citations on the `UserRead` row |
| `V-32-D` | **MEDIUM** | `AC-125-B`'s rule is unasserted: deleting the `await` the criterion rests on leaves the E2E suite **6/6**, because the only landing-route test drives a signed-out visitor, for whom the answer is the same either way. There are **zero** frontend unit tests |
| `V-32-E` | **LOW** | `ad92638` orphaned **20** i18n entries (10 keys × 2 catalogues), and D-105's stated reason for the deletion — that the catalogues carried no `status` key — is false; both carry fifteen |

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

---

## 2. The projection — `V-32-A`, **HIGH** · the control is a blocklist of seven words

The brief is right that *"the payload is the control, not just the permission"*, and right to name Q42's
standing example. The two types are exactly as D-103 describes them, and I checked that first:

| Type | Fields, read from source | Verdict |
|---|---|---|
| `ProjectEntry` | `ProjectId`, `Name`, `Code`, `AccessPath`, `Level`, `Permissions` | as claimed [Verified: 2026-09-04 @ `src/Api/Features/Auth/WhoAmI/Response.cs` -> `ProjectEntry`] |
| `TeamProjectEntry` | `Name`, `Code`, `TeamSize` — **no `ProjectId`** | as claimed [Verified: 2026-09-04 @ `src/Api/Features/Auth/WhoAmI/Response.cs` -> `TeamProjectEntry`] |

**Team size is live, and "never stored" is stronger than a claim here — it is an absence I measured.**
`TeamProjectsAsync` groups active `ProjectAssignment` rows and counts them per call
[Verified: 2026-09-04 @ `src/Api/Features/Auth/WhoAmI/Handler.cs` -> `TeamProjectsAsync`], and a
repo-wide search for `TeamSize` or `team_size` anywhere in `src/` **outside that one projection returns
nothing** — no entity property, no EF configuration, no column. There is no place for it to be stored.
CLAUDE.md's never-store-a-balance rule is honoured, and honoured structurally.

### `MUT-A` — the mutation, and it is the finding of this pass

`AC-105b-F`'s guarantee is carried by one reflection test
[Verified: 2026-09-04 @ `tests/Api.Tests/MeTests.cs` ->
`Hr_and_staff_project_entries_are_distinct_types_with_no_financial_field`]. For HR's type it is
airtight — `BeEquivalentTo(["Name", "Code", "TeamSize"])` fails on **any** added property, financial or
not. **For the staff type it is not a whitelist at all. It is a blocklist of seven words:**

```csharp
string[] forbidden = ["Value", "Cost", "Margin", "Balance", "Budget", "Status", "Client"];
```

So I added a money field whose name is not on that list. `decimal RetainedAmount` on `ProjectEntry`,
populated by `BuildEntry` with `123456.7890m`:

| Gate | Result with the money field present |
|---|---|
| `dotnet build -c Release`, `-warnaserror` | **0 warnings, 0 errors** |
| **Api suite** | **241 / 241 — all green** |
| Domain suite | unaffected |

**And it is on the wire, observed rather than inferred.** I forced the raw response body into a failure
message from the one staff test that reads it, and this is the Owner's actual payload:

```json
"projects":[{"projectId":"01a068f3-3a76-75d8-98ff-b5f5ceaaee19","name":"مشروع أ","code":"ME-PA-000021",
"accessPath":"OwnerGlobal","level":"Supervisor","permissions":["…","ProjectTeamRead"],
"retainedAmount":123456.7890}]
```

**`retainedAmount` is the single most sensitive number in this system.** CLAUDE.md: *"the hold only
grows… it releases once, in full, at handover."* A retained-hold figure per project is precisely what
spec.md §12 and D-051 keep off a payload, and it shipped on `/api/auth/me` — the one endpoint the
frontend trusts to say who it is talking to — against a completely green suite.

### Why the blocklist is the wrong shape, in one sentence

The words that are **not** on it include `Amount`, `Total`, `Price`, `Rate`, `Sum`, `Retention`,
`Hold`, `Advance`, `Extract` and `Paid` — **and several of those are spec.md §14's own mandated
vocabulary**, so the terminology CLAUDE.md requires is disproportionately the terminology the guard
cannot see. HR's half of the same test shows the correct shape already exists two lines above: an exact
field set, not a list of bad words.

### Two more places the same gap is open, checked so the finding is bounded rather than sampled

* **The raw-string sweep runs on HR's response only** [Verified: 2026-09-04 @
  `tests/Api.Tests/MeTests.cs` ->
  `Hr_gets_names_codes_and_team_sizes_including_an_unstaffed_project_and_nothing_financial`], and HR's
  response always has an empty `projects` array by rule 9. **It is structurally incapable of ever
  seeing a `ProjectEntry`**, whatever words are on its list.
* Its word list — `value, cost, margin, balance, budget, clientid, contractvalue` — has the same
  omission, so even pointed at the staff payload it would have passed `retainedAmount`.

**What I am not saying.** I am not saying `ProjectEntry` leaks anything **today** — it does not; the six
fields shipped are the six the story allows. The defect is that **nothing would tell you the day it
stops being true**, and D-103 names this exact risk in its own words — *"the projection, not the
permission, is what rule 8 leans on… nothing in the catalogue stops a `ProjectEntry` from growing a
financial field later"* — then relies on the test I just walked past. D-103 also says the test was
*"proved by construction, not exercised against a mutation."* **This is that mutation, and the
construction does not hold.**

**Severity HIGH**, on `agents.md` §3c's own rule: *"a scenario that passes whether or not the rule is
implemented is worse than no scenario, because it reports safety that does not exist."* For the staff
type, this one does. It is HIGH for what it fails to defend — the payload rule the whole story rests
on — not for a leak present at `HEAD`.

Mutation reverted; `git checkout` on all three files, tree clean.

---

## 3. `ProjectTeamRead`'s grants — four mutations, four correct reds

The row reads `ProjectScoped`, `[owner, hr]`, `TouchesMoney` left at its `false` default
[Verified: 2026-09-04 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectTeamRead`],
exactly as the brief and D-103 state. **The brief asked for the grants to be mutated rather than read,
so all four directions were driven:**

| # | Mutation | Domain suite | Which tests went red |
|---|---|---|---|
| `MUT-B1` | grants narrowed to `[owner]` | **109 / 111, 2 red** | `Owner_and_hr_alone_hold_ProjectTeamRead_and_it_touches_no_money`, `Hr_holds_no_permission_that_touches_money` |
| `MUT-B2` | grants widened to `[owner, hr, finance]` | **110 / 111, 1 red** | `Owner_and_hr_alone_hold_ProjectTeamRead_and_it_touches_no_money` |
| `MUT-B3` | scope flipped to `CompanyWide` | **110 / 111, 1 red** | `Owner_and_hr_alone_hold_ProjectTeamRead_and_it_touches_no_money` |
| `MUT-B4` | `TouchesMoney: true` | **108 / 111, 3 red** | the two above, plus `No_financial_permission_is_granted_to_a_bare_department` |

**All four caught, none silently.** `MUT-B1` reproduces D-103's own claimed pair exactly. `MUT-B3` and
`MUT-B4` were not driven by D-103 and are the ones I added: scope and the money flag are each pinned
independently, so the row cannot be quietly promoted to company-wide or quietly declared financial.

**SM-30 is genuinely paid, not merely cited.** The row's comment names
`Owner_and_hr_alone_hold_ProjectTeamRead_and_it_touches_no_money`, that name exists, and the test it
names really exercises the row rather than mentioning it — which is what `MUT-B1` through `MUT-B4`
establish and what SM-30's 2026-08-22 amendment says a citation alone cannot.

All mutations reverted; Domain back to 111/111.

---

## 4. `V-32-B`, **MEDIUM** · SM-33's rename — paid in the code, unpaid in the record, and the gate cannot see it

### What was paid, and it is more than D-103 claims for itself

`Hr_holds_exactly_three_permissions_and_none_touches_money` became
`Hr_holds_no_permission_that_touches_money` [Verified: 2026-09-04 @
`tests/Domain.Tests/CatalogueCompletenessTests.cs` -> `Hr_holds_no_permission_that_touches_money`].
The new name is a property, not a count — SM-33's cheaper half, correctly applied.

**And one thing I first read as a defect and was wrong about, recorded because accuracy runs both
ways.** `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` still contains the old string. It is
**correct**: it cites the *new* name as the live pin and names the old one explicitly as a marked
rename — *"renamed 2026-09-03, SM-33, from `Hr_holds_exactly_three_permissions_and_none_touches_money`"*
[Verified: 2026-09-04 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `ProjectAccessPolicy`].
That is SM-29's *"marked amendments, never silent edits"* done properly. Likewise
`PermissionCatalogue.cs`'s note that the `ProjectManage` row once cited
`Opening_a_project_needs_no_project` is a **historical record of the 2026-08-22 defect**, not a live
citation — I checked before reporting it, and it is not a finding.

### What was not paid — nine records, and the number is measured

SM-33's text is not discretionary: *"the Scrum Master moves the ones in `meetings/`, `qa/` and
`proposals/` … **and does so in the same commit or the rename does not land**."* The rename landed.
Nine citations did not move. **`MUT-C` puts the count beyond argument** — I removed the single
doc-comment mention of the old name in `CatalogueCompletenessTests.cs` and re-ran the sweep unchanged:

```
SM-31 identifier citations checked: 1110
  broken (identifier absent):        9        <-- was 0
```

| File | Lines |
|---|---|
| `decisions.md` | 2440, 3052 |
| `meetings/2026-09-01-sprint-2-refinement.md` | 338 |
| `process/agile.md` | 383, 420 |
| `proposals/N10-project-creation.md` | 289 |
| `qa/questions.md` | 755 |
| `stories/slice-1-foundation/KAFF-105b-api-me-project-list.md` | 59 |
| `stories/slice-1-foundation/KAFF-107-hr-role-is-bound-to-the-hr-department.md` | 50 |

Reverted; the sweep is back to **1110 / 0 / 0**.

### The part that matters more than the nine — the gate is structurally blind to this class

The Definition of Done says: *"`scripts/check-citations.ps1` reports `broken (identifier absent)` = 0 —
repo-wide, **and this one is absolute**."* It reports 0. **It reports 0 because of how it resolves a
name**, which is a plain substring search over the candidate file
[Verified: 2026-09-04 @ `scripts/check-citations.ps1` -> `identPattern`]:

```powershell
if (Select-String -LiteralPath $candidate -Pattern ([regex]::Escape($ident)) -Quiet) { $found = $true }
```

There is no check that the identifier is *declared* — only that the characters appear somewhere in the
file. **So a renamed test whose old name survives in a comment resolves forever.** And SM-33 *requires*
that comment: a marked amendment is the practice the rule mandates.

**The two rules therefore fight each other.** SM-33 says leave a marked note; SM-31's checker treats that
note as proof the identifier still exists; and the one mechanism the DoD calls *absolute* is the one
that can never detect an SM-33 violation. **A single doc comment held nine dead citations green.**
This is `V-30-A`'s forged-marker shape one level up: not a check that was wrong, but a check that was
looking at the wrong thing and could not tell.

**Not D-103's fault alone, and the routing matters.** D-103 flagged the unmoved citations honestly, by
name, in its own "not done" list — and D-104 and D-105 flagged them again. Three sessions declared it.
What is missing is the Scrum Master's half of SM-33, and the rule as written says the rename should not
have landed without it. **That is a process call for Nabil and the Scrum Master, not mine.**

### `V-32-C`, **LOW** · and the checker never opens a `.cs` file

`$docs` is `Get-ChildItem -Recurse -Filter *.md` [Verified: 2026-09-04 @
`scripts/check-citations.ps1` -> `docs`]. Source files populate `$sources` — the things citations are
resolved *against* — but are never searched *for* citations. **"Repo-wide" is Markdown-wide.**

Re-running the identical patterns over the 230 `.cs`/`.ts`/`.html`/`.sql`/`.ps1` files finds only **2**
citations, and both are the checker's own format examples in its own comments — because source comments
do not use the backticked form at all. They use a bare form, and the `UserRead` row is the specimen
[Verified: 2026-09-04 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserRead`]:

```
// SM-30: pinned by Hr_may_read_the_user_list_and_still_reaches_nothing_financial
// [Verified: 2026-08-22 @ tests/Domain.Tests/PermissionEvaluatorTests.cs:290] and by
// Hr_holds_exactly_three_permissions_and_none_touches_money
// [Verified: 2026-08-22 @ tests/Domain.Tests/CatalogueCompletenessTests.cs:160].
```

**Three defects in four lines, all invisible to every gate:**

1. `Hr_holds_exactly_three_permissions_and_none_touches_money` **does not exist** — a live SM-30
   citation on a catalogue row naming a test that was renamed out from under it. This is the tenth
   instance of `V-32-B`, and the only one in source.
2. Both `file:line` citations are the **legacy form SM-31 retired**, and both have already drifted:
   `CatalogueCompletenessTests.cs:160` now lands on a doc-comment line and
   `PermissionEvaluatorTests.cs:290` on a bare `[Fact]` attribute — the real declarations are at 169
   and 291. SM-31's stated failure mode, observed rather than argued.
3. The legacy pattern requires backticks around the path, so even in a `.md` file this bare form would
   slip through.

`LOW` rather than `MEDIUM` only because the two named tests do both still exist under *some* name and
still genuinely pin the `UserRead` row. The mechanism, not the row, is what is wrong.

---

## 5. `V-32-D`, **MEDIUM** · `AC-125-B` — the guard is right, and nothing whatever asserts it

The brief's instinct is exactly right: KAFF-125's report separating **observed** from **code-reviewed**
is good practice, and it makes the code-reviewed half the unexamined half. `AC-125-B` is the one it
could not observe. So it is the one I attacked.

### The code is correct

`sessionGuard` awaits resolution before deciding [Verified: 2026-09-04 @
`src/Web/src/app/core/auth/session.guard.ts` -> `sessionGuard`], and `SessionResolver.ensureResolved`
shares one in-flight promise across `App`'s boot call and every guard
[Verified: 2026-09-04 @ `src/Web/src/app/core/auth/session-resolver.ts` -> `ensureResolved`]. Read as
source, `AC-125-B` holds and D-104's account of it is accurate.

### `MUT-D` — and the assertion behind it does not exist

I removed the single line the whole criterion rests on — `await resolver.ensureResolved();` — leaving
the guard to decide against an unresolved session, which is precisely the race `AC-125-B` forbids.
The dev server rebuilt (*"Changes detected. Rebuilding… Application bundle generation complete"*,
confirmed in its log before the run, so this is not a stale-bundle result):

```
Test run summary: Passed!   total: 6   failed: 0   succeeded: 6
```

**Six of six, green, with the rule deleted.**

**Why the new landing-route test cannot catch it, structurally.**
`An_unauthenticated_visit_to_the_landing_route_is_sent_to_sign_in` drives a **signed-out** visitor.
For a signed-out visitor the guard's answer is `/sign-in` **whether or not it awaits** — unresolved and
resolved both yield "not authenticated". `AC-125-B` is a statement about a **signed-in** user with a
deep link, and **the suite contains no signed-in browser test at all**. The one test added for the
landing route asserts the single case that is invariant to the rule.

That is `agents.md` §3c's own prohibition — *"a scenario that passes whether or not the rule is
implemented"* — and D-105's claim that *"none of the four repaired/added tests passes independent of
the property it names"* is **true for the property that test names (the redirect) and not true for
`AC-125-B`**, which its own comment invokes by name. That conflation is the finding.

### And the reason nothing else covers it: there are no frontend tests

```
find src/Web/src -name "*.spec.ts"   →   0 files
```

**The Angular application has no unit tests of any kind.** `sessionGuard`, `SessionResolver`,
`landingFor`, `enum-keys.ts`'s `assertNever` exhaustiveness, `AuthService.reset()` versus `clear()` —
none is covered by anything except what six Playwright tests happen to touch through a browser. This
is not a KAFF-125 defect and I am not scoring it as one; it is the context that makes `V-32-D`
unfixable-by-accident, and it is the single largest untested surface in the repository.

**What I did not do:** I did not drive the user-visible regression itself — signing a browser in, then
deep-linking to `/` and watching the bounce — because the driver launches a fresh chromium per command
and does not carry a cookie between them. The finding is *"the rule is unasserted"*, which `MUT-D`
establishes directly; the regression's user-visible shape is read from source, and I say so rather
than implying I watched it. Counted in §12.

---

## 6. `AC-125-C` — the deviation is defensible, and it was not KAFF-125's to make

**This is Nabil's criterion and Nabil's call. I am not resolving it, and nothing below should be read
as resolved.** What follows is the evidence he needs to make it in one sitting.

### D-104's factual claim is true — I checked the file rather than the entry

D-104 justifies the deviation on `ux/screen-inventory.md`'s S-005. That row reads, verbatim:

> **S-005 · My profile · all with a login ·** Own name, phone, role, department, **and the projects I
> am assigned to with my level.** Read-only except password.

[Verified: 2026-09-04 @ `ux/screen-inventory.md` -> S-005]. **So S-005 has always required the
projects, and D-104's claim is accurate.** With KAFF-105b shipped, `ux/screen-inventory.md` and
`AC-125-C` now require opposite things, and no implementation can satisfy both.

### What actually renders

The profile landing renders a `profile.projects.title` ("مشاريعي") section, listing `session.projects`
or an empty-state line [Verified: 2026-09-04 @ `src/Web/src/app/features/landing/landing-page.html`].
So `AC-125-C`'s *"no project or assignment is shown"* is not met — the **section** appears even when
the list is empty.

### The half of the criterion D-104 did not engage with, and it is the load-bearing half

`AC-125-C`'s full text is:

> And no project or assignment is shown, because `/api/auth/me` carries neither today — that is
> KAFF-105b's field, not yet built, **and this criterion is not rewritten the day it ships**

D-104 argues that the *reason* ("because … carries neither today") expired. **That is correct and it is
not the whole sentence.** The clause after the dash is the author writing down, in advance, what should
happen on exactly the day that arrived — and D-104's entry never quotes it, never weighs it, and
answers only the half that supports its conclusion.

**It genuinely reads two ways**, which is why it is not mine to settle:

* **"do not edit this criterion's text when KAFF-105b lands"** — document hygiene. D-104 complied
  exactly: the story text is untouched and the deviation is recorded in `decisions.md`.
* **"this requirement stands after KAFF-105b lands"** — a substantive hold. D-104 breached it.

### My judgement, as asked

**On substance the deviation is probably right.** S-005 is the older and more durable statement of what
the screen *is*; a criterion whose stated reason has expired is weak ground for shipping a screen that
contradicts its own inventory row; and hiding a field the payload now carries would make S-005 wrong on
the day it was finally satisfiable.

**On process it was not KAFF-125's call.** `CLAUDE.md` is unambiguous — *"If `spec.md` doesn't answer a
business question, stop and ask. Do not decide. … An invented rule is always plausible, which is why it
survives review."* Two Nabil-owned documents in direct contradiction is precisely a stop-and-ask, and
`AC-125-C` is marked *(fails if the rule is broken)* — a criterion QA deliberately made falsifiable is
now deliberately unmet, and **no test asserts either reading**, so the disagreement leaves no trace in
any suite.

**What D-104 got right, and it is most of what matters.** It did not patch the criterion, did not
silently comply, and recorded the deviation in the open with its reasoning and an explicit invitation to
reconcile. That is the behaviour the process wants when an agent decides something it should have
asked about. The residue is one message that was never sent.

**One line closes this**, and only Nabil can write it: either `AC-125-C` is amended and S-005 stands, or
the criterion holds and the projects section comes off the profile landing. **It should not close by a
Verifier, and it should not stay open into slice 4** — S-005 is a screen with a real user.

---

## 7. The E2E repair — all six watched failing, and one limit worth knowing

Baseline against the live stack (API on 5080 against `kaff_demo`, SPA on 4200): **6 total, 0 failed,
6 succeeded.** D-105's headline figure reproduces exactly.

**The brief asked that each of the six be broken and watched go red. All six were.**

| Test | Mutation | Result |
|---|---|---|
| `The_application_opens_in_arabic_right_to_left` | `DIRECTION.ar` set to `'ltr'` | **RED** |
| `The_shell_renders_its_arabic_title_from_the_translation_catalogue` | `app.name` changed in `ar.json` | **RED** |
| `An_unauthenticated_visit_to_the_landing_route_is_sent_to_sign_in` | `sessionGuard` returns `true` unconditionally | **RED** |
| `The_health_endpoint_reports_the_database_guards_are_installed` | `KAFF_API` pointed at a stub returning the real degraded body | **RED** — and the message names the missing guard verbatim |
| `The_page_does_not_scroll_sideways_at_phone_width` | `.app-title { min-width: 1400px }` | **RED** |
| `The_suite_is_configured_when_running_in_CI` | `CI=true`, `KAFF_E2E_BASE_URL` unset | **RED** — 5 skipped, this one failed |

**None of the six is a scenario that cannot fail.** The repair is sound, and the health test in
particular is better than the screen assertion it replaced: its failure message carries the real
`missingGuards` array rather than a boolean, exactly as D-105 claims.

**Two qualifications, neither of which changes the verdict.**

**(a) The scroll test's reach is narrower than its own comment.** Its comment says a horizontal
scrollbar *"is the usual symptom of a physical CSS property that should have been logical"*. I first
mutated it that way — `.app-title { margin-left: 900px }`, a physical property under RTL — and the test
**stayed green**; `.app-header` carries `flex-wrap: wrap`
[Verified: 2026-09-04 @ `src/Web/src/app/app.css` -> `.app-header`], which absorbs the stray margin
without growing `scrollWidth`. It took a forced `min-width` to turn it red. **The test detects overflow;
it does not reliably detect the physical-property mistake its comment claims it catches.** Worth
knowing before it is relied on as the RTL regression net.

**(b) The RTL discipline itself is clean at `HEAD`, checked independently** — a sweep for
`margin/padding/border-left|right` and bare `left:`/`right:` across every `.css` file under
`src/Web/src` returns **nothing**. `AC-125-F`'s logical-property rule holds today by construction, not
by the test.

**And the deleted status page (§9) really was the right call for the reason D-105 gives second, not the
reason it gives first** — see below.

---

## 8. The demo seed — both claims true, and the gap is larger than "no project endpoint"

### Claim 1 — seeded through real endpoints, no raw SQL. **True.**

`scripts/seed-demo.ps1` contains no `INSERT`, `UPDATE`, `DELETE`, `SELECT`, no `psql`, no `DbContext`
and no `Npgsql` [Verified: 2026-09-04 @ `scripts/seed-demo.ps1` -> `Post-Json`]. Every write is an HTTP
call: `POST /api/setup`, `POST /api/auth/sign-in`, three `POST /api/users`, then the
`POST /api/projects` probe.

**Better than claimed, and worth naming:** the script *fails loudly if its own premise expires* —

> `Write-Warning 'POST /api/projects did not 404 — a project-creation endpoint may have shipped since
> deploy/DEMO.md was written.'`

A seed script that notices when the documentation around it has gone stale is the opposite of the
failure mode this repository keeps finding.

### Claim 2 — `POST /api/projects` returns 404. **True, and I drove it as the most privileged caller.**

Signed in as `owner_demo` — the Owner, global reach, holding `ProjectCreate` — against the running API:

| Request | Result |
|---|---|
| `POST /api/projects` | **404 Not Found** |
| `GET /api/projects` | **404 Not Found** |
| `PUT /api/projects` | **404 Not Found** |

**404, not 403** — so this is a route that does not exist, not a permission refusal wearing a 404. The
route table confirms it. Five feature folders (`Health`, `Setup`, `Auth`, `Users`, `Assignments`) and
**thirteen routes in the entire API**:

```
GET  /api/health          POST /api/setup            POST /api/users
POST /api/auth/sign-in    POST /api/auth/sign-out    GET  /api/auth/me
POST /api/auth/change-password
POST /api/users/{userId}/role · /department · /deactivate · /reactivate
POST /api/projects/{projectId:guid}/assignments
POST /api/projects/{projectId:guid}/assignments/{assignmentId:guid}/revoke
```

### The gap is bigger than the brief states, and this is the correction Nabil most needs

The brief calls the missing project-creation endpoint *"the largest claimed gap in the system"*.
**It is one of two, and the second is not mentioned anywhere in the brief.**

**There is no client-creation endpoint either.** `Kaff.Domain.MasterData.Client` exists as a complete
domain type; `src/Api/Features/` has **no `Clients` folder and no route mentioning a client**. And
`Project.Create` requires a `ClientId`. So the dependency chain is:

> **no client endpoint → no client row → no project row → no assignment, no team size, no
> unstaffed-site indicator, no per-project permission list.**

Building `POST /api/projects` alone would **not** unblock the demo. It needs `POST /api/clients` first.
D-105 saw this — *"a fabricated project would need a fabricated client under it"* — but framed it as an
argument against raw SQL rather than as the second missing endpoint, and the brief inherited the
single-gap framing.

**`agents.md`'s slice table names slice 1 as "Foundation: auth, roles, assignment, audit, **Client
master**".** The Client master has no API surface. Sprint 2 is closing with a slice-1 deliverable
unbuilt — that is a scope fact for Nabil, not a defect in any of these four commits.

### The demo database, measured

`kaff_demo`: **4 users, 0 projects, 0 clients, 0 assignments, 14 accounts, 0 postings.** The 14 are
`AccountTreeSeeder`'s company accounts. **All four documented credentials work exactly as
`deploy/DEMO.md` §4.4 records them**, driven through the real endpoints:

| Account | Sign-in | `/api/auth/me` |
|---|---|---|
| `owner_demo` | **204** | `Owner`, `mustChangePassword: false`, 0 projects |
| `hend_hr_demo` | **204** | `Hr`, `mustChangePassword: true`, 0 teamProjects |
| `sara_finance_demo` | **204** | `Finance`, `mustChangePassword: true`, 0 projects |
| `karim_sales_demo` | **204** | `MarketingSales`, `mustChangePassword: true`, 0 projects |

**The consequence for the demo, stated plainly: every sprint-2 capability is invisible in it.**
KAFF-105b's per-project payload — the projection, the access path, the level, the per-project
permissions, HR's team sizes — renders as **two empty-state sentences**, because there is no project
for any of it to describe. The demo faithfully shows the shell, the sign-in flow, the forced password
change, the RTL layout and four honest empty states. It shows **none** of what these four commits
actually built.

---

## 9. `V-32-E`, **LOW** · the deleted status page — nothing references it, twenty i18n entries are now dangling, and D-105's stated reason is false

### References — clean

Repo-wide, the only surviving mention of `status-page` / `StatusPage` / `status-panel` /
`status-guards` is a historical comment in `SmokeTests.cs` explaining why they went
[Verified: 2026-09-04 @ `tests/E2E.Tests/SmokeTests.cs` -> `SmokeTests`]. Nothing imports it, nothing
routes to it. **The deletion is complete and correct.**

### i18n — D-105's reason for deleting it is factually wrong

D-105 §2 states, as its lead evidence:

> *"`status-page.ts`'s own template already referenced `status.*` i18n keys that were **not in either
> catalogue any more** [checked: `src/Web/public/locales/{ar,en}.json` have no `status` key]"*

**Both catalogues carry a `status` block, and they always did.** Fifteen keys in each, 178 keys per
catalogue, ar and en in exact agreement (0 keys on either side alone). And ten of the fifteen are
precisely the deleted template's keys — I read them out of the deleted file to be sure:

```
status.title · status.loading · status.refresh · status.api · status.database
status.guards · status.reachable · status.unreachable · status.guards.installed · status.guards.missing
```

So the component would **not** have rendered raw keys; it would have rendered correctly. **The evidence
D-105 gives for "it had already started rotting" is false.**

**The deletion is still right** — for the reason D-105 gives *second*, which needs no i18n claim at all:
nothing routed to it, `ux/` asks for no such screen, and `driver.mjs smoke` plus `/api/health` already
prove the chain it existed to prove. **Right call, wrong evidence** — which matters because that
sentence is the one a future session will quote.

### The dangling keys — the answer to the brief's question is *yes, twenty*

`ad92638` deleted the only consumer of those ten keys, in both catalogues: **20 orphaned entries.**
Verified by searching every `.ts` and `.html` under `src/Web/src` for each key — all ten are referenced
nowhere.

**Two of the fifteen groups must be treated differently, so the cleanup does not overreach:**

* The five `status.kaff.*` keys — لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها — are **not
  orphans of this commit.** They were added in slice 0, have never had a consumer, and `ar.json`'s own
  `_note` reserves them: *"Kaff's status vocabulary under `status.kaff.*` appears verbatim per
  CLAUDE.md and must not be translated, paraphrased or substituted."* They are pre-staged for the
  project screens. **Leave them.**
* `status.loading` is an exact duplicate of the live `shell.boot.loading` ("جارٍ التحميل…"), which is
  what the boot surface actually renders. Only the orphan should go.

### Why no gate caught this

`TranslationCatalogueTests` checks three things — every domain error key has both translations, the two
catalogues describe the same key set, and no translation is empty
[Verified: 2026-09-04 @ `tests/Domain.Tests/TranslationCatalogueTests.cs` ->
`The_two_catalogues_describe_the_same_set_of_keys`]. **All three run key→translation. None runs
key→consumer**, so a catalogue entry nobody renders is invisible to the suite, and deleting a component
can never turn anything red. That is why 20 dangling entries sit inside a green 111/111.

`LOW`: dead data, no runtime effect, and both catalogues stay structurally valid. Listed for the
cleanup in §15, not for a fix here.
