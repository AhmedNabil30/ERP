# qa/strategy.md — how Kaff ERP is tested

For the whole product, not just slice 1. `spec.md` is the business truth; `CLAUDE.md` fixes the four
suites and their order; this file says what goes in each, at which layer, why, and how we know a test
is capable of failing.

**The one sentence:** *the question is never "does this pass?" — it is "what would this look like if
the thing it checks were broken?"* (`decisions.md` D-046).

---

## 1. The four suites, in `CLAUDE.md`'s priority order

The order is not arbitrary. It is the order in which a failure costs Kaff money.

### 1.1 Money — highest priority

**What belongs here:** the `spec.md` §15 worked example asserted end to end, and the five invariants
that go with it.

| Invariant | Source |
|---|---|
| Accumulated hold = exactly 20% of contract value | §15, §5.1 |
| The client advance ledger reaches exactly zero, never negative | §15, §6.4.1 |
| تشوينات in equals تشوينات recovered | §15, §6.4 |
| Total client cash equals contract value exactly | §15 |
| No posting sequence can produce a negative safe balance | §6.1, §15 |

Plus the prohibitions that make those invariants reachable: no stored balance column; no update or
delete path on a posting; the five ledgers never netted; nothing leaves the hold before handover;
`decimal(18,4)` with EF precision configured on every money property.

**Which slice:** the §15 fixture lands in slice 3 as `KAFF-300`, *"present and failing before anything
else is built"*. That ordering is the point — a fixture written after the calculator is a
transcription of the calculator.

**Slice 1 contains no money.** What slice 1 owes the money suite is negative: **prove that nothing in
identity, assignment or the Client master stores or exposes a money value.** `TC-1-046`, `TC-1-128`,
`TC-1-156` and `TC-1-157` are that proof, and they are cheap now and impossible to retrofit once a "contract
value" column has been added to a list response for convenience.

### 1.2 Permissions — slice 1's gate

**What belongs here:** *"one test per role asserting what it cannot reach, hitting endpoints directly
rather than through the UI"* (`CLAUDE.md`). Nine roles against every permission in
`Permission.cs`. The expected outcome per cell is `qa/slice-1/permission-matrix.md`.

Three things are being tested, and they fail independently:

1. **Capability** — does the role hold a grant at all (`PermissionEvaluator`, Domain layer).
2. **Reach** — is the caller allowed on *this* project (`IProjectAccessPolicy`, Api layer, needs the
   database). §9: *"Permission = role × assignment. Role alone is insufficient."*
3. **Liveness** — is the token's account still active and still holding the role it claims. A token
   describes the moment it was issued; the database describes now.

A permission suite that tests only (1) is the most common way to ship a hole, because (1) is the part
that is pure and pleasant to test. **A cell in the matrix is not covered until it has been exercised
through a route.**

**Negatives outnumber positives here, deliberately.** One case proving Finance *can* prepare a
movement is worth less than eight proving Finance cannot approve one, cannot approve a change order,
cannot gate quantities, cannot read another project, cannot mint a user, cannot staff a project,
cannot reach the portal surface, and cannot read a client's internal notes.

### 1.3 State machines

**What belongs here:** every legal transition and — the half that matters — every illegal one, for
each machine in `spec.md` §13: Opportunity, Project, Extract, Design phase, Change order. Plus the
smaller lifecycles slice 1 owns: user active ↔ inactive, assignment active ↔ revoked, client active ↔
archived.

Three rules that hold across all of them:

- **Any rejection returns to origin with a stored reason** (§7). A silent step-back, or a rejection
  with a null reason, is a defect even if the state is right.
- **Doing a transition twice is refused, not absorbed.** Deactivating an inactive user, revoking a
  revoked assignment, archiving an archived client — each returns a named error. Silent idempotence
  hides a double-click and hides a bug.
- **A terminal guard is a guard.** A stopped project issues no extract (§7). A closed period accepts
  no posting (§6.6). These are tested by attempting the illegal act and asserting the refusal, never
  by asserting the happy path and inferring the rest.

### 1.4 End-to-end

**What belongs here:** the slice's demo script, in Playwright, against the running stack — the exact
script Nabil runs at acceptance (`process/agile.md` §4). Slice 1's is at the bottom of
`ux/slice-1-flows.md` and is reproduced as `qa/slice-1/hls.md`.

Plus the two presentation guarantees `CLAUDE.md` makes non-negotiable:

- **Arabic RTL at 390px, with no horizontal overflow on the body.** RTL is the primary direction, not
  a mirror.
- **No hardcoded user-facing strings.** Every visible string resolves through i18n; a raw key on
  screen is a failure, and so is an untranslated literal.

**E2E is the thinnest suite on purpose.** It is the slowest to run, the most brittle, and the worst
place to discover a permission bug. A rule that can be proved at the Api layer is proved there and
merely *demonstrated* end to end.

---

## 2. Which layer tests which kind of rule

| Rule enforced by | Tested at | Why not lower |
|---|---|---|
| A pure function (`PermissionEvaluator`, a `Money` calculation, a `Result<T>` validation) | **Domain** | Nothing else needed. Exhaustive, fast, no fixture. |
| An entity invariant (`Client.Create` refusing withholding on an individual) | **Domain**, and again at **Api** | Domain proves the rule lives on the entity; Api proves the endpoint actually reaches it. `CLAUDE.md`: *"A validator guards one endpoint; the invariant belongs to the entity."* |
| Role × assignment through a route | **Api**, real PostgreSQL | The assignment is a database row and the project comes from the URL. Half the mechanism only exists in the pipeline. |
| A database constraint, trigger, unique index or view | **Api**, real PostgreSQL, **and often by raw SQL** | See §3. |
| Audit records written by the interceptor | **Api**, real PostgreSQL | The interceptor runs on `SaveChangesAsync`. D-041 is the entry that proves reading it is not enough. |
| Layout, direction, i18n resolution | **E2E**, Playwright at 390px | Nothing below the browser can see a horizontal overflow. |

### The Domain/Api pairing is not duplication

For a rule that must hold at both levels the pair answers two different questions:

- *Does the rule exist where it belongs?* — Domain.
- *Is it on the path a real request takes?* — Api.

A rule that passes Domain and fails Api is a wiring bug. A rule that passes Api and fails Domain is a
rule implemented in a validator, which the next endpoint will not have.

---

## 3. Why a database-enforced rule cannot be tested against a fake provider

`spec.md` §6.1 does not say "check the balance before you post". It says **"Enforce in the database,
not only in application code."** The distinction is the whole design:

- Application code guards **the paths we wrote**. A migration, a support script, a psql prompt at
  2am, an `ExecuteUpdate`, a future handler written by an agent with no memory of this rule — none of
  them pass through it.
- A constraint or trigger guards **the table**. That is the point of putting it there.

**A fake provider — the EF Core in-memory provider, or SQLite in memory — has no triggers, no
`CHECK` constraints, no partial unique indexes, no `decimal(18,4)`, no advisory locks, and no view.**
A test of `safe balance MUST NOT go negative` against the in-memory provider therefore asserts on
application code that the test itself set up. It passes with the trigger, and it passes with the
trigger dropped. It is D-046's "green result that was not evidence of anything", in the most
expensive place in the system.

**Concretely, the following are untestable anywhere but real PostgreSQL:**

| Rule | Mechanism |
|---|---|
| The safe never goes negative (§6.1) | trigger + `accounts.enforce_non_negative` |
| Postings are append-only (§6.1) | `UPDATE`/`DELETE` trigger |
| Audit records are append-only (`CLAUDE.md`) | `UPDATE`/`DELETE` trigger |
| The five ledgers never net (§6.4) | posting guard |
| Nothing leaves the hold before handover (§5.1) | posting guard 3 / 3b |
| A posting cannot land in a closed period (§6.6) | posting guard |
| A reversal must mirror its original (§6.1) | posting guard |
| Account configuration is frozen after creation | guard 3c |
| Clients are deduplicated by phone (§2) | unique index on `phone_normalised` |
| Usernames are unique, case-insensitive | unique index |
| One active assignment per user per project | partial unique index |
| Balances are derived, not stored (§6.1) | the `account_balances` view |
| Money is `decimal(18,4)` and does not truncate | column type |

**Several of these must be attacked by raw SQL**, going around the domain entirely, because the
question they answer is "what happens when something other than our C# reaches this table". The
existing `TreasuryGuardTests` already does this and is the pattern to follow. A guard test that only
goes through the entity proves the entity, not the guard.

**Rule for this project:** *if the rule's enforcement is in a migration, the test runs against
PostgreSQL and at least one of its cases bypasses the domain.*

The corollary is an operational one, and it is why `docker-compose.yml` exists: **a developer without
a database cannot run the suites that matter.** That is accepted. It is better than a suite that
appears to run without one.

---

## 4. Test data strategy

**Principles**

1. **Fixtures come from `spec.md`, not from convenience.** The §15 numbers — 1,000,000 contract,
   250,000 advance, 20% hold, 75,000 تشوينات, three extracts of 300/300/400 — are *acceptance
   criteria, not illustrations* (§15). No test rounds them, scales them or invents a fourth extract.
2. **One database per run, seeded per test.** `PostgresDatabase` is a collection fixture; xUnit builds
   a fresh class instance per method, so seeding runs on every method against a shared database.
   Every unique-indexed value therefore needs process-wide uniqueness.
3. **`UniqueNames`, never `Random`.** D-046: nine thousand random suffixes drawn ninety times a run is
   a ~5% chance of a spurious failure — and for a phone number a collision *merges* two seeded
   clients instead of rejecting them, which is quieter and worse. Usernames, client codes, project
   codes and phone numbers all go through `UniqueNames`.
4. **Deterministic time.** Tests pin `Now` to a fixed `DateTimeOffset`. Nothing asserts on
   `DateTimeOffset.UtcNow`, and nothing depends on the order two tests happen to run in.
5. **Arabic in the data, not only in the assertions.** Client and project names are Arabic in the
   fixtures, because a name that is pure Latin never exercises the bidi, collation and normalisation
   paths that Kaff's real data will.
6. **A negative case seeds the minimum.** A test proving Finance cannot approve seeds Finance and a
   project and nothing else. A fixture that seeds "everything" hides which fact made the test pass.
7. **Never seed through raw SQL when the domain can do it** — except in a guard test, where going
   around the domain *is* the test.

**Roles fixture.** Permission work needs the same cast repeatedly, and it must include the awkward
members: an Owner **with no assignment row**, an HR user **with no assignment row anywhere**, a Site
Engineer who is Supervisor on one project and Junior on another, a portal Client belonging to project
A while project B belongs to someone else, and a deactivated user. The existing
`PermissionMechanismTests.SeedAsync` is most of this and its comment on the HR user — *"an assignment
row here would make those tests pass for the wrong reason"* — is the standard.

**What is never in test data:** a real person's phone number, a real client name, or a credential
that also works anywhere else.

---

## 5. How we know a test can fail

This section is the reason the file exists.

### The discipline

**For every case, before it is trusted: remove the rule, run the case, and confirm it goes red.**
Then put the rule back. If the case stays green, the case is worthless and must be rewritten or
deleted — it is not "extra safety", it is a false report of safety.

The mutation is specific to the rule, not generic:

| Rule | The mutation that must turn it red |
|---|---|
| Only the Owner holds `UserManage` | add `finance` to the grant list |
| HR holds no `ProjectRead` | add `hr` to the `ProjectRead` grants |
| An individual client cannot withhold | delete the check from `Client.Create` |
| The safe cannot go negative | `UPDATE accounts SET enforce_non_negative = false` on the safe |
| Postings are append-only | drop the trigger |
| A deactivated user is refused | remove the `IsActive` check in `ProjectAccessPolicy` |
| Assignment is required | make `AssignedAccessAsync` return granted |
| Hold releases only at handover | drop guard 3 |

**Write the mutation down.** Every case in `slice-1/test-cases.md` carries a **`Fails if:`** line,
and that line *is* the mutation, stated as the defect the case catches. A case whose `Fails if:` line
cannot be written is a case that cannot fail, and it does not get written.

### Why this is not paranoia — D-046, the worked example

Three findings in one afternoon, all of them green results that were not evidence of anything:

1. **`dotnet test` ran nothing and had never run anything.** Exit code 5, `Zero tests ran`, ~200ms.
   Every green result in D-043 had come from invoking the executables directly. CI's first push would
   have failed — or worse, been "fixed" by ignoring the exit code.
2. **A schema test asserted on a value that was always null.** `Enum_columns_are_stored_as_text` read
   `GetProviderClrType()`, which reports only an *explicitly configured* provider type and is null
   when the conversion comes from a `ValueConverter` — as ours does. Written with `?.` it would have
   succeeded against null forever. *The underlying behaviour was always correct; the test proving it
   was not testing anything.*
3. **The E2E suite skipped all four tests and exited 0.** `[E2EFact]` skips itself when
   `KAFF_E2E_BASE_URL` is unset. Its own comment said CI sets the variable — and nothing enforced it.
   Drop the variable from the workflow and the gate silently stops being a gate while the job turns
   green.

And a fourth, adjacent: **D-041**, where the audit interceptor threw on first use, so *every state
change in the system would have failed* — while the build was clean, `dotnet format` was clean, and
51 tests passed against a component that could not execute once.

The pattern is one shape. The defence is one habit: **make it red on purpose, once, before you
believe it.**

### Three specific anti-patterns, banned here

- **`?.` in an assertion path.** `x?.Should().Be(y)` succeeds when `x` is null. Assert the thing
  exists, then assert its value — the exact fix D-046 applied.
- **A conditional skip with no enforcement.** Any `Skip` that depends on the environment must be
  paired with an unconditional test that fails when `CI=true` and the suite is unconfigured, as
  `SuiteConfigurationTests` now does.
- **Asserting a count rather than an identity.** "Two audit records exist" passes when both are the
  wrong record. Assert action, actor, entity and changed properties.

### The Verifier's obligation

`process/agile.md`'s Definition of Done requires *"every QA test case for the story executed, with
its result recorded — including the ones that failed and why they now pass"*. Add to that: **a case
that has never been observed red is reported as unconfirmed, not as passed.** Applying the mutation
is cheaper than the afternoon D-046 cost.

---

## 6. What the strategy deliberately does not cover

- **Load, soak and performance.** Kaff is one contractor's office. Nothing in `spec.md` states a
  performance requirement, and inventing one is inventing a rule.
- **Penetration testing.** The specific security properties `spec.md` does state — server-side
  enforcement, no account enumeration on login, no portal leakage, append-only evidence — are tested
  as functional cases and are P1. General security testing is not QA's to scope.
- **Anything on the out-of-scope list** (`spec.md` §1, `CLAUDE.md`). A test for a tax module, a
  manual journal entry, multi-currency conversion or a posting-edit endpoint would legitimise a
  feature that must not exist. **The correct test for those is the reverse: assert no such endpoint
  exists** — `TC-1-118` and `TC-1-183` are that shape, and slice 8's `KAFF-811` (a reflection test
  failing the build if anything cost-shaped is reachable from a portal response) is the same idea at
  its strongest.
