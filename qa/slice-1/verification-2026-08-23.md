# Verification — slice 1, KAFF-116 and KAFF-106 · 2026-08-23

**Verifier Agent, fresh session.** Read `spec.md` §9 / §12, `CLAUDE.md`, `process/agile.md`, and the
two story files. Judged against the stories' acceptance criteria and `spec.md`, not against the
implementation's own intent.

**The Verifier reports. It does not fix.** Nothing in `src/` or `tests/` was changed. This file is
the only artefact created.

Every claim below carries `[Verified: 2026-08-23 @ `File` -> `Identifier`]` per SM-31.

---

## 0. Baseline — the gate before any test result is trusted (D-046)

No `Kaff.Api` process was running when the build ran, so nothing was locked and no suite ran against
a stale binary.

| Gate | Result |
|---|---|
| `dotnet build KaffErp.sln -c Release` | **0 warnings, 0 errors, exit 0** |
| `dotnet format --verify-no-changes` | **exit 0** |
| `Kaff.Domain.Tests.exe` | **75 / 75**, exit 0 |
| `Kaff.Api.Tests.exe` | **67 / 67**, exit 0 |
| `driver.mjs health` (API on 5080, after `docker start kaff-db`) | `200 {"status":"healthy","databaseReachable":true,"guardsInstalled":true,"missingGuards":[]}` |
| `scripts/check-citations.ps1` | **exit 1** — 408 checked, **0 broken**, **97 legacy line-number citations**. See §5.3 |

The migration applies and the column is live. Read back from the running database:

```
grant_path | character varying(64)
"ck_audit_records_grant_path" CHECK (grant_path IS NULL OR project_id IS NOT NULL AND grant_path::text <> 'None'::text)
trg_audit_records_append_only BEFORE DELETE OR UPDATE ON audit_records FOR EACH ROW EXECUTE FUNCTION kaff_reject_mutation()
```

Matches the source [Verified: 2026-08-23 @ `20260822210402_AuditGrantPath.cs` -> `AuditGrantPath`;
@ `AuditConfiguration.cs` -> `ck_audit_records_grant_path`;
@ `001_guards.sql` -> `trg_audit_records_append_only`].

Baseline matches the brief exactly.

---

## 1. KAFF-116 — every audit record says how the actor reached the project

**6 acceptance criteria, `AC-116-A` … `AC-116-F`. The brief's count is correct.**

### Verdict: all six satisfied.

| AC | Verdict | Evidence |
|---|---|---|
| `AC-116-A` assigned actor | **Satisfied** | [@ `PermissionMechanismTests.cs` -> `An_assigned_actor_leaves_the_assignment_path_on_the_record`] asserts **both** halves the criterion names — `ProjectId` = A **and** `Assignment` |
| `AC-116-B` the Owner leaves a trace | **Satisfied** | [@ `PermissionMechanismTests.cs` -> `The_owners_reach_is_named_although_it_leaves_no_row`] — the actor is `_ownerUnassigned`, so there is genuinely no row, and the record reads `OwnerGlobal` |
| `AC-116-C` HR distinguishable | **Satisfied** | [@ `PermissionMechanismTests.cs` -> `Hrs_global_reach_is_distinguishable_from_an_assigned_actors`] |
| `AC-116-D` portal action | **Satisfied**, and the corrected name is the one built | [@ `PermissionMechanismTests.cs` -> `A_portal_clients_record_names_the_portal_boundary`] asserts `PortalClient`, not the retired `ClientOfProject` |
| `AC-116-E` company-level carries none | **Satisfied**, twice over | Same request, second record: `companyWide.ProjectId` null and `GrantPath` null [@ `PermissionMechanismTests.cs` -> `The_owners_reach_is_named_although_it_leaves_no_row`]. And on the criterion's literal scenario — the Owner creating a user — [@ `CreateUserTests.cs` -> `The_creation_leaves_an_audit_record_naming_the_owner_the_role_and_the_department`] |
| `AC-116-F` cannot be added later | **Satisfied** | Raw SQL half: [@ `AuditMechanismTests.cs` -> `An_audit_record_cannot_be_changed_afterwards`], which drives a real `UPDATE audit_records` and asserts the trigger refuses it. API half: no endpoint reads or writes `audit_records` — the shipped route table is `GET /api/health` and `POST /api/users` and nothing else [Verified: 2026-08-23 — `IEndpoint` implementors under `src/Api/Features/` are `Health/GetHealth` and `Users/CreateUser`] |

### Are the four paths genuinely distinguishable on a written record?

**Yes, and the distinguishability is structural rather than incidental.** Four separate things hold
it:

1. Four distinct enum members exist and each is produced on its own branch
   [@ `ProjectAccessPolicy.cs` -> `GlobalReachAsync`, `ClientAccessAsync`, `AssignedAccessAsync`].
   The Owner and HR go through the same method but are handed different `path` arguments and
   different `AssignmentLevel`s, so the collapse the story warns about cannot recur silently.
2. The value is taken from the policy that admitted the request, not re-derived
   [@ `PermissionAuthorizationHandler.cs` -> `HandleRequirementAsync`, which calls
   `_auditContext.GrantedThrough(access.Path)`; @ `AuditContext.cs` -> `GrantedThrough`]. Rule 6 is
   met — there is one derivation.
3. The column is `character varying(64)` holding the member **name**, not an ordinal, so a row
   written today is readable in ten years without today's assembly.
4. Four tests, one per path, each through a real HTTP request against a real gate and each asserting
   a value read back from the database — not a stub [@ `ProbeEndpoint.cs` -> `WriteOwnerRoute`,
   `WriteHrRoute`, `WritePortalRoute`, `WriteAssignedRoute`, each of which performs the same audited
   write so there is a record to read].

### Can a refusal claim a grant path?

**No, at three independent layers.** This is the strongest part of the story.

- `Granted` is **derived** from `Path` rather than stored beside it
  [@ `PermissionEvaluator.cs` -> `record ProjectAccess`], so the contradiction is unrepresentable in
  the type.
- `GrantedThrough` is only reached inside the `decision == PermissionDecision.Granted` branch
  [@ `PermissionAuthorizationHandler.cs` -> `HandleRequirementAsync`].
- The database refuses `'None'` outright and refuses any path over a null project
  [@ `AuditConfiguration.cs` -> `ck_audit_records_grant_path`], asserted against the live database by
  [@ `AuditMechanismTests.cs` -> `A_grant_path_is_refused_without_a_project_and_may_never_be_None`],
  which drives raw SQL and therefore tests what happens when something other than our C# reaches the
  table. That is TC-1-135's stated reason for existing and it is honoured.

### Two latent weaknesses. Neither fails a criterion; both are the Architect's to rule on.

**W-1 — a company-wide permission declared with a project scope would hand `None` to the audit
context.** `PermissionEvaluator.Evaluate` returns `Granted` at the `PermissionScope.CompanyWide`
branch **before** it looks at `projectAccess` [@ `PermissionEvaluator.cs` -> `Evaluate`]. The gate
then calls `GrantedThrough(access.Path)` whenever `access is not null`, which for such an endpoint
could be `ProjectAccess.Denied` — i.e. `None`. The check constraint turns that into a failed save
rather than a corrupt row, so it fails loud, but it fails as a 500.

**Not reachable today**: every company-wide `RequirePermission` call uses the one-argument overload,
whose scope is `ProjectScope.None` [@ `PermissionPolicyProvider.cs` -> `RequirePermission`;
@ `Endpoint.cs` -> `Map`]. It becomes reachable the first time somebody writes
`.RequirePermission(someCompanyWidePermission, ProjectScope.FromRoute())`. The one-line guard is to
condition on `access.Granted` rather than on `access is not null`.

**W-2 — the grant path is paired with the *presence* of a project, not its *identity*.** The
interceptor writes `projectId is null ? null : _auditContext.GrantPath`
[@ `AuditSaveChangesInterceptor.cs` -> `ExtractProjectId`, and the `AuditRecord.For` call beside it].
If one request is authorised on project A and the same save touches an entity carrying `ProjectId` =
B, the record for B claims the path that admitted the caller to A. Nothing today produces that shape,
and no criterion covers it — but `AC-116-A` is worded as *"the audit record carries `ProjectId` = A
and grant path `Assignment`"*, and the code guarantees the pairing only when A is the only project in
the save.

### Built that no criterion asked for

- The i18n keys. All four exist in **both** catalogues [@ `ar.json` -> `audit.grant.portal_client`;
  @ `en.json` -> `audit.grant.portal_client`], matching the story's i18n bullet, and they cannot
  drift apart between catalogues [@ `TranslationCatalogueTests.cs` ->
  `The_two_catalogues_describe_the_same_set_of_keys`]. **However** — nothing pins a key to its enum
  member. The story claims they are *"named after the enum member so the pair cannot drift"*; rename
  `PortalClient` and no test goes red. Small, cheap to close, and it is the SM-30 shape of defect: an
  absence, invisible to a green suite.
- Four probe write routes in the test host [@ `ProbeEndpoint.cs` -> `WriteAssignedRoute`]. Test
  infrastructure, not shipped surface. Correct and necessary — the story is only observable on a
  record the gate admitted, and the four existing probes wrote nothing.

### Asked for and not built

Nothing. All six criteria and the i18n bullet are delivered.

**One process gap:** `decisions.md` has **no build entry for KAFF-116** the way KAFF-106 has D-066.
The column and the check constraint are referenced in passing by D-063 and D-064 but no entry records
the story as built or the choices in it. A new column plus a new check constraint on an append-only
table is structural, and `CLAUDE.md`'s Definition of Done requires the entry. **Backend's to close.**

---

## 2. KAFF-106 — the Owner creates a user with a role and a department

**11 acceptance criteria, `AC-106-A` … `AC-106-K`. The brief's count is correct.**

| AC | Verdict |
|---|---|
| `AC-106-A` | **Satisfied in part** — the sign-in half is not verifiable |
| `AC-106-B` | **Not satisfied** — the 403 holds; `errors.auth.forbidden` is never emitted |
| `AC-106-C` | **Satisfied** |
| `AC-106-D` | **Satisfied** |
| `AC-106-E` | **Satisfied**, and wider than asked |
| `AC-106-F` | **Satisfied** — the brief is wrong that this is unmapped |
| `AC-106-G` | **Satisfied** |
| `AC-106-H` | **Not satisfied — nothing is built** |
| `AC-106-I` | **Satisfied** |
| `AC-106-J` | **Not verifiable — out of scope for an API story, and correctly so** |
| `AC-106-K` | **Satisfied, at the right level** |

### `AC-106-K` — the one the brief asked me to look at hardest

**Satisfied, and the level is genuinely the endpoint's, not the domain's.**

The criterion's whole point is that a handler which never calls `Create` — or one that "helpfully"
corrects the department before it does — bypasses a domain guard that stays green. I checked the
level three ways rather than trusting the outcome.

1. **The refusal reaches the caller through HTTP.**
   [@ `CreateUserTests.cs` -> `An_hr_user_cannot_be_created_outside_hr_at_the_endpoint`] drives
   `POST /api/users` four times — `Finance`, `Marketing`, `Operations`/`Administrative`, and **no
   department at all** — and asserts 400, the message key
   `errors.identity.hr_role_requires_hr_department`, **and that no user exists afterwards**. All four
   placements the criterion names are covered, including the null one, which is the case a naive
   guard misses. The null case is genuinely refused because the domain condition is
   `role == Role.Hr && department != Department.Hr`, and null satisfies it
   [@ `User.cs` -> `ValidateDepartment`].
2. **The file cannot silently degrade into a domain test.** Every case in `CreateUserTests` goes
   through `_client.SendAsync` against a real host with the real gate
   [@ `CreateUserTests.cs` -> `CreateAsync`]. There is no direct `User.Create` call anywhere in it.
3. **The handler earns it structurally.** It passes `request.Department` through untouched and
   returns whatever `Create` says [@ `Handler.cs` -> `HandleAsync`], and the validator deliberately
   does **not** restate the rule [@ `Validator.cs` -> `ValidateAsync`]. A second copy in the
   validator is what would eventually disagree with the entity.

The second half of the criterion is also covered — an HR user in `Department.Hr` is created normally
[@ `CreateUserTests.cs` -> `An_hr_user_in_the_hr_department_is_created_normally`], which is TC-1-060
and the reason the constraint is not "HR may hold no department".

The domain assertion the story pins still exists and is not the thing being relied on
[@ `CatalogueCompletenessTests.cs` -> `An_hr_user_cannot_be_placed_in_another_department`].

**D-066 §2 records a mutation run** — a helpful-correction line was inserted; the endpoint test went
red on 201 with the HR user in Finance, and the domain test stayed green. I did not re-run the
mutation, but the two tests exist and are at the two different levels the entry describes, which is
the checkable half of the claim.

### `AC-106-C` — HR refused

**Satisfied, both halves.**

- HR is refused at the endpoint with 403, inside the six-role sweep
  [@ `CreateUserTests.cs` -> `Nobody_but_the_owner_can_create_a_user`], and separately
  [@ `PermissionMechanismTests.cs` -> `Only_the_owner_administers_users`].
- *"HR's ability to assign existing users to projects is unaffected"* — this is the half that would
  be missed if the refusal had been implemented by removing HR's grant, and it is held
  [@ `PermissionMechanismTests.cs` -> `Hr_staffs_a_project_it_was_never_assigned_to`] and, sharper,
  [@ `PermissionMechanismTests.cs` -> `Hr_reaches_every_project_and_can_read_none_of_them`], which
  asserts `ProjectAssignmentManage` succeeding and `ProjectRead` refusing on the same project one
  line apart. That is TC-1-053, satisfied at the probe level; there is no real assign endpoint yet
  (KAFF-113).

### The three the brief could not map — my independent read

**`AC-106-F` (a client user must name a client) — the brief is wrong. It is mapped, and to both
halves.**
[@ `CreateUserTests.cs` -> `A_client_user_names_a_client_and_nobody_else_does`] drives the endpoint
twice: a `Role.Client` with no client id → 400 `errors.identity.client_user_requires_client`, and a
Finance user given a client id → 400 `errors.identity.non_client_user_cannot_carry_client`. That is
the criterion verbatim, and TC-1-058. **Satisfied.** I suspect the brief missed it because the test
name does not contain "F" or "client_requires" — it reads as prose.

**`AC-106-H` (the temporary password is not a permanent one) — correctly unmapped, and genuinely
nothing is built. Not satisfied.**

This is not "untested"; it is absent. Three things the criterion needs, none of which exist:

- No sign-in endpoint and no change-password endpoint. Confirmed against the running API: an
  unauthenticated `POST /api/users` returns **401 with no `messageKey`**, and there is no route to
  authenticate against.
- **No gate anywhere reads `MustChangePassword`.** The property is set and stored
  [@ `User.cs` -> `SetTemporaryPassword`] and returned in the response
  [@ `Response.cs` -> `MustChangePassword`], and nothing consults it
  [Verified: 2026-08-23 — the only reads of `MustChangePassword` under `src/` are the property
  declaration, its EF configuration, migration snapshots, and the response DTO].
- **`errors.auth.password_change_required` exists in no file in the repository**
  [Verified: 2026-08-23 — the string is absent from `src/`, `tests/` and both locale catalogues].

**D-066's "Not done" section states this plainly and correctly**, and the flag being *stored* is the
right half to have built now — the value cannot be backfilled onto accounts created before the gate
exists. I record `AC-106-H` as **not satisfied and correctly deferred**, and the note that matters
for the next session is D-066's own: *"the gate that reads it is not built and must not be assumed to
exist."* The criterion belongs to KAFF-101a / KAFF-103 and should be re-verified there, not signed off
here.

**`AC-106-J` (Arabic, RTL, at mobile width) — not verifiable. Out of scope for an API story. I am not
scoring it as failed, and I am not scoring it as passed.**

There is no screen. `src/Web/src/app/features/` contains exactly one feature, `status`
[Verified: 2026-08-23 — the only component files under `src/Web/src/app/features/` are
`status-page.ts`, `status-page.html`, `status-page.css`]. The `users.*`, `enum.Role.*`,
`enum.Department.*` and `enum.OperationsSubDepartment.*` keys the story's i18n bullet lists **are not
in either catalogue** [Verified: 2026-08-23 — `en.json` holds 104 keys; none begins `users.` or
`enum.`]. `src/Web` was touched only for the two error keys.

This is Frontend's, with the screen, and D-066 says so. **Status: unverified, blocked on the UI. It
must not be counted toward the story's completion, and KAFF-106 must not be marked Done on an
11-of-11 reading.**

### The one real defect: `AC-106-B`'s second half

**`AC-106-B` is not satisfied.** The criterion reads *"every attempt is refused with 403 **and
`errors.auth.forbidden`**, and every refusal is logged."* TC-1-050 says the same.

- The **403 holds** for all six roles named — Finance, Technical Office, Site Engineer, Marketing, HR
  and a portal Client [@ `CreateUserTests.cs` -> `Nobody_but_the_owner_can_create_a_user`], and each
  attempt is asserted to have created nothing.
- **`errors.auth.forbidden` is never emitted.** The error constant exists
  [@ `SeparationOfDuties.cs` -> `AuthorizationErrors`] and has **no production caller**
  [Verified: 2026-08-23 — the only references to `SeparationOfDuties.` outside its own file are three
  lines in `PermissionEvaluatorTests.cs`, all calling `EnsureDifferentActor`]. An authorization
  refusal is produced by the ASP.NET pipeline, not by `ResultExtensions.Problem`, so the body is a
  bare `ProblemDetails` with no `messageKey` extension. Confirmed empirically on the running API
  against the analogous 401:
  `{"type":"…","title":"Unauthorized","status":401,"traceId":"…"}` — no `messageKey`
  [@ `Program.cs` -> `AddProblemDetails`, and there is no `StatusCodePages` or forbidden handler].
- The test asserts only `HttpStatusCode.Forbidden`. **It cannot fail on the missing key**, which is
  why a green suite reports this criterion as met. This is precisely the class of thing the brief
  asked me to go behind.

**The consequence is user-visible, not cosmetic.** `CLAUDE.md` requires no hardcoded user-facing
strings; a 403 with no key gives the SPA nothing to translate, so the Arabic UI has no refusal message
to show. The key already exists in both catalogues [@ `en.json` -> `errors.auth.forbidden`] — only the
emitter is missing.

**Routed to Backend.** The smallest fix is one `Fail`/result mapping on the authorization pipeline;
the test that closes it asserts the `messageKey` alongside the status inside the existing six-role
loop.

*"Every refusal is logged"* **is** satisfied, at the log level rather than the audit level: the gate
logs the permission, the user, the project and the `PermissionDecision`
[@ `PermissionAuthorizationHandler.cs` -> `HandleRequirementAsync`], and the Api suite's own output
carries lines of exactly that shape —
`Refused FinancialMovementApprove for user … on project …: NotAssignedToProject.` That is TC-1-051.
No automated assertion covers it; nothing in `spec.md` §9 requires a refusal to write an *audit
record*, and none is written.

### The rest, briefly

- **`AC-106-A`** — satisfied except the sign-in clause. The user exists, is active, carries the
  right role and department, and the password is hashed and forces a change
  [@ `CreateUserTests.cs` -> `The_owner_creates_a_finance_user`,
  `The_password_the_owner_sets_is_temporary_and_is_not_stored_as_typed`]. The audit half is fully
  covered, including that the after state carries **role and department** and that `PasswordHash` and
  `SecurityStamp` are redacted
  [@ `CreateUserTests.cs` -> `The_creation_leaves_an_audit_record_naming_the_owner_the_role_and_the_department`].
  *"Can sign in only to change that password"* is `AC-106-H`'s clause and is unbuilt — see above.
- **`AC-106-D`** — satisfied [@ `CreateUserTests.cs` -> `An_operations_user_must_carry_a_sub_department`],
  plus the inverse, TC-1-055 [@ `CreateUserTests.cs` -> `Only_operations_users_may_carry_a_sub_department`].
- **`AC-106-E`** — satisfied and **wider than the criterion asked**: the test covers
  `Role.Subcontractor` as well as `Role.Client`
  [@ `CreateUserTests.cs` -> `An_external_role_cannot_be_given_a_department`]. That is TC-1-057 and
  it is the right call — D-035's hole is open under either role.
- **`AC-106-G`** — satisfied, and the enforcement is at two levels: a pre-check plus the unique index,
  with the race's loser mapped to the same refusal rather than a 500
  [@ `Handler.cs` -> `IsUserNameCollision`;
  @ `CreateUserTests.cs` -> `A_username_cannot_be_taken_twice_in_a_different_case`]. Built under the
  D-062 §1 waiver; **Q51 remains open** and this verification does not close it.
- **`AC-106-I`** — satisfied, plus the boundary below it
  [@ `CreateUserTests.cs` -> `Eight_lower_case_characters_are_accepted_as_a_temporary_password`,
  `Seven_characters_are_refused`]. The eight is Karim's number in one place
  [@ `User.cs` -> `MinimumPasswordLength`] and no complexity rule is applied.

### Built that no criterion asked for

**`Request.TemporaryPassword` is optional** [@ `Request.cs` -> `TemporaryPassword`], so the Owner can
create an account with **no credential and no forced change**. D-066 §7 raises this honestly and
argues it from S-007 and rule 10. I agree it is defensible and I am flagging it anyway, because it is
the shape of thing that becomes a rule by accident: **no acceptance criterion describes creating a
user without a password, and nothing refuses it.** The reading is Karim's to confirm, not the
implementation's to settle. **Route to the BA → Nabil.**

Two smaller ones, both fine: `Seven_characters_are_refused` (the inverse of `AC-106-I`, from D-049
ruling 3), and the `Role.Subcontractor` arm of `AC-106-E`.

Nothing in the slice adds a stored balance, mutates a posting, uses floating point, or touches
anything on the out-of-scope list.

### Asked for and not built

`AC-106-B`'s message key · `AC-106-H` entirely · `AC-106-J` and the `users.*` / `enum.*` keys ·
no user read endpoint (correctly — Q42's projection warning stands).

---

## 3. The `PasswordHasher` hole — confirmed, and it is real

**I agree with the brief. `PasswordHasher` is named in no test.**
[Verified: 2026-08-23 — the identifier appears in exactly two files, `PasswordHasher.cs` itself and
`Handler.cs`, and in no file under `tests/`.]

**Why it is a real hole and not a pedantic one.** All three security parameters are `private const`
[@ `PasswordHasher.cs` -> `Hash`]. Nothing outside the file can read them, nothing asserts them, and
each can be weakened in a one-character diff that builds clean and turns **no test red**:

| Weakening | Diff | Suite result |
|---|---|---|
| `Iterations` 600_000 → 1 | one line | green |
| `SaltBytes` 16 → 0 (or a `static readonly` constant salt) | one line | green |
| `HashBytes` 32 → 4 | one line | green |
| PBKDF2 → a single SHA-256 pass | a few lines | green |

The brief's diagnosis is exactly right: the one existing assertion —
`created.PasswordHash.Should().NotContain(Password)`
[@ `CreateUserTests.cs` -> `The_password_the_owner_sets_is_temporary_and_is_not_stored_as_typed`] —
survives every row of that table. It is a good test of the *handler* (it proves the plaintext never
reaches the column) and it is not a test of the *primitive*.

**And the exposure is larger than one story.** KAFF-101a's verifier must read the parameters back out
of the stored string, so the format is a contract between two features written in different sessions.
Nothing pins the format either.

### The smallest test that closes it — one `[Fact]`, no fixture, no database

Do not assert "the output differs from the input". Assert the **stored form's four fields**, which
the format deliberately exposes:

1. **The prefix is `pbkdf2-sha256`** and the string splits on `$` into exactly four parts — pins the
   format KAFF-101a will parse.
2. **The stated iteration count parses as an integer and is ≥ 600_000** — catches a lowered constant.
3. **The salt field Base64-decodes to 16 bytes and the hash field to 32** — catches a truncated salt
   or a weakened derivation length.
4. **Hashing the same password twice yields two different strings** — catches a constant salt. This
   is the assertion the brief's strawman test is missing, and it is one line.

**One thing that assertion set still cannot see, and it is worth one more line.** Points 1–4 assert
the *label*; a change that writes `600000` into the string while deriving with one iteration passes
all four. Close it by **recomputing**:

> take the salt and the iteration count parsed out of the stored string, run
> `Rfc2898DeriveBytes.Pbkdf2(password, salt, statedIterations, SHA256, 32)`, and assert the result
> equals the stored hash bytes.

That is three lines, needs no new dependency, and proves the stored hash is the one the stated
parameters actually produce — which is also, exactly, what KAFF-101a's `Verify` will have to do. It
turns the test into a specification of the verifier rather than a duplicate of the hasher.

**Cost:** ~5 PBKDF2 runs at 600k iterations, roughly one second. Acceptable for a security primitive.

**Placement:** `tests/Api.Tests/` — `Kaff.Infrastructure` is not referenced by `Domain.Tests`, and no
new test project should be added for this.

**This is Backend's to write. I have not written it.**

---

## 4. Errors in the brief

Per principle 7. Four, one of which changes a conclusion.

1. **"Three criteria I could not map to a test … `AC-106-F`" — wrong. `AC-106-F` is mapped**, to both
   of its halves, by [@ `CreateUserTests.cs` -> `A_client_user_names_a_client_and_nobody_else_does`].
   It is two, not three. This matters: it was heading for a "not verified" that would have been false.
2. **"Api tests covering the four grant paths" — right, but not where implied.** The brief lists them
   alongside the migration; they live in `PermissionMechanismTests.cs`, not `AuditMechanismTests.cs`,
   and the file explains why (the value is produced by the gate, and that fixture already seeds four
   kinds of actor). `AuditMechanismTests.cs` carries only the constraint test. No defect — but an
   agent looking for them in the audit file will not find them.
3. **The brief did not mention that `AC-106-B` is only half-implemented.** It named `AC-106-K` and
   `AC-106-C` as the two to look hardest at; both pass. The one that fails is `AC-106-B`, which the
   brief treated as covered. That is the brief's own point about green suites, landing on the brief.
4. **KAFF-106's story file still reads `**Status:** Ready`**, not `BUILT — awaiting verification`,
   while KAFF-116's was updated. Cosmetic, but SM-29's subject: the file asserts a state in the
   present tense and it is now wrong. **BA's to close.**

Everything else in the brief checked out: both AC counts, the baseline figures, the migration name,
the `PasswordHasher` gap, D-066's existence, and the `MSB3021` lock behaviour (no API was running, and
the build was clean).

---

## 5. Findings, routed

| # | Finding | Severity | Owner |
|---|---|---|---|
| **V-A** | `AC-106-B` / TC-1-050: a 403 from the authorization gate carries no `messageKey`. `AuthorizationErrors.Forbidden` has no production caller; the Arabic UI has no refusal string to render. The existing test asserts the status only and cannot fail on this | **Defect** | **Backend** |
| **V-B** | `PasswordHasher` has no test. Iterations, salt length and hash length are all one-character weakenings that keep the suite green. §3 names the smallest test | **Defect** | **Backend** |
| **V-C** | W-1: the gate calls `GrantedThrough` on `access is not null` rather than `access.Granted`. A company-wide permission declared with a project scope would pass `None` and 500 on the check constraint. Not reachable today | Latent | **Architect** → Backend |
| **V-D** | W-2: the grant path is paired with the presence of a `ProjectId`, not its identity. A save touching a second project would mislabel that project's record | Latent | **Architect** |
| **V-E** | No `decisions.md` build entry for KAFF-116, though it added a column and a check constraint to an append-only table. DoD requires one | Process | **Backend** |
| **V-F** | Nothing pins an `audit.grant.*` key to its `ProjectAccessPath` member. The story claims the pair "cannot drift"; renaming the enum member turns nothing red. SM-30's shape — an absence | Coverage | **QA** → Backend |
| **V-G** | TC-1-132 still names `ClientOfProject`. The story corrected this to `PortalClient` on 2026-08-22 under SM-29; `qa/slice-1/test-cases.md` was not updated with it | Stale | **QA** |
| **V-H** | TC-1-047 expects `PasswordHash` **null** on creation and TC-1-048 expects a sign-in refusal. The first contradicts `AC-106-A` under D-049 ruling 4; the second is unexecutable — there is no sign-in. Already raised by D-066 §7 and confirmed here | Stale | **QA** |
| **V-I** | `TemporaryPassword` is optional, so an Owner may create a credential-less account. Defensible and defended in D-066 §7, but no criterion describes it and nothing refuses it | Question | **BA** → Nabil → Karim |
| **V-J** | `scripts/check-citations.ps1` exits 1: **97 legacy line-number citations**, 0 broken identifiers. **None is in either story, in `decisions.md`, or in any file this work touched** — they are in `meetings/2026-08-21-sprint-1-refinement.md` (76), `qa/slice-1/test-cases.md` (11), `stories/questions-for-karim.md` (6), `qa/questions.md` (4). The DoD line *"check-citations.ps1 passes"* cannot be ticked for **any** story until this pre-existing debt clears | Process | **Scrum Master** |
| **V-K** | KAFF-106 story file reads `Status: Ready` though the story is built | Stale | **BA** |

---

## 6. Recommendation

**KAFF-116 — accept.** Six of six criteria satisfied, the four paths are distinguishable by
construction rather than by convention, and a refusal cannot claim a path at three independent
layers. V-C, V-D, V-E and V-F are follow-ups, not blockers.

**KAFF-106 — do not accept as complete.** Eight of eleven criteria satisfied, including the two the
brief singled out. One is a **defect** (`AC-106-B`, V-A), one is **unbuilt and correctly deferred to
KAFF-101a / KAFF-103** (`AC-106-H`), and one is **unverified pending the UI** (`AC-106-J`).

Accept `AC-106-A` and `AC-106-C` through `AC-106-K` minus `AC-106-H`; hold the story open on V-A,
which is a one-line fix plus one assertion inside a loop that already exists. **`AC-106-J` must be
carried forward explicitly rather than tacitly passed** — the temptation on a green suite is to read
11 of 11.

**V-B is independent of both stories and should not wait for either.** It is the only finding here
that concerns a security primitive.

---

*Verified 2026-08-23 in a session that wrote none of this code. Nothing was fixed. All figures are
from runs made today, through `/run-kaff-erp`, against a build whose exit code was checked first.*
