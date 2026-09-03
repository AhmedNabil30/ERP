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
| `V-32-A` | **HIGH** | A money field added to the staff `ProjectEntry` reaches the wire on `/api/auth/me` with the whole suite green — the anti-leak test is a seven-word blocklist, and `Amount`, `Total`, `Price`, `Rate`, `Hold`, `Retention` and `Advance` are not on it |
| `V-32-B` | **MEDIUM** | SM-33's rename left **nine** records citing a test that does not exist, and the DoD gate written as *absolute* cannot see them: the checker resolves an identifier by plain substring, so the marked-amendment note SM-33 itself mandates keeps every stale citation green forever |
| `V-32-C` | **LOW** | `scripts/check-citations.ps1` reads `*.md` only. SM-30's citations live in `.cs` comments in a bare, backtick-less form, so neither the broken nor the legacy pattern can ever match them — including two drifted `file:line` citations on the `UserRead` row |

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
