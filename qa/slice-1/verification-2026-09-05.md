# Verification — 2026-09-05

**Verifier, fresh session.** `CLAUDE.md`: *"If you wrote the code, you do not certify it."* I wrote
none of the seven stories in scope, and none of the eight commits.

**Scope:** KAFF-118, 119, 120, 121, 123, 124, 126 — seven stories delivered 2026-09-04/05, none
independently verified — plus `51a0c5a`, which moved `ForwardLimit` into configuration and therefore
changed what the audit trail records as the caller's IP.

> **⚠️ Appended by the author 2026-09-05, after this report was written — the verdicts below are the
> Verifier's and are NOT edited.** `V-33-A` (HIGH) and `V-33-B` (MEDIUM) are **repaired**; see
> decisions.md **D-118**, and the fix is tests only — **no file under `src/` changed**, because the
> defect was missing coverage and not a wrong grant. Both repairs were watched failing under the
> Verifier's own two mutations. **`V-33-C`, `D`, `E`, `F` and `G` are open** and routed in
> `stories/backlog.md`. This note exists so a later reader does not act on a closed finding as though
> it were live; **nothing else in this file has been touched.**

**Brief:** `meetings/BRIEF-2026-09-05-verifier.md`. It says of itself that it was written by the
session that wrote all the code it describes, and that every claim in it — including the gate
figures — is a claim to check. It was right to say so: three of its statements needed correction and
one of its three "disbelieve this" items turned out to be true. §1a records both directions.

---

## 0. Progress of this report

Everything was `pending` until reached. Nothing is marked done on an author's evidence.

| # | Item | State |
|---|---|---|
| 1 | Opening gate — `HEAD`, `git status`, stranded hosts, baseline measured | **done** — §1 |
| 1a | Corrections to the brief | **done** — §1a |
| 2 | Suite 1 · Money — §6.7 at the domain, the create path and the edit path | **done** — §2 |
| 3 | Suite 2 · Permissions — the role census, and the mutation that stayed green | **done** — §3 |
| 4 | Suite 3 · State machines — archive, no delete, no unarchive | **done** — §4 |
| 5 | Suite 4 · End to end — re-measured at `HEAD`, not at `e0fd5cf` | **done** — §5 |
| 6 | The frontend guard — both mutations the brief asked for | **done** — §6 |
| 7 | `Q57` — the burnt code, confirmed empirically rather than from the note | **done** — §7 |
| 8 | `51a0c5a` and `Kaff:ForwardedProxyHops` — what I could and could not check | **done** — §8 |
| 9 | Every absence test I met, and how it fails | **done** — §9 |
| 10 | The development database, and why the documented E2E path does not run | **done** — §10 |
| 11 | Closing gate — `HEAD`, `git status`, suites re-run | **done** — §11 |
| 12 | Criteria I could not reach — a list, because silence is not a pass | **done** — §12 |
| 13 | Verdict per story | **done** — §13 |
| 14 | The one thing Nabil should know | **done** — §14 |

### Findings index

| ID | Severity | Subject |
|---|---|---|
| `V-33-A` | **HIGH** | `Role.HeadOfDesign` is asserted against **no endpoint anywhere in the repository**. Granting it `ClientManage` — one term in one catalogue row — leaves build 0/0, Domain **125/125** and Api **295/295** green while handing a Head of Design every client file in Kaff, internal notes included. Slice 1's gate is *"one test per role asserting what it cannot reach"*, and for one of the nine roles there is no such test |
| `V-33-B` | **MEDIUM** | Role coverage across the six client endpoints is uneven, and the two thinnest are the two that matter most: `GET /api/clients/{id}` asserts 2 of 6 refused roles and `POST /api/clients/phone-check` asserts **1 of 6** — on a route that returns client **names**. Demonstrated, not inferred: granting `ClientManage` to `Role.TechnicalOffice` reddened three endpoints and left the other three silent |
| `V-33-C` | **MEDIUM** | `await resolver.ensureResolved()` in `clientManageGuard` is unasserted — deleting it leaves E2E **11/11**. It is D-113 §2's fix for a real user-visible defect, and it is load-bearing only for as long as nobody reorders one `canActivate` array. **Zero frontend unit tests** — `V-32-D` is still open and now guards more |
| `V-33-D` | **MEDIUM** | `AC-126-C`'s two empty states and `AC-126-F`'s 409-reopens-the-warning path are **implemented and asserted by nothing** — no E2E test, no unit test. Both were discharged by one session driving Chromium once |
| `V-33-E` | **MEDIUM** | `AC-126-L` is half-discharged at the UI. Its E2E test drives **Finance only**; the portal `Role.Client` half is undriven and **cannot** be driven, because `scripts/seed-demo.ps1` creates no `Role.Client` user at all. The client-portal boundary of spec.md §12 has no UI-level evidence anywhere in this repository |
| `V-33-F` | **MEDIUM** | The `kaff` development database is permanently degraded and **the documented E2E and demo path does not run against it**: `PROBE-UNFLOORED`, a `Safe` account with `enforce_non_negative = false` carrying two postings. `/api/health` answers **503**, smoke fails three checks. Both repairs are refused by the guards. This is `V-31-A` realised — reported because its operational cost is new, not its cause |
| `V-33-G` | **LOW** | `.claude/skills/run-kaff-erp/SKILL.md` — principle 9's single source of truth for running the stack — states the API *"refuses to start at all if the PostgreSQL guards are missing."* It refuses only outside Development, and I watched it log the failure and start. The same file's §3 still says the application is one endpoint and one page |

**Two HIGH-adjacent things that are NOT findings, because I proved them sound:** spec.md §6.7's
refusal (§2) and `AC-126-L`'s `/forbidden` fix (§6) both went red under mutation, for the right
reason, on the right assertion.

---

## 1. Opening gate

**Recorded before anything was built, measured or mutated.**

| Gate | Value |
|---|---|
| `git rev-parse HEAD` | **`c468e47d90a5785d22c8df41111c077afa33246b`** |
| `git status --porcelain` | **empty — tree clean** |
| Target in the brief | `93a517b` or later — **satisfied**, `c468e47` is two commits later |
| `docker ps` | `kaff-db  Up 13 days (healthy)` |
| **Stranded hosts** | **none** — `Get-CimInstance Win32_Process` matched on **command line**, the corrected form |

### Baseline, measured rather than repeated

Every figure below is one I ran. The brief's column is what it claimed.

| Gate | Brief claimed | Measured | |
|---|---|---|---|
| `dotnet build KaffErp.sln -c Release -warnaserror` | 0 / 0 | **0 warnings, 0 errors, exit 0** | ✅ |
| `dotnet format --verify-no-changes` | exit 0 | **exit 0** | ✅ |
| Domain suite | 125/125 | **125/125, 0 skipped, exit 0** | ✅ |
| Api suite | 295/295 | **295/295, 0 skipped, exit 0** — 5m 04s | ✅ |
| Citations | 1155 / 0 / 0 | **1155 checked / 0 broken / 0 legacy, exit 0** | ✅ |
| E2E suite | 11/11 **at `e0fd5cf`, flagged stale** | **11/11 at `c468e47`** — §5 | ✅ re-measured |

**The build result was read before every test result**, every time, per D-046 and the skill's
`MSB3026` amendment. `MSB3026` never appeared. Where a mutation was involved I also compared the
`LastWriteTime` of the emitted assemblies against the mutated source before trusting a run — D-109
§3's second false negative was a revert that left stale binaries, and a fast incremental build looks
exactly like a skipped one.

### 1a. Corrections to the brief

Principle 7. Four things, two of which change what the brief concluded.

**1. "On a read … the permission test is the *entire* control" (item 3) is wrong, and wrong in the
safe direction.** Removing `.RequirePermission(Permission.ClientManage)` from
`GET /api/clients/{clientId}` reddens **two** tests, and the brief is right about the count — but it
names only one control. The second is
`EndpointPermissionCoverageTests.Every_mapped_endpoint_carries_a_permission_requirement`, a
structural backstop that reads the routes the host actually built and fails on any ungated one. So a
**missing** gate on a read has two independent controls, not one.

What the per-endpoint test *is* the entire control against is a **wrong** permission rather than a
missing one — the meta-test is satisfied by any parseable requirement. That distinction is what
`V-33-A` and `V-33-B` are about, and it is sharper than the one the brief drew.

**2. "`AC-126-C`'s empty states, `AC-126-F`'s 409-reopens-the-warning path … do not exist yet"
(claim 2) is wrong as written.** All of it exists in code: both empty states are rendered by
`client-list-page.html`, both key pairs are in `ar.json` and `en.json`, and the 409 branch is in
`client-form-page.ts`. What does not exist is any **test**. The criteria are unasserted, not
unbuilt — which is a different repair, and a smaller one. See `V-33-D`.

**3. Claim 3 is correct and was worth making.** The gate figures all held on re-measurement, and the
E2E figure was indeed not this commit's. It is now: **11/11 at `c468e47`**.

**4. Item 2 — the `ILike` escape rewrite — is sound.** I checked the rewrite rather than the story,
as instructed. `Wildcards_typed_into_the_search_box_are_matched_literally` now asserts both halves:
searching `%` must **not** return the plain fixture, and must **still** return
`شركة … 100% للتنفيذ`, whose name contains a literal per cent sign. That is the assertion the
original got backwards. Nothing further owed.

---

## 2. Suite 1 · Money — spec.md §6.7

Slice 1 moves no money. §6.7 is money's edge and it is in scope: *"collections will never match
issued extracts and staff will invent adjustments to close the gap."*

**What should be true, derived from spec.md §6.7 and its 2026-08-21 amendment**, not from the code:
individual clients do not withhold; that is enforced in two places, a rate on an individual's
contract and a tax registration number on an individual, *"which is the same claim by another
field"*; and the registration number stays on the client because it identifies the legal entity.

**Mutation.** I deleted the guard from `Client.SetClassification` — the whole `if`, not a
comparison operator, so the failure could not be an artefact of a half-applied edit. Verified
applied (`git diff --stat`: 5 deletions), verified built (all three copies of `Kaff.Domain.dll`
newer than the mutated source), verified exit code 0 before trusting any test result.

| Level | Result | Named failure |
|---|---|---|
| Domain | **3 red** of 125 | incl. `ClientEditingTests.A_corporate_client_carrying_a_registration_number_cannot_become_an_individual` |
| API — create path | **1 red** of 13 | `CreateClientTests` — `An_individual_cannot_be_given_a_tax_registration_number` |
| API — edit path | **1 red** of 12 | `EditClientTests` — the corporate→individual transition |

Reverted; tree clean; suites green again.

**Held.** §6.7 is asserted at the domain, through the API, and on both the create and the edit path,
exactly as the brief asked. The rule lives in the entity rather than in a validator, so
`SetTaxRegistration` delegating to `SetClassification` means there is one copy of it and my single
mutation reached every caller — which is the property KAFF-120 rule 5 exists to buy.

`AC-120-F` is held by a **whitelist** of the entity's members, not a blocklist of suspect words —
D-106's lesson applied. A withholding category added under any name fails it.

---

## 3. Suite 2 · Permissions — slice 1's acceptance gate

`agents.md` §7: *one test per role asserting what it cannot reach, hitting endpoints directly.*
`qa/slice-1/permission-matrix.md` §2 is the matrix: `ClientManage` is **CompanyWide**, granted to
Owner and MarketingSales, and refused — **R** — to Finance, TechnicalOffice, SiteEngineer,
**HeadOfDesign**, Client and Hr. Subcontractor is **X**: spec.md §9, *"record only, no login."*

Six endpoints carry the permission:

```
POST   /api/clients                    POST /api/clients/phone-check
GET    /api/clients                    GET  /api/clients/{clientId}
PUT    /api/clients/{clientId}         POST /api/clients/{clientId}/archive
```

### The census — which refused role is asserted against which endpoint

Counted from the suite, not from the stories.

| Endpoint | Finance | TechOffice | SiteEng | **HeadOfDesign** | Client | Hr | Covered |
|---|---|---|---|---|---|---|---|
| `POST /api/clients` | ✅ | ✅ | ✅ | **✗** | ✅ | ✅ | 5/6 |
| `PUT /api/clients/{id}` | ✅ | ✅ | ✅ | **✗** | ✅ | ✅ | 5/6 |
| `GET /api/clients` | ✅ | ✅ | ✅ | **✗** | ✅ | ✅ | 5/6 |
| `POST /api/clients/{id}/archive` | ✅ | ✗ | ✗ | **✗** | ✅ | ✅ | 3/6 |
| `GET /api/clients/{id}` | ✅ | ✗ | ✗ | **✗** | ✅ | ✗ | 2/6 |
| `POST /api/clients/phone-check` | ✗ | ✗ | ✗ | **✗** | ✅ | ✗ | **1/6** |

**The brief's one explicit instruction on this suite is met**: the portal `Role.Client` user is
asserted on **every one of the six**, including `phone-check`, whose own test comment says *"a route
called 'check' reads as innocuous and is exactly where `Role.Client` gets forgotten."* That column
is the strongest thing in the table and it is the one that most needed to be.

The `HeadOfDesign` column is empty. So is most of the bottom two rows.

### `V-33-A` — HIGH · a role nothing refuses

**`Role.HeadOfDesign` appears exactly once in the entire test tree** — `UserTests.cs`, an
`[InlineData]` row about whether the role may hold a staff session (it may). It is asserted against
**no endpoint at all**, on any feature, in any suite.

**Watched staying green.** I added `Role.HeadOfDesign` to the `ClientManage` row of
`PermissionCatalogue` — the shape of a real mistake, one term in one list, in a diff about something
else:

```
new(Permission.ClientManage, PermissionScope.CompanyWide,
    [owner, marketing, new AccessGrant { Role = Role.HeadOfDesign }], "§2"),
```

| | Result |
|---|---|
| `dotnet build -c Release -warnaserror` | **0 warnings, 0 errors, exit 0** |
| Domain suite | **125/125 — green** |
| Api suite | **295/295 — green**, 0 skipped |

A Head of Design would then create, read, edit, list and archive **every client Kaff has**, and
`GET /api/clients/{clientId}` is the one payload in the slice carrying **internal notes**, which
spec.md §12 says the client MUST NEVER see and which `permission-matrix.md` annotates
*"§12, absolutely. This permission reaches every client Kaff has."* Nothing in the repository
notices. The whole slice-1 gate reports pass.

**Why the other two absent roles are not this.** `Role.Client` and `Role.Subcontractor` each carry a
**catalogue-wide** pin in `CatalogueCompletenessTests` —
`A_portal_client_holds_nothing_outside_the_portal` and `No_permission_is_granted_to_a_subcontractor`
— which fail on any grant added anywhere, on any permission, in any slice. Those are the right
shape. `Role.HeadOfDesign` has no equivalent, **despite `PermissionCatalogue.cs` stating the
invariant in its own prose**: *"Role.HeadOfDesign holds exactly one row: `ProjectRead`."* That is a
documented invariant with no machine behind it, which is D-067's exact pattern — prose a reviewer
relies on to answer a safety question.

**This is the finding the pass exists to produce.** Slice 1's gate is stated as *permission tests
pass*; the tests pass, and for one role in nine the gate is asserting nothing. Routed to the
**Architect** (permission scope) per `agents.md` §3b, with QA owed the coverage case.

### `V-33-B` — MEDIUM · three of six endpoints catch a widened grant, three do not

Demonstrated by a second, independent mutation: granting `ClientManage` to `Role.TechnicalOffice` —
a role that *is* tested, somewhere.

**3 red of 295** — `ListClientsTests`, `CreateClientTests`, `EditClientTests`, each
`Only_marketing_and_the_owner_may_…`. `GetClientTests`, `ArchiveClientTests` and every `phone-check`
assertion stayed green.

So the same defect is caught or missed depending on which endpoint the attacker uses, and the two
weakest rows are the two worst to be weak on: `GET /api/clients/{id}` is the internal-notes payload,
and `phone-check` returns client **names** to anyone who can reach it.

**Not the same finding as `V-33-A`.** `V-33-A` is a role with no assertion anywhere; `V-33-B` is
roles that are asserted on some endpoints and not others. Fixing one does not fix the other.

### What is genuinely well built here, and should not be lost in the repair

- `EndpointPermissionCoverageTests` reads **endpoint metadata from the routes the host actually
  built**, never source text — D-067's lesson correctly applied. Removing a gate cannot be quiet.
- Its allow-list and self-only list each carry a written reason per entry, and `IsSelfOnlyListed`
  requires `LiveSession.IsApplied` — being on the list is a claim, and the claim is paid for.
- `Every_permission_requirement_declares_the_scope_its_catalogue_row_names` closes the
  wrong-scope substitution, which is why a project-scoped permission cannot be swapped onto a
  company-wide client route to widen it. That test is doing real work and it is why `V-33-A` needed
  a *company-wide* grant to demonstrate.

---

## 4. Suite 3 · State machines — archive

The only machine in this slice. spec.md §2 requires full history, §3 requires a reopened
opportunity to attach to the same client — both impossible if the row can disappear.

| Claim | Checked | Result |
|---|---|---|
| active → archived, audit record naming the actor | `ArchiveClientTests` | held |
| archiving twice refused, `errors.master.already_archived` | domain + API | held |
| **no delete path exists** (`AC-123-D`) | `No_endpoint_in_the_application_deletes_anything` | held, and **well built** |
| **no unarchive path exists** | `Client` exposes three public mutators — `SetContactDetails`, `SetClassification`/`SetTaxRegistration`, `Archive` — pinned by a member whitelist in `ClientEditingTests` | held |
| archived client still surfaces in the duplicate check, flagged archived, save not blocked | `ArchiveClientTests`, per D-049 ruling 8 | held |
| archived client still readable by id | `GetClientTests` | held |

`AC-123-D` deserves its note: it enumerates **every route the host mapped** and asserts none answers
`DELETE`, so a delete route added under any name in any feature folder fails it. The brief's item 7
worried about absence tests proved *about a word*; this one is not. `V-32-A`'s shape is not repeated
here.

---

## 5. Suite 4 · End to end — re-measured

Run through `/run-kaff-erp` (principle 9), not hand-rolled. API on 5080, SPA on 4200, smoke green
before any assertion was believed.

**11/11 at `HEAD` = `c468e47`**, 0 skipped, exit 0 — the brief's figure was `e0fd5cf`'s and is now
this commit's.

**It took a fresh database to get there**, and that is `V-33-F` — §10. Against the shared `kaff`
development database the suite ran **4 failed / 7 passed**, every failure a sign-in timeout, because
that database is unseeded *and* degraded. Neither is a defect in the seven stories, and reporting
`4 failed` as a slice-1 result would have been wrong. I record both numbers so the next session
knows which is which.

The suite fails rather than skips when the seeded users are missing — `SuiteConfigurationTests`
makes the skip itself checkable. That is the right choice and it is why I noticed the environment
rather than banking a quiet pass.

---

## 6. The frontend guard — both mutations the brief asked for

### `AC-126-L` · the `/forbidden` fix is real — **the brief's item 4 is discharged**

Item 4 flags that `clientManageGuard` shipped returning `parseUrl('/')`, which `ux/navigation.md`
forbids in as many words — *"a redirect that hides what happened"* — and that the fix is one day old
and was tested by its own author.

I put the defect back: `parseUrl('/forbidden')` → `parseUrl('/')`.

**1 red of 11** —
`ClientScreenTests.A_role_without_client_manage_is_refused_visibly_rather_than_sent_to_its_landing`,
failing on the assertion it is named for. Reverted, 11/11 restored.

The test is not a status-code check. It waits for `forbidden-page`, asserts the URL, asserts the
rendered refusal is **not** the raw key `errors.auth.forbidden` — an Arabic-speaking user reading a
key — and asserts the app chrome survived. That is `AC-126-L` as written, and it holds.

### `V-33-C` — MEDIUM · the `await` that pins nothing — **the brief's item 5 is confirmed**

I deleted `await resolver.ensureResolved()` from `clientManageGuard`. Verified applied (`git diff
--numstat`: `0 2`), dev server rebuilt, suite re-run.

**11/11 green.**

That line is not decoration. D-113 §2 records the defect it fixes: `/clients/new` and
`/clients/{id}`, typed, bookmarked or refreshed, bounced to `/`, and the landing forwarded to
`/clients` — **the operator asked for a form and silently got a list**, with no error anywhere. It
was found by hard-loading the route in a real browser.

It stays green because `app.routes.ts` runs
`canActivate: [sessionGuard, mustChangePasswordGuard, clientManageGuard]`, and `sessionGuard`
resolves the session first. **So the guard's own defence is redundant today and unasserted always.**
The day somebody reorders that array, drops `sessionGuard` from the client routes, or copies this
guard to a route that has no `sessionGuard` in front of it, D-113 §2's defect returns and every
suite stays green.

**My judgement, since the brief asks for one:** this is not acceptable coverage, and the reason is
not the missing assertion — it is that `A_bookmarked_client_form_url_loads_the_form_and_not_the_list`
**reads** like the regression test for that `await` and is not one. A test that appears to pin a
mechanism and pins only an outcome is worse than an absent one, because it stops anybody looking.
This is `V-32-D` recurring on a different line, and `V-32-D` is still open: **there are still zero
frontend unit tests in `src/Web`** — no `.spec.ts`, no `.test.ts`, anywhere.

Routed to **Frontend**, with `V-32-D` un-closed rather than re-opened.

---

## 7. `Q57` — confirmed empirically, as instructed

The brief's item 1 asks for the behaviour, not the note. Two independent confirmations.

**1. PostgreSQL's semantics, against the live schema.** In `kaff_demo`, which carries the real
`client_code_seq`:

```
before          10002
BEGIN; SELECT nextval('client_code_seq');  ->  10003 ; ROLLBACK;
after_rollback  10003
clients         C-10001, C-10002
```

The number advanced through a `ROLLBACK`. `C-10003` will never exist. Confirmed on the deployed
schema, not reasoned from the manual.

**2. Which refusals actually burn one, which do not.** The handler draws `nextval` as the argument
to `Client.Create`, so everything refused before that costs nothing and the two refusals after it
each burn a number. Seeding a fresh database exercised both sides and produced **`C-10001` then
`C-10002` with no gap** — including a `409 duplicate_phone_not_acknowledged` in between, which is
refused *before* the draw. So the handler's own account of itself is accurate: the duplicate path is
free, and it is the blank name and the individual-with-a-registration-number that cost a code.

**Known-open, not re-reported as new** — `Q57`, D-107 open question 1, Karim's. The note is correct
and the mechanism is one expression. I add only that the two burning refusals are both reachable
from the API today, and one of them — the §6.7 refusal — is reachable only from a non-browser
client, because `AC-126-H`'s form cannot assemble the illegal pair.

> **Disclosure:** my `ROLLBACK` probe advanced `kaff_demo`'s sequence by one. The next client
> registered in that demo database will be `C-10004`, not `C-10003`. No repo content was touched.

---

## 8. `51a0c5a` · `Kaff:ForwardedProxyHops` — what I checked and what I cannot

The brief's item 6: if the hop count and the deployment disagree, every audit row records one fixed
address for every user in the world, and nothing about it is visible — the column is populated and
the value is a plausible IP.

**Checked and consistent.** `deploy/docker-compose.staging.yml` declares
`Kaff__ForwardedProxyHops: "2"` with the reasoning beside it; `deploy/README.md` states 2 and carries
a troubleshooting row for exactly this symptom; `Program.cs` reads the key with a default of 1.
`Two_proxies_deep_the_recorded_address_is_still_the_caller` is a good test — it uses the real header
shape, asserts the caller is recorded, asserts **neither** proxy is, and asserts the forged
left-most entry is never consumed whatever the hop count. Its sibling records, correctly, that the
allowlist and not `ForwardLimit` is the security control.

**Cannot check, and it is the half that matters.** The test proves the mechanism *given* a hop count
of 2. Nothing in this repository can prove the staging host actually has exactly two proxies in
front — that is a property of a deployment I have no access to. The chain also depends on Caddy
running on the host and reaching nginx through the published `127.0.0.1:8080`, which makes the
address nginx sees the Docker bridge gateway; that must fall inside
`Kaff__TrustedProxyNetworks__0: "172.28.0.0/24"` for the second hop to be consumed at all. It
plausibly does, at `172.28.0.1`. **I did not verify it and it is not verifiable from here.**

**Recorded as a criterion I could not reach** (§12), not as a finding against the commit. The
mechanism is right; the deployment is unwitnessed. Somebody with staging access should read one real
audit row and confirm it is not the same address twice.

---

## 9. Every absence test I met, and how it fails

The brief's item 7 asks this question of every absence assertion. Answering it for each:

| Absence assertion | How it fails | Verdict |
|---|---|---|
| `AC-118-H` — ten reads write nothing | **Positive control in the same method**: after twenty reads leave the count unchanged, one real `POST /api/clients` must move it | Sound — D-116 applied, and the control is doing the work the criterion cannot |
| `AC-118-I` — a refused write writes nothing | Same shape, **two** refusals (domain and gate) then the accepted version of the same request | Sound |
| `Every_entity_is_audited_unless_it_is_a_named_exemption` | Whitelist of exempt types (`AuditRecord` alone) **plus** an assertion that the model enumerates more than five entities — *"if the model stops being enumerable this test passes by describing nothing"* | Sound, and this is the only assertion covering slice 3's `Posting` before it exists |
| `AC-123-D` — no delete endpoint | Enumerates routes the host mapped; a `DELETE` under any name fails | Sound |
| `AC-120-F` — no withholding category on the client | Member **whitelist** of entity, response contract and table | Sound — D-106 applied |
| `AC-124-G` — no money in any client payload | Member whitelist across all five client-shaped payloads | Sound |
| `No_endpoint_deletes_a_project_assignment` | Route enumeration | Sound |
| `A_portal_client_holds_nothing_outside_the_portal` | Catalogue-wide, any permission, any slice | Sound — **and it is the shape `V-33-A` wants for `HeadOfDesign`** |
| **`Role.HeadOfDesign` holds exactly one row** | **Nothing. It is a sentence in an XML comment** | **`V-33-A`** |

The pattern is good almost everywhere. D-116's lesson — that two of three tests went red on the
positive control rather than on the assertion they were named for — has been taken seriously and the
controls are real, not decorative. The one place the discipline was not applied is the one place
nobody thought to look, which is where it always is.

---

## 10. `V-33-F` — the development database, and the path that does not run

The API refused nothing and started; `/api/health` answered **503 / degraded**;
`driver.mjs smoke` failed three checks:

```
FAIL  API /api/health returns 200        — got 503
FAIL  health reports healthy             — degraded
FAIL  database guards installed          — ["accounts.enforce_non_negative on PROBE-UNFLOORED"]
```

`PROBE-UNFLOORED` is a **`Safe`** account with `enforce_non_negative = false`, carrying **two
postings**. spec.md §6.1 makes the safe floor a MUST enforced in the database. On that account it is
absent.

**Both repairs are refused, by the guards, working correctly:**

```
UPDATE accounts SET enforce_non_negative = true WHERE code = 'PROBE-UNFLOORED';
  ERROR:  KAFF_ACCOUNT_IMMUTABLE: account PROBE-UNFLOORED configuration cannot be changed after creation.

DELETE FROM postings WHERE id = '…b1';
  ERROR:  KAFF_APPEND_ONLY: postings is append-only; DELETE is not permitted.
```

**This is `V-31-A` — HIGH, known-open, the Architect owes a repair story — and I am not re-reporting
it as new.** What is new is its cost, which no previous report states: the shared development
database is now in that state, so `/run-kaff-erp`'s smoke check fails, the E2E suite cannot run, and
`deploy/DEMO.md`'s path does not work against it. I ran E2E against a fresh database (`kaff_v905`,
migrated on boot, seeded by `scripts/seed-demo.ps1`) and got 11/11. Anyone who does not do that will
see 4 failures and reasonably blame the stories.

**`V-31-A` has stopped being theoretical.** That is the argument for the repair story, and it is
stronger now than when it was written.

### `V-33-G` — LOW · the runbook overstates a guarantee

`.claude/skills/run-kaff-erp/SKILL.md`: *"It also refuses to start at all if the PostgreSQL guards
are missing — see Gotchas."* `Program.cs` reads:

```csharp
if (missingGuards.Count > 0 && !app.Environment.IsDevelopment())
```

Development is exempt, and I watched the host log
`fail: Database guards are missing: accounts.enforce_non_negative on PROBE-UNFLOORED` and then
`Application started`. The exemption is defensible; the sentence describing it is not, and principle
9 makes this file the one place every agent learns how the stack behaves. `agents.md` §B0 repeats
the same claim.

The smoke check **did** catch it, loudly, which is the file's own point about `guardsInstalled` being
load-bearing. The machine worked; the prose beside it does not match.

Same file, same finding: §3 *"What the app actually is today"* still says **"One endpoint — `GET
/api/health` … One page — the status page at `/`."* There are now twenty-odd endpoints, a sign-in
screen, and four client screens. Routed to **Scrum Master** (the skill is shared infrastructure).

---

## 11. Closing gate

**Re-measured after every mutation was reverted.**

| Gate | Value |
|---|---|
| `git rev-parse HEAD` | **`c468e47d90a5785d22c8df41111c077afa33246b`** — unchanged |
| `git status --porcelain` | **empty — tree clean** |
| Build, `-c Release -warnaserror` | **0 warnings, 0 errors, exit 0** |
| Domain suite | **125/125, exit 0** |
| Api suite | **295/295, exit 0** |
| E2E suite | **11/11, exit 0** |
| `dotnet format --verify-no-changes` | **exit 0** |
| Citations | **1155 / 0 / 0, exit 0** |

**Seven mutations were applied and all seven reverted.** Each was verified applied before running
(`git diff --stat` or `--numstat`, never assumed), verified built by exit code, and for the .NET ones
verified by comparing assembly timestamps against the mutated source. `git status` is empty and
`src/` is byte-identical to `c468e47`. **No production code was changed and nothing was fixed** —
`agents.md` §7.

Databases touched, none of them repo content: `kaff_demo`'s sequence advanced by one (§7,
disclosed); `kaff_v905` created for the E2E run.

---

## 12. Criteria I could not reach

A criterion I could not check is a finding, not a silence.

| Criterion | Why not | Recorded as |
|---|---|---|
| `Kaff:ForwardedProxyHops` matches the real staging topology | No staging access. Not verifiable from this repository by any test — the mechanism is proved, the deployment is not | §8 |
| `AC-126-C` — both empty states rendered | No automated test exists, and `driver.mjs` launches a fresh browser per command, so an authenticated multi-step drive is not available outside `flow`. I confirmed the templates, both key pairs in both catalogues, and the two-state logic exist; I did **not** watch them render | `V-33-D` |
| `AC-126-F` — a 409 reopens the warning rather than reading as a failure | Same. The branch exists in `client-form-page.ts`; driving it needs a race between the blur check and the save, which nothing automates | `V-33-D` |
| `AC-126-L`, portal `Role.Client` half | Undrivable: `scripts/seed-demo.ps1` creates Owner, Hr, Finance and MarketingSales — **no `Role.Client` user exists** in the demo or staging data | `V-33-E` |
| 80 `[Verified:` markers in `.cs` / `.ts` | `scripts/check-citations.ps1` reads `*.md` only. The 1155/0/0 above therefore covers the Markdown corpus and not those 80 | Known-open, D-110 §5, still not cut as a story |
| `AC-125-C` | Nabil's, an unperformed check, not a defect | Known-open, unchanged |

---

## 13. Verdict per story

| Story | Verdict |
|---|---|
| **KAFF-119** — register a client | **Pass.** Code generated from a sequence, duplicate warns without blocking (D-049 ruling 8), acknowledgement recorded as an audit event keyed to the matched client rather than parsed out of prose. `Q57`'s gaps are Karim's, open, and confirmed real |
| **KAFF-121** — edit a client | **Pass.** §6.7 holds on the edit path under mutation; the kind/number pair is set together, which is the right shape for a rule about a pair |
| **KAFF-124** — find a client | **Pass.** Wildcard escaping asserted correctly after the rewrite; the three-state filter refuses an unknown value rather than defaulting it, which is the difference between an empty archive and a mistyped one |
| **KAFF-123** — archive a client | **Pass.** No delete, no unarchive, both asserted structurally |
| **KAFF-120** — an individual carries no rate | **Pass.** The strongest-evidenced story in the set |
| **KAFF-118** — everything is audited | **Pass.** The positive controls are real and the opt-out whitelist covers slice 3 before it exists |
| **KAFF-126** — the client screens | **Conditional.** `AC-126-L` half-discharged (`V-33-E`), `AC-126-C` and `AC-126-F` unasserted (`V-33-D`), and the guard's own mechanism unpinned (`V-33-C`) |
| **`51a0c5a`** — staging behind Caddy | **Conditional.** Mechanism correct and well tested; the deployment claim is unwitnessed (§8) |

**None of the seven stories is defective in what it built.** Every business rule I attacked held.
The findings are about what is *asserted*, which is the harder half and the half this pass exists
for.

---

## 14. The one thing Nabil should know

**Slice 1's gate is "permission tests pass". They pass, and for one role in nine they are asserting
nothing.**

Adding `Role.HeadOfDesign` to one catalogue row — one term, in a list, in a diff about something
else — hands a Head of Design every client record Kaff has, including the internal notes spec.md §12
says a client must never see, and the build stays clean, Domain stays 125/125, Api stays 295/295 and
E2E stays 11/11. I ran it. That is not a gap in coverage statistics; it is the acceptance criterion
for this slice reporting a safety that is not there.

The repair is small and it already exists in the same file: `Role.Client` and `Role.Subcontractor`
each have a catalogue-wide assertion that fails on any grant added anywhere. `Role.HeadOfDesign`
needs the same one sentence turned into a test — `PermissionCatalogue.cs` already **states** the
invariant, it just states it in a comment.

The uncomfortable part is why it was missed, and it is not carelessness. `HeadOfDesign` is
phase 2, so no story has ever needed it, so no test ever named it — and the coverage that exists is
coverage of the roles somebody was thinking about. Every other absence test in this slice has a
positive control, because D-116 taught that lesson eight days ago. The one assertion that has no
control is the one nobody knew they had written.

---

*Verifier, 2026-09-05. `HEAD c468e47`, tree clean at open and at close. Seven mutations applied,
seven reverted, nothing fixed.*
