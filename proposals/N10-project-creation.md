# N10 · A permission that can authorise *opening* a project

**Status:** ✅ **APPROVED — Nabil with the Architect, 2026-08-22. `decisions.md` D-055 §3.**
**Raised by:** D-052 §2, QA finding F-27, SM-24 · **Blocks:** KAFF-407, KAFF-416 (slice 4)
**Affects slice 1:** no — see §4.4 · **Written:** 2026-08-22, against the code as it stands after D-053.

> ### What was approved, and the one thing that changed
>
> **Design A, as written.** `ProjectCreate` — company-wide, Owner and Technical Office. `ProjectManage`
> — unchanged: project-scoped, same grants, documentation narrowed to editing. §4's migration path is
> the work order.
>
> **Changed: it is built now, not in slice 4.** Nabil: *"Split the permission immediately."* The
> reason is not urgency but the 🟡 `SCOPE IS UNRESOLVED` comment sitting on the `ProjectManage`
> catalogue row — an answered question left standing as an open one is a claim a reader trusts, which
> is the D-035 failure mode this repository has recorded four times. The comment is rewritten, not left.
>
> **§7's questions are still open and are not approved with the design.** Q-N10-1 and Q-N10-3 remain
> for Karim, carried in `decisions.md` D-055 §8.
>
> **Q-N10-2 is CLOSED — and not the way §7 offers.** §7 named three ways out: Finance joins
> `ProjectManage`, the withholding category gets its own permission, or field-level authority is
> expressed some third way. **The ruling takes the second, and rules out the first by name:** *"The
> Finance department will never hold `ProjectManage`. An accountant must not alter the engineering
> scope of a project."* The new row is **`ProjectFinancialsEdit`**, held by Finance and the Owner. See
> `decisions.md` D-055 §1 — including the 🟡 it raises, that Finance has no global reach and so cannot
> set a new contract's rate until somebody assigns Finance to that project.
>
> **§6's finding stands:** `ProjectAssignmentManage` is *not* the same defect. Global reach already
> solves it, and reach is unavailable to N10 because there is nothing yet to reach.

---

## 1. The problem

Karim ruled on 2026-08-21 (`decisions.md` D-052 §2, `spec.md` §2 amendment) that **only the Owner and
the Technical Office may open a project.** `ProjectManage` was given those two holders. The permission
cannot authorise the act he ruled on.

### The evidence, verified against the current files

**(a) The row is project-scoped.**

`PermissionCatalogue.cs` -> `Permission.ProjectManage`

```csharp
new(Permission.ProjectManage, PermissionScope.ProjectScoped,
    [owner, technicalOffice],
    "§2 — holder ruled by Karim 2026-08-21; SCOPE still unresolved, see decisions.md D-052"),
```

**(b) A project-scoped permission with no project id is refused, before any assignment is consulted.**

`PermissionEvaluator.cs` -> `ProjectNotSpecified`

```csharp
if (definition.Scope == PermissionScope.CompanyWide)
{
    return PermissionDecision.Granted;
}

if (projectId is null)
{
    return PermissionDecision.ProjectNotSpecified;
}
```

**(c) A create request cannot supply a project id, and the API deliberately will not read one from
the body.** `src/Api/Authorization/ProjectScope.cs` offers `Route` and `Query` only, and says why:
*"The body is deliberately excluded: a body has to be buffered and parsed before authorization can
run, which means an unauthorised request would be read and deserialised before it was refused."*
`PermissionAuthorizationHandler.ResolveProjectId` (`PermissionAuthorizationHandler.cs` -> `ResolveProjectId`)
returns `null` for `ProjectScopeSource.None` (line 129-132), and returns `null` for `Route`/`Query`
when the value is absent or unparseable (line 145). `POST /api/projects` has no id in either place, because the project does not
exist yet.

**(d) There is a second, independent refusal behind the first.** Even if (b) were bypassed, the
handler would call `IProjectAccessPolicy`, and every branch of
`src/Infrastructure/Authorization/ProjectAccessPolicy.cs` is bounded by the project existing:
`GlobalReachAsync` (lines 78-89) refuses when `Projects.AnyAsync(...)` is false — so the **Owner** is
refused too — and `AssignedAccessAsync` (lines 109-125) can find no assignment row for a project that
has no rows. A create is unauthorisable on two counts, not one.

**Net effect:** `ProjectManage` authorises *editing* a project (holder is assigned, or has global
reach, and the project exists) and can never authorise *opening* one. The half Karim ruled on is the
half that does not work. `Permission.cs` lines 36-37's summary — *"Create and edit a project"* — is
therefore the one line in the enum that is actively false, not merely incomplete.

### The constraint that makes this non-trivial

`spec.md` §9: *"A user MUST be assigned to a project to open it or act on it. Role alone is
insufficient."* Whatever fixes creation must not weaken that sentence for the projects already
running. Widening `ProjectManage` to company-wide fixes the create path by removing the assignment
requirement from **every** use of the permission, including edit — one line of code, and §9 quietly
stops applying to project editing for the life of the system.

---

## 2. Candidate designs

### A · A separate company-wide `ProjectCreate`, `ProjectManage` unchanged

Add `Permission.ProjectCreate`, `PermissionScope.CompanyWide`, granted to the Owner and the Technical
Office — the exact holders D-052 §2 named. `ProjectManage` keeps its scope, its grants and its
holders, and its summary narrows to *"edit an existing project"*.

**Cost.**
- One enum member and one catalogue row. No change to the evaluator, the access policy, the handler,
  the endpoint conventions, or the database.
- Two rows whose grant lists happen to be identical today. Somebody must keep them in step if a
  ruling changes one of them — although a ruling that changes only one is exactly the evidence that
  the split was real.
- Two names to learn where the codebase previously had one, and ten documents that say
  "`ProjectManage`" when they mean creating get one line each.
- **The permission model gains a company-wide capability held by a non-Owner role.** Today only the
  Owner (`UserManage`) and Finance (`TreasuryPostCompany`, `PeriodClose`) hold anything company-wide.
  Technical Office joins them. That is what Karim ruled, but it is worth stating plainly rather than
  arriving at by accident.

### B · Make `ProjectManage` company-wide, and put the assignment check somewhere else

One-word change in the catalogue; the create path then works. The assignment requirement for editing
moves into the edit handler as a hand-written check, or into a new project-scoped `ProjectEdit`.

**Cost.**
- If the check moves **into the handler**: §9's second factor stops being enforced by the one place
  that enforces it and becomes per-feature code that each edit endpoint must remember. `CLAUDE.md`
  and `PermissionAuthorizationHandler`'s own remarks say the rule lives in Domain *"so it can be
  tested exhaustively without an HTTP context and cannot be quietly amended by an endpoint"*. This
  amends it by endpoint, by design.
- If it moves **into a new `ProjectEdit`**: that is design A with the names exchanged, and worse in
  one specific way — the *broader* permission inherits the name that ten existing documents, one
  domain test and QA's permission matrix all use for the narrower one. Every one of those references
  silently changes meaning in the dangerous direction (wider than the reader thinks). D-048's
  postscript is the precedent: the data was right and the sentence was stale, *"the more dangerous
  way round, because a reader trusts the sentence and does not check the table."*
- Either variant produces a window in which any endpoint declaring `ProjectManage` loses its
  assignment check with no diff at the endpoint. There are no such endpoints today, which makes this
  cheap to do now and invisible when it bites later.

### C · A third `PermissionScope` value — "project id optional"

`PermissionScope.ProjectOptionalScoped`: check the assignment when the request names a project, allow
it through when it does not. One permission, one row, create and edit both authorised.

**Cost.**
- It converts the evaluator's central branch from a binary into a tri-state whose third state means
  *"§9 applies, unless the URL omits the project."* The strength of the check becomes a property of
  the route shape rather than of the permission. Any future endpoint that requires this permission
  without a `{projectId}` segment — by intent, by copy-paste, or by a typo in `ProjectScope.FromRoute`'s
  key — silently gets the unassigned variant and no test fails.
- That is the exact failure family this project has already paid for three times: D-048 (a
  revalidation that only ran on one of two paths, and two tests that both exercised the path that
  worked), F-04 / D-052 §1 (a grant satisfied more broadly than its author intended), D-035. Each was
  found by someone reading, not by anything failing.
- It is a generalisation built for one caller. `CLAUDE.md` forbids exactly this.

### D · Leave the permission alone; let the create endpoint carry its own rule

`POST /api/projects` requires authentication only, and the handler checks the role itself.

**Cost.** Deny-by-default and the "one table to read" property of `PermissionCatalogue` both stop
being true for the single most consequential creation act in the system. The next reader asking *"who
can open a project?"* reads the catalogue and gets no answer. Listed only because it is what happens
by default if nobody decides.

---

## 3. Recommendation — design A

**Add `Permission.ProjectCreate`, company-wide, granted to the Owner and the Technical Office. Leave
`ProjectManage` exactly as it is and narrow its documentation to editing.**

### What decides it

1. **It changes the behaviour of nothing that exists.** The evaluator is untouched, so no permission
   in the catalogue decides differently after this change than before it. Options B and C both alter
   how an *existing* permission is evaluated, and their cost falls on projects already running.
   §9 keeps its guarantee for editing, unaltered and still enforced in one place.
2. **The scope split follows the shape of the two acts, it is not a workaround for one.**
   `PermissionScope` is a statement about the *subject* of the act. Editing has a subject that can be
   named and assigned to; creating does not have one yet. A create is company-wide by construction,
   not by exception — which is why it belongs in the same half of the catalogue as `UserManage`, the
   other permission that brings a thing into existence.
3. **Two permissions is the honest count.** D-052's own framing — *"the permission as written can
   authorise editing a project and cannot authorise opening one"* — is the observation that these are
   two capabilities sharing one row. They also have different blast radii: a wrongly-granted
   `ProjectCreate` produces junk projects; a wrongly-scoped `ProjectManage` puts an unassigned user
   inside a live project's data. Rows that are wrong in different ways should be revocable separately.
4. **It is the smallest diff that is also the smallest semantic change.** B is a smaller diff and a
   much larger semantic change.

### On the name

`ProjectCreate`, not `ProjectOpen`, despite Karim's wording being *"open (create) a new project"*.
`spec.md` §9 already uses "open" in the opposite sense — *"a user MUST be assigned to a project **to
open it** or act on it"*, meaning access. A permission called `ProjectOpen` sitting next to
`ProjectRead` would be read in §9's sense by anyone who has just read §9. `CLAUDE.md` fixes entity and
domain terms (`Extract`, `Hold`, `Bab`); this is a permission identifier, and the constraint that
applies is the one against fragmenting meaning.

### What would make me revisit

- **Three or more permissions want "sometimes project-scoped."** Then option C stops being a
  generalisation for one caller and becomes a pattern. Revisit at three, not at two.
- **Karim scopes creation below the company** — e.g. a Technical Office user who may open projects
  only for certain clients or regions. Nothing suggests this, and `CLAUDE.md` puts multi-branch out of
  scope, but it would make company-wide the wrong shape rather than the right one.
- **The two grant lists must be kept identical by rule.** If a ruling ever says "whoever may edit may
  create, always", the duplication becomes maintenance with no meaning. Note that even then the rows
  cannot merge: the scopes still differ. The response would be to derive one list from the other in
  the catalogue, not to collapse the rows.
- **`ProjectCreate` acquires a second holder that `ProjectManage` should not have.** That is not a
  reason to revisit; it is the evidence the split was correct, and it should be recorded as such.

---

## 4. Migration path

### 4.1 `src/Domain/Authorization/Permission.cs`

Add to the "Project access" block. `5` is free — the block runs 1-4 and master records start at 10.

```csharp
/// <summary>Open a new project. spec.md §2 amendment — Karim 2026-08-21, decisions.md D-052 §2.</summary>
/// <remarks>
/// Company-wide, deliberately: a create request cannot name the project it is about to create, and
/// PermissionScope.ProjectScoped requires one. Deliberately separate from ProjectManage, which stays
/// project-scoped so spec.md §9's assignment requirement keeps applying to every edit of a project
/// that already exists. Merging the two would fix creation by removing that requirement from editing.
/// </remarks>
ProjectCreate = 5,
```

And amend `ProjectManage`'s summary (`Permission.cs` lines 36-37) from *"Create and edit a project"* to
*"Edit an existing project"*, with a pointer to `ProjectCreate`. This is a correction, not a
tidy-up — the current sentence describes a capability the row does not have.

### 4.2 `src/Domain/Authorization/PermissionCatalogue.cs`

Add one row beside `ProjectManage` (after line 194):

```csharp
new(Permission.ProjectCreate, PermissionScope.CompanyWide,
    [owner, technicalOffice],
    "§2 — ruled by Karim 2026-08-21, see decisions.md D-052 §2 and N10"),
```

Replace the 🟡 SCOPE IS UNRESOLVED block (`:183-191`) with a short note that the scope question is
closed and *why the two rows exist*, so a later session does not helpfully merge them.

**`TouchesMoney` stays `false`, matching `ProjectManage` today.** Opening a project brings the
container into existence; it writes no posting and opens no account — `AccountManage` is the flagged
row that does that. Both grants name a role, so D-053's evaluator guard
(`PermissionEvaluator.cs` lines 132-136) would discard nothing even if the flag were set. Flagging it would
also require editing `CatalogueCompletenessTests`' written-out list of eleven money-touching
permissions, which is pinned by name on purpose. **Judgement call, stated so it can be overruled
cheaply:** if the Architect reads "triggers … accounting ledgers, and cost tracking" as governing the
ledger, set the flag and extend that list — nothing else changes.

### 4.3 Evaluator, access policy, handler, endpoint conventions, database

**No change to any of them.**

- `PermissionEvaluator` already returns `Granted` for a company-wide permission at line 143-146.
- `IProjectAccessPolicy` is never consulted for a company-wide permission — the handler's
  `if (subject is not null && projectId is not null)` guard (`PermissionAuthorizationHandler.cs` line 75)
  sees `null`. This is the same path
  `UserManage` takes today.
- The create endpoint declares `.RequirePermission(Permission.ProjectCreate)` — the no-scope overload
  at `PermissionPolicyProvider.cs` lines 57-59, which encodes `ProjectScope.None`.
- **No database change.** Permissions are code: `PermissionCatalogue.Build()` returns a
  `FrozenDictionary` and nothing persists a permission id. `grep -i permission` over
  `src/Infrastructure/Persistence` hits only `IdentityConfigurations.cs`, and only in prose. No
  migration, no seed data, no backfill.
- Authority is still re-read per request through `IPermissionSubjectReader` including the security
  stamp (D-053) — company-wide permissions are covered by that since D-048, which is what makes a
  company-wide `ProjectCreate` safe to hold in the first place. Under the pre-D-048 code this
  proposal would have been a bad idea.

### 4.4 Tests

| Test | Change |
|---|---|
| `PermissionEvaluatorTests.Only_the_owner_and_the_technical_office_may_open_a_project` (`:211-226`) | Repoint to `ProjectCreate` — it carries Karim's ruling, and his ruling is about opening. Its Marketing assertion is decided at `RoleNotGranted` before scope is consulted, so it passes either way; repointing is about it asserting the right thing, not about keeping it green. |
| ~~**New**~~ — **BUILT** `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`] | Technical Office subject, `ProjectAccess.Denied` → `NotAssignedToProject`. **This is the test the whole proposal exists to make possible.** It fails the day someone widens `ProjectManage` to company-wide, which is the mistake this design is chosen to prevent. Without it the design is a comment. **It very nearly was one:** the rows shipped on 2026-08-22 and this test did not, until the recovery in D-056 §3. The mutation was run — widening the row turns this test, and only this test, red. |
| ~~**New** — `Opening_a_project_needs_no_project`~~ — **FOLDED IN, not written separately** [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`] | The assertion it wanted — a Technical Office subject reaching `ProjectCreate` with `projectId: null` → `Granted` — lives inside the repointed `Only_the_owner_and_the_technical_office_may_open_a_project` instead, where it sits beside the grant list it belongs to. **What was dropped:** the explicit `Unresolved is false` check. `The_set_of_unresolved_permissions_has_not_grown` already covers it for every row at once, so a second copy would pin the same fact twice. |
| `CatalogueCompletenessTests.The_set_of_unresolved_permissions_has_not_grown` (`:257-283`) | **No change.** `ProjectCreate` has named holders, so it never joins the set; `PeriodClose` stays the only row. |
| `Hr_holds_no_permission_that_touches_money` [Verified: 2026-09-04 @ `CatalogueCompletenessTests.cs` -> `Hr_holds_no_permission_that_touches_money`] | **Changed after all, by a different ruling.** This row predicted "no change" and was right about `ProjectCreate` — but `UserRead` (D-055 §2) added HR's third permission, so the test was renamed from `..._two_permissions_and_neither_...`. It also gained the money assertion its old name had only claimed. See D-056 §2. |
| `No_financial_permission_is_granted_to_a_bare_department`, `A_portal_client_holds_nothing_outside_the_portal` | **No change** — both grants name roles; no client grant. |
| Api tests / `ProbeEndpoint` | **No change.** No probe route requires `ProjectManage`, and adding one for `ProjectCreate` buys nothing until KAFF-407 builds the real endpoint, which will bring its own permission tests. |

`dotnet build` with warnings as errors is the first gate as usual; adding an enum member does not
break any exhaustive switch — nothing switches on `Permission`.

### 4.5 Slice 1's fifteen committed stories — **not affected**

Verified rather than assumed:

- **No code depends on `ProjectManage`.** `src/Api/Features` contains `Health/GetHealth` and nothing
  else; there is no Projects feature folder. The only references anywhere in `src/` are the catalogue
  row and the enum member.
- **The only test that names it** is `PermissionEvaluatorTests:215-226`, and it passes either way
  (see the table above).
- **`ProjectManage`'s grant list does not change**, so `TC-1-206` (*"held by the Owner and the
  Technical Office, and by nobody else"*) stays true exactly as written, and `TC-1-207`'s
  `Unresolved` set is untouched.
- **Slice-1 stories mention it as a boundary, not a dependency.** KAFF-113 §"Not in this story"
  (*"nothing in slice 1 creates one … slice 1 continues to assign against projects that arrive in
  seed data"*), KAFF-120, and KAFF-122 — which slice 4's KAFF-416 already replaces. None of their
  acceptance criteria exercise `ProjectManage`.

**Documentation that needs a line when this lands** (none of it slice-1 acceptance, none of it code):
`qa/questions.md` F-27 closes; `qa/slice-1/permission-matrix.md` (the two 🟡 `ProjectManage` cells and
the caveat under §1 — the matrix gains a `ProjectCreate` row); `ux/screen-inventory.md` S-051, which
still reads *"`ProjectManage` is granted to nobody"* and is now wrong twice over; `stories/backlog.md`
lines 272-277, where N10 is named as what blocks KAFF-407 and KAFF-416; `architecture.md`'s permission
section; and a `decisions.md` entry recording this choice with the §9 reasoning, since the whole point
is that the next session must not merge the rows back.

---

## 5. What I am deliberately **not** proposing

- **No new abstraction over permissions.** No `IPermissionScopeResolver`, no per-permission strategy,
  no attribute-driven scope. One enum member and one data row is the entire mechanism, and the
  mechanism already exists.
- **No third `PermissionScope` value** (option C). One caller does not justify a general case, and the
  general case it would create is *"§9 applies unless the URL omits the project"* — a weaker guarantee
  wearing the same name.
- **No auto-assignment of the creator to the project they just opened.** It is a plausible convenience
  and it is a business rule nobody has given; see question Q-N10-1. Building it would also put a
  §9-exempting write inside a create handler, which is where such things become invisible.
- **No compile-time or test-time check that an endpoint's declared `ProjectScope` matches the
  catalogue's `PermissionScope`.** A mismatch already fails **closed** and loudly: a project-scoped
  permission declared with `ProjectScope.None` returns `ProjectNotSpecified` and a logged 403 on the
  first request — which is precisely how this defect was found. A test to pre-empt a failure that is
  already safe and already visible costs more than it saves. Revisit if a company-wide permission is
  ever declared *with* a project scope, which fails **open** (the assignment check is skipped at line
  143) and is therefore the direction actually worth pinning — today no endpoint does it.
- **No renaming of `ProjectManage`.** The name is correct for what the row will do, and it is cited by
  ten documents, one domain test and QA's matrix.
- **No endpoint.** KAFF-407 builds it. This proposal makes it buildable.

---

## 6. `ProjectAssignmentManage` — checked, and it does **not** have the same defect

It looks like it should: project-scoped (`PermissionCatalogue.cs` lines 201-203), HR has global reach, and
somebody must make the first assignment on a brand-new project. It is fine, and the reason is worth
recording because the two cases are genuinely different in kind.

**By the time an assignment is made, the project exists and the route can name it.**
`POST /api/projects/{projectId}/assignments` supplies the id from the route;
`ResolveProjectId` returns it; the evaluator passes line 148; `ProjectAccessPolicy.EvaluateAsync`
routes Owner and HR to `GlobalReachAsync`, which grants without an assignment row for any project
that exists. The circularity — *"requiring an assignment in order to create assignments"* — was
already solved, by **reach** (D-044 ruling 3), and the permission stayed project-scoped on purpose so
that HR's reach still stops at a project that does not exist.

Evidence it works as described: `PermissionEvaluatorTests.cs` lines 366-381
(`Hr_reaches_a_project_without_an_assignment_but_sees_nothing_financial` — HR +
`ProjectAssignmentManage` + global reach → `Granted`, and `ProjectRead` on the same reach refused),
KAFF-113 AC1 and AC3, and D-044's Api test *"HR staffing a project it was
never assigned to"*.

**The two problems only rhyme.** Assignment's subject exists and can be named, so *reach* is the right
instrument. Creation's subject does not exist and cannot be named, so *scope* is the only instrument
that reaches it. Fixing N10 with reach is impossible — there is nothing to reach.

**One boundary this puts on KAFF-407's endpoint design**, stated so it is not discovered later: if a
future "create project with its team in one request" endpoint is ever built, the assignment half
lands in exactly N10's position — no project id to name. Creation returns the id; staffing is a
second call. That is not a new rule, just the shape the current mechanism requires.

---

## 7. Business questions — raised, not answered

These came out of the analysis. None is mine to decide, and none blocks the decision above.

**Q-N10-1 · Does opening a project put its creator on it?**
A Technical Office user who opens a project holds no assignment row on it, so one line after creating
it they cannot read or edit it — `ProjectRead` and `ProjectManage` are both project-scoped and TO has
no global reach. The Owner is unaffected (D-010). So either HR/the Owner staffs every new project
before its creator can touch it, or opening implies an assignment. Both are defensible; §9's
*"role alone is insufficient"* leans against the second. **For Karim (or Nabil as workflow).**

**Q-N10-2 · ~~Does D-052 §2 settle who may *edit* an existing project, or only who may open one?~~**
**✅ CLOSED — Nabil, 2026-08-22, `decisions.md` D-055 §1.** Finance never holds `ProjectManage`; the
contract's tax and financial settings move behind a new `ProjectFinancialsEdit`, granted to Finance
and the Owner. The question below is kept as written because the collision it describes — two of
Karim's own rulings pointing opposite ways — is the reasoning the answer rests on.
Karim's words are about opening. `ProjectManage`'s grants — Owner and Technical Office — were written
from that ruling and now govern editing alone. **This already conflicts with committed slice-4 work:**
KAFF-416 gives **Finance** the contract's withholding category (D-049 rulings 9-10 — *"a strict
accounting parameter, not a marketing detail"*), and `Project.SetWithholding` exists — but Finance
holds no `ProjectManage` grant, so an edit endpoint gated on `ProjectManage` refuses Finance the field
Karim assigned to them. Either Finance joins `ProjectManage`, or the withholding category needs its
own permission, or field-level authority is expressed some third way. **This should be asked before
KAFF-416 is estimated, and it is a separate question from N10.** For Karim.

**Q-N10-3 · Does opening a project require the Owner's approval?**
Karim's phrasing puts the Technical Office and the Owner side by side, which reads as either acting
alone. Opening a project "triggers … accounting ledgers", and §9 puts the Owner on every financial
movement. If a project opened by the Technical Office should be an Owner-approved act rather than a
unilateral one, that is a state machine, not a permission, and it belongs in KAFF-407's story rather
than in this file. Raised because a permission is the wrong instrument for it and choosing one now
would foreclose the question. For Karim.

---

## 8. Summary of the change, if approved

| File | Change |
|---|---|
| `src/Domain/Authorization/Permission.cs` | `+ ProjectCreate = 5`; correct `ProjectManage`'s summary |
| `src/Domain/Authorization/PermissionCatalogue.cs` | `+ 1 row`, company-wide, Owner + Technical Office; replace the 🟡 scope block |
| `src/Domain/Authorization/PermissionEvaluator.cs` | — |
| `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` | — |
| `src/Api/Authorization/*` | — |
| Database / migrations | — |
| `tests/Domain.Tests` | 1 test repointed, 2 added |
| `tests/Api.Tests`, `tests/E2E.Tests` | — |
| Slice 1's 15 stories | — |
| Docs | `decisions.md` entry; 5 files carrying F-27 / N10 get a line |
