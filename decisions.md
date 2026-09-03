# decisions.md — Kaff ERP

**Status:** authoritative record of *why*. `spec.md` is the business truth, `CLAUDE.md` is the rules,
this file is the reasoning. Read it before proposing a change to anything it covers.

Format for every entry: **Decision · Why · What we rejected and why · What would make us revisit.**

Entries marked **OPEN** contain a question for Nabil. The implementation states an assumption rather
than hiding one; nothing here is a business rule invented to fill a gap.

Written by: Architect agent, slice 0, 2026-08-17.

---

## Treasury and money

### D-001 · Balances are derived by a database view, never stored

**Decision.** There is no balance column anywhere in the schema. `account_balances` is a PostgreSQL
view that sums postings per account and reports `inflow`, `outflow`, `raw_balance` and
`signed_balance`. EF maps it as a keyless, view-backed read model.

**Why.** CLAUDE.md forbids a stored balance and spec.md §6.1 requires balances to be derived "always".
A view makes that structural rather than disciplinary: there is no column for anything to drift from,
and no code path — application, report, or a person at a psql prompt — that can write one. It also
keeps the summation in PostgreSQL over an index, so reading a balance stays a single aggregate
instead of materialising a project's postings into memory.

**Rejected.** A LINQ helper over `DbSet<Posting>`: `Money` maps through a value converter, and EF
Core does not reliably translate `Sum` over a converted property, so the aggregate would have run
client-side. A cached or materialised balance table: that is the stored balance the rule exists to
forbid, wearing a different name.

**Revisit if.** Balance reads become slow enough to matter at Kaff's volume. The next step is a
`MATERIALIZED VIEW` refreshed on commit — still derived, still not writable — not a column.

---

### D-002 · Enums are stored as text

**Decision.** Every enum column is `varchar`, not an integer ordinal.

**Why.** The rules that must never break are enforced in SQL, and those triggers read these columns.
`ledger_kind = 'Hold'` is a rule a person can check in a code review. `ledger_kind = 2` is a rule
that silently inverts the day somebody reorders the enum — and the failure would be a hold ledger
being debited, discovered months later. Renaming a member becomes a data migration, which is the
correct amount of friction for a vocabulary spec.md fixes.

**Rejected.** Integer ordinals: smaller rows, unreadable guards. Native PostgreSQL enum types: better
still on both counts, but every addition becomes `ALTER TYPE` in a migration, and Npgsql needs
per-enum mapping registration that a future session will forget.

**Revisit if.** Posting volume makes row width matter. It will not at a contractor's scale.

---

### D-003 · Append-only is enforced twice, and the database is the authority

**Decision.** `postings` and `audit_records` carry `BEFORE UPDATE OR DELETE` and `BEFORE TRUNCATE`
triggers that raise `KAFF_APPEND_ONLY`, plus grants revoked from the application role where it
exists. `AppendOnlySaveChangesInterceptor` refuses the same operations in EF with a message naming
the rule.

**Why.** CLAUDE.md: "There is no update path and no delete path. Do not add one, not even for admins,
not even for 'fixing test data.'" The trigger holds whatever code runs — a support script, a
migration, a person at a prompt at 2am, a future session that forgot. The interceptor exists only so
the attempt fails with a message a developer can act on rather than as a PostgreSQL error halfway
through a transaction.

**Rejected.** The interceptor alone: it protects only code that goes through our `DbContext`, which
is exactly the code least likely to break the rule. A revoked grant alone: developer machines connect
as the database owner, so the rule would be off precisely where test data gets "fixed".

**Revisit if.** Never. This is the rule most likely to be argued away during an incident, which is
why it is enforced where argument does not reach.

---

### D-004 · Identifiers are UUID v7, generated in the domain

**Decision.** `Entity.NewId()` returns `Guid.CreateVersion7()`. Nothing is database-generated.

**Why.** Two reasons, one of which is load-bearing. UUID v7 is time-ordered, so index locality is
preserved and inserts do not scatter across the B-tree the way v4 does. More importantly, a
client-side key means the audit interceptor can write a complete record in the *same transaction* as
the change — it never has to save first to learn the key. Without that, auditing would need two
round trips and a window where a change exists without its evidence.

**Rejected.** Database sequences or `gen_random_uuid()`: both force the two-phase audit write.
UUID v4: random insert order, worse index behaviour, no benefit here.

**Revisit if.** Nothing foreseeable.

---

### D-005 · One non-negative-balance mechanism, driven by a column on the account

**Decision.** `accounts.enforce_non_negative` marks accounts with a floor. A deferred constraint
trigger checks the *signed* balance at commit and raises `KAFF_NEGATIVE_BALANCE`. Accounts are locked
with `pg_advisory_xact_lock` in identifier order before the check.

**Why.** spec.md §6.1 makes the safe rule a MUST and says to enforce it in the database. spec.md §15
imposes the same shape on the client advance: "reaches exactly zero, never negative". One mechanism
for both is one thing to get right. Deferring to commit means a multi-posting transaction is judged
on its final state rather than on the order postings happen to be inserted in — an extract writes
several postings and any single one of them might dip a balance transiently.

The advisory lock closes the race where two transactions each see a sufficient balance and together
overdraw it; taking locks in identifier order means two transactions touching the same pair of
accounts cannot deadlock.

**Rejected.** Application-level checking: spec.md §6.1 explicitly says "not only in application
code", and a read-then-write check is a race by construction. A `CHECK` constraint: it cannot see
other rows. A stored running balance with a `CHECK`: that is a stored balance (D-001).

**Revisit if.** Contention on the safe becomes visible. The fix is a narrower lock granularity, not
removing the check.

---

### D-006 · Ledger netting and hold direction are database rules

**Decision.** A trigger rejects any posting whose two accounts carry different non-null
`ledger_kind` values, and any posting that takes value *out* of a Hold account unless its type is
`HoldRelease` or it is a reversal.

**Why.** CLAUDE.md states both as prohibitions rather than preferences: the five ledgers never net,
and the hold only grows. Both are the kind of rule that a plausible-looking feature request erodes —
"just let the snag deduction come off the hold" is a sentence somebody will say. Putting them in the
database means that conversation has to happen before the code can run, not after.

The handover precondition on `HoldRelease` is *not* here: the ledger cannot see project state, and
reaching into `projects` from a treasury trigger would couple the two in a way that makes the
treasury unusable in isolation. That check lives with the handover flow (slice 8) and is noted here
so it does not get lost.

**Rejected.** Enforcing only in `Posting.Create`: correct, and bypassed by anything that is not our
domain code.

**Revisit if.** spec.md §5.1 changes. It is currently unambiguous.

---

### D-007 · A reversal mirrors its original exactly, once · **OPEN**

**Decision.** `Posting.Reverse` produces a posting with the same amount, same type and same source
document, accounts swapped, pointing at the original through `reverses_id`. A database trigger
verifies the mirror; a unique index on `reverses_id` allows each posting to be reversed once.

**Why.** spec.md §6.1 says corrections are "new reversing postings referencing the original" and says
nothing about partial ones. A reversal that could differ from its original would be an editable
posting wearing a disguise, and the append-only rule would be decorative. Reversing twice would
double-count the correction.

**OPEN for Nabil.** Does the business ever need a *partial* reversal — a correction of part of a
posting? Today the answer is no and the answer is enforced. If it is yes, the shape is two postings
(a full reversal and a corrected re-post), not a loosened reversal.

**Rejected.** A free-form correcting posting with a reference field: indistinguishable from an edit.

**Revisit if.** Nabil answers the open question above.

---

### D-008 · Money rounds away from zero at four decimal places · **OPEN**

**Decision.** `Money` normalises to 4 decimal places with `MidpointRounding.AwayFromZero`, matching
`decimal(18,4)`. `Percentage` stores a fraction at `decimal(18,6)`.

**Why.** Away-from-zero is the Egyptian commercial convention (half up on positive amounts) and is
what a person doing the sum by hand produces. Banker's rounding would disagree with the accountant on
half-piastre cases. The `Percentage` type exists to kill one specific bug: 20 versus 0.20. Both
constructors are explicit — `FromPercent(20)` and `FromFraction(0.20)` — so a bare number can never
be mistaken for the other convention.

**OPEN for Nabil.** Confirm the rounding convention with Kaff's accountant, and confirm what happens
at *display*: figures are stored at four decimals and shown at two. Which of the two is contractual
on an extract?

**Rejected.** `decimal` without a wrapper: CLAUDE.md forbids passing bare decimals for money.
Carrying a currency inside `Money`: spec.md §1 puts conversion out of scope, and a currency-bearing
money type implies a conversion capability that must not exist. Currency lives on the account, and
the database rejects a posting whose two sides disagree.

**Revisit if.** The accountant gives a different convention, or a second currency ever becomes real.

---

### D-009 · Account semantics live in one table and are copied onto each account row

**Decision.** `AccountTypes` maps every `AccountType` to its class, normal balance, ledger kind,
project scope, required party, postability, non-negative floor, and a spec.md reference. `Account`
copies those onto the row at creation; the SQL guards and the balances view read the columns.

**Why.** These are constants of the account type, not derived business values, and the triggers need
them without a lookup. Deriving them in SQL would put a second copy of the same table in PL/pgSQL,
where it would go stale. A test asserts every enum member has a row and that every account's stored
values match its metadata, which is what makes the denormalisation safe.

`NormalBalance` is stored explicitly rather than derived from `AccountClass` because contra accounts
break the derivation: accumulated depreciation is an asset that increases on the credit side, and
deriving the sign from the class would invert it — silently, in the balance guard.

**Rejected.** Deriving in SQL with a `CASE`; a separate `account_types` lookup table (a second thing
to migrate and keep in step with the enum).

**Revisit if.** Account types start needing per-instance overrides beyond the non-negative floor.

---

### D-010 · Owner is globally scoped · **ANSWERED 2026-08-17**

**Answer from Karim, via Nabil, 2026-08-17:** *"owner role is like the admin so yes global."*

**Decision.** `ProjectAccessPolicy` grants any Owner access to any project that exists, without an
assignment row. Every other role still needs one. This is the only exception in the system.

**Why here and not in the catalogue.** The exception is about *reach*, not about capability: an Owner
still holds only the permissions the catalogue grants, and still cannot, for example, gate quantities.
Putting it in `ProjectAccessPolicy` keeps it to one method that a reviewer can read in ten seconds,
and leaves `PermissionEvaluator` a pure expression of role × assignment.

**Not unconditional.** The project must exist. An identifier that names no project is still refused,
so a typo or a probe cannot pass as access.

**What this changes.** `ProjectAssignment` rows for the Owner are now optional rather than required.
Existing ones are harmless. Two tests were rewritten to express the rule with a non-Owner role.

**Rejected.** A general `IsSuperUser` flag on `User`: spec.md describes one owner, not a class of
administrators, and a flag invites a second one to be created without a decision.

**Revisit if.** Kaff ever has more than one owner, or an operations manager needs the same reach.
Both are business changes, not technical ones.

---

### D-011 · No ASP.NET Core Identity

**Decision.** A plain `User` entity and JWT bearer validation. Password hashing and token issuance
belong to slice 1.

**Why.** Identity's `UserStore` is a repository over the data layer, which CLAUDE.md forbids
outright. Its role and claim tables duplicate the role × assignment model spec.md §9 requires, and
having two places that describe who a user is means one of them will eventually be the wrong one.
The parts of Identity that are genuinely hard — password hashing — are available as
`Microsoft.AspNetCore.Cryptography.KeyDerivation`, a BCL-adjacent primitive, without the schema.

**Rejected.** Full Identity; Identity with custom stores (all of the coupling, none of the benefit).

**Revisit if.** External identity providers become a requirement. Even then the fit is
OpenID Connect, not Identity's local user store.

---

## Permissions and audit

### D-012 · The permission catalogue is data, every grant cites spec.md, and gaps stay visible · **OPEN**

**Decision.** `PermissionCatalogue` is a table of `(permission, scope, grants, specReference,
unresolved)`. The constructor rejects an empty spec reference. Permissions spec.md does not assign an
owner to are marked `Unresolved`, logged as warnings at every start-up, and pinned by a test so the
set cannot grow quietly.

**Why.** Permissions *are* business rules, and CLAUDE.md says an invented business rule is the most
expensive failure available here. Requiring a citation makes writing one down without a source
awkward. Deny-by-default means a capability nobody was granted is reachable by nobody — which is the
correct outcome, and a loud one.

**Answered 2026-08-17.** `ProjectAssignmentManage` is granted to the Owner and to the HR department.
Karim: *"owner and hr."* HR is granted by department rather than by role because spec.md §9 has no HR
role and §2 already puts the people records with HR.

> **Follow-up this creates.** `ProjectAssignmentManage` is project-scoped, because spec.md §9 counts
> assigning somebody to a project as acting on it. The Owner is globally scoped (D-010) so can always
> assign; **an HR user must themselves be assigned to a project before they can assign anyone else to
> it.** That bootstraps fine — the Owner assigns HR first — but it is a real operational step, and if
> Karim meant "HR administers assignments across the company" then this permission should be
> company-wide instead. One sentence from Nabil settles it. Tracked as question 2 below.

**Still open — two rows:**

| Permission | Question | Current state |
|---|---|---|
| `ProjectManage` | spec.md §2 assigns Project to "Projects", which is not one of the eight roles in §9. Who creates and edits a project? | **No grants. Nobody can.** Blocks slice 4. |
| `PeriodClose` | spec.md §6.6 requires a month-end close but not who performs it, nor whether the Owner must approve it as a financial movement. | Finance assumed. Blocks slice 7. |
| `AuditRead` | spec.md requires the trail but not who may read it. | Owner assumed. |

**Rejected.** Attributes on endpoints: the answer to "what can a Site Engineer reach?" would be a
search across the codebase rather than one table. A database-driven permission editor: spec.md §9
fixes the model; a runtime editor invites it to be changed without review.

**Revisit if.** Nabil answers any of the four.

---

### D-013 · Assignment level sits on the assignment, not the user · **OPEN**

**Decision.** `ProjectAssignment.Level` carries Junior / Supervisor. `User` does not.

**Why.** spec.md §9 describes Junior and Supervisor as a seniority distinction within the Site
Engineer role, not as separate roles. Putting it on the assignment is the superset: a uniform
per-person seniority is expressible by giving every assignment the same level; the reverse is not.

**OPEN for Nabil.** Is seniority a property of the person, or can the same engineer be a supervisor
on one project and a junior on another? If it is the person, this moves to `User` and the assignment
gets simpler.

**Rejected.** Separate `SiteEngineerJunior` / `SiteEngineerSupervisor` roles: it would break the count
of eight roles spec.md §9 gives, and duplicate every engineer grant.

**Revisit if.** Nabil answers.

---

### D-014 · Kaff's Arabic status vocabulary is not bound to `ProjectStatus` · **OPEN**

**Decision.** لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها are in the i18n catalogue under
`status.kaff.*`, verbatim, in both the Arabic and English files. They are **not** mapped onto the
`ProjectStatus` enum.

**Why.** CLAUDE.md requires the five labels to appear verbatim in the UI. spec.md §13 gives the
project eight states — `Setup · Active · HandoverPending · Handover · UnderWarranty · Closed ·
Stopped · Terminated` — which do not map onto the five: there is no project state for متعثرة or
تم تأجيلها, and spec.md §13 gives the project no `OnHold`. Guessing a mapping would put a wrong label
on a real project on a real screen.

**OPEN for Nabil.** What do the five labels describe — the project's state, or something adjacent
like a health flag? Specifically: is متعثرة the same as `Stopped`, or is it a separate "in trouble
but still running" signal? And is تم تأجيلها a project state spec.md §13 is missing?

**Rejected.** A plausible mapping (`لم تبدأ` → `Setup`, `متعثرة` → `Stopped`, and so on). It reads
fine and it might be wrong, which is the definition of the failure this project is trying to avoid.

**Revisit if.** Nabil answers. **This blocks the UX agent's screen inventory.**

---

### D-015 · The project state machine follows spec.md §13, with two documented additions · **OPEN**

**Decision.** Transitions are a table. Two entries are not literally in spec.md §13:

1. `Stopped → Active`. spec.md names `Stopped` but never says how a project leaves it. Without this
   a stopped project is stranded.
2. Design projects use a different, smaller table: `Setup → Active → Closed`, with no
   `HandoverPending`, `Handover` or `UnderWarranty`.

**Why.** The second comes straight from spec.md §11: "Design closure differs: final documents
delivered, last 10% collected, IP transfers. No snag list, no handover, no hold." A design project
that could enter `UnderWarranty` would contradict it.

This is the one place where contract type affects something other than `IBillingCalculator` and
`IProgressMetric`. It is a lookup of which transition table applies, not a fork of the project module,
and it is flagged here so the next session sees it deliberately rather than discovering it.

**OPEN for Nabil.** Can a stopped project resume, and does it resume to `Active` or to the state it
was in when it stopped? Today it resumes to `Active`.

**Rejected.** Leaving `Stopped` terminal (strands the project); allowing every transition (the state
machine would then document nothing).

**Revisit if.** Nabil answers, or spec.md §13 gains the missing transitions.

---

### D-016 · Employee and Worker are one entity with a `Kind` · **OPEN**

**Decision.** One `Employee` table with `EmployeeKind` of `Salaried` or `DayLabour`. The worker
registry of spec.md §10 is this entity filtered to `DayLabour`.

**Why.** spec.md §2 names the entity "Employee / Worker" and requires "every costed person, exactly
one record". Two tables would let the same person exist twice, which is the defect that requirement
exists to prevent. spec.md §10 reinforces it: "Nobody appears in both."

**OPEN for Nabil.** Karim's team may think of the worker registry as a visibly separate register. Is
one record with a type acceptable, or does the UI need to present them as two lists? (The second is a
presentation answer, not a schema one — but worth confirming before the UX agent designs it.)

**Rejected.** Separate `Employee` and `Worker` tables.

**Revisit if.** A person genuinely needs to move between the two populations while keeping history.
`Kind` is immutable today; a move would be a business process, not a field edit.

---

### D-017 · Opportunity has both a Stage and a Status · **OPEN**

**Decision.** `OpportunityStage` is the pipeline position (Lead → Contract). `OpportunityStatus` is
whether it is live, stalled, on hold, won or lost.

**Why.** spec.md §3 describes Stalled as something that happens *to* an opportunity while it keeps
its position: "day 7 status becomes Stalled … Activity revives it." Collapsing the two would lose the
stage a stalled opportunity reverts to.

**OPEN for Nabil.** spec.md §13 lists `Stalled`, `OnHold`, `ClosedLost` and `Reopened` alongside the
stages without saying which are stages and which are statuses, and spec.md §16 assumption 6 marks the
stage names themselves as unconfirmed. Confirm both. Note that `Reopened` is modelled as a return to
`Active`, not as a status of its own.

**Rejected.** One flat enum: unable to express "stalled at Quotation".

**Revisit if.** Nabil confirms the pipeline. The entity is deliberately thin — slice 4 owns its
behaviour — so the cost of changing it now is low.

---

### D-018 · `CatalogueItemStatus` has two values · **OPEN**

**Decision.** `Active` and `Archived`.

**Why.** spec.md §4.1 lists `status` as a field without enumerating it. Two values are the minimum
the freeze rule needs, and adding more later is cheap.

**OPEN for Nabil.** What statuses does the Technical Office actually use? Note that
`pendingCatalogueReview` from spec.md §4.5 is deliberately *not* here: it is a flag on a BOQ line for
a custom item, and spec.md is explicit that such items "MUST NOT write to the catalogue
automatically".

**Revisit if.** Nabil answers.

---

### D-019 · The owner current account is a single company-wide account

**Decision.** `OwnerCurrentAccount` carries no party and is unique across the company, enforced by a
partial unique index.

**Why.** spec.md §6.4.5 speaks of "the owner current account" — جاري المالك — in the singular, and
Kaff has one owner. Modelling it as a party sub-ledger would invite a second one and imply a
partnership structure spec.md does not describe.

**Rejected.** A per-owner sub-ledger.

**Revisit if.** A second partner appears. The change is a metadata row and a migration, not a redesign.

---

### D-020 · Request validation is hand-rolled

**Decision.** `IRequestValidator<TRequest>` returns a `Result` carrying an i18n key. Applied per
endpoint with `ValidationFilter<TRequest>`.

**Why.** DataAnnotations emits English sentences as error messages, and CLAUDE.md forbids a
user-facing string that did not come through i18n. FluentValidation would be a dependency doing what
these forty lines do, and CLAUDE.md forbids adding one for that. Every failure — validation or
domain — arrives at the client in the same shape, so there is one way to render both.

The filter is applied per endpoint rather than globally on purpose: a filter that silently applies to
everything is one whose absence nobody notices.

**Rejected.** FluentValidation; DataAnnotations; .NET 10's built-in minimal API validation (same
English-message problem).

**Revisit if.** Validation rules grow complex enough that the forty lines become four hundred.

---

## Tooling and delivery

### D-021 · FluentAssertions is pinned to 7.2.0

**Decision.** `Directory.Packages.props` pins FluentAssertions to `7.2.0` and must not be bumped
without a decision.

**Why.** CLAUDE.md names FluentAssertions in the pinned stack. Version 8 and later moved to the Xceed
commercial licence, which requires a paid seat per developer for a commercial product — which Kaff
ERP is. 7.2.0 is the last Apache-2.0 release. **This is a licensing exposure, not a preference:**
a routine `dotnet outdated` bump would put a paid licence obligation into a production system without
anyone deciding to.

**Rejected.** Silently taking version 8. Switching to Shouldly or bare xUnit asserts — possible, but
CLAUDE.md pins the stack and this is Nabil's call, not the Architect's.

**Revisit if.** Nabil buys licences, or decides to move off FluentAssertions. Flagging it is the
Architect's job; choosing is not.

---

### D-022 · Integration tests use a real PostgreSQL from a connection string

**Decision.** `PostgresDatabase` reads `KAFF_TEST_DB`, creates a uniquely-named database for the run
and drops it afterwards. CI provides a PostgreSQL 16 service container.

**Why.** The rules that matter most in this system live in PostgreSQL: append-only triggers, the
non-negative balance guard, the ledger prohibitions, the closed-period check, the balances view. A
provider that does not run them would turn every test green while enforcing none of them — worse than
no tests, because it reports safety that does not exist.

**Rejected.** EF's in-memory provider and SQLite: neither runs the guards. Testcontainers: it works,
but it makes Docker a hard requirement for running the suite, and CI service containers give the same
isolation for a connection string. The fixture creates and drops its own database, so it never
touches an existing one.

**Revisit if.** Developers find provisioning PostgreSQL locally a friction point. `docker compose up
-d db` is already in the repository for exactly that.

---

### D-023 · The staging deployment target is unspecified · **ANSWERED 2026-08-25 — see D-076**

**Decision.** `deploy-staging.yml` builds and publishes container images for the API and the web
application on merge to main, tags them `staging`, and stops at a deployment step gated on a
`STAGING_DEPLOY_TARGET` variable. If the variable is unset the job warns and succeeds.

**Why.** CLAUDE.md's definition of done includes "Runs on staging, not only locally". Nothing in
`spec.md` or `CLAUDE.md` says where staging is. Inventing infrastructure is the same mistake as
inventing a business rule: plausible, unreviewed, and wrong. Container images are the portable half
and are genuinely useful whatever the answer.

**~~OPEN for Nabil.~~ ANSWERED 2026-08-25.** An **Oracle Cloud ARM64 VPS**, reached over SSH, running
the published images through `deploy/docker-compose.staging.yml`. Secrets live on the host in a
`.env` CI never reads or writes. **The application is deployed and reports healthy with its database
guards installed.** Full record in **D-076**; operational steps in `deploy/README.md`.

**~~Revisit if.~~** It said *"the deploy job is a handful of lines once the target is known."* It was
four steps and **four defects**, none of which were about deployment — see D-076. The estimate was
wrong in a way worth remembering: the unknown was never the deploy job, it was everything the deploy
job would be the first thing to execute.

---

### D-024 · A runtime i18n catalogue, not `@angular/localize`

**Decision.** `I18nService` loads `public/locales/{ar,en}.json` at bootstrap and exposes `t(key)`
plus `Intl`-backed number, money and date formatting. Locale and direction are signals; an effect
writes `lang` and `dir` onto the document element.

**Why.** The API returns error *identifiers*, not sentences — a ProblemDetails carries `messageKey`,
because CLAUDE.md forbids the server sending user-facing prose. Resolving a key known only at runtime
is exactly what `$localize` cannot do, so `@angular/localize` would have needed a second mechanism
beside it. One mechanism for both beats two. It also avoids a per-locale build, and lets a user switch
language without a page load.

`t()` reads the locale and catalogue signals, so any template expression calling it is tracked and
re-renders on a language change — which works without Zone.js, as CLAUDE.md requires.

**Rejected.** `@angular/localize` (cannot resolve dynamic keys; per-locale builds).
`@ngx-translate/core` (a dependency for what a signal and a JSON file already do).

**Revisit if.** ICU plural or gender forms become necessary in a template. `Intl.PluralRules` is
already in the browser and the service is the place to reach for it.

---

### D-025 · snake_case naming is implemented here, not taken from a package

**Decision.** Table names are explicit in the EF configurations; column names are converted from
property names by `SnakeCase.Convert` in `OnModelCreating`.

**Why.** PostgreSQL folds unquoted identifiers to lower case, so a PascalCase model produces columns
that must be quoted in every hand-written statement — and this system has hand-written statements
that matter. Readable SQL in the guards is worth fifty lines, and CLAUDE.md forbids a dependency that
duplicates what the framework can already do.

**Rejected.** `EFCore.NamingConventions`: a good package, and a dependency whose entire job is
reformatting strings. Quoted PascalCase identifiers: unreadable triggers.

**Revisit if.** The conversion turns out to disagree with the package on an edge case that matters.

---

### D-026 · Two generated artefacts were missing · **MIGRATION DONE 2026-08-19 · LOCKFILE STILL OPEN**

**Decision.** Slice 0 ships without an EF migration and without `src/Web/package-lock.json`.

**Why.** The environment this slice was authored in had no .NET SDK installed at all, and Node 18 —
below Angular 22's floor. Neither `dotnet ef migrations add` nor `npm install` could be run.

Hand-writing them was rejected. A hand-written migration needs a hand-written model snapshot beside
it: a large, unverifiable artefact that is wrong in a way nobody notices until it is applied to a
real database. A hand-written lockfile is worse — it pins hashes that cannot be checked.

**Resolved for the migration, 2026-08-19.** `Persistence/Migrations` now holds `Initial` and the model
snapshot, generated on a machine with the SDK. Verified against the rules that mattered: every money
column is `numeric(18,4)` and every rate `numeric(18,6)` — the EF convention held, so nothing was left
at a provider default — all 26 check constraints are present, no column is named anything resembling a
balance, and `account_balances` was not scaffolded as a table, because it is a view living in
`002_views.sql`.

Two things changed to make it work, both worth keeping:

* **`KaffDbContextFactory`**, an `IDesignTimeDbContextFactory`. Without it the EF tools reach into the
  Api's `Program` for a service provider, and therefore want a connection string, a JWT signing key and
  an environment name — none of which emitting a migration requires. The connection string in the
  factory is never opened; a real one comes from `KAFF_DESIGN_TIME_DB` for commands that do connect.
  This is why the migration could be generated with PostgreSQL switched off.
* **`dotnet-ef` is a local tool**, pinned in `dotnet-tools.json`, not a global install. The version that
  generates migrations is now part of the repository like every other version in it.

Migrations are marked `generated_code = true` in `.editorconfig`. The scaffolder's output trips IDE0161
and CA1861, and hand-editing a migration to satisfy an analyser would mean hand-editing the model
snapshot to match — the two must agree exactly, or the next migration is computed against a model that
was never real.

**Still open: `src/Web/package-lock.json`.**

**What to do, on a machine with the .NET 10 SDK and Node 22:**

```bash
cd src/Web && npm install                     # commit package-lock.json
```

Until the lockfile exists, `npm ci` fails — which means the `web` and `e2e` CI jobs and the web
container build all fail. That is the correct behaviour and it is loud; do not paper over it by
switching CI to `npm install`.

Until the migration exists, `DatabaseInitializer` supports `SchemaStrategy.CreateFromModel`, which is
what the test harness uses, so the Domain and Api suites run today.

**Note.** The guard scripts are deliberately *not* part of migration history. They are embedded
resources applied after the schema on every start-up, and they are idempotent. That way a database
can never be running with today's schema and last month's triggers — a state in which the application
would look healthy while the safe-never-negative rule was simply absent.

---

### D-027 · Contract types dispatch through a sealed input hierarchy

**Decision.** `IBillingCalculator` and `IProgressMetric` take a context carrying the `Project` and a
type-specific input. Inputs are a sealed record hierarchy — `LumpSumBillingInput`,
`CostPlusBillingInput`, `DesignBillingInput`. A generic base class does the type check and the cast,
so an implementation never sees the wrong input.

**Why.** CLAUDE.md: "Lump Sum, Cost Plus and Design differ only through `IBillingCalculator` and
`IProgressMetric`." The three types need genuinely different evidence, and a single wide input record
would put تشوينات on a Design contract and stage approvals on a Lump Sum one — leakage the interfaces
exist to prevent. Terms (hold rate, supervision rate, design rate) come from the `Project` on the
context, so those numbers have exactly one home.

`ProgressReading` has a private constructor and three factories. `MonetaryOnly` takes no percentage
and cannot produce one, which makes spec.md §5.2's "no percentage progress bar" a property of the
type rather than something a reviewer has to notice.

**Rejected.** A generic `IBillingCalculator<TInput, TOutput>` (awkward to resolve by contract type);
one wide input record (leakage); a `switch` in each billing handler (repeated, and eventually
divergent).

**Revisit if.** A fourth contract type appears. It would add one calculator, one metric, one input
and one registration line.

---

### D-028 · Stub calculators fail loudly rather than returning zero

**Decision.** The three calculators and three progress metrics are registered and dispatchable, and
each returns `BillingErrors.CalculatorNotImplemented`.

**Why.** These sit directly in the money path. A stub returning zero would produce an extract for
nothing, and an extract for nothing looks like a business outcome — it can be reviewed, approved and
issued. A thrown `NotImplementedException` would surface as a 500 and read as an outage. A distinct,
translatable failure is the only honest option, and a test pins the specific error code so the
placeholder cannot be mistaken for a rejection.

**Revisit if.** Slice 5 implements them, which is the point.

---

### D-029 · There is no posting type or document type for a free-form journal entry

**Decision.** `PostingType` and `SourceDocumentType` are closed enums with no `Manual`, `Other` or
`JournalEntry` member. A test asserts those names never appear.

**Why.** spec.md §1 puts "a general ledger with free-form manual journal entries" out of scope. A
`Manual` member is precisely how that creeps back in — one member, added reasonably, and the ledger
becomes editable prose. Every posting traces to a business event spec.md names.

**Revisit if.** spec.md §1 changes. It is unusually emphatic.

---

### D-030 · Seeding creates the company-level accounts only

**Decision.** `AccountTreeSeeder` inserts the safe, the company node, جاري المالك, owner drawings,
three equity accounts, fixed assets and accumulated depreciation, the two withholding tax accounts,
and three overhead expense accounts. It is idempotent and additive.

**Why.** Nothing can post until these exist. Seeding runs against a live database, so it behaves like
every other write here: it adds, it never rewrites.

**Deliberately not seeded.** Bank accounts — spec.md §6.3 gives QNB, CIB and الأهلي as examples and
the real list is Karim's. A VAT payable account — spec.md §6.7 and assumption 15 leave Kaff's
registration status open, and seeding one would invite somebody to use it. A loan account, for the
same reason under assumption 16. Project accounts and party sub-ledgers, which are created with the
project and the party they belong to.

**Revisit if.** Karim provides the bank list, or confirms VAT registration.

---

### D-031 · One audit mechanism, opt-out, in the same transaction

**Decision.** `AuditSaveChangesInterceptor` writes an `AuditRecord` for every insert, update and
delete of every entity that does not implement `IAuditExempt`. Reason and correlation id come from a
scoped `IAuditContext`; properties marked `[AuditRedacted]` are written as a placeholder.

**Why.** CLAUDE.md: "This is one mechanism in Domain/, not per-feature code." Opt-out rather than
opt-in means an entity added in a later slice is audited from its first commit without anyone
arranging it — the opposite arrangement fails silently and is only discovered when the trail is
needed. Records are added to the same `DbContext` before it builds its commands, so the change and
its evidence commit or roll back together.

Redaction lets the trail record *that* a credential changed without recording the credential. The
property still appears in `ChangedProperties`, so the change is visible.

**Rejected.** Per-feature audit calls (forgettable); a database trigger (cannot see the actor or the
reason); writing audits in a second transaction (a window where a change exists without evidence).

**Revisit if.** Audit volume needs partitioning. That is a table strategy, not a mechanism change.

---

### D-032 · A posting's project tag is exactly the project its accounts belong to

**Decision.** If either account belongs to a project, the posting must name that project. If neither
does, the posting must name none. Two accounts on different projects are rejected outright.

**Why.** spec.md §6.10: "Every expense is tagged project or company at the moment of spending — never
both, never neither. This is what makes gross and net margin correct." Deriving the tag from the
accounts rather than trusting the caller means it cannot disagree with the ledger it describes.

**Rejected.** Allowing an informational project tag on company-level postings: it would let project
cost reports be built two different ways and give two different answers.

**Revisit if.** A legitimate movement turns out to need a project reference its accounts do not carry.

---

### D-033 · The application refuses to start when the database guards are missing

**Decision.** `DatabaseInitializer.ApplyGuardsAsync` runs on every start-up. `FindMissingGuardsAsync`
then verifies the triggers and the view exist; outside Development, a missing guard throws and the
process does not start. `/api/health` reports the same thing.

**Why.** A database that lost its triggers — a restore from a schema-only dump, a migration applied
by hand, a new environment provisioned from the model — serves traffic normally and passes every
application-level test, while the rule spec.md §6.1 insists must live in the database is simply not
there. There is no signal for that failure except looking for it. Refusing to start is the only
response that cannot be ignored.

**Revisit if.** Start-up latency becomes a problem. The check is four `pg_trigger` lookups.

---

## Findings from the slice-1 kickoff review (2026-08-18)

Three agents — BA, UX and an independent Architect — reviewed slice 0. The full record is in
`meetings/2026-08-18-slice-1-kickoff.md`. Three findings changed the code immediately.

### D-034 · تشوينات is a liability, not an asset · **DEFECT, FIXED**

**What was wrong.** `MaterialAdvance` was modelled `AccountClass.Asset` / `NormalBalance.Debit` with
a non-negative floor. Walk spec.md §15, Extract 1: `300,000 − 60,000 − 75,000 + 75,000 = 240,000`.
تشوينات **adds** to what the client pays. Under an asset-with-a-floor the only legal posting
direction is the one that *reduces* the client payment, so Extract 1 would have netted 90,000 and the
correct posting would have been rejected by the system's own balance guard.

**Why it is a liability.** The client pays 75% of the value of material that is on site but not yet
built into certified work. That is money received for work not yet done — structurally identical to
`ClientAdvance`, which was modelled correctly, and recovered the same way as the material is
installed. `EnforceNonNegative` still holds: §15 requires "تشوينات in equals تشوينات recovered", so
it returns to exactly zero and never past it.

**How it survived slice 0.** The Architect's own domain test posted `MaterialAdvanceIssue` in the
wrong direction, so the test agreed with the defect. `MaterialAdvanceRecovery` would then have had to
run in the *same* direction as the issue, which is self-evidently impossible — and nothing checked
that. The replacement test asserts issue and recovery move in opposite directions, which is the
property that cannot be satisfied by a wrong sign.

**The wider lesson, and the real gap.** Slice 0 has no test of the spec.md §15 worked example, even
though CLAUDE.md puts it first in the testing priority order. A structural test of the account
catalogue passed while the catalogue said something economically false. **The §15 fixture must exist
before slice 3 opens** — failing or skipped, but present, so the gate is a build outcome.

### D-035 · The portal boundary was one careless endpoint from leaking · **DEFECT, FIXED**

**What was wrong.** Two independent paths let a portal client reach internal data.

1. `Permission.ProjectRead` granted `Role.Client`. Any internal endpoint requiring only `ProjectRead`
   — a project header, a summary, a BOQ view, the obvious permission to reach for — would have been
   reachable by a portal user, because the access policy matches their client to the project and lets
   them through. spec.md §12: the client "MUST NEVER see costs, margins, catalogue, subcontractors,
   internal notes".
2. Grants may be written against a **department alone** — HR owns the people records (§2), Operations
   / Administrative confirms site expenses (§8) — and such a grant matches any role carrying that
   department. Nothing stopped a `Role.Client` user being given `Department.Hr`, which would have
   handed a client `EmployeeManage`: company-wide, so evaluated with **no project check and no client
   check at all**.

**Fixed.** `Role.Client` removed from `ProjectRead`; the portal reaches projects through `PortalRead`
and `PortalApprove` and nothing else. `User.Create` and `MoveToDepartment` refuse a department on
`Role.Client` or `Role.Subcontractor`. Two tests pin both, in the style of the existing test that
keeps subcontractors out of the catalogue entirely.

**Still owed, before slice 8.** A separate `/api/portal/*` surface with its own response types and no
shared DTOs — the failure mode being guarded against is a shared DTO with an `if (isClient) omit`
branch that somebody forgets to update. Plus a reflection test failing the build if any Domain entity
or cost-shaped property name is reachable from a portal response.

### D-036 · Every money figure would have rendered in Arabic-Indic digits · **DEFECT, FIXED**

**What was wrong.** `I18nService` formatted with the locale `ar-EG`, whose default numbering system
is `arab`. `formatMoney(1234.5)` returned `١٬٢٣٤٫٥٠` — Arabic-Indic digits, U+066B decimal separator.
`styles.css` claimed to prevent this with `font-variant-numeric: lining-nums`, which selects a *glyph
style for digits that are already Latin* and cannot change what `Intl` emits. The comment and the
code said opposite things and the code won.

**Fixed.** The locale is pinned to `ar-EG-u-nu-latn`, the calendar to `gregory`, and the false comment
is replaced with one that points at where the decision actually lives. `font-feature-settings: "ss01"`
was also removed — a stylistic set means something different in every font, and applying an unnamed
one to all Arabic text in a production system is a gamble on whichever family resolves.

**Also fixed, same area.** `I18nService.t()` now wraps every interpolated parameter in U+2068/U+2069
bidi isolates. Codes, amounts and phone numbers are Latin runs inside Arabic sentences, and without
isolation the bidi algorithm moves their trailing punctuation to the wrong visual end — `KF-2026-014`
renders as `014-2026-KF`. Fixing it inside `t()` rather than per template means it cannot be forgotten,
and plain characters survive text interpolation where a `<bdi>` element would not.

### D-037 · What the first real compile changed (2026-08-18)

Slice 0 was authored on a machine with no .NET SDK. This is the record of the first build, so the
next session knows which of these were decisions and which were consequences.

**Package versions — two known-vulnerable transitives, both fixed by moving forward, not by pinning
around them.** `dotnet restore` failed with ten `NU1903` errors, promoted from warnings by
`TreatWarningsAsErrors`. `Microsoft.AspNetCore.OpenApi 10.0.0` pinned `Microsoft.OpenApi` to exactly
`2.0.0` (GHSA-v5pm-xwqc-g5wc); `10.0.11` widens that range to `[2.7.5, 3.0.0)`.
`Microsoft.EntityFrameworkCore.Design 10.0.0` reached `System.Security.Cryptography.Xml 9.0.0`
through `Microsoft.Build.Tasks.Core`, carrying eight advisories; `10.0.11` dropped that dependency
altogether. Central transitive pinning was available and deliberately not used — bumping the direct
package is the fix that stays fixed.

The audit stays an error. A production system holding real money should not build green while
shipping a known high-severity vulnerability. The cost — an unrelated change failing the day a new
advisory lands — is the point, not a side effect.

**Test platform.** `xunit.v3` 4.x runs on Microsoft.Testing.Platform, and the .NET 10 SDK removed the
VSTest bridge it used to be driven through. The runner is selected in `global.json`
(`"test": { "runner": "Microsoft.Testing.Platform" }`) — not in `dotnet.config`, and not by the
`TestingPlatformDotnetTestSupport` property, which actually imports the *old* bridge. Consequences:
`Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` are gone (each test project is an executable
that hosts itself), and CI takes `--report-trx --report-trx-filename x.trx` instead of
`--logger "trx;…"`.

**Analyser suppressions, two, both argued rather than convenient.** `CA1711` on `Permission` — the
suffix is reserved for Code Access Security types that no longer exist, and spec.md §9 calls the
concept a permission, so renaming it would put a synonym into a vocabulary CLAUDE.md fixes.
`CA1716` on `Error` — the rule protects Visual Basic consumers, of which this C#-only solution has
none. `CA1873` joined `CA1848` in the NoWarn list: same logging-cost family, and every call site is
start-up or a refused request, none hot.

**Naming rule corrected, not code renamed.** `IDE1006` fired on every `private static readonly`
field, because the rule required `_camelCase` of all private fields. Those fields are the frozen
catalogues and transition tables — constants in everything but the keyword — so `.editorconfig` gained
a more specific rule ahead of the general one rather than the code gaining underscores.

**Two genuine bugs the compiler and the tests found, both mine, both from this session's edits.**
`ProjectAccessPolicy` was rewritten treating `PermissionSubject.UserId` as nullable; it is not. And
the new تشوينات direction test (D-034) compared two *different* accounts, because the test factory
mints a fresh identifier per call — the assertion would have passed for the wrong reason once fixed
naively. Reused instances now.

**State at the end of the session:** build clean at 0 warnings with warnings-as-errors on,
`dotnet format --verify-no-changes` clean, **51 of 51 Domain tests passing**. The Api integration
suite compiles and its harness works — it reached PostgreSQL and reported a clear authentication
failure — but has not yet run against a database, and the E2E suite skips until an application is
running. `xunit.v3` 4.x's `xUnit1051` is enforced: every database and HTTP call in the tests threads
`TestContext.Current.CancellationToken`, which matters here because the balance guard takes advisory
locks and a deadlocked test would otherwise hang the suite rather than fail it.

---

## Open questions, collected

For Nabil, in the order they block work:

### Answered

| Question | Answer | Date | Entry |
|---|---|---|---|
| Is the Owner globally scoped, or does it need an assignment per project? | **Global.** "Owner role is like the admin." | 2026-08-17 | D-010 |
| Who assigns users to projects? | **Owner and HR.** | 2026-08-17 | D-012 |

### Still open

| # | Question | Blocks | Entry |
|---|---|---|---|
| 1 | What do the five Arabic status labels describe, and how do they relate to spec.md §13? | UX agent | D-014 |
| 2 | Is `ProjectAssignmentManage` for HR project-scoped (HR must be assigned first) or company-wide? Raised by the answer above. | Slice 1 | D-012 |
| 3 | Who creates and edits a project? spec.md §2 names "Projects", which is not a §9 role. | Slice 4 | D-012 |
| 4 | Is Junior/Supervisor a property of the person or of the assignment? | Slice 1 | D-013 |
| 5 | Who may read the audit trail? | Slice 1 | D-012 |
| 6 | Can a stopped project resume, and to which state? | Slice 4 | D-015 |
| 7 | Confirm the pipeline stage names, and which of §13's entries are stages versus statuses. | Slice 4 | D-017 |
| 8 | Confirm the money rounding convention, and whether the contractual figure on an extract is 2 or 4 decimals. | Slice 5 | D-008 |
| 9 | Are partial reversals ever needed? | Slice 3 | D-007 |
| 10 | Who performs the month-end close, and does the Owner approve it? | Slice 7 | D-012 |
| 11 | What catalogue item statuses does the Technical Office use? | Slice 2 | D-018 |
| 12 | Is one Employee record with a type acceptable, or must Worker be a separate register in the UI? | Slice 2 | D-016 |
| 13 | Where does staging run, and who holds its secrets? | Definition of done | D-023 |
| 14 | May a bank account go negative, or should the non-negative floor apply to banks too? | Slice 3 | D-005 |
| 15 | FluentAssertions 8+ needs paid commercial licences. Stay on 7.2.0, buy licences, or change library? | Ongoing | D-021 |

Every 🟡 in `spec.md` §16 remains open and is not repeated here.

---

### D-038 · The guards now ship with the schema · **DEFECT, FIXED 2026-08-19**

**What was wrong.** `dotnet ef migrations script` produced 682 lines containing **zero triggers and
zero functions**. The guards lived only in `001_guards.sql`, applied by `DatabaseInitializer` when
the application booted.

So a database provisioned the documented way — `dotnet ef database update`, or by handing a DBA the
output of `migrations script`, which is how production is built — came up with tables and **no
append-only trigger, no safe floor, no ledger-netting rule, no hold rule**. The application would
install them at its next start-up, which leaves a window in which the database holds money and
enforces nothing. Anything loaded by script in that window never passed a guard at all.

D-026's reasoning for keeping them out — "so schema and triggers cannot drift" — bought a real
property and paid too much for it.

**Fixed.** A `DatabaseGuards` migration applies the scripts, so the schema and its rules arrive
together; the generated script is now 1180 lines and carries all 8 triggers, all 5 functions, the
`KAFF_NEGATIVE_BALANCE` check and the `account_balances` view. `GuardScripts.ReadAllInOrder` is the
single reader, used by both the migration and `DatabaseInitializer` — so the test harness, which
builds its schema from the model rather than from migrations, still gets them.

Applying them twice is correct rather than merely tolerated: the scripts are idempotent by design
(`CREATE OR REPLACE FUNCTION`, `DROP TRIGGER IF EXISTS`), which is what preserves D-026's original
anti-drift property on top of the new one.

**Revisit if.** A guard ever stops being idempotent. Then the two application paths stop converging
and this needs rethinking.

---

### D-039 · Four hard floors are invented, and spec.md mandates two · **OPEN — money**

**Found by the acceptance review, 2026-08-19.** `AccountTypes` sets `EnforceNonNegative: true` on
six account types. spec.md justifies **two** of them:

* `Safe` — §6.1, "The safe balance MUST NOT go negative", a MUST.
* `ClientAdvance` — §15, "Advance ledger reaches exactly zero, never negative".

The other four — **`Hold`, `FirmAdvance`, `PettyCashAdvance` and `MaterialAdvance`** — are the
Architect's inference. Each is plausible: a hold that only grows should not go negative, تشوينات
nets to zero by §15, عهدة is money someone is holding. Plausible is exactly the problem. CLAUDE.md:
"An invented rule is always plausible, which is why it survives review and surfaces months later
during acceptance."

They have not been left in place quietly. **The floors stay for now** — removing them would be
equally unreviewed, and a floor that is too strict fails loudly at insert time rather than silently
producing a wrong balance — but each is a question for Karim, and every one of them can reject a
legitimate posting in slice 3. `FirmAdvance` is the one most likely to be wrong: spec.md §6.4.3
describes Kaff spending on a client's behalf under a cap, and a recovery that overshoots is easy to
imagine.

**Question for Nabil → Karim:** "Besides the safe, which of these can never go below zero — the
retention you hold from the client, money you spend on a client's behalf, عهدة with a site engineer,
and material paid for but not yet installed?"

---

### D-040 · Two smaller inventions in the Client master · **OPEN**

Also from the acceptance review. Neither is in the money path, both are business rules with no
source:

* **`Client.Code` is required and uniquely indexed.** spec.md §2 describes a Client as
  "project-independent, full history, deduplicated by phone" and gives it no code. §4.1 gives one to
  `CatalogueItem` deliberately. So Marketing now has a mandatory field on their form that nobody
  asked for, and somebody has to decide whether it is typed or generated.
* **A corporate client defaults to `WithholdingCategory.None`,** and `Client.Create` does not refuse
  a withholding category on an individual — even though spec.md §6.7 states plainly that
  "Individual clients do not withhold". That is a rule the spec *does* answer and the code does not
  enforce, which makes it a defect rather than an ambiguity. The default is the exact failure §6.7
  says the field exists to prevent: "collections will never match issued extracts and staff will
  invent adjustments to close the gap."

The individual-client check should be enforced in `Client.Create` in slice 1. The code is a question.

---

### D-041 · Every audit write would have thrown on the first save · **DEFECT, FIXED 2026-08-19**

**Found by the first run against a real database.**

`KaffJson.Build()` ended with `options.MakeReadOnly()`. The parameterless overload throws
`InvalidOperationException: JsonSerializerOptions instance must specify a TypeInfoResolver setting
before being marked as read-only` — it refuses to infer a resolver, because doing so would silently
opt the application into reflection-based serialisation.

`KaffJson.Options` is a `static readonly` field, so the failure surfaced as a
`TypeInitializationException` from the static constructor, thrown inside
`AuditSaveChangesInterceptor.ToNode`. **The audit interceptor runs on every `SaveChangesAsync`, so
every state change in the system would have failed** — not silently written a bad record, but thrown.
CLAUDE.md makes the audit trail a non-negotiable, and it was inoperative.

**Fixed** by setting `TypeInfoResolver = new DefaultJsonTypeInfoResolver()` before freezing.

**Why nothing caught it earlier.** The Domain suite never serialises anything, so `KaffJson`'s static
constructor was never triggered. The build was clean, `dotnet format` was clean, and 51 tests passed
against a component that could not execute once. **This is the entry that justifies CLAUDE.md's
rule that the author does not certify their own work** — no amount of reading found it, and the
first real save found it in ten seconds.

---

### D-042 · Two test-harness defects the same run exposed · **FIXED 2026-08-19**

**`WebApplicationFactory` configuration arrived too late.** All fifteen permission tests failed with
`ArgumentException: The value cannot be an empty string (Parameter 'connectionString')` from
`Program.cs`. `Program` reads the connection string and the JWT settings immediately after
`WebApplication.CreateBuilder(args)`, which runs *before* `WebApplicationFactory` applies
`ConfigureAppConfiguration`. The values are now set as environment variables in the factory's
constructor, which `CreateBuilder` does read in time.

Worth remembering when slice 1 adds real endpoints: **anything `Program` reads before
`builder.Build()` cannot be supplied by `ConfigureAppConfiguration`.**

**A schema test was passing vacuously.** `Enum_columns_are_stored_as_text` asserted on
`GetProviderClrType()`, which reports only an *explicitly configured* provider type and is null when
the conversion comes from a `ValueConverter` — as ours does. Written with `?.` it would have
succeeded against null. It now reads the converter, asserts the converter exists, and then asserts
its provider type, so it fails if either is wrong.

The underlying behaviour was always correct — the generated DDL shows
`normal_balance character varying(64)` — but the test proving it was not testing anything.

**State after these fixes:** build clean at 0 warnings, `dotnet format` clean, **51/51 Domain and
34/34 Api integration tests passing** against PostgreSQL 16 in Docker. The database guards have now
executed for the first time, including the overdraw refusal spec.md §6.1 requires.

---

### D-043 · Slice 0 complete and verified end to end · 2026-08-19

Everything below was **executed**, not read. Earlier entries were written against code that had never
compiled; this is the first state where that is not true.

| Gate | Result |
|---|---|
| `dotnet build --configuration Release` | 0 errors, **0 warnings**, warnings-as-errors on |
| `dotnet format --verify-no-changes` | clean |
| Domain tests | **51 / 51** |
| Api tests, real PostgreSQL 16 | **34 / 34** |
| E2E, Playwright against the running stack | **4 / 4** |

**The whole startup path ran for the first time.** The API applied both migrations, installed the
guards, seeded the account tree and answered `/api/health` with
`{"status":"healthy","databaseReachable":true,"guardsInstalled":true,"missingGuards":[]}`.

The database afterwards: 14 accounts, **14 audit records** — one per account, which is the audit
interceptor demonstrably working after D-041 — 8 triggers, and `account_balances` returning rows.
`SAFE-MAIN` is the only floored account present, correct because the other four floored types are
project-scoped and no project exists yet.

**What the run proved that reading could not.** The safe refuses an overdrawing posting at the
database with `KAFF_NEGATIVE_BALANCE`; postings and audit records cannot be updated or deleted even
by raw SQL; the five ledgers cannot be netted; nothing leaves the hold before handover; a posting
cannot land in a closed period; a reversal that does not mirror its original is refused. The Angular
application opens in Arabic, right to left, at 390px with no horizontal overflow, resolving its
catalogue rather than rendering raw keys.

**Node.** Upgraded to 24.19.0 via winget. The Angular 22 CLI requires v22.22.3 / v24.15.0 / v26.0.0 —
higher than the floor originally written into `engines`, which is now corrected. The build produces
235 kB initial with the status page lazy-loaded, and `strictTemplates` type-checks every template.

**What is still not verified, and must not be assumed:**

* **CI has never run.** The workflow is written and its commands match what was run by hand, but no
  push has exercised it.
* **Staging has never been deployed to** — there is no target (D-023).
* The Playwright job in CI installs browsers and starts both services; that sequence has only been
  performed manually here, not by the workflow.

**What slice 0 does not contain** is unchanged and listed in `architecture.md`: no BOQ, extracts,
change orders, daily logs, عهدة, snags, handover, warranty, portal, Excel import, authentication
endpoints, reports or mobile. The `Opportunity` and `Project` entities are deliberately thin.

**Open before slice 1 starts** — `decisions.md` questions 1–5, plus the two blockers the kickoff
meeting found: there is no `UserManage` permission, so nobody can create a user; and HR is a
department while every user must hold one of §9's eight roles, so the HR user Karim's answer requires
cannot be created without deciding which role they hold. Both are in
`meetings/2026-08-18-slice-1-kickoff.md`.

---

### D-044 · Karim's eight rulings of 2026-08-20 · **APPLIED**

Eight business questions answered in one message, closing every blocker that stood in front of
slice 1. Recorded here as **Decision · Why · What we rejected · What would make us revisit**, and
separated into the ones that only confirmed what the code already did and the ones that changed it.

#### 1. `UserManage` — global, Owner only

**Decision.** New `Permission.UserManage`, `CompanyWide`, granted to `Role.Owner` alone.

**Why.** There was no way to create a user. Every other permission in the catalogue was therefore
unreachable, which is what made this the first blocker rather than a missing feature. Karim: "The
UserManage permission is strictly Global and held exclusively by the Owner."

**What we rejected.** Folding user creation into `ProjectAssignmentManage`, which HR holds. HR
staffing a project with existing users is a different act from HR minting a login and choosing its
role — the second would let HR grant itself the financial visibility ruling 2 denies it.

**Revisit if** Kaff grows past the point where one person can create every account.

#### 2. `Role.Hr` — a ninth role

**Decision.** `Role.Hr = 9`. The HR grants moved from `{ Department = Department.Hr }` to
`{ Role = Role.Hr }`.

**Why.** Karim: "Create a dedicated Role.Hr (as the 9th role) to ensure strict segregation of duties,
rather than dangerously piggybacking on the Finance role." A department-only grant matches **any**
role carrying that department, so a Marketing user moved to HR held `EmployeeManage`. A test asserted
exactly that, and now asserts the opposite.

**Two mechanisms, not one.** The catalogue alone cannot deliver "zero financial visibility", because
`SiteExpenseConfirm` and `PhotoPublish` are granted to Operations / Administrative *by department
with no role named*. An HR user parked there would confirm site expenses. So `User.Create` now
refuses an HR user in any department but HR — the same hole, closed from the other direction.

**What we rejected.** Leaving HR as a department and relying on nobody writing a department-only
grant in future. That is a convention, not a control, and this project has already seen one such
grant leak (D-035).

**Revisit if** a role legitimately needs to span departments. Nothing in spec.md suggests one does.

#### 3. HR has global reach for assignments

**Decision.** `ProjectAccessPolicy` grants HR access to any project that exists, with no assignment
row. `OwnerAccessAsync` became `GlobalReachAsync(projectId, level, …)`, shared by both.

**Why.** Karim: "HR does not need to be assigned to a project first in order to staff it." Requiring
an assignment in order to create assignments is circular — on a new project nobody is assigned, so
nobody could make the first one.

**The two callers pass different levels, deliberately.** Owner gets `Supervisor`; HR gets `Standard`.
The risks are not symmetric. For the Owner, `Standard` would silently refuse the first grant anyone
writes with a minimum level, and the failure would read as a permission bug. For HR, `Standard` costs
nothing today and a future levelled HR grant would fail as a *refusal* — safe and visible — whereas
pre-emptively granting supervisor seniority would make that same grant silently succeed.

**Revisit if** HR ever holds a permission carrying a minimum assignment level.

#### 4. The Owner reaches all master data

**Decision.** `owner` added to `ClientManage`, `CatalogueManage`, `BabManage`, `EmployeeManage`,
`SubcontractorManage`, `SupplierManage`, `OpportunityManage` and `AccountManage`. spec.md §2's
ownership column still says which department owns a record day to day; the Owner sits beside it.

**Why.** Karim: "The Owner has Global Reach for all master data … without departmental restrictions."
Consistent with D-010, where Karim called the Owner role "like the admin".

**🟡 One reading recorded, not resolved.** The ruling's *rule* line says "all master data"; its
*action* line names three (Clients, Suppliers, Banks). The rule line is applied, on the reading that
the three are examples. If Karim meant literally those three, `CatalogueManage`, `BabManage`,
`EmployeeManage`, `SubcontractorManage` and `OpportunityManage` should lose the Owner grant. **This
is a question, not a decision — see D-045.**

**Safe meanwhile for `AccountManage`:** `Account.Create` can only turn a floor **on**, never off
(`meta.EnforceNonNegative || …`), and guard 3c freezes an account's configuration after creation.
Opening an account is not moving money through it.

#### 5. Seniority stays on the assignment — *confirmed, no change*

Karim: "An engineer can be a Supervisor on one project and a Junior on another." The superset model
already in `AssignmentLevel` is correct as written. **D-013 closed.**

#### 6. Money rounds at 4 in storage, 2 in display — *confirmed, no change*

`decimal(18,4)` throughout the backend; `I18nService.formatMoney` already pinned
`minimumFractionDigits: 2, maximumFractionDigits: 2`. The comment now records *why* the rounding
happens at the last possible moment: rounding earlier lets display precision leak back into the
arithmetic, which is the exact failure the 4/2 split exists to prevent.

#### 7. متعثرة and تم تأجيلها are health tags, not states — *confirmed, no change to slice 0*

**Decision.** They do not map onto `ProjectStatus`. A struggling project stays structurally `Active`.
**D-014 closed.**

**Why this matters more than it looks.** The obvious mapping was onto `Stopped`. spec.md §7: "A
stopped project MUST NOT issue extracts." Flagging a project as متعثرة would therefore have frozen
the material purchases and subcontractor payments meant to unstick it — the flag would have caused
the problem it describes.

**Not built here.** The tag itself is slice 4's. `Project` stays thin; no column added.

#### 8. Ledger floors: exactly three — **this one removes protection**

**Decision.** `EnforceNonNegative` is true for `Safe`, `ClientAdvance` and `PettyCashAdvance` (عهدة),
and false for `Hold`, `FirmAdvance` and `MaterialAdvance`. This answers **D-039**, where four of the
six floors were flagged as inference rather than spec.

**Why.** Karim: "Hard, non-negative database floors apply specifically to the Safe, Client Advance,
and Petty Cash (عهدة)." Only two of these were in spec.md (§6.1 and §15); the rest were mine.

**What is given up, stated plainly rather than buried:**

| Account | Protection lost | Residual protection |
|---|---|---|
| `Hold` | none in practice | Guard 3 refuses any posting **out** of a hold account before handover; guard 3b requires a release to leave it at exactly zero. The floor was a third lock on a door with two. |
| `FirmAdvance` | **real** — nothing now stops recovery past zero, which would read as Kaff owing the client on an advance the client never made | §6.4.3's hard **cap**, which is slice 3's to build and does not exist yet |
| `MaterialAdvance` (تشوينات) | **real** — §15's "تشوينات in equals تشوينات recovered" is no longer enforced at the point of posting | the §15 reconciliation in slice 5, which catches it later and more expensively |

**These two are accepted exposures, not oversights.** They are listed in the slice 3 and slice 5
stories as risks the QA scenarios must cover, since the database will no longer catch them.

**Revisit if** either ledger goes negative in practice. That is the signal the floor was doing work.

**No SQL changed.** The trigger reads `accounts.enforce_non_negative` from the row, so the rule is
data. Note the consequence: **guard 3c makes account configuration immutable, so any database seeded
before 2026-08-20 keeps the old floors.** Only `SAFE-MAIN` exists so far, and it is floored either
way, so nothing needs correcting — but a project created against an old database would carry a
floored hold.

#### What was verified after applying all eight

| Gate | Result |
|---|---|
| `dotnet build --configuration Release` | 0 errors, **0 warnings** |
| `dotnet format --verify-no-changes` | clean |
| Domain tests | **58 / 58** (51 before; 7 added) |
| Api tests, real PostgreSQL 16 | **38 / 38** (34 before; 4 added) |

The four new Api tests hit real routes: HR staffing a project it was never assigned to, HR refused
`ProjectRead` on that same project one line later, HR's reach stopping at a project that does not
exist, and `UserManage` refused to everyone but the Owner.

---

### D-045 · Two questions the rulings raised rather than closed · **OPEN**

**1. What is `BankManage`?** Ruling 4 names "Banks (BankManage)" as master data the Owner may create
and edit. **There is no Bank master record in spec.md** — a bank is an account of `AccountType.Bank`
in the §6.3 tree. It has been mapped onto `AccountManage`, which is broader: it also opens every
other account type. Is opening any account intended, or should a distinct Bank master entity exist
with its own permission?

**2. Does "all master data" mean all of it?** Ruling 4's rule line says "all master data"; its action
line lists three. The rule line was applied — see D-044 §4. If the list was meant literally, five
Owner grants come back out.

**Neither blocks slice 1.** Both should be settled before slice 2 (masters) and slice 3 (treasury).

**D-040 remains open and unanswered by these rulings**: `Client.Code` is a required, uniquely-indexed
field spec.md never asked for, and `Client.Create` still does not refuse a withholding category on an
individual client even though §6.7 says "Individual clients do not withhold". The second is a defect
with a spec answer and is scheduled into slice 1.

---

### D-046 · The test suites were never running in CI, and the harness was flaky · **DEFECT, FIXED 2026-08-20**

Two findings, both from actually running the commands CI runs rather than reading them.

#### `dotnet test` ran nothing, and had never run anything

`dotnet test` with the TRX options reports **`Zero tests ran`, exit code 5, in about 200ms**. CI's
first test step would have failed on the first push. It had never been noticed because **every green
result in D-043 came from invoking the test executable directly**, which works perfectly.

Two separate causes, found in order:

**(a) The executables were not MTP applications at all.** `global.json` set
`"test": { "runner": "Microsoft.Testing.Platform" }`, which D-037 recorded as the fix. That setting
governs what `dotnet test` *speaks*. What the executable *is* comes from
`UseMicrosoftTestingPlatformRunner`, which xunit.v3 reads when it generates the entry point at build
time — and which was never set. The generated `Main` was xUnit's own in-process console runner:

```csharp
// before — the console runner, which does not speak MTP
return global::Xunit.Runner.InProc.SystemConsole.ConsoleRunner.Run(args)…
```

Now set in a new `tests/Directory.Build.props`. Two traps in writing that file, both worth
remembering: MSBuild stops at the **first** `Directory.Build.props` it finds walking up, so it must
explicitly `Import` the root one or the test projects lose every setting and restore fails with
`NETSDK1013`; and `GetPathOfFileAbove` searches strictly *above* the directory it is handed, so from
`tests/` it steps over the repository root — an explicit relative import is used instead.

A third trap, twice: **an XML comment cannot contain a double hyphen.** Quoting the TRX command-line
option inside a comment silently emptied `Directory.Packages.props` of every `PackageVersion`, and
the resulting error (`NU1010`, on nine unrelated packages) named nothing to do with the cause.

**(b) The TRX writer was missing.** The report option was rejected as unknown. xunit.v3 carries
`TrxReport.Abstractions` but not the writer. Added `Microsoft.Testing.Extensions.TrxReport` 2.3.3,
matching the MTP version xunit.v3 4.0.0 carries. **New dependency, per CLAUDE.md:** CI publishes a
test report from the TRX, and a run whose results cannot be read is a run nobody looks at.

**(c) What remains broken, upstream.** Even as a correct MTP application with the TRX writer present,
`dotnet test` still reports `Zero tests ran`. The MTP diagnostic log shows the host launched with
`--server dotnettestcli` and a pipe argument, then stopping immediately after `TestHostBuilder`
setup. `TestingPlatformDotnetTestSupport=true` makes no difference. This is an SDK 10.0.400 /
xunit.v3 4.0.0 / MTP 2.3.3 integration problem and is not fixable from here.

**Decision: CI invokes the test executables directly.** They discover and run every test, write TRX,
and return correct exit codes — verified. README and `architecture.md` now say so, with the reason,
because "just run `dotnet test`" is what everyone will try first.

**Revisit** when the SDK or xunit.v3 moves.

#### The Api harness had a one-in-twenty chance of failing for no reason

`A_site_engineer_cannot_approve_money` failed on
`23505: duplicate key value violates unique constraint "ux_users_user_name"` — in seeding, in a test
about permissions.

`PostgresDatabase` is a collection fixture, so **one database serves the whole run**, and xUnit
builds a fresh class instance per test method, so `SeedAsync` re-seeds on every one. Names were
suffixed `Random.Shared.Next(1000, 9999)`: nine thousand values, drawn roughly ninety times a run.
That is a birthday problem with about a **5% chance of a spurious failure per run**. It had simply
been lucky.

Replaced with `UniqueNames`, a process-wide `Interlocked` counter — the right scope, because the
database is process-wide too. Phone numbers moved to it as well: spec.md §2 deduplicates clients by
phone, so a collision there would **merge** two seeded clients rather than reject them, which is a
quieter failure and a worse one.

**Why this matters beyond one red test.** A suite that fails at random teaches people to press
re-run. The next real failure gets re-run too.

#### Also fixed

`README.md` documented `postgres/postgres` for the local test database. `docker-compose.yml` creates
`kaff/kaff`. Anyone following the README got `28P01: password authentication failed` and no obvious
reason why. (CI's own service container uses `postgres/postgres` and is unaffected.)

#### And the same failure shape, found once we were looking for it

The end-to-end suite reported **4 skipped, exit code 0** with `KAFF_E2E_BASE_URL` unset. Every test
carries `[E2EFact]`, which skips itself when the variable is absent so the suite stays runnable on a
laptop with no stack up. The attribute's own comment said "CI sets the variable in the end-to-end
job, where a skip would hide a real failure" — **and nothing enforced it.** Drop the variable from
the workflow, or mistype it, and the end-to-end gate silently stops being a gate while the job
turns green.

Closed with `SuiteConfigurationTests`, a plain `[Fact]` that runs unconditionally and fails only
when `CI=true` and the suite is unconfigured. Verified both ways: unconfigured locally is 1 passed /
4 skipped / exit 0; unconfigured with `CI=true` is 1 failed / exit 2.

**The pattern worth carrying forward.** Three findings in one afternoon — `dotnet test` running
nothing, the flaky seed, and this — share a shape: **a green result that was not evidence of
anything.** The question that finds them is not "does this pass?" but "what would this look like if
the thing it checks were broken?" It is the question the Verifier role exists to ask, and it is
cheaper to ask of the harness than of the code.

---

### D-047 · `spec.md` now carries Karim's rulings, as marked amendments · 2026-08-20

**The problem, found by the BA agent while writing the slice 1 backlog.** Karim's rulings had been
recorded in `decisions.md` (D-010, D-044) and applied to the code — but not to `spec.md`.
`CLAUDE.md` says: *"If code and `spec.md` disagree, `spec.md` wins."* So the file that wins was the
only one that still said the Owner needs a project assignment, that there are eight roles, and that
nobody may create a user.

**Why that is worse than a stale document.** `agents.md` requires the Verifier to work in a fresh
session reading `spec.md` rather than the implementation. A Verifier following its own instructions
would have read §9, tested the Owner against it, and **correctly failed the permission model** — for
implementing a rule Karim gave. The process would have produced a false defect report with the rules
on its side, and the only way to resolve it would have been to re-ask Karim a question he had already
answered.

This had been outstanding since 2026-08-18 as kickoff action A1.

**Decision: amendments beside the original text, never silent edits.** Five 📌 AMENDMENT blocks now
sit in `spec.md` — §6.1 (four decimals stored, two displayed), §6.4 (exactly three floored accounts,
and what is given up), §9 (the Owner and HR rulings, seven numbered points), §13 (health tags are not
states). §0 explains the convention and states that an amendment has the same force as the paragraph
above it and wins where the two disagree.

**What we rejected.** Editing the paragraphs in place. `agents.md` gives the BA the job of "keeping
`spec.md` current when Karim changes his mind, **marking superseded rules loudly rather than editing
them silently**". A silent edit destroys the record of what was true before, which is exactly what
you need when a posting made in June is questioned in November.

**Each amendment carries its own open 🟡 where the ruling left one** — rounding direction (§6.1), what
متعثرة actually means (§13), whether "all master data" was literal (§9). The amendment is not used to
imply more was settled than was.

**Revisit if** the amendment blocks outgrow the text they annotate. At that point `spec.md` should be
rewritten wholesale, with the superseded version kept as a dated file rather than deleted.

#### Also corrected: two spellings of a word required to be verbatim

`agents.md` said **متأجلة**. `CLAUDE.md`, the locale catalogue, and Karim's own ruling of 2026-08-20
all say **تم تأجيلها**. `CLAUDE.md` requires this vocabulary "verbatim, no translations, no
substitutes" — two spellings of it in the continuity files is a defect in the continuity files.

Corrected to تم تأجيلها, with the correction noted inline so the next session does not read it as a
drafting slip and "fix" it back. Three sources agreeing against one, plus Karim's own wording, is not
a business question.

#### And one defect this session introduced and closed

`errors.identity.hr_role_requires_hr_department` was added to the domain earlier today with **no
entry in either locale catalogue**, so it would have rendered to the user as its own key. Found by
the BA agent reading the files.

Fixed, and then made structural: `TranslationCatalogueTests` reflects over every `*Errors` catalogue
in the domain and asserts each `MessageKey` exists in both `ar.json` and `en.json`, that the two
catalogues hold the same key set, and that no entry is blank. Adding a domain error is the most
routine change in this codebase and should not depend on remembering two files in a third language.

**The test was checked against its own standard** (D-046): the key was removed, the suite went red
with two failures, the key was restored, and it went green. A test nobody has watched fail is a test
nobody has tested.

---

### D-048 · Company-wide permissions were never revalidated · **DEFECT, FIXED 2026-08-20**

**Found by the QA agent** while building the nine-role permission matrix — by reading the handler
against the catalogue rather than by running anything.

#### The defect

`PermissionAuthorizationHandler` built its `PermissionSubject` from **token claims**, and the only
thing that checked those claims against the database was `IProjectAccessPolicy` — which the handler
calls only when the request resolves a project:

```csharp
if (subject is not null && projectId is not null)      // ← the whole condition
{
    access = await _projectAccessPolicy.EvaluateAsync(...);
}
```

A `PermissionScope.CompanyWide` permission names no project. So for every one of them the decision
was made **entirely from a token**, with no liveness check and no role check.

**What that meant in practice.** A deactivated user kept every company-wide permission until their
token expired:

| Permission | Held by | Consequence |
|---|---|---|
| `UserManage` | Owner | a revoked Owner can create accounts — including a new Owner |
| `TreasuryPostCompany` | Finance | **a revoked Finance user can still move company money** |
| `AccountManage` | Owner, Finance | open accounts |
| `ClientManage`, `SupplierManage`, `EmployeeManage`, … | various | edit master data |
| `PeriodClose` | Finance | close a period, which is irreversible |
| `AuditRead` | Owner | read the trail that watches them |

The same gap made the **department** claim authoritative. Department is an independent grant axis, and
two grants name a department and no role at all (`SiteExpenseConfirm`, `PhotoPublish`), so a user
moved out of a department kept its permissions until their token expired.

Note this is **staleness, not forgery** — the token is signed, so nobody mints claims. It is the gap
between what was true when the token was issued and what is true now, which is the gap
`decisions.md` D-010 named when it wrote *"a token issued this morning describes this morning"* —
and then closed on only one of the two paths.

#### Why two tests appeared to cover this and did not

`A_deactivated_user_is_refused_at_the_next_request` and
`A_token_claiming_a_role_the_user_no_longer_holds_is_refused` both existed, both passed, and **both
hit project-scoped probe routes.** They exercised the one path where revalidation happened and
reported it as though it were general.

**This is the fourth finding today with that shape** — after `dotnet test` running nothing, the
random-suffix flake, and the end-to-end suite passing with everything skipped (D-046). A green result
is not evidence until you know what red would have looked like.

#### The fix: the token supplies identity, the database supplies authority

New `IPermissionSubjectReader` (Domain) with `PermissionSubjectReader` (Infrastructure). The handler
takes **only the user id** from the principal and reads role, department, sub-department, client
scope and liveness from the users table on every authorized request. A deactivated or deleted account
yields `null`, which the evaluator treats as an unauthenticated caller.

`ProjectAccessPolicy` lost its own re-read, because the subject reaching it is now already
database-derived and the claims-versus-stored comparison had become dead code. One fewer query per
project-scoped request, and one fewer place where the same truth is established twice.

**What we rejected.** Calling `IProjectAccessPolicy` for company-wide permissions with a sentinel
project id — it would have put project semantics on a question that has nothing to do with projects,
and the next person to read it would have had to work out what the sentinel meant. Also rejected:
putting a security stamp in the token and comparing it, which is a second mechanism for the same job
and only closes liveness, not the role and department staleness.

**The cost** is one indexed primary-key lookup per authorized request. `ProjectAccessPolicy` already
made one on project-scoped requests, so for those the count is unchanged; company-wide requests gain
one. Correct authorization is worth a keyed read.

**Revisit if** that read shows up in profiling. The answer then is a short request-scoped cache, not
a return to trusting the token.

#### Verified by watching it fail

Three tests added: a deactivated user losing company-wide permissions, a deactivated **Owner** losing
`UserManage`, and a stale department claim granting nothing.

The handler was then reverted to the pre-fix claims-based build and the suite re-run. **Five tests
failed** — the three new ones and the two existing ones, which now depend on the reader. Restored:
**41 / 41 Api and 61 / 61 Domain**, 0 warnings, `dotnet format` clean.

#### Two related findings, one fixed and one left open

**Fixed.** `PermissionCatalogue`'s own documentation said *"Role.HeadOfDesign holds nothing yet"*
while the `ProjectRead` row granted it. The data was right and the comment was stale — the more
dangerous way round, because a reader trusts the sentence and does not check the table.

**Open, and it needs Karim.** `SiteExpenseConfirm` is granted to
`{ Department = Operations, OperationsSubDepartment = Administrative }` **with no role named**, and
`User.Create` will happily place a `Role.SiteEngineer` there. spec.md §8: *"Site financial expenses
are entered by Finance or Admin, **not the engineer**."* This is the same mechanism a third time —
D-035 (a portal client with a department), D-044 ruling 2 (an HR user in another department), and now
every remaining role.

It is **not exploitable today**: no endpoint requires `SiteExpenseConfirm`, and site expenses are
slice 6. But the fix shape depends on an answer we do not have — whether a site engineer is ever
legitimately part of Operations / Administrative. If never, the fix is an invariant on `User`, the
same shape as the HR one. If sometimes, the grant itself must exclude the role, which needs a
mechanism the `AccessGrant` record does not yet have. Recorded as QA-1 in
`stories/questions-for-karim.md`.

**The general lesson, which outlives all three instances:** a grant written against a department
alone is satisfied by *any* role that carries that department. Every such grant is a standing
invitation to this defect. There are two left.

---

### D-049 · Karim's ten rulings of 2026-08-21 · **APPLIED**

Ten answers that close every question blocking sprint 1 except one. Two changed the schema; one
changed it in a way that could not have been deferred without cost.

#### 1. The audit trail is the Owner's alone

**Decision.** `AuditRead` stays company-wide and Owner-only, and is no longer marked `Unresolved`.

**Why.** It had been an *assumption* since slice 0, logged on every boot and pinned by a test. The
assumption was right; that is not the same as it being answered. From slice 3 the trail records every
movement of money.

**What we rejected, in Karim's words.** A project-scoped audit read for the people working on that
project — "completely hidden from all other roles, **even for their own projects**". The trail
carries financial movements, so scoping it by project would have reopened the zero-financial-visibility
rule from a direction nobody was watching.

🟡 The ruling anticipates "a Global Finance/Audit role (if added later)". **Not added.** Creating a
role nobody has asked for yet is how a permission model grows a member that means nothing.

**The governance point stands and is now explicit:** the only person who reaches every project is the
only person who can read the record of what he did there. That is Karim's business to accept, and he
has.

#### 2–4. Passwords, sessions, onboarding

**Decisions.** Minimum 8 characters, no forced complexity; lockout for 15 minutes after 5 consecutive
failures. Sessions expire after 30 minutes of inactivity; signing out on one device does not sign out
the others; **a password change or a deactivation kills every active session**. Onboarding is a
temporary password set by the Owner which the user must change on first sign-in.

**Why the absent complexity rule is a requirement, not a gap.** Karim: forced complexity is avoided
"so site workers don't struggle to log in". A rule that makes the site engineer write the password on
the inside of a helmet is worse than a simple one he remembers.

**Why forced change matters more than it looks.** Karim: it protects "the integrity of the audit
trail (non-repudiation)". If the Owner sets a permanent password, then for every action that account
ever takes there are two people who could have taken it, and the trail cannot tell them apart.

**Applied here:** `JwtOptions.InactivityMinutes`, defaulting to 30, so the number is configuration
rather than something typed into a handler. The rest is slice 1's to build — but two mechanisms it
needs already exist and should not be rebuilt:

* `User.SecurityStamp` is already rotated by `SetPasswordHash` and by `Deactivate`. That is the
  global-sign-out hook.
* **D-048 already makes deactivation instant** regardless of tokens: every authorized request re-reads
  the user row, so a deactivated account is refused on its next call. The stamp is what closes the
  *password-change* half.

🟡 Not asked, and it matters for the lockout: is the lockout per account or per account-and-address?
Per account alone means anyone who knows a username can lock a site engineer out of the system for
fifteen minutes at a time, indefinitely. Raised as a new question.

#### 5. Leavers are deactivated, and return with nothing

**Decision.** Never deleted; they stay on historical project teams; a returning employee gets a new
password and **zero project assignments**.

**Why it is not just a policy note.** `User.Deactivate` does not touch assignment rows, and
`Reactivate` does not either — so today a returning employee would come back **with every assignment
still active**, which is the opposite of the ruling. Deactivation must revoke the active assignments,
leaving the revoked rows in place as the history Karim wants to keep. `ProjectAssignment.Revoke`
already does exactly that; nothing calls it on deactivation.

Not built here — it is handler work, because the User entity cannot reach the assignment rows.
Recorded against the story, and flagged so it is not mistaken for done.

#### 6. A role change is refused while the user supervises a project

**Decision.** Block the change; do not auto-remove. HR takes them off each project first.

**Why.** Karim: auto-removing a supervisor "leaves a construction site headless". The manual step is
the point — it is what a handover looks like in the data.

This answers the model-shape problem the BA found: `ProjectAssignment.Create` refuses a Supervisor
level for anyone who is not a Site Engineer, so a role change would otherwise leave rows the system
would never have allowed. The answer is that the change is refused, not that the rows are fixed.

Slice 1 work — `User` has no `ChangeRole`, and the guard needs assignment data the entity cannot see.

#### 7. Client codes are generated

**Decision.** Sequential, `C-10001`, manual entry and editing both forbidden. **Closes D-040's first
half**, which had flagged `Client.Code` as a required field spec.md never asked for. It was right to
flag and the answer is that it should exist.

**Applied:** `Client.Create` documents the code as generator-supplied, and there is deliberately no
setter, so a code cannot change after creation. The generator itself is slice 1's.

#### 8. Duplicate client phones are allowed, with a warning · **SCHEMA CHANGE**

**Decision.** Warn, name the client that already holds the number, and **do not block the save**.

**Why.** Karim: "a corporate client and its CEO might be registered as two separate entities sharing
the same contact number."

**This reverses a constraint, which is the direction that costs something.** `ux_clients_phone` was a
**unique index** — the database refused the save outright. It is now `ix_clients_phone`, non-unique.
spec.md §2's "deduplicated by phone" and §3's "never create a duplicate client" are amended in place.

**What is given up.** Nothing now prevents two client records for one person. The control moves from
the database to a human reading a warning, and a human dismissing a warning is a well-understood
failure mode. The mitigation is that the match must be *good*: it runs on the normalised phone, so
`+20 10 …`, `0020 10 …` and `010 …` all match. **A missed match used to mean a wrongly-accepted save;
it now means a warning nobody sees.** Matching is more load-bearing after this ruling, not less.

**Revisit if** duplicate clients start appearing in the data. The signal is two client records with
one phone and overlapping projects.

#### 9–10. Withholding moves to the contract · **SCHEMA CHANGE**

**Decision.** The withholding category lives on `Project`, not `Client`. Finance sets it during
contract creation or approval; Marketing cannot.

**Why the old model could not have been right.** spec.md §6.7 sets the rate by *what is supplied* —
1% contracting and supplies, 3% services, 5% professional fees — and §5.4 explicitly links a design
project to its execution project **for one client**. One value per client cannot express 5% on the
design and 1% on the execution. The BA found this writing KAFF-122; §6.7's own sentence "each Client
carries a flag" is what had been implemented, and it contradicted the section it sits in.

**Why Finance and not Marketing.** Karim: the rate "directly dictates ledger entries and money
reconciliation. It is a strict accounting parameter, not a marketing detail." §6.7's justification is
that a wrong flag means "collections will never match issued extracts and staff will invent
adjustments to close the gap" — a permanent 1–5% shortfall on every collection, small enough to be
closed by hand and large enough to matter by the end of a project.

**Applied:**

* `Project.WithholdingCategory`, defaulting to `None` — the safe default, because between creation and
  Finance's decision the contract must claim no rate rather than guess one. A guessed rate is
  indistinguishable from a decided one by the time an extract is issued.
* `Project.SetWithholding(category, clientKind)` refuses a rate on an individual client's contract
  (§6.7). The client's kind is passed in rather than looked up, because the domain holds only
  `ClientId` — and the rule is too expensive to leave to the caller.
* `Client.SetWithholding` became `Client.SetTaxRegistration`, which refuses a registration number on
  an individual. **That closes the second half of D-040** — the defect where the code accepted what
  §6.7 plainly denies.
* The registration number itself stayed on `Client`: it identifies the legal entity and does not vary
  by contract.

**Migration `WithholdingOnContractAndSoftPhoneDedup`.** EF scaffolded `defaultValue: ""` for the new
column. The enum is stored as text so the SQL guards can compare it by name, and `""` is not a member
of it — an existing row backfilled with `""` would have failed to materialise on the next read, as a
cast error naming nothing. Corrected to `"None"` in both `Up` and `Down`.

**What `Down` cannot do**, and the migration says so: a project's rate cannot be pushed back onto its
client when two projects for one client disagree. Reversing restores the shape, not the data.

🟡 **Not ruled on: subcontractors and suppliers.** §6.7's next paragraph — "when Kaff pays
subcontractors and suppliers, Kaff withholds" — has exactly the same shape, and those rates are still
held on the party record. Karim's ruling named the client only, so nothing was changed there.
Extending it would be inventing the ruling he did not give. **New question.**

#### Verified

| Gate | Result |
|---|---|
| `dotnet build --configuration Release` | 0 errors, **0 warnings** |
| `dotnet format --verify-no-changes` | clean |
| Domain tests | **67 / 67** (61 before; 6 added, all withholding) |
| Api tests, real PostgreSQL 16 | **41 / 41** |
| Migration against the **existing** database | applied; `clients.withholding_category` dropped, `ix_clients_phone` non-unique, `ux_clients_code` still unique, `projects.withholding_category` defaulting to `'None'` |

The migration was run against the database that already had the old schema, not only against the
fresh one the test fixture builds — those are different code paths, and only the second had ever been
exercised.

---

### D-050 · The access token lives in an HttpOnly cookie · 2026-08-21

**Decision** (Nabil and the Architect, answering N1). The access token is carried in an
`HttpOnly; Secure; SameSite=Strict` cookie. **`localStorage` and `sessionStorage` are prohibited for
it.** UI state comes from a separate `GET /api/auth/me` returning profile claims and no token.

**Why.** `localStorage` is readable by any script that reaches the page. In a system holding real
ledgers, a single injected script would hand over an authentication token — and the token's holder can
approve extracts. This was flagged at the 2026-08-18 kickoff as "a real decision, not a default" and
deferred to the slice-1 auth design; slice 0 had shipped the `localStorage` version in the meantime.

**Applied now, before the login story is written:**

* `AuthService` no longer stores anything. It holds profile facts fetched from `/api/auth/me`, and
  the `Session` type **has no token field** — the shape itself refuses the mistake.
* `authInterceptor` sets `withCredentials: true` on same-origin `/api` calls instead of attaching a
  bearer header. There is nothing left for it to attach and nothing left to steal.
* `JwtBearerEvents.OnMessageReceived` reads the token from the cookie named by `JwtOptions.CookieName`.
* CORS gained `AllowCredentials()`, which is required for the cookie to travel — and is the reason the
  origin list must stay explicit: a browser rejects a wildcard origin outright when credentials are in
  play. An empty `Kaff:AllowedOrigins` therefore means no browser origin may call the API, which is
  the correct default rather than an oversight.

**Two details that are load-bearing, not decoration:**

**The cookie name is `__Host-kaff-auth`.** A browser accepts a `__Host-` cookie only when it is
`Secure`, path `/`, and carries no `Domain` — which means a subdomain cannot set it. That closes
cookie fixation from a neighbouring host, and turns the prefix into a constraint rather than a naming
convention.

**`SameSite=Strict` is the CSRF control**, and it is the whole of it. The browser will not attach the
cookie to a cross-site request at all, so no anti-forgery token is needed. **If that ever relaxes to
`Lax` or `None`, an anti-forgery token is required the same day** — a cookie sent automatically on
cross-site requests is the textbook CSRF setup.

**The `Authorization` header still works, deliberately.** Service-to-service callers and the
integration suite use it, and neither is reachable by an XSS payload in the SPA. The event only
supplies the cookie when the header is absent.

**What we rejected.** A refresh-token pair with a short-lived access token in memory. It is defensible
and widely used, but it is two mechanisms where one suffices for a first-party SPA on one origin, and
the memory-held access token still passes through JavaScript. Revisit if a third-party client or a
mobile app needs bearer tokens — the header path is already open for exactly that.

**Not built here.** Issuing the cookie, sliding its expiry against `JwtOptions.InactivityMinutes`,
clearing it on sign-out, and the `/api/auth/me` endpoint are slice 1 (KAFF-101, KAFF-105). What is
built is the constraint: the storage the ruling forbids is gone, and the path it requires is wired.

---

### D-051 · Karim's five answers and the Architect's three · 2026-08-21 (second round)

Closes every question blocking sprint 1. **No code changed** — all eight land in stories that are
not built yet. What follows is what they decided and the one place a ruling reversed itself.

#### Q27 · A role change now revokes every assignment · **REVERSES D-049 §6**

**Decision.** Moving a Site Engineer to the Technical Office **automatically revokes every project
assignment they hold — Supervisor and Junior alike.** If they are still needed on the project, HR
re-assigns them in the new role.

**This is the opposite of yesterday's answer.** D-049 §6 said the change is *refused* while the user
is an active Supervisor, because auto-removal "leaves a construction site headless". Today: "their
direct link to the site must be severed automatically to prevent lingering responsibilities."

Both rulings weigh the same two risks — a headless site against a lingering liability — and land on
opposite sides. The second is the answer. **The reversal is left visible in `spec.md` §9 rather than
edited away**, because a rule that changed direction is exactly the kind a future session will
"correct" back if it only sees the current state.

**It also closes the gap the first ruling left.** A Site Engineer holds only Junior or Supervisor
rows, so blocking on Supervisor alone let a Junior-only engineer through, leaving rows
`ProjectAssignment.Create` would refuse to create. "Whether Supervisor or Junior" covers it, and the
mirror case — an office user becoming a Site Engineer with `Standard` rows — is covered by the same
mechanism: revoke on any role change, then re-assign.

**Not built.** `User` has no `ChangeRole`, and revoking assignments needs rows the entity cannot
reach. Handler work, KAFF-109. `ProjectAssignment.Revoke` already does the right thing and keeps the
row as history.

#### Q31 · The first Owner comes from a one-time setup screen

**Decision.** Shape B. A screen that appears **only when the users table is empty**, creates the
Owner, and locks permanently afterwards.

**Why, in Karim's words:** "I do not want hidden database scripts. My name and account creation date
must appear naturally in the Audit Trail from day one."

That is the deciding argument, and it is an audit argument rather than a convenience one. A seeded
account has no actor — the first row in the trail would name nobody.

**What this costs, and it must be built for:** an unauthenticated endpoint that creates an Owner,
whose entire correctness rests on an emptiness check. It is the most privileged endpoint that will
ever exist here. Two things the story has to answer: the check must be atomic against a concurrent
second request, and "locks permanently" must mean the emptiness test, not a flag anyone can clear.

#### Q32 · HR sees a project's name and its team, and nothing else

**Decision.** "HR may only see the project name and the list of assigned engineers … If the main
project dashboard contains financial data, HR must be routed to a separate 'Project Team' tab/screen
that contains zero financial details."

This resolves the tension D-044 ruling 2 created and answers a question raised three times in three
registers. Note the shape of the answer: **a separate surface, not a filtered view** — the same
pattern spec.md §12 uses for the client portal, and the same reason. A filtered view leaks the first
time somebody adds a field.

Implies a new narrow permission rather than granting HR `ProjectRead`. Naming it is the story's.

#### Q33 · The client portal is a separate host

**Decision.** Clients sign in at a different URL. "Their portal must be a completely isolated
interface."

Strengthens D-035, which found the portal one careless endpoint from leaking. A separate host makes
the boundary infrastructural rather than a matter of every future endpoint remembering.

🟡 Not asked: whether the portal is a separate deployment or the same API behind a second origin. The
second still needs the cookie's `Domain` and CORS thought through — see D-050.

#### Q38 · Password recovery is an Owner-generated link sent by SMS or WhatsApp

**Decision.** The employee tells the office; the Owner generates a **temporary reset link**; it goes
to their registered phone.

**Why the Owner cannot simply type a new password:** "that would compromise the non-repudiation of
the Audit Trail" — the same reasoning as D-049 ruling 4, applied consistently. If the Owner sets a
password the user keeps, every action that account takes has two possible authors.

🟡 The story must decide link lifetime, single-use, and what happens to active sessions on reset —
D-049 ruling 3 says a password *change* kills all sessions, and a reset is a password change.

#### N5 · No session table; two mechanisms, each for its own case

**Decision** (Architect). Routine per-device sign-out clears the cookie in that browser. Global kill
— stolen phone, password change — rotates `User.SecurityStamp`, and the API rejects any token
carrying the old one.

**Correct, and it accepts a known limit rather than hiding it:** with no per-session identity there is
no way to revoke *one other* device. Losing a phone means signing out everywhere. That is the right
trade for a first-party SPA on one origin, and adding a session table later is additive.

**What does not exist yet.** `KaffClaimTypes.SecurityStamp` is defined and `User.SecurityStamp`
rotates on `SetPasswordHash` and `Deactivate` — but **nothing compares the two.** The global kill is
declared, not implemented. Deliberately not built here: with no token issuance, validation would need
a "skip when the claim is absent" fallback, and a revocation check with a bypass is worse than an
absent one. **It belongs to KAFF-101a and the story must say so.**

Note `Reactivate` does not rotate the stamp. Combined with Q27's revoke-on-role-change and D-049
ruling 5's return-with-zero-assignments, reactivation is the one path that should rotate and does not.

#### KAFF-105 split, approved

`105a` returns identity and roles — unblocked, and the frontend needs it to know anyone is signed in
at all (D-050). `105b` returns the project list, deferred behind Q32's new permission.

The report recommended this split; the Architect approved it. Recorded because it is the second time
a story was blocked whole when only part of it was unanswerable — the first was KAFF-101. Worth a
Definition-of-Ready line: **block the smallest thing that is actually unanswerable.**

---

### D-052 · F-04 fixed, project creation answered, the Owner's own password · 2026-08-21

Three rulings — one Architect, one Nabil, one Karim. Two changed code and both are verified by having
been watched to fail first.

#### 1. F-04 · `SiteExpenseConfirm` no longer reachable by a site engineer · **DEFECT, FIXED**

**Architect's ruling:** *"No documented exceptions. The gate must pass with 100% compliant code …
Financial permissions like `SiteExpenseConfirm` must never be granted to a bare department without
specifying a role."*

**The defect.** The grant read `{ Department = Operations, OperationsSubDepartment = Administrative }`
with **no role**. `PermissionEvaluator.Matches` skips the role comparison when a grant's `Role` is
null, so the grant was satisfied by *any* role carrying that department — and `User.Create` will
place a `Role.SiteEngineer` there. spec.md §8 names that exact exclusion: *"Site financial expenses
are entered by Finance or Admin, **not the engineer**."*

**This was reachable today, and I had said otherwise.** I recorded it as "not reachable until slice 6"
on the grounds that no endpoint requires the permission. That is true of the **Api** half only; the
**Domain** half needed nothing but a call to the evaluator, which is where the rule actually lives.
The Scrum Master corrected it against `PermissionCatalogue.cs` line 213 and `PermissionEvaluator.cs` line 124.
Worth keeping because the reasoning error is reusable: *"no endpoint calls it"* is a statement about
reach, not about whether a rule is wrong.

**The fix.** `SiteExpenseConfirm` is granted to `Role.Finance`, and to `Role.TechnicalOffice`
**conditional on** Operations / Administrative. Every criterion set on a grant must match, so the
Technical Office holds it only from the Administrative sub-department, and a site engineer parked
there holds nothing.

**Also added: `No_financial_permission_is_granted_to_a_bare_department`**, which pins the *mechanism*
across all eleven money-touching permissions rather than the one row that failed. F-04 is the third
appearance of this shape — D-035 (a portal client with a department), D-044 ruling 2 (an HR user in
another department), and now every remaining role. Fixing the row without pinning the class would
have invited a fourth.

**🟡 `PhotoPublish` is still a bare-department grant, and is deliberately left.** The ruling is scoped
to *financial* permissions and publishing a photo moves no money, so extending it there would be
applying a rule nobody gave. It is the last one. If §9's "Operations / Administrative owns reports,
photos and tasks" is not meant to include, say, a site engineer moved into that sub-department, it
needs its own ruling.

**Verified by watching it fail.** With the role line removed from the grant — reproducing exactly the
pre-fix condition — both new tests go red. Restored: **70 / 70 Domain, 41 / 41 Api**, 0 warnings,
`dotnet format` clean.

*(A note on that verification: the first attempt reported green against a stale binary because an
incremental build had silently failed. The check that catches this is trivial — confirm the build
exit code before believing the test result — and it is the same family as D-046.)*

#### 2. Q17 · Only the Owner and the Technical Office may open a project

**Karim:** Marketing brings in the client and registers their master file, but opening a project
*"triggers engineering items, accounting ledgers, and cost tracking. It is strictly a technical and
administrative responsibility. Site Engineers and Marketing have no business creating projects."*

**This was the oldest open question in the catalogue** (D-012, raised at slice 0). `ProjectManage` was
granted to **nobody**, so no project could be created at all. `PermissionCatalogue.Unresolved` is now
down to a single row — `PeriodClose`, where spec.md §6.6 requires a month-end close and does not say
who performs it.

**🟡 The scope is still wrong, and it now matters.** The row is `ProjectScoped`, so the evaluator
refuses when the request names no project — and a **create** request cannot name one, because the
project does not exist yet. As written the permission can authorise *editing* a project and cannot
authorise *opening* one, which is the half Karim just ruled on.

Fixing it means either making the row company-wide — which would also drop the assignment requirement
from editing, weakening §9 — or splitting create from edit into two permissions. That is an
architecture decision with a §9 consequence, not a drafting choice, so it is **raised rather than
taken**. Lands in slice 4 with KAFF-407.

#### 3. Q44 · The first Owner is not forced to change his own password

**Nabil's ruling.** The forced-change rule of D-049 ruling 4 exists for an account created *for
somebody else* with a credential its creator knows. The first Owner types his own password at the
setup screen; nobody else has ever known it, so the non-repudiation the rule protects is not at risk,
and forcing a change would be ceremony.

Recorded as a clarification nested inside the §9 amendment rather than as a new rule, because that is
what it is — the scope of an existing rule, not an exception to it. Story-level; no code.

**Why it needed answering at all:** QA and UX disagreed about whether it was Karim's question or
Nabil's, and the Scrum Master routed it here rather than resolving it. That is the routing working.

---

### D-053 · The session kill is real, and money never rides on a department · 2026-08-22

Two directives from Nabil's sprint-1 broadcast, both built and both verified by being watched to fail
first.

#### 1. `SecurityStamp` — the global sign-out now exists

**It was declared and not implemented.** `KaffClaimTypes.SecurityStamp` was defined at slice 0.
`User.SecurityStamp` rotated on `SetPasswordHash` and on `Deactivate`. **Nothing ever compared the
two.** D-051 recorded the gap when the Architect chose stamp rotation over a session table (N5); this
closes it.

**How it works.** `ICurrentUser` gained `SecurityStamp`, read from the token claim.
`IPermissionSubjectReader.ReadAsync` now takes it and refuses a mismatch, so rotating the stamp
invalidates **every token in existence for that user, at once**. That is the whole of Karim's rule
(D-049 ruling 3): *"a password change or account deactivation must kill all active tokens"*.

**No bypass, deliberately.** There is no "skip the check when the claim is absent" path — D-051 names
that trap by name, and a revocation check with a bypass reads as protection while granting none. A
token without the claim is not a token this system issued, so it is refused. `A_request_with_no_security_stamp_is_refused` pins it.

**The comparison lives in the `WHERE` clause**, not in memory, which keeps it ordinal and
case-sensitive at the database. A stamp is an opaque identifier, not text a human reads; a
culture-aware or case-insensitive comparison could only widen what matches.

**What the harness had to give up, and why that is correct.** `TestAuthHandler` now emits the stamp
only when a header supplies one. Giving the test double a stamp that always matches would have
disabled the global sign-out for the entire suite — a harness reporting safety the product does not
have, which is the failure D-046 catalogues four times. `SendAsync` reads the user's *current* stamp
by default, which is what a token minted a moment ago carries; a test proving revocation passes a
stale one on purpose.

**Note what the new test does not do.** It rotates via a **password change**, leaving the account
active. Testing it through `Deactivate` would have proved nothing about the stamp, because `IsActive`
would have refused the request first — the assertion would have passed whether or not the mechanism
existed.

**Accepted limit, restated because it will surprise somebody:** revocation is all-or-nothing. Losing
a phone signs you out everywhere. That is N5's trade, and a session table remains additive later.

#### 2. Money never rides on a bare department — now enforced at the point of decision

D-052 fixed the one row that had leaked. Nabil's directive asked for the refusal in
`PermissionEvaluator` itself, and that is a stronger thing than it looks: **the catalogue test only
protects the rows that exist today.**

`PermissionDefinition` gained `TouchesMoney`, set on the eleven permissions that move money,
authorise a movement, or govern the ledger. The evaluator discards any grant with a null `Role` on
those before matching. A bare-department grant added to a financial permission tomorrow is refused at
runtime, not merely reported by a test somebody has to run.

**Scoped exactly to the ruling.** `PhotoPublish` is still a bare-department grant and still works —
it moves no money, and the Architect's ruling is about financial permissions. Extending it would be
inventing a rule nobody gave. It is the last one, and it is Q52 in the register for slice 6.

**A second evaluator overload** takes a `PermissionDefinition` directly, so the refusal can be tested
against a definition the shipped catalogue deliberately no longer contains. The `Permission` overload
looks up the definition and calls it — one code path, not two.

**The flag is pinned in both directions.** The test asserts the expected eleven as a written-out list
rather than reading `TouchesMoney`, because reading the flag would let a permission quietly stop
being financial and still pass.

#### Why this is the third mechanism against one mistake, and not one too many

A department-only grant is satisfied by **any** role carrying that department. That single fact has
produced three separate leaks: D-035 (a portal client with a department reaching `EmployeeManage`),
D-044 ruling 2 (a Marketing user moved to HR holding it), and F-04 (a site engineer confirming site
expenses — the one role spec.md §8 excludes by name). Each was found after the fact, by someone
reading rather than by anything failing.

There are now three layers: the catalogue names roles, a test fails the build if that regresses, and
the evaluator refuses it at runtime. For a mechanism with three prior incidents in a system holding
real ledgers, that is proportionate.

#### Verified

| | |
|---|---|
| Build | 0 errors, **0 warnings** |
| `dotnet format` | clean |
| Domain | **71 / 71** |
| Api, real PostgreSQL 16 | **43 / 43** |

**Each new guard was watched to fail.** With the stamp comparison removed and the evaluator's money
guard removed, three tests went red — `Rotating_the_security_stamp_kills_every_existing_session`,
`A_request_with_no_security_stamp_is_refused`, and the evaluator's constructed-definition case — then
green on restore. The build exit code was checked **before** the test result each time, which is the
correction D-052 records from getting that wrong.

---

### D-054 · The test harness recommended credentials this repository never creates · **FIXED 2026-08-22**

Found by the Scrum Master agent, whose own first Api run died on it.

`PostgresDatabase` defaulted to `postgres/postgres` when `KAFF_TEST_DB` was unset, and **the error
message it threw on failure recommended the same wrong credentials.** `docker-compose.yml` creates
`kaff/kaff`. So the documented first-run path — start the compose database, run the tests — failed
with `28P01: password authentication failed`, and the guidance printed alongside the failure sent the
reader back to the credentials that had just been refused.

The README had the same error and was fixed on 2026-08-20 (D-046). **This was the copy that fix
missed**, and it is the worse one: a stale document is read once, a wrong default fires on every run.

Both call sites now say `kaff/kaff`, and the error message names `docker compose up -d db` so the
next person does not have to work out where the credentials were supposed to come from.

**Verified the way the defect would have been caught:** the Api suite now passes **43 / 43 with
`KAFF_TEST_DB` unset entirely**, which is the path nobody had ever exercised — every previous run in
this project set the variable explicitly, which is exactly why the default was free to be wrong.

**CI is unaffected.** Its service container really is `postgres/postgres` and it sets the variable
explicitly, so CI would never have caught this. Local-only defaults are not exercised by the pipeline
that is supposed to protect them.

**Worth recording what the harness got right in the same breath**, because it is the counter-example
to D-046's theme: it refused to fall back to an in-memory provider and said why — *"the rules they
check live in the database, and a provider that does not run them would report safety that does not
exist."* A red result that is evidence. The defect was the credentials, not the design.

---

### D-055 · Nabil's rulings of 2026-08-22 — the permission split, and story currency becomes law · **APPROVED**

Six rulings in one message: two business (Karim's, via Nabil), three technical and process (the
Architect's, signed by Nabil), and one order of movement. Recorded here because three of them change
the permission model, one changes the Definition of Ready, and one is a waiver that must be visible
rather than assumed.

---

#### 1. Q-N10-2 · `ProjectFinancialsEdit` — a third permission, because two correct rulings collided

**Decision.** **The Finance department will never hold `ProjectManage`.** An accountant must not alter
the engineering scope of a project. The contract's tax and financial settings move to their own
endpoint (`Project.SetWithholding`) behind a new permission, **`ProjectFinancialsEdit`**, granted to
`Role.Finance` and `Role.Owner` alone.

**Why this needed a ruling at all, and it is worth being precise about.** Nobody invented anything and
nobody drifted. **Two of Karim's own rulings met and pointed opposite ways:**

* **D-052 §2** granted `ProjectManage` to the Owner and the Technical Office, from a ruling about
  *opening* a project — *"strictly a technical and administrative responsibility."*
* **D-049 rulings 9–10** gave **Finance** the contract's withholding category — *"a strict accounting
  parameter, not a marketing detail"* — and `Project.SetWithholding` already exists
  ([Verified: 2026-08-22 @ `Project.cs` -> `SetWithholding`]).

Finance holds no `ProjectManage` grant, so an edit endpoint gated on `ProjectManage` would refuse
Finance the one field Karim assigned to them. The Architect raised it as Q-N10-2
(`proposals/N10-project-creation.md` -> `Q-N10-2`) and this refinement carried it as **SM-30**.

**What we rejected, and the rejection is the substance of the ruling.** Adding Finance to
`ProjectManage` was the one-line fix and it is the wrong one: `ProjectManage` governs the engineering
scope of a project, and a grant written to reach one field hands over the whole record. That is the
same shape as D-035, D-044 ruling 2 and F-04 seen from the other direction — **a grant wider than the
act it was written for.** Splitting keeps each grant the size of its ruling.

**🟡 Scope left to the Backend brief deliberately, with the reasoning stated so the choice is
reviewable rather than incidental.** The row governs a contract's tax setting, so `TouchesMoney`
(D-053) and §9's assignment requirement are both in play. Recorded because the decision is cheap to
make and expensive to make silently:

* **`TouchesMoney: true` is the expected setting.** Karim's own justification for moving withholding
  onto the contract is that the rate *"directly dictates ledger entries and money reconciliation"* —
  that is *governing the ledger*, which is D-053's own test for the flag. Both grants name a role, so
  the evaluator's guard ([Verified: 2026-08-22 @ `PermissionEvaluator.cs` -> `TouchesMoney`])
  discards nothing today; the flag exists for the grant somebody writes next year. Setting it makes the
  written-out list of eleven money-touching permissions twelve — which is deliberate friction: that
  list is pinned by name so the change has to be a conversation.
* **`ProjectScoped` is the expected scope**, unlike `ProjectCreate`. The project exists, so the request
  can name it, and §9's *"role alone is insufficient"* therefore still applies. Finance already holds
  `ProjectRead`, `TreasuryPostProject`, `FinancialMovementPrepare` and `FinancialMovementDisburse` as
  project-scoped rows; a company-wide financial row would be the odd one out in Finance's own set.

**🟡 One consequence this raises rather than closes — Q-N10-1's exact shape, one entity across.**
Finance has **no global reach**: the access policy gives it only to `Role.Owner` and `Role.Hr`, and
everyone else falls through to the assignment lookup
([Verified: 2026-08-22 @ `ProjectAccessPolicy.cs` -> `GlobalReachAsync`]). So on a
newly-opened project **Finance cannot set the withholding category until HR or the Owner assigns
Finance to that project.** Karim said Finance sets the rate *"during contract creation or approval"*,
which reads as immediate. Either staffing precedes the tax setting, or opening a project implies
something. **A workflow question for Karim, not a permission question, and not resolved here.** It
blocks nothing before slice 4 — no endpoint in the committed fifteen touches it.

**Revisit if** a second contract-level financial field appears that Finance owns and the Technical
Office does not. One field behind its own permission is proportionate; five would argue for a
`ProjectFinancials` surface rather than a field-level grant.

---

#### 2. Q42 · `UserRead` — HR may see who exists, and nothing more

**Decision.** A new **`UserRead`** permission, **CompanyWide**, granted to `Role.Hr` and `Role.Owner`.
**Names and roles only** — no editing, and no visibility into salary if one is ever added.

**Why.** HR holds `ProjectAssignmentManage` and could not name a single person to put on a project.
There was no user-read member at all
([Verified: 2026-08-22 @ `Permission.cs` -> `enum Permission`]). HR could reach every
project and staff none of them.

**The register's trap is respected, not overridden.** `stories/questions-for-karim.md` -> `Q42` warned in
terms: *"Do not close it by handing HR the Owner's user list"* — that list carries usernames, roles,
departments and active state for every account in Kaff, which would repeat one screen over the mistake
Q32 was answered to avoid. **Nabil's ruling is narrower than the warning's worst case:** names and
roles. **So the permission is not the whole control — the endpoint's projection is.** A `UserRead`
endpoint returning the full user row satisfies the permission and breaks the ruling. Whoever builds it
projects name and role, and stops.

**The register's second trap also holds:** `EmployeeManage` looks like the answer and is not. `User`
and `Employee` are different entities; the Employee register is slice 2 and could not produce a login
list in slice 1 even if HR's grant reached it.

**Routing, recorded because it is the second instance.** Q42 sat in the register as *for Karim*
(`stories/questions-for-karim.md` -> `Q42`). **Nabil answered it himself**, as he did Q44 (D-052 §3). That
is legitimate — he is the decision owner's proxy — and it is worth noting only because it saved a
Karim round trip on the one open question that was blocking committed work. The register's split
between *"for Karim"* and *"for Nabil and the Architect"* is doing real work.

**What we rejected.** Granting HR `ProjectRead`, or the Owner's user-administration surface. Both give
HR reach it has no business having, and D-044 ruling 2's *"zero financial visibility"* is pinned by a
test precisely so it cannot erode one screen at a time.

**Revisit if** HR needs to see anything about a user beyond name and role — which is a question for
Karim, not a widening of this row.

---

#### 3. N10 approved · `ProjectCreate` splits from `ProjectManage`

**Decision.** **Approved as proposed** (`proposals/N10-project-creation.md`, design A). Opening a
project requires **`ProjectCreate`** — **CompanyWide**, Owner and Technical Office. Modifying a project
stays behind **`ProjectManage`** — **ProjectScoped**, so §9's assignment requirement keeps applying to
every edit.

**Why company-wide is not a weakening.** A create request cannot name the project it is about to
create, and `PermissionScope.ProjectScoped` requires one — the evaluator returns `ProjectNotSpecified`
([Verified: 2026-08-22 @ `PermissionEvaluator.cs` -> `ProjectNotSpecified`]). Scope is the only
instrument that reaches the act; reach cannot, because there is nothing to reach. The alternative —
widening `ProjectManage` itself — fixes creation **by removing the assignment requirement from
editing**, which is the §9 consequence that made this a decision rather than a drafting choice.

**Immediate, not deferred.** D-052 raised the defect and left it for slice 4. Nabil's ruling is *"split
the permission immediately"*, and the reason is the one this project keeps relearning: the 🟡
`SCOPE IS UNRESOLVED` comment on the `ProjectManage` row
([Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`]) is a claim a
reader trusts. **It is now answered and must be rewritten, not left.** A stale comment a reader
believes is the D-035 failure mode, recorded four times in this repository.

**What does not change**, verified rather than assumed: `ProjectManage`'s grants stay
`[owner, technicalOffice]`; the evaluator, the access policy and the authorization handler are
untouched; and there is **no database change** — permissions are code, `PermissionCatalogue.Build()`
returns a `FrozenDictionary` and nothing persists a permission id. **Zero impact on the committed
fifteen.**

**The proposal's check on `ProjectAssignmentManage` stands and is worth keeping:** it is *not* the same
defect. Its subject exists and can be named, so global reach (D-044 ruling 3) is the right instrument
and already solves it.

---

#### 4. The readiness waiver — signed, and it must be visible in the stories

**Decision.** The Architect, in writing: *"I accept the six stories containing uncited rules to pass
them through the Definition of Ready so the sprint does not stall. I take this on my own responsibility
as the Architect."*

`process/agile.md`'s Definition of Ready says an uncited rule is a question, not a story. Applied
literally, nothing entered the sprint. The waiver is the named exception, and **naming it is the whole
point** — a waiver recorded only in a decisions file is invisible to whoever builds the story.

**Applied:** each of the six stories records the waiver **in the story**, against the rule it covers,
with the signature and the date. Q45–Q51 remain open. The waiver lets the story be built; it does not
answer them.

**Revisit if** a waived rule turns out to be wrong. The cost then lands on the Architect by his own
signature, which is the correct place for it and the reason a signed waiver is worth more than a shrug.

---

#### 5. SM-29 becomes workplace law · the Story Currency Law

**Decision, in Nabil's words:** *"Any story that claims a state in the code (e.g. 'The code refuses X')
must carry a verification date, filename, and line number next to it (e.g.
[Verified: 2026-08-22 @ User.cs:232]). Stories commanding the code to match a past state are
disguised defects. The 'evidence before trust' rule applies to the documentation just as it does to
the code."*

**Why it earned a law rather than an action.** Five stale story assertions in three days, and one was
not cosmetic: `AC-108-A` asserted the **F-04 leak as correct behaviour**, and KAFF-108 is third in the
build order. **A story can command a defect.** Backend builds what the story says.

**The mechanism it fixes is structural, not careless.** `spec.md` has amendment blocks, `decisions.md`
has D-numbers and superseded markers, `qa/questions.md` has strike-through. **Stories had no staleness
mechanism at all** — a story asserts the state of the code in the present tense, is written once, and
is read as current forever. The code moved four times in three days.

**Added to the Definition of Ready** in `process/agile.md`. It binds every agent, not only the BA: no
finding is repeated from a document without re-reading the file that document names, **today**.

---

#### 6. What this deliberately does **not** change

Stated because each is a plausible next step somebody will take:

* **`ProjectManage` keeps its name and its grants.** It is not renamed to `ProjectEdit`; the name is
  correct for what the row does and it is cited across the stories and the QA matrix.
* **No permission is merged.** Three narrow rows are the design, not an intermediate state. A later
  session that "tidies" `ProjectCreate` back into `ProjectManage` re-opens the §9 hole the split
  avoids — the comment on the row must say so.
* **`PhotoPublish` is still a bare-department grant.** Q52, slice 6. The Architect's ruling is scoped
  to financial permissions and a photo moves no money; extending it here would be applying a rule
  nobody gave.
* **No Bank master entity, no global finance/audit role, no consultant role.** D-045's and D-049's 🟡s
  are untouched.
* **No database migration for any of the three permissions.** Permissions are code. The migration
  D-055 does require is for the `User` fields in §7 below, which is a separate thing.
* **The withholding rate on subcontractors and suppliers is untouched.** D-049's 🟡 stands — Karim's
  ruling named the client only.

---

#### 7. The four fields the rulings put ahead of the business logic

Nabil, to Backend: *"before writing the core business logic, you must immediately"* close N-14/15/16
and N-19. Recorded here because the ordering is a decision, not a preference.

**`User` gains a must-change-password flag, lockout state, and a way to clear a credential.**
[Verified: 2026-08-22 @ `User.cs` -> `MustChangePassword`] — none of the three exists today. Five
committed stories depend on the flag alone. Migration required; the permissions are not.

**`ProjectAccess` must return the grant path** — `OwnerGlobal`, `HrGlobal` or `Assignment`. Today one
branch serves the first two ([Verified: 2026-08-22 @ `ProjectAccessPolicy.cs` -> `GlobalReachAsync`],
both calling `GlobalReachAsync`) and the returned record carries only `Granted` and `Level`
([Verified: 2026-08-22 @ `PermissionEvaluator.cs` -> `record ProjectAccess`]).

**Why this one cannot wait, stated plainly:** KAFF-116 records *how* access was granted, the audit
table is append-only and trigger-protected, and **a field never written cannot be backfilled.** If it
ships after the first access-policy consumer, the gap is permanent. That is why it lands before any
consumer, not merely before KAFF-116.

---

#### 8. Open after this entry

| # | Question | Owner | Blocks |
|---|---|---|---|
| **Q-N10-1** | Does opening a project put its creator on it? A Technical Office user who opens a project holds no assignment row and cannot read it one line later | Karim | KAFF-407, slice 4 |
| **Q-N10-2b** | **New, raised by this ruling.** Finance has no global reach, so Finance cannot set a new contract's withholding until somebody assigns Finance to that project. Karim said *"during contract creation or approval"* | Karim | KAFF-416, slice 4 |
| **Q-N10-3** | Does opening a project require the Owner's approval? A state machine, not a permission | Karim | KAFF-407, slice 4 |
| **Q45–Q51** | The six uncited rules. **Waived, not answered** — §4 | Karim | nothing; waiver signed |
| **Q52** | `PhotoPublish`'s bare-department grant | Karim | slice 6 |

**Q17, Q42, Q-N10-2 and F-27 are closed by this entry**, and must be marked closed at their source —
`stories/questions-for-karim.md`, `qa/questions.md`, `qa/slice-1/permission-matrix.md` — not only here.
A question answered in one file and left open in three is how a closed finding gets re-reported as
live, which happened four times on 2026-08-21.

> ⚠️ **§7 above went stale within the hour it was written.** It says the four fields do not exist;
> Backend then built them, in the same run, before the session limit stopped it. `MustChangePassword`,
> `FailedSignInAttempts`, `LockedOutUntil`, `SetTemporaryPassword`, `SetOwnPassword` and
> `ClearPassword` all exist [Verified: 2026-08-22 @ `User.cs` -> `SetTemporaryPassword`], with
> migration `20260821221842_UserLockoutAndForcedPasswordChange`. `ProjectAccessPath` exists and is in
> use. **Read §7 as the reason for the ordering, not as a statement of what is built.** See D-056.

---

### D-056 · What the interrupted sprint run left behind · **RECOVERED 2026-08-22**

The Scrum Master's sprint-1 execution run was killed mid-edit by an account session limit at 01:19.
It had written D-055, amended `spec.md`, `process/agile.md` and the UX documents, built the four
`User` fields, added `ProjectAccessPath`, and added the three new permission rows. **The tree it left
did not build.**

This entry records the recovery, because "an agent stopped early" is not a description anybody can
act on six weeks from now, and because two of the four things found are defects rather than
loose ends.

#### 1. The build was broken — a rename with two missed call sites

`User.SetPasswordHash` had been split into `SetOwnPassword` (no forced change) and
`SetTemporaryPassword` (forced change) — the right shape for D-049 ruling 4. Two Api test call sites
still named the old method, so `Kaff.Api.Tests` did not compile. Both were a holder changing their own
password, so both take `SetOwnPassword`.

**Note what this means about the interruption:** the last file the agent touched was a test file. Had
it stopped ninety seconds earlier the tree would have built and the sprint would have looked finished.

#### 2. Two guards failed, and both were correct to fail

`Hr_holds_exactly_two_permissions_and_neither_touches_money` and
`No_financial_permission_is_granted_to_a_bare_department` both went red, because both pin their
expected set as a **written-out list** rather than reading it back from the catalogue. `UserRead` and
`ProjectFinancialsEdit` joined those sets by approved ruling, so the lists needed updating — which is
the design working exactly as D-053 intended. A catalogue cannot grow quietly.

**And updating them surfaced a real gap.** `Hr_holds_exactly_two_permissions_and_neither_touches_money`
**never asserted the money half of its own name.** It pinned the set and nothing else; the money claim
was decoration a reader would have trusted. Adding a financial permission to HR would have been caught
only by the set changing. The assertion now exists, and was watched to fail: marking `UserRead` as
`TouchesMoney` turned it red, then green on revert.

#### 3. The defect: three permissions shipped with no test, and one test lied about its subject

`ProjectCreate`, `ProjectFinancialsEdit` and `UserRead` were reachable in the catalogue and named in
**no test anywhere** — the two list-pins above mention them, and nothing exercised them.

Worse, `Only_the_owner_and_the_technical_office_may_open_a_project` still asserted against
**`ProjectManage`** — which, after the D-055 §3 split, is the permission that *cannot* open a project.
The test would have stayed green forever while testing something its own name disclaimed.

`proposals/N10-project-creation.md` -> `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project` had predicted the test that mattered and called it *"the test
the whole proposal exists to make possible … without it the design is a comment."* It was not written.
It is now: `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`, plus
`Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope` and
`Hr_may_read_the_user_list_and_still_reaches_nothing_financial`.

**Watched to fail.** Widening `ProjectManage` to `CompanyWide` — the exact mistake the split exists to
prevent, and the smaller diff a future session will be tempted by — turns **one** test red, the new
one. Before today that mutation was caught by nothing at all.

#### 4. A process finding, recorded against the process and not the agent

The permission rows carry unusually good reasoning: `ProjectFinancialsEdit`'s comment argues its own
scope, and it **raised Q-N10-2b rather than resolving it**, which is the behaviour `agents.md`
principle exists to produce. The prose was excellent and the tests were absent.

**These fail in opposite directions.** Prose that is wrong gets read and doubted; a permission with no
test is invisible. The Definition of Done already requires the test — what it does not require is that
the test be **named in the same change as the row**, which is what would have made the gap visible
while the agent still had budget. Proposed as **SM-30**, alongside SM-29: a new catalogue row and its
test land together, and the row's comment cites the test by name.

#### 5. My own mistake, recorded because it will recur

The two call sites in §1 were first fixed with a PowerShell `Get-Content -Raw` / `Set-Content` round
trip. **PowerShell 5.1 reads a BOM-less UTF-8 file as Windows-1252**, so every `§`, `—` and `…` in
both files was silently double-encoded, and `Set-Content -Encoding utf8` then added a BOM. The build
would still have passed — mojibake in comments compiles.

Reversed by reading back as UTF-8 and writing cp1252 bytes, then verified: valid UTF-8, no BOM, six
`§` and eighteen `—` intact, zero `Â`/`â€` sequences. **Use the editing tools for text edits in this
repository, not shell string replacement.** This repository is Arabic-facing; a mangling that compiles
clean is the worst available kind.

#### Verified after recovery

| | |
|---|---|
| Build, Release, warnings as errors | 0 errors, **0 warnings** |
| `dotnet format --verify-no-changes` | clean |
| Domain | **74 / 74** (71 before, plus the three new) |
| Api, real PostgreSQL 16 | **43 / 43** |

**Not done, and not started:** QA's relock of the 241 test cases; the BA's story updates for the three
new permissions and for SM-29; marking Q17/Q42/Q-N10-2/F-27 closed at their source, which D-055 §8
explicitly requires and which remains open. The endpoints for all three new permissions are slice 4.

---

### D-057 · SM-30 adopted, amended by its own first failure · 2026-08-22

**Scrum Master's ruling on the proposal in D-056 §4, plus three stale claims found while closing the
registers it named. Recorded here because one of the three is inside D-055 and the ruling changes
`process/agile.md`.**

#### 1. SM-30 is adopted, with an amendment the proposal could not have known it needed

**Decision.** **A new permission catalogue row and a test that names it land in the same change. The
row's comment cites that test by name, and the name must be one that exists.** Written into
`process/agile.md` beside SM-29, and into both the Definition of Ready and the Definition of Done.

**Why, against the one real argument on the other side.** The argument for rejecting SM-30 is that the
Definition of Done already requires permission tests, so the rule buys nothing. **It does not hold,
and the reason is structural rather than a matter of diligence.** The DoD is a *slice* gate, checked
at the end. On 2026-08-22 `ProjectCreate`, `ProjectFinancialsEdit` and `UserRead` shipped reachable in
the catalogue and named in no test anywhere, while the suite stood at **74/74 green**. **A row with no
test does not make any test fail.** A gate that tests for red cannot see an absence.

**And SM-30 is the inverse of SM-29, which is why adopting both is not redundancy.** SM-29 catches a
claim that is **wrong**; SM-30 catches a claim that is **missing**. D-056 §4 put it exactly right:
these fail in opposite directions — prose that is wrong gets read and doubted, a permission with no
test is invisible.

**The amendment, and it is not pedantry — SM-30's mechanism had already misfired twice before it was
ruled on.** SM-30 as proposed requires the comment to cite a test by name. **Two such citations were
checked this morning and both are wrong**, in the two files the rule most directly governs:

* **`src/Domain/Authorization/PermissionCatalogue.cs`, the `ProjectManage` row**, cites two tests as
  pinning the design and **one does not exist**. `Opening_a_project_needs_no_project` appears only at
  `proposals/N10-project-creation.md` -> `Opening_a_project_needs_no_project`, where it was a *proposed* name; the identifier is absent
  from `tests/` [Verified: 2026-08-22]. The real test is
  `Only_the_owner_and_the_technical_office_may_open_a_project`
  [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`], repointed the same
  day from `ProjectManage` to `ProjectCreate`.
* **D-056 §2 itself** — the entry that proposes SM-30 — names
  `Hr_holds_exactly_two_permissions_and_neither_touches_money` twice. **That test no longer exists
  under that name.** It is `Hr_holds_no_permission_that_touches_money`
  [Verified: 2026-09-04 @ `CatalogueCompletenessTests.cs` -> `Hr_holds_no_permission_that_touches_money`], renamed **in the
  same run that wrote the entry**, because `UserRead` made the set three. D-056 §2's *substance* is
  correct and was verified: the money half of the name is now genuinely asserted (`:183-185`), and it
  was decoration before.

**So the rule's own proposal contains an instance of the rule's own failure mode.** That is not an
argument against SM-30 — it is the strongest available argument that the citation half needs a check
behind it, because both misfires were written by careful agents in entries that are otherwise
exemplary. **A citation nobody can mechanically check decays into exactly the thing SM-29 exists to
stop.**

Two consequences follow, and the second is the one that matters:

* **A cited test name is a claim about the code, so SM-29 already binds it.** Verify before writing.
* **The enforceable half is coverage, not prose.** A comment tells the next reader where to look; it
  cannot tell them the cover is real. **What can is one test that fails when a catalogue row is named
  in no test.** Backend owes it. Until it exists, SM-30 is enforced by reading the diff in refinement,
  **which is weaker, and is recorded as weaker rather than described as done.**

**What SM-30 does not require.** An endpoint. All three new rows have none until slice 4, and their
tests are catalogue and evaluator tests. That is the right level, and it is the level at which the
mutation was watched to fail: widening `ProjectManage` to `CompanyWide` turns exactly one test red,
and before 2026-08-22 it turned nothing red at all.

**Rejected.** Deferring SM-30 until the slice-4 endpoints exist. That is the reasoning D-052 used
about F-04 and D-056 quotes back at it: ***"no endpoint calls it" is a statement about reach, not
about whether a rule is wrong.*** The rule lives in the Domain half, which needs nothing but a call to
the evaluator.

**Revisit if** the coverage test lands and makes the comment citation redundant. At that point the
citation is a convenience, not a control, and can be relaxed to optional.

#### 2. The four closures D-055 §8 required were made, and the sweep found two more

D-055 §8 required **Q17, Q42, Q-N10-2 and F-27** to be marked closed at their source and not only in
`decisions.md`. That was still outstanding this morning. **Done now**, in
`stories/questions-for-karim.md`, `qa/questions.md` and `qa/slice-1/permission-matrix.md`, each with
the D-number and the date. **N10** is closed with them — it was the residual of Q17 and was still
carried as an open Architect decision. **Q-N10-2b** is registered as open for Karim, and so are
**Q-N10-1** and **Q-N10-3**, which D-055 §8 lists as open and which **had never reached the master
register at all** — they lived only in the proposal.

**Two closed findings were still marked live and were found only by re-reading the source, not the
finding.** Both are recorded because the discovery method is the point:

* **F-26** — the `SecurityStamp` global kill, marked *"declared and not implemented"* in both
  `qa/questions.md` and `qa/slice-1/permission-matrix.md`. It was built on 2026-08-22 (D-053 §1)
  [Verified: 2026-08-22 @ `PermissionSubjectReader.cs` -> `ReadAsync`], and an
  **absent** stamp claim is refused before the query rather than skipped — which is the trap D-051
  named. `Reactivate` still does not rotate the stamp; that is KAFF-112 rule 9a, not this finding.
* **F-25's permission half** — *"no permission expresses who may set the withholding category"*.
  `ProjectFinancialsEdit` is that permission (D-055 §1). **F-25's other half is untouched** and is the
  half that was always the risk: `SetWithholding` trusts a `ClientKind` the caller supplies.

#### 3. D-055 §4 says "Applied" and it was not

D-055 §4 records the Architect's signed readiness waiver and states: *"**Applied:** each of the six
stories records the waiver in the story."* **No file under `stories/` contains the word "waiver"**
[Verified: 2026-08-22]. The waiver exists only in `decisions.md` — which is precisely what §4's own
next sentence says is worthless: *"a waiver recorded only in a decisions file is invisible to whoever
builds the story."*

**This is the third stale claim inside D-055 in two days**, after §7 (four `User` fields said not to
exist, built within the hour — D-056) and §8 (four closures required, none made). Assigned to the BA
in this run.

**A count discrepancy is left open rather than resolved:** §4 says **six** stories; seven story files
reference Q45–Q51, because **Q51 spans four of them**. The BA was told to work it out from the files
and report it, not to force the number either way.

#### 4. What this says about the process, and it is the reason the entry exists

**The same failure class has now been found three days running, in three different artefacts:** the
stories on 2026-08-21, `decisions.md` on 2026-08-22 (D-056), and `decisions.md` again in this entry.
SM-29 was written for stories. **`decisions.md` needs it more than the stories do**, because a D-entry
is read as settled history by every future session — it is the file `CLAUDE.md` sends agents to first.

**The pattern is specific and worth naming: it is the word "Applied".** D-055 §7 stated a code state
that a later paragraph of the same run falsified. D-055 §4 stated an action as complete that was never
performed. **Both were written by the agent that intended to do the work, in the same breath as
intending it.** An entry that records an intention in the past tense is indistinguishable from one
that records a fact, and nothing in the format separates them.

**Ruling: a `decisions.md` entry may state what was *decided* in the past tense. It may not state what
was *applied* unless the application was verified after it happened, with a `file:line`.** Where an
entry records work still to do, it says so under **"Not done"** — which is the one thing D-056 got
right and D-055 did not. Added to `process/agile.md` under SM-29, which already binds every agent
rather than only the BA.

#### 5. The Scrum Master broke SM-29 while enforcing it, within the hour — and this is the finding

**Twelve of the `[Verified: 2026-08-22 @ file:line]` citations written into the registers during this
very sweep were wrong.** They were found by the BA, checking my brief against the files exactly as it
had been instructed to. They are corrected. **The mechanism deserves recording far more than the
error does.**

| What I wrote | What is true |
|---|---|
| `PermissionCatalogue.cs` lines 172-210 — the three new rows (×8 sites) | `ProjectManage` **`:200`**, `ProjectCreate` **`:213`**, `ProjectFinancialsEdit` **`:238`**, `UserRead` **`:257`**. My range covered one of the four |
| `PermissionCatalogue.cs` lines 212-224 — `UserRead` (×2 sites) | **`:257`**. `:212-224` is a different row entirely |
| `PermissionEvaluator.cs` lines 148-151 — `ProjectNotSpecified` (×2 sites) | **`:197`** |

**How it happened, precisely, because the mechanism is general.** I read the catalogue with
`sed -n '168,232p'`, saw the rows I needed inside that window, and **wrote the line numbers from the
window I had asked for rather than from the rows themselves.** The window was real, the rows were
real, and the citation was invented. **I trusted my own transcript of a file I had read four minutes
earlier** — which is, word for word, the thing SM-29 exists to forbid and the thing I put in writing
in three briefs that morning.

**Two of the same class in `decisions.md` D-055, not mine, left in place deliberately.** D-055 cites
`PermissionEvaluator.cs` line 135 for the evaluator's money guard (it is **`:182`**) and
`PermissionEvaluator.cs` lines 148-151 for `ProjectNotSpecified` (it is **`:197`**). **D-055's body is not
edited** — it is a historical record and D-047's convention is to mark rather than rewrite. They are
corrected here and nowhere else.

**And one substantive error the same check caught, which is worse than a line number.** D-055 §7 says
`ProjectAccess` must return *"`OwnerGlobal`, `HrGlobal` or `Assignment`"* — **three paths. There are
four.** `ProjectAccessPath` also carries **`PortalClient`** ([Verified: 2026-08-22 @
`PermissionEvaluator.cs` -> `enum ProjectAccessPath`] — `None = 0`, `OwnerGlobal = 1`,
`HrGlobal = 2`, `Assignment = 3`, `PortalClient = 4`). It is deliberately not folded into `Assignment`
because a portal client holds no assignment row; the match is `Project.ClientId` against
`User.ClientId`. **My brief to the BA repeated D-055's three without checking, and the BA caught both
of us.** KAFF-116 had four all along and was right.

**Why this belongs in the permanent record rather than in a meeting note.** The audit table KAFF-116
writes to is **append-only and trigger-protected**. A story built from *"three grant paths"* writes a
column that can never carry the fourth, and **a field never written cannot be backfilled** — D-055 §7's
own argument for why the field had to land early, turned against the sentence that makes it.

**What this says about SM-29, and it is not that the rule failed.** The rule worked. It produced a
dated, checkable claim; a second agent checked it in minutes and found it false. **An undated claim
would have been unfalsifiable and would have survived.** The lesson is narrower and more useful:

> **A `file:line` is only as good as the command that produced it. Cite from a `grep -n` on the
> identifier, never from the bounds of a window you happened to read.**

Added to `process/agile.md` under SM-29. It is the cheapest possible fix — one search — and it is the
difference between a citation that is checkable and one that is merely dated.

**The count, stated plainly for the record:** over three days this failure class has now been found in
the **stories** (2026-08-21), in **`decisions.md`** (D-056), in **`decisions.md` again** (D-057 §3, the
"Applied" waiver), in a **source comment** (F-28, a cited test that does not exist), and in **the
Scrum Master's own enforcement sweep** (this section). Five artefacts, five authors, one mechanism.
**It is not carelessness and it will not be fixed by asking people to be careful.**

---

### D-058 · A `file:line` citation rots on the next edit, and rots silently · 2026-08-22

D-057 diagnosed the failure correctly and then prescribed *"cite from a `grep -n` on the identifier,
never from the bounds of a window you happened to read"* — **which is asking people to be careful**,
the thing its own last line says will not work. This entry is the demonstration, and it is mechanical
rather than argued.

#### What happened, in one hour

The Scrum Master's sweep verified its register citations and reported them correct. That report was
true when written. Then `PermissionCatalogue.cs` was edited — the F-28 fix, plus the three SM-30
citations the new rows owed — adding roughly twenty comment lines **above** the rows most documents
cite. Every citation below the insertion point shifted.

**Nobody wrote anything wrong. The citations decayed because a line number is a position, and an edit
moves positions.**

#### Measured, not estimated

A sweep of every `@ <path>:<line>` in the repository — 197 citations across 30 files:

| | |
|---|---|
| Citations found | **197** |
| File missing | 3 — all three are SM-29's own **example** `User.cs` line 232, an illustration of the format, not a claim |
| Line out of range | 0 |
| **Resolvable but pointing at the wrong thing** | **at least 14** |

The `ProjectManage` row is at `PermissionCatalogue.cs` line 208 [Verified: 2026-08-22 by
`Select-String -Pattern 'new\(Permission\.ProjectManage'`]. **Nine documents cite `:200`**, which is
now the middle of a comment sentence — `qa/questions.md` ×4, `qa/slice-1/permission-matrix.md` ×3,
`stories/questions-for-karim.md`, `stories/slice-1-foundation/KAFF-113`. `EmployeeManage` is at
`:310`; several documents cite `:315`, which is the `// ---- Site execution ----` section header.
`KAFF-117:33` cites `:396`, **a blank line**.

#### The part that matters, and the reason this is its own entry

**Every one of those 14 passes a resolvability check.** The file exists, the line exists, nothing
errors. A checker that asks *"does this line exist?"* reports 194/197 healthy on a corpus where at
least 14 point at the wrong thing. That is not a weaker version of correct — **it is a green light
with no evidence behind it**, which D-046 catalogued four times under its own name and which this
project keeps re-inventing in new forms.

And the decay is **invisible at the moment it happens**: the person editing `PermissionCatalogue.cs`
breaks citations in nine files they never opened.

#### The amendment this argues for — SM-31, for the Scrum Master to rule on

> **Cite a stable identifier, not a position.** `PermissionCatalogue.cs → the Permission.ProjectManage
> row`, or `User.cs → SetTemporaryPassword`. A line number may follow as a convenience hint, never as
> the claim.

An identifier survives every edit that does not delete the thing being cited — and if it *is* deleted,
a search for it returns nothing, which is a **loud** failure instead of a silent one. That inverts the
current behaviour, where deleting the cited code leaves the citation pointing confidently at whatever
slid into its place.

It is also mechanically checkable, which `grep -n` discipline is not: a script can assert that the
cited identifier exists in the cited file. The verification script used for the numbers above is at
`scratchpad/check-citations.ps1`; it currently checks resolvability only, and **its own output is the
argument for why that is not enough.**

#### Not done

**The 14 stale citations are not fixed.** They span roughly a dozen documents owned by the BA and QA,
and rewriting them to line numbers would only reset a clock that starts ticking again on the next
edit — the remediation and the rule should land together. Raised for the Scrum Master, who is out of
session budget until 17:10; this entry is the handover, not the fix.

**Also unresolved:** whether SM-29's dated-claim requirement survives in its current form. It should —
D-057 §5 is right that dating made the claim falsifiable, and an undated claim would have survived
unchallenged. **The date is doing real work; the line number is doing harm.** Those are separable and
this entry proposes separating them.

---

### D-059 · SM-31 adopted — cite the identifier, keep the date, drop the line · 2026-08-22

**Scrum Master's ruling on D-058. ADOPTED, with one part decided against D-058's framing and one
number corrected upward.**

#### 1. The ruling

> **Cite a stable identifier, not a position.**
> `[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`]`
>
> **The date stays. The line number may follow as a convenience hint, never as the claim.**

Written into `process/agile.md` as **SM-31**, into the Definition of Ready and the Definition of
Done, and enforced by `scripts/check-citations.ps1`. **D-057 §5's remedy is superseded**, four hours
after it was adopted, and its section is struck through rather than deleted — it is the reasoning
that produced this one.

#### 2. Why I did not simply accept D-058: the date and the line number are separable

D-058 frames SM-29's dated-claim requirement as *"unresolved"*. **It is resolved, and it resolves the
other way from the framing.** The two halves of a citation do different jobs:

* **The date says *when the claim was checked*.** Nothing else carries this. It is what made a false
  claim falsifiable within the hour on 2026-08-22 — an undated claim would have survived
  unchallenged, which D-057 §5 records against the Scrum Master's own work.
* **The line number said *where*.** An identifier says where better and says it stably.

**So SM-29 is untouched and SM-31 replaces only its position half.** Keeping the date is not
conservatism: a dated identifier citation can still go stale *in meaning* — the identifier survives
while the code beneath it changes — and the date is the only thing that tells a reader how much to
trust it. **Date plus identifier is strictly stronger than either.**

#### 3. D-058 undercounted, and the real number changes the remediation

D-058 measured *"at least 14"* citations resolving to the wrong thing. Re-measured independently:
**~68 citations point at `PermissionCatalogue.cs` across 30 distinct line numbers, from `:58` to
`:396`** [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`].

| Cited | Times | What is there now |
|---|---|---|
| `:238` | **16** | middle of a comment sentence |
| `:258` | **10** | a **blank line** |
| `:200` | **9** | middle of a comment sentence |
| `:180` | 7 | middle of a comment sentence |
| `:257` | 7 | `TouchesMoney: true),` — a fragment of a row's arguments |
| `:315` | 5 | the `// ---- Site execution ----` section header |
| `:208` | **1** | **the row itself — the only correct citation of the set** |

These are **archaeological strata of one file's edit history**, and every one was correct on the day
it was written. Nobody wrote anything wrong.

**And the migration is not 14 rows, it is 182.** `scripts/check-citations.ps1` on first run:
**3 identifier citations (0 broken), 182 legacy line-number citations** [Verified: 2026-08-22].
Fourteen are *provably* wrong today; **the other 168 are wrong on a schedule nobody controls.**
Migrating only the fourteen would fix the ones we happened to catch and leave the mechanism intact —
which is the shape of every fix this project has already had to redo.

#### 4. The check, and why it is a grep rather than a tool

`scripts/check-citations.ps1` asserts that a cited identifier appears in the cited file. That is all
it does.

**The point is what the old check could not do.** A resolvability check — *"does line 200 exist?"* —
reported **194/197 healthy** on a corpus where dozens of citations pointed at blank lines and comment
fragments. A line number **always** resolves. That is not a weaker check; **it is a green light with
no evidence behind it**, which is D-046's failure wearing new clothes.

**The failure mode inverts, which is the actual prize.** Delete the cited code today and the line
number points confidently at whatever slid into its place. Delete it under SM-31 and the search
returns **nothing** — loud instead of silent.

**Two deliberate limits, stated so they are choices rather than gaps.** It does not verify that the
cited identifier *supports the claim made about it* — no script can, and SM-29's date is what covers
that. And it currently exits **1**, because 182 legacy citations remain; it goes green when the
migration lands, not before. **An enforcement script that is green before the work is done would be
the third instance of the same failure in this entry.**

**One implementation note worth keeping.** The first draft of the script died on a Unicode arrow:
PowerShell 5.1 read the BOM-less UTF-8 file as cp1252 and mangled it into a parse error. The script
is now **pure ASCII with no backslash literals** — D-056 §5 applies to the tooling as much as to the
prose it checks.

#### 5. What I rejected

* **Keeping `grep -n` discipline (D-057 §5).** D-058's argument is unanswerable and it is D-057's own:
  *"it will not be fixed by asking people to be careful."* D-057 then asked people to be careful. The
  counter-evidence arrived in four hours.
* **Dropping the date with the line number.** §2 above. This is the one place I ruled against D-058's
  framing.
* ~~**Banning line numbers outright.** A hint after the identifier costs nothing and helps a human
  scroll. It is demoted from the claim to a courtesy, and **no checker ever reads it.**~~ **REVERSED the same day — see §9 below.**
* **Fixing only the 14 known-wrong citations.** §3. That restarts a clock instead of stopping it.
* **Building a citation index, linter or CI gate.** The check is a grep. Whether it runs in CI is a
  separate decision for whoever owns `ci/`, and it should not be smuggled in here.

#### 6. Not done

**The 182 legacy citations are not migrated.** Delegated to the BA (`stories/`) and QA (`qa/`), who
own the documents; the Scrum Master's own registers are in the same sweep. **The rule and the
remediation land together** — rewriting to fresh line numbers would only reset the clock, which is
why D-058 correctly declined to fix them.

**Backend still owes the SM-30 enforcement test** — one test that fails when a catalogue row is named
in no test. Recorded as owed in D-057 §1 and still owed. **It is a sibling of this script and must not
be merged with it:** one asserts a name exists in a C# test file, the other asserts a name exists in a
source file from a markdown document. Same shape, different domains, and combining them would be
clever rather than lazy.

#### 7. The judgement this entry exists to record

**This failure class has now been found in six artefacts by six authors in three days**: the stories
(2026-08-21), `decisions.md` (D-056), `decisions.md` again (D-057 §3, the "Applied" waiver), a source
comment (F-28, a cited test that never existed), the Scrum Master's own enforcement sweep (D-057 §5),
and now the citation corpus entire (D-058).

**Five of the six fixes were rules asking the next author to be more careful. None of them held.**
D-057 §5's lasted four hours, and it was written by the agent enforcing the rule it broke.

**SM-31 is the first of them that is not a request.** It does not ask anyone to check anything; it
changes what a citation *is*, so that the common case — an unrelated edit somewhere else — cannot
break it, and the uncommon case — a deletion — breaks it loudly. **That is the test for whether this
converges: not whether the next rule is stricter, but whether it can be checked by a machine that
does not care how careful anyone was.**

#### 8. Migration status at the time of writing

| Area | Owner | Legacy citations | State |
|---|---|---|---|
| `decisions.md` | Scrum Master | 13 | **migrated** |
| `process/agile.md` | Scrum Master | 2 | **migrated** |
| `proposals/` | Scrum Master | 3 | **migrated** |
| `stories/` | BA | 146 | delegated, in progress |
| `qa/` | QA | 18 | delegated, in progress |
| **Total** | | **182** | **18 done, 164 outstanding** |

`scripts/check-citations.ps1` after the Scrum Master's own 18: **23 identifier citations, 0 broken,
164 legacy remaining, exit 1** [Verified: 2026-08-22].

**On editing `decisions.md`'s historical entries, because it looks like a violation of D-047 and is
not.** D-047's convention is that a *ruling* is marked superseded rather than rewritten. **A citation
is a pointer, not a ruling.** Every claim in D-051 through D-056 is untouched, verbatim; only the
form of the pointer beside it changed, from a line number that had already decayed to an identifier
that cannot. Where D-055's pointer was recorded as wrong in D-057 §5 — `PermissionEvaluator.cs` line 135
and `:148-151` — the identifier now names what the sentence was always talking about
(`TouchesMoney`, `ProjectNotSpecified`). **Rewriting a broken link is not rewriting history; leaving
it broken to honour a convention about rulings would be the error.**

#### 9. SM-31 amended within the hour — the "convenience hint" exemption is withdrawn

**Found by QA while migrating `qa/`, challenging the rule it had been given rather than applying it.
This is the most useful thing produced today and it is worth being precise about why.**

SM-31 as first ruled (§1) permitted a line number *"as a convenience hint, never as the claim"*. I
reasoned that a hint costs nothing and helps a human scroll, and that no checker would read it.
**Both halves of that were wrong, and the second is what made the first dangerous.**

QA measured the exemption instead of accepting it: **77 bare line hints repo-wide**, of the form
`` (`PermissionCatalogue.cs` line 258) `` rather than `` @ `File.cs` line 123 ``, **which
`scripts/check-citations.ps1` did not count** because its pattern required the `@` prefix.

| Hint | Times | What is there |
|---|---|---|
| `` `PermissionCatalogue.cs` line 258 `` | **8** | a **blank line** |
| `` `PermissionCatalogue.cs` lines 238-248 `` | 5 | mid-comment |
| `` `PermissionCatalogue.cs` lines 180-182 `` | 4 | the `Permission.ProjectManage` row, now at 208 |
| `` `PermissionEvaluatorTests.cs` lines 96-131 `` | 4 | shifted |

**They decay at exactly the same rate as the claims they were exempted from.** A hint is a line
number; a line number is a position; an edit moves positions. Nothing about calling it a courtesy
changed its physics.

**And the checker was blind to every one of them** — which means SM-31, as ruled at 16:40, would have
reported **green on a corpus containing 77 stale pointers.** That is D-058's central finding
reproduced *inside D-058's own remedy*, by the person ruling on it, four hours after writing that this
class *"will not be fixed by asking people to be careful"*.

**Ruling: the exemption is withdrawn. A line number is not a citation in any position.**
`scripts/check-citations.ps1`'s legacy pattern no longer requires the `@` prefix, so a bare
`` `File.cs` line 123 `` is counted wherever it appears. On the amended check: **230 identifier citations,
0 broken, 98 legacy remaining** [Verified: 2026-08-22] — the legacy count *rose* against the migration
because the checker can now see what it was missing, which is the correct direction for a number to
move when a blind spot closes.

**The general lesson, and it is not about line numbers.** Every exemption I wrote into SM-31 was an
exemption from the *check*, not from the *rule*. **An exemption a checker cannot see is indistinguishable
from a violation it cannot see.** The test for a rule on this project is no longer "is it strict
enough" but "is all of it mechanically visible" — and the honest answer for a hint was no.

**What this says about the process, given §7.** Five of six fixes to this failure class were requests
to be careful. SM-31 was the first that was not — and it still shipped with a carve-out that
reintroduced the defect at one-third the volume. **It was caught because a subagent was briefed to
report anything in its brief that was wrong when checked against the files, and did.** That instruction
is doing more work than any of the five rules did.

#### 10. What the two migration agents found in the briefs I gave them

**Both were told to report anything in their brief that did not survive contact with the files. Both
did, and between them they corrected the Scrum Master four times.** Recorded because the instruction
is now demonstrably the most productive control on this project — see §9.

**1. The worked example of a correct SM-31 citation was itself a broken citation.** My brief modelled
an enum member as the form *"PermissionEvaluator.cs, arrow, ProjectAccessPath.PortalClient"*. That string
does not occur in that file [Verified: 2026-08-22 @ `PermissionEvaluator.cs` -> `ProjectAccessPath`]:
inside the enum the member is declared bare as `PortalClient = 4`, and the qualified form appears only
where it is *used*, in `ProjectAccessPolicy.cs`. **A C# enum member is never self-qualified at its
declaration site**, so the most natural-reading citation form fails the check. Recorded in
`process/agile.md` as the first of three identifier-choice traps.

**2. The per-line counts in D-059 §3 are repo-wide and were quoted at the BA as if scoped.** `:258` —
ten times repo-wide — occurs **zero** times in `stories/`. The measurement is right; the framing was
misleading and it wasted the BA's search. **A number is not a finding until it says what it counted.**

**3. `.json` was a blind spot in both directions.** Locale-catalogue citations were neither counted as
legacy nor verifiable as identifiers. Both agents hit it independently. `json` is now in the checker's
extension list, so ``@ `ar.json` -> `errors.identity.hr_role_requires_hr_department` `` is checked like
any other citation. **The extension list is the checker's real scope, and nobody had looked at it.**

**4. The `146` figure was exact for the `@`-prefixed form and undercounted the work** by the bare
hints and the JSON refs — which the §9 amendment then confirmed from the other direction.

#### 11. A live defect found outside the fence, and it is the F-28 shape in the file that matters most

The BA found this while verifying and correctly did not touch it — `src/` is Backend's.

`src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `ProjectAccessPolicy`, in the class
remarks that justify HR's global reach, states: *"Reach only: HR holds just `EmployeeManage` and
`ProjectAssignmentManage`, so global reach buys no financial visibility."*

**HR has held three since `UserRead` landed** (D-055 §2), and the catalogue says so in its own remarks
— *"Role.Hr holds exactly three rows: EmployeeManage, ProjectAssignmentManage and UserRead"*
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.UserRead`]. **Two source files now
contradict each other about HR's grant set, and the stale one is the file where the access rule
lives.**

**Why this is more than a comment.** The sentence is not decoration — it is the *argument for the
rule*: global reach is safe **because** the grant set is small and touches no money. A reader
checking that argument against a two-item list will not notice a third item they were told does not
exist. The conclusion still holds — `UserRead` is `TouchesMoney: false` and reaches no project — **but
the reasoning shipped with a fact that is no longer true, which is exactly F-28 with a comment instead
of a test name.**

**Owner: Backend. One sentence.** To be carried into `qa/questions.md` with the next unused `F-`
number by QA; recorded here so it is not lost if that does not happen.

#### 12. Final state of the migration

| | |
|---|---|
| Identifier citations, verified | **279** |
| **Broken (identifier absent)** | **0** |
| Legacy line-number citations remaining | **48**, all in `qa/`, in progress |
| `stories/` | **zero** line-number tokens of any kind, in any position |
| `decisions.md`, `process/`, `proposals/` | **zero** |

Started at **182** by the `@`-prefixed count, which the amendment in §9 revealed was itself an
undercount. **The number went up twice while the work went forward** — once when the `@` requirement
was dropped, once when `.json` was added — and both rises were blind spots closing. **A remediation
count that only falls is a remediation count that is not looking hard enough.**

#### 13. Where the sweep stops, and why it stops there rather than at zero

**Final: 283 identifier citations, 0 broken, 103 legacy remaining** [Verified: 2026-08-22,
`scripts/check-citations.ps1`]. `stories/`, `qa/`, `decisions.md`, `process/` and `proposals/` are at
zero for code-file citations. **The sweep is deliberately not finished, and the residue is assigned:**

| Pool | Count | Owner | Why it is not done today |
|---|---|---|---|
| `meetings/` | **~76** | Scrum Master, next session | Minutes of a conversation on a date. Lowest value, largest pool, nothing builds from them |
| `.md` -> `.md` cross-references in `stories/` and `qa/` | **~25** | BA, QA | A different kind of citation needing its own identifier convention — see below |

**On `.md` -> `.md` citations, which the checker could not see until this afternoon.** QA found them
one file-type over from the blind spot it had just closed: `qa/` cited
`KAFF-115-project-team-panel.md` lines 82-87 and `decisions.md` line 1787 by line, into files the BA was
rewriting all day. **`md` is now in the checker's extension list**, which is why the legacy count rose
from 48 to 113 while the work went forward.

**The identifier for a document already exists and is better than anything code has**: `AC-115-H`,
`D-055 §3`, `F-27`, `TC-1-242`, or a section heading. These are permanent by their own rules
(`stories/README.md`, `process/agile.md`) — **stories and QA cases have had stable identifiers since
SM-23 and were still citing each other by line number.** That is the same failure the AC-ID scheme was
adopted to end, surviving in the citations *between* the documents the scheme fixed.

**Why I stopped rather than pressing on.** The rule is settled, the checker sees every pool including
the two it was blind to this morning, and the two highest-value pools are at zero. **The previous
sprint run was killed mid-edit of a test file at 01:19 with the tree not building** (D-056) — the
lesson recorded there is that the last thing attempted on a long run is the thing that breaks. **A
finished rule with a measured, owned residue is worth more than an unfinished sweep with no rule.**

**One known gap in the checker, stated rather than left.** A citation written without the `@` prefix —
`` `questions-for-karim.md` -> `Q42` `` — is correctly not counted as legacy, but is also **not
verified**, because the identifier pattern requires the `@`. It is a small hole and it fails safe
(toward not-counted rather than falsely-green). Worth one word in the regex next time somebody has the
file open; **not worth reopening the script today**, and recorded so the next reader does not assume
the 283 is the whole corpus.

#### 14. Illustrations — ruled explicitly, because a checker cannot tell one from a claim

The repo sweep reported **3 "missing file" citations, and all three were SM-29's own worked example**,
`User.cs` line 232 — an illustration of the format, never an assertion about the code. Left alone, the next
person to run a checker chases three phantoms and learns to distrust the output, which is how a
control stops being read.

**Ruled, and deliberately not as an exemption:**

> **An illustration must either be a true citation, or must not use citation markup.**

**Two ways to satisfy it, and the first is preferred.** Cite something that really exists — an example
that is also true costs nothing and stays checkable forever. Where the words cannot be changed because
they are somebody's quotation, **strip the code-span markup and annotate**.

**Applied to SM-29's example, which is a direct quotation of Nabil and could not be rewritten.** In
`process/agile.md` and in D-055 §5 the sentence is preserved **character for character**; only the
surrounding backticks were removed, and a note beside it records that the *format* it demonstrates is
superseded by SM-31 while the *rule* it states is unchanged and binding. The current form is shown
beside it as a true citation. **The quotation is intact, the phantom is gone, and no exemption was
created.**

**Why not an exemption list, stated because it is the obvious alternative.** An exemption is a hole
the checker cannot see into, and §9 above is the record of what one costs — 77 stale hints hiding
inside a carve-out, four hours after the rule was written. **There is no such thing as a harmless
placeholder:** a fake example is indistinguishable from a broken citation to a reader and to a
checker. The distinction the checker enforces is not *illustration vs. claim* — it cannot see intent —
it is **valid vs. invalid**, and an illustration that is true is simply valid.

**This is the third carve-out considered for SM-31 in one day, and the third one refused.** The hint
(§9), the writing-about-citations case (the prose convention), and now the illustration. All three
were resolved by changing what gets *written* rather than what gets *checked*. **That is the test I
would apply to the next one.**

---

### D-060 · The SM-30 enforcement test lands, and it finds eight more rows · 2026-08-22

**Backend. Closes the debt recorded as owed in D-057 §1 and again in D-059 §6.**

#### 1. The test

`Every_permission_catalogue_row_is_named_in_a_test`
[Verified: 2026-08-22 @ `PermissionCoverageTests.cs` -> `Every_permission_catalogue_row_is_named_in_a_test`].
It enumerates `Permission` and asserts each member's name appears as text in some `.cs` file under
`tests/`. **SM-30 is now enforced by something that goes red**, not by reading the diff in refinement;
D-057 §1's "which is weaker, and is recorded as weaker" no longer applies.

**Watched to fail, twice.** Deleting one row from the baseline list — the shape of a row shipped with
no test — turns **exactly one** test red and the message names the row. Adding a scratch member to
`Permission` turns two red, this one and
`Every_permission_has_a_catalogue_entry_with_a_spec_reference`. Reverted; the suite is 75/75.

#### 2. It scans all three suites, not only `Domain.Tests`

The brief said Domain test sources. That would report `ClientManage` as untested, and it is not — it
is exercised by `Kaff.Api.Tests`
[Verified: 2026-08-22 @ `ProbeEndpoint.cs` -> `Permission.ClientManage`]. **SM-30 asks whether a row is
named in a test, not in which project**, so the scan is `tests/` entire.

#### 3. Eight rows are named in no test, and they were not the three anyone was counting

D-057 §1 names `ProjectCreate`, `ProjectFinancialsEdit` and `UserRead`. **All three are covered
today.** The first run of the coverage test found eight *others*, all reachable in the catalogue and
all predating SM-30: `CatalogueManage`, `BabManage`, `SubcontractorManage`, `SupplierManage`,
`OpportunityManage`, `ExtractPrepare`, `QuantityGateApprove`, `DailyLogWrite`.

They are written out as a baseline in `NamedInNoTestYet` rather than skipped, pinned in **both**
directions — the same shape as `The_set_of_unresolved_permissions_has_not_grown`. A new untested row
fails. So does leaving a name in the list after its slice tests it. **The list shrinks on the record
and cannot quietly grow.**

#### 4. Two mechanics worth knowing before editing it

* **The file excludes itself from the scan.** The baseline names the rows it lists, so without the
  exclusion the test would report its own list as covered and could never fail. Rename the file and
  the exclusion stops matching — which fails loudly rather than silently, by design.
* **The test sources are found by walking up from `AppContext.BaseDirectory`** to the directory
  holding `Kaff.Domain.Tests.csproj`. Not the working directory, which is the runner's business, and
  not `CallerFilePath`, which `ContinuousIntegrationBuild` rewrites and CI turns on.

#### 5. Not merged with `scripts/check-citations.ps1`

D-059 §6 ruled on this and nothing here changes it. Same shape, different domains.

#### 6. An experiment: a comment that cites its test instead of restating the fact

`ProjectAccessPolicy`'s class remarks justified HR's global reach with *"HR holds just
`EmployeeManage` and `ProjectAssignmentManage`"*. **HR has held three since `UserRead` landed**
(D-055 §2). The sentence was the *argument for the rule* — global reach is safe because the grant set
is small and touches no money — so a reader auditing that argument against a two-item list would not
notice the third item they had just been told did not exist.

Fixed by citing `Hr_holds_no_permission_that_touches_money`
[Verified: 2026-09-04 @ `CatalogueCompletenessTests.cs` -> `Hr_holds_no_permission_that_touches_money`]
rather than by correcting the number. **One instance only. Not a rule.** See §7 for where it does not
fit.

#### 7. Not done

* **The eight rows have no tests.** This entry records the gap; it does not close it. Each belongs to
  a slice that has not been built (2, 4, 5, 6) and each is a real, granted, reachable row today.
* **Catalogue row comments are not audited against SM-30's citation half.** The coverage test proves
  a row is named somewhere; it does not prove the test a row's comment *cites* exists. That half is
  `scripts/check-citations.ps1`'s domain only for citations written in markdown — a cited test name
  inside a `.cs` comment is checked by nobody.
* **The cite-the-test convention is applied to one comment.** Nothing else was touched.
* **101 legacy line-number citations remain** [Verified: 2026-08-22, `scripts/check-citations.ps1`:
  284 identifier citations, 0 broken, 101 legacy, exit 1]. Down from 164 at D-059 §9's count, still
  the BA's and QA's sweep, untouched here.

---

### D-061 · The audit trail records events, not only entity changes · 2026-08-22

**Architect. Routed by the Scrum Master as V-01, found by a Verifier; the same gap as N-06 / SM-14.
Built, with the mechanism only — no consumer.**

#### The defect, re-verified before deciding

`AuditSaveChangesInterceptor` iterates the change tracker and skips any entry whose state is not
`Added`, `Modified` or `Deleted`; a `Modified` entry with no changed property yields nothing, and a
save that produces no records returns before writing
[Verified: 2026-08-22 @ `AuditSaveChangesInterceptor.cs` -> `WriteAuditRecords`, `Describe`]. **The
trail was therefore a function of entity state**, and three committed slice-1 facts are not entity
state: **sign-out** (KAFF-102 rule 2 — clearing the cookie is the whole mechanism, and it
deliberately does not rotate the stamp), **a clean sign-in** (`RecordSuccessfulSignIn` sets the
counter to 0 and the lockout to null, so on an account that is already clean nothing is modified
[Verified: 2026-08-22 @ `User.cs` -> `RecordSuccessfulSignIn`]), and **a failed sign-in against a
username that does not exist** — which is Karim's to rule on, and is not settled here.

**KAFF-100 is not one of them, and the brief's table said it was.** Bootstrap *is* an entity change:
one `User`, `Created`, written by the interceptor today. Its problem is the **actor**, and it is a
different problem with a different fix. Both are decided here because both were routed together.

---

**Decision.**

**One mechanism, two inputs.** `AuditSaveChangesInterceptor` remains the only thing in the system
that constructs an `AuditRecord`. It now builds them from two sources instead of one: the change
tracker, exactly as before, and **events declared on `IAuditContext`** — the scoped per-request
channel that already carries what the change tracker cannot see, and exists for precisely that
reason (D-031: the reason and the correlation id).

A handler that must record something no row describes calls
`IAuditContext.Record<TSubject>(kind, subjectId)`
[Verified: 2026-08-22 @ `IAuditContext.cs` -> `Record`]. It states *what happened*; the mechanism
decides what is written, and with which actor, time, correlation id and path. **KAFF-118 rule 2 holds
in full and D-031 is not relaxed** — no handler constructs an `AuditRecord`, and no per-feature audit
code exists. Declaring a fact is the same act as `SetReason`, which nobody has ever read as
hand-writing a record.

**One table, one row, one guarantee.** An event is an `AuditRecord` with `AuditAction.Occurred` and a
non-null `EventType` naming the `AuditEventKind`
[Verified: 2026-08-22 @ `AuditRecord.cs` -> `Occurred`, `EventType`, `ForEvent`]. `EntityType` and
`EntityId` carry the **subject** the event happened to — for every slice-1 event, the user.
`BeforeJson` and `AfterJson` are null: an event has no state to snapshot, it is the whole fact.

**Append-only needs nothing new, and this is the whole answer to "what keeps the guarantee true for
it".** `trg_audit_records_append_only` and `trg_audit_records_no_truncate` are triggers on the
`audit_records` **table**, so they bind every row in it whatever shape the row has, and
`FindMissingGuardsAsync` already refuses to start without them
[Verified: 2026-08-22 @ `DatabaseInitializer.cs` -> `FindMissingGuardsAsync`]. An event row inherits
the guarantee by being in the table, not by anyone remembering to give it one.

**Two check constraints hold the two shapes apart**, in the database rather than in the interceptor
[Verified: 2026-08-22 @ `AuditConfiguration.cs` -> `ck_audit_records_event_shape`]:

| Constraint | Rule |
|---|---|
| `ck_audit_records_event_shape` | `(action = 'Occurred') = (event_type IS NOT NULL)` — a row is an entity change or an event, never a hybrid and never neither |
| `ck_audit_records_has_state` | widened to `event_type IS NOT NULL OR before_json IS NOT NULL OR after_json IS NOT NULL`. "A record with neither a before nor an after describes nothing" is still true; an event is the case where naming it *is* the description |

`AuditEventKind` is stored as **text**, not as a number, because `ApplyEnumsAsStrings` stores every
enum here as text [Verified: 2026-08-22 @ `KaffDbContext.cs` -> `ApplyEnumsAsStrings`] and the reason
applies with extra force to this table: it is append-only, it will be read long after today's code is
gone, and a row that means `2` is only legible with that code in hand.

**It ships with two members — `SignedIn` and `SignedOut` — and no more**
[Verified: 2026-08-22 @ `IAuditContext.cs` -> `AuditEventKind`]. Those are the two the committed
stories require and nobody disputes. **Adding a member later is one line and needs no backfill**,
which is exactly why the open question below does not block the mechanism: what cannot be added later
is the *column* and the *path that writes it*, and both land now.

#### The bootstrap actor

`IAuditContext.AttributeTo(AuditActor)` names the actor for the next save
[Verified: 2026-08-22 @ `IAuditContext.cs` -> `AttributeTo`]. The setup handler builds the Owner,
declares it as the actor, and saves; the interceptor puts that id, display name and role on the
`Created` record it was already going to write. No handler constructs a record, and D-051 (Q31)'s
requirement — *"my name and account creation date must appear naturally in the Audit Trail from day
one"* — is met by the one mechanism rather than around it.

**The guard is the point, not the feature.** An override is legal only on a request that carries no
identity at all; an authenticated request that names a different actor throws
[Verified: 2026-08-22 @ `AuditSaveChangesInterceptor.cs` -> `ResolveActor`]. **Impersonation written
into an append-only table cannot be corrected afterwards**, so the refusal is loud rather than
silent, and it is asserted by
[Verified: 2026-08-22 @ `AuditMechanismTests.cs` -> `An_authenticated_request_may_not_name_a_different_actor`].

---

**What we rejected.**

* **A second table for events.** The Scrum Master imposed this as a constraint and invited a
  pushback. **There is none to make — the constraint is right, for a reason stronger than the one
  given.** The stated reason was KAFF-116: a second table gives the grant-path column two places to
  land. The stronger reason is that a second table starts with **no append-only trigger, no
  no-truncate trigger, and no entry in `FindMissingGuardsAsync`** — a forensic table that can be
  edited. D-033 exists because a guard nobody notices is missing is the failure mode of this whole
  design. One table, one set of guards, one thing to verify.
* **Letting a handler write the record.** Forbidden by KAFF-118 rule 2 and by D-031, and the
  prohibition is right: a per-feature call is one a future session forgets, and the forgetting is
  invisible.
* **Making the event an entity.** A `SignOutEvent` row would be audited by the existing mechanism
  with no changes at all — the laziest answer, and wrong. It stores the same fact twice, and the
  first copy lands in an ordinary mutable table while the audited copy lands in the protected one.
* **Inferring the bootstrap actor from the change tracker** — *"if the request is anonymous and the
  save creates exactly one `User`, that user is the actor"*. Zero API surface, and undiscoverable: it
  fires by accident the first time a background job creates a user, and nothing in the code says so.
* **`AuditAction` members per event** (`SignedIn = 4`, `SignedOut = 5`, …). It makes a domain-wide
  enum grow with each feature's vocabulary, which is the per-feature-code smell in a different
  costume. `Occurred` plus a separate kind keeps `AuditAction` the CRUD vocabulary it already is.
* **Making `EntityType` and `EntityId` nullable now.** Every event this system has a source for
  happens *to somebody* — the user signing in or out. The only case with no subject is the unknown
  username, which is Karim's. **N-19 does not apply to nullability**: relaxing `NOT NULL` later is an
  ALTER, not a backfill, and it touches no existing row.
* **A payload column for event details.** Nothing committed needs one. The only proposed payload is
  the attempted username, which is the open question. The argument for landing the mechanism early
  does not extend to landing fields nobody has asked for.

**Revisit if.** An event needs a project tag — `ForEvent` writes `ProjectId` as null today because no
slice-1 event has one, and KAFF-116's grant path will want the same treatment. Or if an event turns
out to need a subject that is not an entity, which is the unknown-username case and only that.

---

#### What was built, and what was not

**Built:** `AuditEventKind`, `AuditEvent`, `AuditActor`, `IAuditContext.Record`, `AttributeTo`, and
`Clear` — which replaces `ClearReason`, a name that would now be a lie; `AuditAction.Occurred`;
`AuditRecord.EventType` and `AuditRecord.ForEvent`; the interceptor's second source and
`ResolveActor`; the two check constraints; and a migration
[Verified: 2026-08-22 @ `20260822170235_AuditEvents.cs` -> `AuditEvents`], applied to the running
database and confirmed there as `event_type character varying(64)` with both constraints present.

**Not built, deliberately.** No consumer. Sign-in, sign-out and setup are Backend's stories
(KAFF-101a, KAFF-102, KAFF-100) and none of those endpoints exists yet — today's API still serves
`GET /api/health` alone. **The mechanism lands before the first consumer, which is the whole of
N-19's requirement.** Also not built: KAFF-116's grant-path column, which is Backend's and lands next
on the same table.

**Evidence.** Build 0 warnings / 0 errors, exit 0. `dotnet format --verify-no-changes` exit 0.
Domain **75/75**, Api **48/48** — five new, from 43:
`An_event_that_changes_no_entity_still_writes_a_record`,
`An_event_and_an_entity_change_saved_together_share_one_correlation_id`,
`Only_an_Occurred_record_carries_an_event_type`,
`A_request_with_no_identity_may_name_the_actor_it_is_creating`, and
`An_authenticated_request_may_not_name_a_different_actor`
[Verified: 2026-08-22 @ `AuditMechanismTests.cs` -> `An_event_that_changes_no_entity_still_writes_a_record`].

**One of them settled a framework question the design depended on and that no amount of reading this
codebase could answer.** Sign-out saves nothing, so the whole approach rests on EF Core invoking a
`SaveChanges` interceptor when the change tracker is **empty**, and on the records that interceptor
adds during the call still being saved. It does, on EF Core 10.0.11:
`An_event_that_changes_no_entity_still_writes_a_record` tracks nothing, calls `SaveChangesAsync`, and
reads the row back. **Had it gone the other way the design would have had to change** — which is why
it is a test and not a paragraph.

#### For Karim, not for us — Q53

**Is a failed sign-in against a username that does not exist recorded at all, and if so what may the
record hold?** KAFF-101a's audit paragraph says it writes one *"with the attempted username and no
actor id"* and cites nothing. It is plausible and may well be right, but the thing being stored is
**whatever the person typed into the username box** — commonly a typo of a real username,
occasionally the password — and it lands in a table that is append-only by trigger, cannot be
corrected, and is read by the Owner. **That is a business and privacy question, not an engineering
one.** Raised as **Q53**. The mechanism is built either way; if the answer is yes it costs one enum
member and one nullable subject.

*Noted for the BA, not for Karim: KAFF-102's audit line and KAFF-101a's success line are likewise
uncited — CLAUDE.md requires a record for every state **change**, and neither is one. Both are
harmless, both store nothing that is not already stored, and this entry treats them as the
engineering requirements they read as. Q53 is separated from them because the payload is the part
that is not harmless.*

---

### D-062 · Nabil's three rulings of 2026-08-22 evening — the waiver, the staff door, and what a failed sign-in may store · **APPROVED**

Three rulings in one message. **Two unblock committed stories; one closes a discrepancy the stories
had been flagging against themselves for a day.** The third collides with D-061's model in two places,
and those collisions are recorded here as consequences rather than left for whoever writes the story
to discover.

---

#### 1. The readiness waiver — signed for **seven**, by **Nabil**

**Decision, verbatim:** *"Signed and approved. I officially approve adding KAFF-106 as the seventh
story under the waiver. The numerical discrepancy flagged by the Scrum Master is accurate, and the
story is essential to complete the creation flow for the first user (Owner). This closes the
discrepancy and allows the build to proceed."*

**What this changes, and both halves matter.** D-055 §4 recorded a waiver **for six**, signed **"the
Architect"**. The waiver text is carried in **seven** story files — KAFF-100, 101a, 103, 106, 110, 112,
114 [Verified: 2026-08-22]. **The count is now seven and the signatory is now Nabil.**

**Nobody crept it in.** KAFF-106 raised the discrepancy **in its own text, against itself** — which is
the behaviour SM-29 exists to produce: a story that flags its own uncited state rather than reading as
settled. It was surfaced, not resolved, and the decision owner ruled.

**Why the signatory matters as much as the count.** An Architect's waiver is one agent accepting risk
on rules **Karim has not answered**; Q45–Q51 remain open. Nabil is the decision owner's proxy, so a
waiver in his name is the right weight for seven committed stories to be built on. **The waiver still
answers nothing** — it lets the stories be built, and the cost of a waived rule turning out wrong now
lands where the authority sat.

**Revisit if** a waived rule turns out to be wrong, or if an eighth story acquires an uncited rule — in
which case it needs its own approval and does not inherit this one.

---

#### 2. V-02 / N9 — a `Role.Client` credential is refused at the staff door. **HARD NO**

**Decision, verbatim:** *"It is strictly forbidden from a security standpoint for any user holding the
`Role.Client` to sign in or authenticate through the staff portal (Staff Origin)."* And: *"Rule 16 in
story KAFF-101a must be modified. A sign-in request from a Client against this endpoint must
explicitly fail (e.g. returning Unauthorized or Forbidden due to origin mismatch). The staff endpoint
is exclusively for staff."*

**This unblocks KAFF-101a**, which the Scrum Master marked `BLOCKED` earlier today for failing the
Definition of Ready's last line.

**The ruling is stronger than the story's current position, and the difference is the whole point.**
KAFF-101a rule 16 says a client credential *"authenticates, and reaches only `PortalRead` /
`PortalApprove`"* — that is, **it gets in and then finds nothing**, because those two rows happen to be
`ProjectScoped` and no staff endpoint requires them. **That safety is a property of what the permission
catalogue currently contains.** Karim has ruled for **an explicit refusal at the endpoint**, which is a
property of the door.

**Those are different mechanisms and only the second survives a catalogue change.** A future session
that adds one company-wide row a client happens to hold re-opens the first and cannot re-open the
second. This is D-035's shape — *a grant wider than the act it was written for* — seen from the
authentication side, and it is the fourth time this project has recorded that shape.

**Left open deliberately, and routed.** Karim wrote *"e.g. returning Unauthorized or Forbidden"*.
**401 and 403 are not interchangeable here.** A 403 says *your credential was valid and you may not
come in*, which confirms to an attacker that the username and password are real; a 401 says only *no*.
**That is an Architect decision, not a drafting choice for whoever writes the handler.**

---

#### 3. Q53 — log the event, **forbid the input**

**Decision, verbatim:** *"Log the attempt as a security event, but strictly FORBID storing the typed
input. Users frequently type their password into the username/email field by mistake. Storing the exact
typed input in the audit log means we risk recording actual plaintext passwords in the database, which
is a critical security vulnerability."* And: *"The system must write an audit record stating 'Failed
sign-in — Unknown user', capturing only metadata like the IP address and timestamp, completely omitting
the entered string."*

**This unblocks KAFF-101a's audit criterion**, which cited nothing and proposed storing *"the attempted
username"*.

**Karim's reasoning is better than the story's, and it is the justification, not just the outcome.**
The audit table is **append-only by database trigger** — a plaintext password written into it **cannot
be deleted**. Not by an admin, not by a migration, not by the Support agent, because D-033 and the
append-only rule exist precisely to prevent that. **The one table in this system that can never be
corrected is the worst possible place to put an unvalidated string a human typed.**

**Two consequences for D-061's model.** Neither makes the ruling wrong; both make it **not free**, and
they are recorded here so the BA does not write a criterion against a mechanism that does not exist —
**which is V-01 repeating.**

**(a) There is no IP address field.** `AuditRecord` carries `OccurredAt`, `Action`, `EntityType`,
`EntityId`, `ActorUserId`, `ActorDisplayName`, `ActorRole`, `BeforeJson`, `AfterJson`,
`ChangedProperties`, `Reason`, `CorrelationId`, `ProjectId`, `RequestPath` and `EventType` — **and no
IP** [Verified: 2026-08-22 @ `AuditRecord.cs` -> `class AuditRecord`]. The ruling requires capturing it,
so this is **a new column and a migration** on the table D-033 protects. **Whether an IP belongs on
every audit record or only on security events is an Architect decision** — and it is also a
**data-retention question nobody has asked**, because an IP address is personal data with a lifetime,
in a table that by construction has none.

**(b) An unknown username has no subject.** `AuditRecord.EntityId` is a **non-nullable `Guid`**, and
`AuditEvent(Kind, SubjectType, SubjectId)` requires one [Verified: 2026-08-22 @ `IAuditContext.cs` ->
`AuditEvent`]. **There is no `User` row to point at — that is the entire premise of the case.** So
either `EntityId` becomes nullable, or a sentinel is used, and **a sentinel in a forensic table reads
as a real id to whoever queries it in two years.**

**D-061 named this exception itself and priced it.** It refused a nullable `EntityId` on the grounds
that *"every event this system has a source for happens to somebody"*, and said *"the only case with no
subject is the unknown username, which is Karim's"* — adding that *"if the answer is yes it costs one
enum member and one nullable subject."* **The answer is yes.** D-061's reasoning still holds: relaxing
`NOT NULL` later is an ALTER, not a backfill. **The IP column is different, and N-19 applies to it in
full** — a field never written cannot be backfilled into an append-only table.

**Routed to the Architect before the BA writes KAFF-101a's criteria.**

---

#### What this does not change

* **KAFF-105a stays `BLOCKED`.** V-03 — rule 3 versus `AC-105a-C`, field-or-refusal — is **untouched
  by all three rulings**. It is N-04 / Q-UX-18 / SM-16 and still open. Checked rather than assumed.
* **KAFF-116 is unaffected and is first in the build order.** Nabil said go; it was started rather than
  held behind this entry.
* **Q45–Q51 remain open.** The waiver permits the build; it answers nothing.

---

### D-063 · The staff door, the IP column, and the subject that does not exist · 2026-08-23

**Architect. The three consequences D-062 routed — §2's status code and §3's two collisions with
D-061's model. Decided in full; deliberately built in none. The reasons are in "What was built" and
they are not the same reason for all three.**

> **On the date.** This session opened on the evening of 2026-08-22 and the clock passed midnight
> during it. Every citation below carries **2026-08-23** because that is when the file was opened and
> read. SM-31: the date says when the claim was checked, and nothing else.

---

#### What the code says today, checked before deciding

Three facts the three rulings all stand on, and one of them contradicts a widespread assumption:

* **There is no sign-in endpoint.** The API serves `GET /api/health` and nothing else
  [Verified: 2026-08-23 @ `Program.cs` -> `MapKaffEndpoints`]; the only feature folder under
  `src/Api/Features` is `Health/GetHealth`. **There is no handler to put a refusal in**, and no
  `errors.auth.invalid_credentials` anywhere — `AuthorizationErrors` carries `NotAuthenticated`,
  `Forbidden` and `RoleCannotLogIn` and no credential error at all
  [Verified: 2026-08-23 @ `SeparationOfDuties.cs` -> `AuthorizationErrors`].
* **`AuditRecord` has no IP field.** Confirmed, as D-062 §3a states
  [Verified: 2026-08-23 @ `AuditRecord.cs` -> `class AuditRecord`].
* **`AuditContext.Record` already refuses `Guid.Empty`** — *"An audited event must name its
  subject"* [Verified: 2026-08-23 @ `AuditContext.cs` -> `Record`]. This is the fact that decides §3
  below, and it was not in the brief.

**KAFF-116's migration landed *during* this session, and the correction is worth more than the
claim.** At **00:02** the newest migration in the tree was D-061's and no grant-path column existed.
At **00:04**, while this entry was being written, Backend committed
[Verified: 2026-08-23 @ `20260822210402_AuditGrantPath.cs` -> `AuditGrantPath`] — `grant_path`,
`character varying(64)`, nullable, plus a **third** check constraint on the table,
`ck_audit_records_grant_path`. `KaffDbContextModelSnapshot` was regenerated with it.

**This paragraph originally said the migration had not landed. It was true when checked and false
twelve minutes later, and it is corrected here rather than rewritten** — SM-29's whole subject is a
claim that ages between being verified and being read, and the fastest example this project has
produced took twelve minutes.

**No collision.** Backend's migration adds a column and a constraint and touches neither
`entity_id`'s nullability nor any IP field — verified in the regenerated snapshot, which shows
`GrantPath` and an unchanged `entity_id`. Nothing in this entry touches the grant path. **The tree
was re-verified after it landed: build 0/0 exit 0, Domain 75/75, Api 48/48** — see "What was built".

---

#### 1. A `Role.Client` credential is refused with **401**, in the same shape as every other refusal

**Decision.** The staff sign-in endpoint answers a `Role.Client` credential with **`401`, the same
body, the same `messageKey` and the same time envelope as a wrong password, an unknown username and a
locked account.** No `Set-Cookie`. No distinguishing field anywhere in the response.

**That is not a fourth rule — it is the existing one, extended by one member.** KAFF-101a rule 13
already requires a wrong password and an unknown username to be indistinguishable, rule 14 adds the
locked account, and `AC-101a-B` asserts all three are *"identical in status, body and `messageKey`"*
[Verified: 2026-08-23 @ `KAFF-101a-sign-in-api.md` -> `AC-101a-B`]. Karim's *"e.g. Unauthorized or
Forbidden"* is a choice between joining that set and standing outside it.

**Why 401 and not 403 — three reasons, in the order they matter.**

1. **403 is a credential-validity oracle on an anonymous endpoint.** *"Your credential was valid and
   you may not come in"* tells an attacker holding a leaked list that this username and this password
   are real. The whole value of rules 13 and 14 is that the door returns one answer; a fourth answer
   that fires only on a **correct** client credential is the single most informative response the
   endpoint could give. **Karim's own reasoning in D-062 §3 is that the audit trail must not become a
   place where a secret is confirmed. The same instinct at the wire gives the same answer.**
2. **401 is not a lie — it is the precise HTTP semantic.** RFC 9110: 401 means the request lacks
   valid credentials **for the target resource**. A client credential is not valid for the staff
   resource. 403 is for a request whose identity *has been* established and is then denied — and the
   entire point of the ruling is that the staff origin must never establish this identity at all.
   Returning 403 would mean authenticating first and refusing second, which is the mechanism Karim
   rejected wearing a different status code.
3. **It costs nothing.** The client is not a confused staff member who needs a better error; the
   client has a portal at another host (D-051 Q33) and a bookmark to it. There is no support burden
   to trade against the leak.

**The consistency requirement, stated so it cannot be read out of the ruling:**

| Attempt at the staff sign-in endpoint | Response |
|---|---|
| Unknown username | 401, generic key, same envelope |
| Known username, wrong password | 401, generic key, same envelope |
| Known username, correct password, account locked | 401, generic key, same envelope |
| **Known username, correct password, `Role.Client`** | **401, generic key, same envelope** |

**And the timing half is a real constraint, not a caveat.** The role must be checked **after** the
password verification has run, not before. A handler that short-circuits on `Role.Client` before
hashing returns in a fraction of the time and re-creates the oracle it just closed — now as a clock
instead of a status code. Rule 13's *"in the same time envelope"* already says this; it is repeated
here because the natural way to write the guard breaks it.

**Where the refusal lives, so the next endpoint cannot forget it.**

**In the function that mints a staff session, not in the sign-in handler.** The staff session is one
thing — a token for `JwtOptions.Audience` carried in `JwtOptions.CookieName`
[Verified: 2026-08-23 @ `JwtOptions.cs` -> `CookieName`]. Every present and future staff door must go
through it: sign-in, the forced password change that completes a sign-in (KAFF-103), a reset link
(KAFF-104), anything slice 8 adds. **One guard there refuses `Role.Client` for all of them**, and a
future endpoint that forgets the rule cannot mint a session anyway.

That guard is a **programmer-error guard: it throws.** It is not the user-facing path. The handler
still owns the response, and the response is the generic 401 it already returns three other times —
one `if` returning an error that must exist regardless. **Two places, two jobs**: the minter
guarantees no staff session for a `Role.Client` can exist; the handler decides what the caller is
told. This is deliberately not the same rule written twice.

**This is the difference D-062 §2 was drawing.** Rule 16's safety — *"reaches only `PortalRead` /
`PortalApprove`"* — is a property of what `PermissionCatalogue` happens to contain today. The minter
guard is a property of the door, and it survives any catalogue change. It is also **not** the
existing `RoleCannotLogIn` path [Verified: 2026-08-23 @ `SeparationOfDuties.cs` -> `RoleCannotLogIn`],
which is authorization on an already-authenticated request and never runs on an anonymous endpoint.

**Not decided here, and named so nobody assumes it was:** how the *portal* host authenticates a
client is slice 8. This ruling says only that the staff minter refuses `Role.Client`; it says nothing
about what the portal's own minter does, except that it is a different one.

---

#### 2. The IP address is a column on **every** audit record, written by the middleware

**Decision.** `AuditRecord` gains **one nullable IP column, populated on every row**, from the same
place and by the same mechanism that already populates `RequestPath` — never by a handler, never by a
feature.

**Every record, not only security events. Three reasons.**

1. **"Only on security events" is a classification nobody has defined**, and every future session
   would have to remember to apply it. That is per-feature audit code in a different costume, and
   D-031 refuses it for the reason that applies here exactly: opt-in *"fails silently and is only
   discovered when the trail is needed."*
2. **The plumbing already exists and is uniform.** `AuditCorrelationMiddleware` hands the correlation
   id and the request path to the audit context on every request
   [Verified: 2026-08-23 @ `AuditCorrelationMiddleware.cs` -> `BindToRequest`], and it is registered
   **before** authentication
   [Verified: 2026-08-23 @ `Program.cs` -> `UseMiddleware<AuditCorrelationMiddleware>`],
   so it already sees anonymous requests — which is the entire population of Karim's case. An IP is
   the same shape of metadata as a request path, available at the same instant, from the same object.
   Adding it as a third argument to the existing call is smaller than inventing a rule about when to
   add it.
3. **It does not increase the retention exposure in kind, only in volume**, and the retention
   question below is unanswered for one row as much as for all of them. Splitting the column to
   reduce the footprint would be deciding the retention question sideways, which is not mine.

**Null where there is no request.** Migrations, seeding and scheduled work carry no connection.
`RequestPath` is already nullable for exactly this reason, and the IP follows it.

**Stored as PostgreSQL `inet`, from `System.Net.IPAddress`.** Npgsql maps the two natively with no
converter. The database then refuses a malformed value, normalises v4-mapped-v6, and sorts and
subnet-matches correctly — all things a `varchar(45)` would leave to whoever queries a forensic table
in five years. This is the same argument that stored the enums as text (D-061): the table outlives
the code that writes it.

**The source is the connection, and only the connection — this is the part that is easy to get
wrong.** `HttpContext.Connection.RemoteIpAddress`, never `X-Forwarded-For`. Behind a reverse proxy
the connection address is the proxy's, which is admittedly weak evidence — **but `X-Forwarded-For` is
a caller-supplied string, and writing a caller-supplied string into an append-only table that can
never be corrected is precisely the class of act Karim's §3 ruling forbids.** An attacker who can set
that header can write anything he likes into Kaff's permanent forensic record, including someone
else's address. Reading it becomes legitimate only once `ForwardedHeadersOptions` is configured with
an explicit `KnownProxies` / `KnownNetworks` allowlist, which is a deployment fact this project does
not have — **D-023, the staging target, is still open.** Until then: the connection address, or null.

**Why it lands before the consumer, and why "before the consumer" is not "tonight".** N-19 applies in
full: a column never written cannot be backfilled into a trigger-protected append-only table, so
every row written before it exists lacks an IP permanently and irreparably. **The deadline is the
first audit-writing endpoint** — KAFF-100 or KAFF-101a, whichever ships first — not this session.
Today the only rows are test and development rows. See "What was built".

---

#### 3. `AuditRecord.EntityId` becomes nullable. **Not a sentinel** — and the argument is mechanical

**Decision.** `AuditRecord.EntityId` becomes `Guid?` and `AuditEvent.SubjectId` becomes `Guid?`.
**`EntityType` stays required.** A failed sign-in against an unknown username is
`EntityType = "User"`, `EntityId = null` — *a sign-in was attempted against a User that does not
exist*, which is the true statement and is more useful in a query than either alternative. That is
D-061's *"one enum member and one nullable subject"*, priced exactly as it priced itself.

**The sentinel is not merely ugly, it is unavailable.** `AuditContext.Record` **throws** on
`Guid.Empty` today — *"An audited event must name its subject"*
[Verified: 2026-08-23 @ `AuditContext.cs` -> `Record`]. Adopting `Guid.Empty` as the sentinel means
**deleting that guard**, and that guard's job is to catch a handler that forgot to pass an id. After
the deletion the table can no longer distinguish *"deliberately subjectless"* from *"somebody's
bug"* — in the one table where a mistake can never be corrected. The Scrum Master's argument (a
sentinel reads as a real id in two years) is right and I am not overruling it; this is a second,
independent, and stronger one, because it is enforced by code that already exists rather than by
whoever is reading the table later.

**The two existing check constraints stay true, and a third is required.**

| Constraint | Effect of the change |
|---|---|
| `ck_audit_records_event_shape` — `(action = 'Occurred') = (event_type IS NOT NULL)` | **Untouched.** It names `action` and `event_type` only [Verified: 2026-08-23 @ `AuditConfiguration.cs` -> `ck_audit_records_event_shape`] |
| `ck_audit_records_has_state` — `event_type IS NOT NULL OR before_json IS NOT NULL OR after_json IS NOT NULL` | **Untouched.** It names neither column [Verified: 2026-08-23 @ `AuditConfiguration.cs` -> `ck_audit_records_has_state`] |
| **`ck_audit_records_entity_change_has_subject`** — `action = 'Occurred' OR entity_id IS NOT NULL` | **New, and it is the point.** Dropping `NOT NULL` silently permits an *entity change* with no subject, which was impossible five minutes earlier. The database goes on saying what it said before for every row that is not an event |

**Shape-level, not vocabulary-level.** The tighter constraint —
`event_type = 'SignInFailedUnknownUser' OR entity_id IS NOT NULL` — was considered and rejected: it
writes a business vocabulary into a schema object, and every future subjectless event would need a
migration to be allowed to exist. The shape is the invariant; the vocabulary is not.

**`FindMissingGuardsAsync` needs no change, and that is a finding rather than a relief.** It verifies
eight triggers, three indexes and one view — **and not one check constraint**
[Verified: 2026-08-23 @ `DatabaseInitializer.cs` -> `FindMissingGuardsAsync`]. `audit_records` now
carries **three** — D-061's two and KAFF-116's `ck_audit_records_grant_path`
[Verified: 2026-08-23 @ `20260822210402_AuditGrantPath.cs` -> `ck_audit_records_grant_path`] — and
adding a fourth to a table whose three are already unguarded changes nothing about the guard list.
Routed below as **A-01**; not fixed here, because it is a behaviour change to start-up on every
environment and it is not one of the three things I was asked to decide.

**Not decided here.** The `AuditEventKind` members this case needs. Karim ruled the record says
*"Failed sign-in — Unknown user"*, which implies at least one new member, and the
client-at-the-staff-door refusal in §1 arguably implies another. **D-061 settled that adding a member
is one line and needs no backfill**, so the vocabulary belongs to the story that consumes it
(KAFF-101a) and inventing it now would be guessing at events nobody has specified. **The nullability
is the part that cannot be added casually; the enum is the part that can.**

---

#### What we rejected

* **403 with a distinct `messageKey` for the client.** The most *honest* response and the only one
  that leaks a verified credential. Rejected in §1.
* **A dedicated 401 with its own key** — *"wrong door"*. Same leak with better manners: a distinct
  key on a 401 is still an oracle, because it fires only when the credential is real. **Any response
  that varies with the truth of the credential is the defect, whatever number precedes it.**
* **Checking the role before verifying the password.** Faster, and a timing oracle. §1.
* **Putting the refusal in the sign-in handler alone.** One endpoint's memory. The catalogue-shaped
  failure D-062 §2 exists to prevent, relocated.
* **An IP column only on security events.** Rejected in §2 — a classification nobody has defined,
  applied by hand, forgotten silently.
* **A generic "metadata" or payload JSON column** to hold the IP and whatever comes next. D-061
  rejected a payload column for events and the reasoning holds harder here: an untyped bag on an
  append-only table is where the plaintext password Karim just forbade eventually arrives, written
  by somebody who did not read this entry.
* **`X-Forwarded-For`.** An attacker-controlled string in a table nobody can correct. §2.
* **`varchar` for the address.** No validation, no normalisation, no subnet query. `inet` is native.
* **`Guid.Empty` as the subject sentinel.** Requires deleting a guard that exists. §3.
* **Making `EntityType` nullable too.** There is a real subject type — `User` — even when there is no
  row. D-061 declined this and nothing has changed.
* **A tighter check constraint naming the event kind.** §3.
* **Building any of it tonight.** Below, and it is a decision rather than an omission.

---

#### What was built, and what was not

**Built: nothing. This entry is the whole deliverable, and that was the instruction and the right
call.** Three separate reasons, one per decision — they are not interchangeable:

1. **§1 has nothing to build against.** There is no sign-in endpoint, no token minter, no
   `errors.auth.invalid_credentials`. Every artefact this ruling constrains is KAFF-101a's, and
   KAFF-101a is now unblocked by D-062 §1 and §2. **The ruling is a constraint on that build, and it
   is written above in the form the handler needs.**
2. **§2 is urgent but not tonight, and the collision was real while it lasted.** KAFF-116's
   grant-path migration was authored on `audit_records` during this same session and landed at
   00:04. Two migrations authored concurrently against one table is how
   `KaffDbContextModelSnapshot` gets corrupted, and the corruption is quiet. **N-19's deadline is the
   first audit-writing endpoint, not this session** — today's only audit rows are test and
   development rows. **The blocker is now cleared: KAFF-116's migration is in the tree, so the IP
   column can be authored next, and must be before KAFF-100 or KAFF-101a ships.** It is one property,
   one EF line, one middleware argument and one migration.
3. **§3 is explicitly not urgent, on D-061's own reasoning.** Relaxing `NOT NULL` is an `ALTER`, not
   a backfill, and it touches no existing row. It can land with KAFF-101a, in the same migration as
   the event kind it exists to serve — which is better than landing alone, because the two are one
   change.

**Nothing else was touched.** No grant-path column, no `AuditConfiguration` edit, no migration, no
model change, no story file. `KaffDbContextModelSnapshot` was not modified by this session and shows
no changes anyone did not author.

**Verified twice, and the two runs differ — because Backend landed KAFF-116 between them, not
because anything here changed.** No `.cs` file was touched by this session.

| | Build | `dotnet format` | Domain | Api |
|---|---|---|---|---|
| Session start, 00:02 | 0 warnings / 0 errors, exit 0 | exit 0 | 75/75 | 48/48 |
| After KAFF-116 landed, 00:12 | 0 warnings / 0 errors, exit 0 | exit 0 | 75/75 | **53/53** — five new, Backend's |

`scripts/check-citations.ps1`: **broken 0** at both. Its `legacy` count is **98** and its exit code is
**1** at both — every one of those 98 predates this session (76 in the 2026-08-21 refinement minute
alone) and this entry added none. **The script's green light is `broken: 0`; its exit code has been
red repo-wide since SM-31 was adopted and is not a signal about tonight's work.**

**No test was written, and therefore none was watched fail.** `agents.md` §3c requires the author of
a test to watch it turn red; the honest consequence is that a session which builds nothing owes no
such evidence and must not manufacture any. **The tests these three rulings need belong to the change
that implements them**, and each has an obvious mutation: return 403 for the client credential and
`AC-101a-B`'s fourth case must fail; null the IP source and the security-event assertion must fail;
restore `NOT NULL` and the unknown-username event must fail to insert.

---

#### For Nabil and Karim, not for us — **Q54**

**An IP address is personal data. This table has no retention mechanism and cannot be given one
without breaking the rule it exists to keep.**

`audit_records` is append-only and no-truncate by database trigger, and D-033 refuses to start the
application without them. **There is no delete path, and adding one would be the defect** — the
prohibition is in CLAUDE.md and it is not qualified. So the moment the first IP is written, Kaff
holds a personal-data field with **no expiry, by construction, forever.**

**This is not an engineering question and I am not answering it.** Three things a ruling would have
to say, and none is derivable from anything in `spec.md`:

* Is indefinite retention of sign-in IP addresses acceptable to Kaff? *(If yes, this is a one-line
  answer and §2 proceeds exactly as written.)*
* If not, what is the lifetime — and how is it honoured in a table whose whole design forbids
  deletion? The only mechanisms that do not break the append-only rule are **partition-and-detach by
  age**, or **storing a keyed hash instead of the address** (which still answers *"the same source
  again"* and no longer answers *"which source"*).
* Does the answer differ for a failed sign-in against an unknown username — the one case Karim
  specifically asked to capture — versus every other row?

**Raised as Q54, added to `stories/questions-for-karim.md`. It does not block KAFF-101a.** Karim has
already ruled that the event is logged with the IP; the question is what happens to it afterwards,
and the second answer can arrive later than the first. **It must not be settled by an agent**, which
is why it is here in full rather than as a footnote.

---

#### Routed, not settled — three findings for other owners

**A-01 · No check constraint is verified at start-up.** `FindMissingGuardsAsync` checks eight
triggers, three indexes and a view
[Verified: 2026-08-23 @ `DatabaseInitializer.cs` -> `FindMissingGuardsAsync`], and **none of the
repository's check constraints** — including D-061's own two, KAFF-116's new
`ck_audit_records_grant_path`, `ck_postings_amount_positive`
[Verified: 2026-08-23 @ `TreasuryConfigurations.cs` -> `ck_postings_amount_positive`] and the rest.
The asymmetry is not principled: `ux_postings_reverses` is an ordinary EF model index
[Verified: 2026-08-23 @ `TreasuryConfigurations.cs` -> `ux_postings_reverses`] and **it is** in the
list, so the list is not "raw SQL only". A database missing a check constraint starts, serves, and
reports no missing guards. **This is D-033's exact failure mode inside D-033's own mechanism.**
The fix is one array and one `pg_constraint` query; it is not done here because it changes start-up
behaviour in every environment and is outside the three decisions. **Architect's own backlog.**

**A-02 · `AC-101a-G` is a username-enumeration oracle at the same door §1 just closed.** It requires
a subcontractor sign-in to be refused with `errors.auth.role_cannot_log_in`
[Verified: 2026-08-23 @ `KAFF-101a-sign-in-api.md` -> `AC-101a-G`], a real key that exists in the
catalogue [Verified: 2026-08-23 @ `en.json` -> `errors.auth.role_cannot_log_in`]. A subcontractor can
hold no credential at all — `StorePasswordHash` refuses the role
[Verified: 2026-08-23 @ `User.cs` -> `StorePasswordHash`] — so that response can only be produced from
the **username alone**, and it announces *"this username exists and belongs to a subcontractor"* to
anybody who types it. It contradicts `AC-101a-B` in the same story. **My §1 ruling makes the client
case the fourth member of one indistinguishable set; `AC-101a-G` is a fifth case sitting outside it,
and a door with one leak is a leaking door.** The architectural position is the same as §1 — one
refusal shape at an anonymous endpoint — but `AC-101a-G` is the BA's text and Q47 already owns the
underlying question. **Routed to the BA via the Scrum Master; Q47's three cases are now five.**

**A-03 · `Q47`'s question, as written in the register, lists three cases.** §1 adds a fourth (a valid
`Role.Client` credential) and A-02 a fifth. **The register row should be widened before it goes to
Karim**, or he will answer a narrower question than the one the door now asks. **BA / Scrum Master.**

---

**Revisit if.** The portal gets its own minter (slice 8) and the two need a shared shape — at that
point *"which audience is this token for"* becomes a real parameter rather than a constant. Or if
Karim's answer to Q54 is a lifetime, in which case §2's column becomes a partitioning decision and
the shape above is the input to it, not the conclusion. Or if a subjectless event turns out to need a
**typeless** one too, which §3 deliberately did not permit.

---

### D-064 · The start-up guard check now verifies check constraints, read from the EF model · 2026-08-23

**Backend. D-063 A-01, closed.**

**What was wrong.** `FindMissingGuardsAsync` queried `pg_trigger`, `pg_indexes` and `pg_views` and
never `pg_constraint`. A database missing `ck_postings_amount_positive` started, served, and reported
`missingGuards: []` — while CLAUDE.md's *"the safe balance can never go negative … enforced by a
database constraint, not application code"* was silently not enforced. D-033's own failure mode
inside D-033's own mechanism.

**Decision.** One more query in the same method, and the list of names is **read from the EF model,
not written out**: every check constraint in the repository is declared with `HasCheckConstraint` in
`src/Infrastructure/Persistence/Configurations`, and the migrations are generated from that same
model, so the model is the complete list and cannot drift. A hand-written list of 28 names is a list
somebody forgets to extend — the same class of defect being fixed
[Verified: 2026-08-23 @ `DatabaseInitializer.cs` -> `FindMissingGuardsAsync`].

**Two details that are not obvious and cost a build each.**

* **The design-time model, not `DbContext.Model`.** The run-time model is read-optimised and drops
  check constraints entirely; `GetCheckConstraints()` throws *"The requested configuration is not
  stored in the read-optimized model"* on it. `_context.GetService<IDesignTimeModel>().Model` carries
  them. Both are cached singletons, so nothing is rebuilt per call.
* **One query, not one per name**, unlike the trigger and index loops above it. There are 28 of them
  and `/api/health` calls this method on every poll; 28 extra round trips per health check is a real
  cost and D-033's *"revisit if"* clause is about exactly this.

**Behaviour change, deliberately, in every environment.** Outside Development a database missing any
check constraint now refuses to start, in the existing message shape and naming what is missing. The
test fixture builds the schema with `EnsureCreatedAsync` from the same model, so it creates all 28
and the suites are unaffected.

**Watched to fail, not merely written.** `ck_postings_amount_positive` was dropped from the running
`kaff` database: `/api/health` went from `200 healthy … missingGuards: []` to
`503 degraded … missingGuards: ["ck_postings_amount_positive"]`, and a Staging start-up refused with
*"Refusing to start: database guards are missing — ck_postings_amount_positive."* Restored, and the
`/run-kaff-erp` smoke check passed all seven. The permanent test drops and restores the constraint
itself for the same reason
[Verified: 2026-08-23 @ `SchemaInvariantTests.cs` -> `A_dropped_check_constraint_is_reported_as_a_missing_guard`].

**Not done.** Column nullability, foreign keys, unique constraints and column types are still
unverified at start-up. The check constraints were the gap D-063 routed; a schema-wide comparison
against the model is a different and much larger thing, and nothing has asked for it.

**Revisit if.** A check constraint is ever added by raw migration SQL rather than
`HasCheckConstraint` — the model would not know about it and the guard list would silently miss it.
Today there are none [Verified: 2026-08-23 @ `TreasuryConfigurations.cs` -> `ck_postings_amount_positive`].

---

### D-065 · Q47 ruled — one answer at the door, with one exception that is flagged back · 2026-08-23

**Nabil, as Owner and Architect, unifying the sign-in responses to seal off user enumeration.** Four
of the five cases are unambiguous and unblock immediately. **The fifth is recorded as OPEN, not
applied**, because it contradicts the other four and the story's own stated reasoning.

#### The ruling

| # | Case | Ruled response |
|---|---|---|
| 1 | Wrong password | **401**, generic invalid-credentials key |
| 2 | Unknown username | **401, the exact same message.** *"Never tell an attacker the account does not exist."* |
| 4 | `Role.Client` at the staff origin | **401**, same. Already settled in D-063 §1 |
| 5 | `Role.Subcontractor` | **401**, same. *"If we return a specific `errors.auth.role_cannot_log_in`, we are explicitly telling the attacker: 'This account exists and belongs to a subcontractor.' That is a security breach. The door must treat a subcontractor exactly the same way it treats a non-existent user."* |
| 3 | Locked account | 🟡 **423 Locked**, distinct key. *"A legitimate user needs to know their account has been locked due to failed attempts so they stop trying and contact administration."* **See the flag below — NOT to be built yet.** |

**Case 5 closes A-02**, which D-063 routed rather than settled and which the BA correctly handed back
rather than resolving. **Case 4 confirms D-063 §1** rather than changing it.

**One consequence in code worth stating, because somebody will otherwise tidy it away:**
`errors.auth.role_cannot_log_in` **stops being reachable from the sign-in door**. It still exists and
`SeparationOfDuties` still uses it [Verified: 2026-08-23 @ `SeparationOfDuties.cs` -> `RoleCannotLogIn`].
**Do not delete the key on the strength of this ruling.**

---

#### 🟡 OPEN — Case 3 contradicts Cases 2 and 5, and it is back with Nabil

**A locked account only exists if the username exists.** A distinct **423** therefore announces *"this
username is real"* — the precise thing Cases 2 and 5 were ruled to prevent. And it is reachable on
demand: an attacker who can trigger the lockout — five failed attempts, per `LockoutOptions` — can
**manufacture** the 423 for any username, turning it into an enumeration primitive **and** a
denial-of-service one.

**The story already contains the counter-argument, in its own words, and that is the strongest
evidence here.** KAFF-101a rule 14 reads: *"A **locked** account produces that same refusal. Saying
'locked' tells an attacker the username is real and that their lockout worked"*
[Verified: 2026-08-23 @ `KAFF-101a-sign-in-api.md` -> rule 14]. `AC-101a-B` asserts all three cases are
identical **in status, body and `messageKey`**. D-063 §1 widened that set to four. **This ruling widens
it to five and simultaneously removes one — those cannot both be the intent.**

**Nabil's reason is legitimate and is not dismissed.** A locked-out user given a generic 401 keeps
trying and generates support load. **A standard resolution satisfies both halves and has been put to
him: return 423 only when the submitted credentials were otherwise correct.** A wrong password against
a locked account gets the generic 401; **the right password** against a locked account gets the 423.
The 423 then leaks nothing, because only someone who already holds the correct password can see it —
and that is exactly the legitimate user the reasoning is about.

**Until he answers: Cases 1, 2, 4 and 5 are built; Case 3 is built neither way.** Sequenced last so it
is not a prerequisite. **If he reaffirms the flat 423, it is built as ruled and the accepted trade-off
is recorded here as an explicit decision, not a defect** — his call to make, and it must be visible
that he made it.

#### 🟡 OPEN — the ruled key namespace does not exist

The ruling names `errors.identity.invalid_credentials` and `errors.identity.account_locked`.
**Neither key exists in either catalogue** [Verified: 2026-08-23 — absent from `en.json` and `ar.json`].

**The namespaces are already divided along a line this ruling crosses:**

* **`errors.auth.*`** holds **door and authorization refusals** — `not_authenticated`, `forbidden`,
  `not_assigned_to_project`, `role_cannot_log_in` [Verified: 2026-08-23 @ `en.json` -> `errors.auth.role_cannot_log_in`].
* **`errors.identity.*`** holds **`User` entity validation** — `hr_role_requires_hr_department`,
  `password_hash_required`, `full_name_required`.

**A sign-in refusal is a door refusal.** Scrum Master's call, as a consistency decision rather than a
business one: **`errors.auth.invalid_credentials` and `errors.auth.account_locked`.** Raised rather
than applied silently, and reversible by Nabil at no cost — no code depends on either name yet.

**Whichever names are chosen, both keys must be created in `en.json` and `ar.json` together.**
`TranslationCatalogueTests` fails the build when a `MessageKey` lacks either locale
[Verified: 2026-08-23 @ `TranslationCatalogueTests.cs` -> `TranslationCatalogueTests`]. That guard is
doing its job; it is named here so it is not discovered as a mystery build break.

---

#### What this unblocks, and what it does not

**KAFF-101a moves toward `Ready`** on Cases 1, 2, 4 and 5. It is **not** fully unblocked: `AC-101a-O`
still depends on two decided-but-unbuilt mechanisms — the **IP column** (D-063 §2, and N-19 applies:
it must land before the story ships) and a **nullable subject** (D-063 §3).

**KAFF-105a is untouched.** V-03 — rule 3 versus `AC-105a-C`, field-or-refusal — is N-04 / Q-UX-18 /
SM-16 and remains open. Checked, not assumed.

**A-01 is already closed** — D-064, Backend, before this ruling arrived. The start-up guard now reads
the required check constraints from EF's design-time model and refuses to start when one is missing,
proven by dropping `ck_postings_amount_positive` and watching start-up refuse with exit 82.

---

### D-066 · KAFF-106 built — the create-user endpoint, and where its two checks live · 2026-08-23

**Backend. The first vertical slice with real endpoint surface: before this the API served
`GET /api/health` and nothing else.**

`POST /api/users` — `src/Api/Features/Users/CreateUser/`, the five files CLAUDE.md dictates. No
MediatR, no repository, no forwarding service; the handler holds `KaffDbContext` and talks to it.

#### 1. The permission check is one line, and it is the endpoint's

`.RequirePermission(Permission.UserManage)` [Verified: 2026-08-23 @ `Endpoint.cs` -> `Map`]. Both
halves of "role x assignment" are decided from the catalogue row, which is `CompanyWide` and granted
to `Role.Owner` alone [Verified: 2026-08-23 @ `PermissionCatalogue.cs` -> the `Permission.UserManage`
row]. **Company-wide is the assignment half's answer, not its absence** — the permission names no
project, so there is nothing to be assigned to, and declaring a project scope on a route with no
project in it would refuse every caller including the Owner.

**Watched to fail.** The `RequirePermission` line was removed, the solution rebuilt clean, and
`Nobody_but_the_owner_can_create_a_user` went red on **Finance receiving 201 Created**. The fallback
policy still required an authenticated caller and admitted her — which is the exact failure mode, and
the reason "it is behind auth" is not the check.

#### 2. AC-106-K is enforced by routing through `User.Create` and returning its refusal

The domain guard binding `Role.Hr` to `Department.Hr` already existed
[Verified: 2026-08-23 @ `User.cs` -> `ValidateDepartment`]. **The criterion adds the level above it**,
and the handler earns it by passing the request's department through untouched.

**Watched to fail, and this is the mutation worth recording.** The handler was given one plausible
line — a conditional replacing the department with `Department.Hr` whenever the role was `Role.Hr`, a
handler "helpfully" correcting it on the way past. It compiled clean, and:

* `An_hr_user_cannot_be_created_outside_hr_at_the_endpoint` went **red**: 201 Created where 400 was
  expected, and the HR user existed in Finance.
* `An_hr_user_cannot_be_placed_in_another_department` stayed **green**
  [Verified: 2026-08-23 @ `CatalogueCompletenessTests.cs` ->
  `An_hr_user_cannot_be_placed_in_another_department`].

**That is the whole argument for AC-106-K in one run:** a Domain test cannot see this, because the
domain was never asked. SM-21's condition on the KAFF-107 fold is met by the endpoint test, not by
the domain one.

#### 3. `SetTemporaryPassword`, and it was watched to fail too

D-049 ruling 4. Swapping in `SetOwnPassword` — the two differ in one flag — built clean and turned
`The_password_the_owner_sets_is_temporary_and_is_not_stored_as_typed` red on `MustChangePassword`
being false. A credential the Owner chose and the holder never has to replace means two people
permanently share the identity the trail attributes actions to.

#### 4. Password hashing: PBKDF2 from the BCL, no package

D-011 pointed at `Microsoft.AspNetCore.Cryptography.KeyDerivation`. That package wraps
`Rfc2898DeriveBytes.Pbkdf2`, which is already in the framework, so **nothing was added to
`Directory.Packages.props`** — CLAUDE.md forbids a package that duplicates the framework.

**The stored form names its own parameters**: algorithm, iteration count, salt and hash, separated by
`$`, both binary halves Base64 [Verified: 2026-08-23 @ `PasswordHasher.cs` -> `Hash`]. Raising the
iteration count later must not invalidate credentials issued before it, and a bare hash leaves the
verifier nothing to work back from. **Verification is KAFF-101a's** and must read the parameters from
the string, not from these constants. There is no `Verify` here — nothing signs in yet, and KAFF-101a
is where the timing-safe comparison and the rehash decision belong together.

#### 5. Two new message keys, both catalogues, and the guard was watched to fire

`errors.identity.username_taken` (AC-106-G; the screen already named it as "new — backend must emit"
[Verified: 2026-08-23 @ `ux/slice-1-flows.md` -> `errors.identity.username_taken`]) and
`errors.auth.password_too_short` (D-049 ruling 3). Both added to `en.json` and `ar.json` together.

**Watched to fail:** deleting the Arabic entry for `errors.identity.username_taken` turned two of
`TranslationCatalogueTests`' three tests red, naming the key and the file. The guard is real.

**Namespace, flagged not decided.** D-065 divided `errors.auth.*` (door refusals) from
`errors.identity.*` (`User` validation) and settled *sign-in* refusals. A password **policy** refusal
at creation is neither. `errors.auth.password_too_short` was chosen because that is the key S-007
names and drifting from it would silently break the screen. **Scrum Master's or Nabil's to confirm.**

#### 6. Two pieces of shared wiring the first real endpoint had to add

* **`IRequestValidator<>` is now discovered by assembly scan**, beside `IEndpoint`
  [Verified: 2026-08-23 @ `IEndpoint.cs` -> `AddKaffEndpoints`]. `ValidationFilter` resolves the
  validator from the request scope and **skips validation silently when none is registered**, so a
  validator that exists but was never registered is an endpoint that quietly stopped validating.
  Scanning removes the step somebody forgets, and keeps slices out of a shared registration file.
* **Enums travel as member names.** `KaffJson` already called itself "the single JSON configuration
  used for audit before/after snapshots and for API payloads" and had never been wired into the HTTP
  pipeline, because slice 0's one endpoint carried no enum. `ConfigureHttpJsonOptions` now adds the
  string enum converter [Verified: 2026-08-23 @ `Program.cs` -> `ConfigureHttpJsonOptions`]. The
  audit table stores enums as text so a row outlives today's code, and the UI keys them as
  `enum.<Type>.<Member>`; a numeric wire form would be the one place the same value is a number.

#### 7. The temporary password is optional on the request, and that is a judgement worth seeing

`AC-106-A` supplies one and `AC-106-I` proves eight lower-case characters are enough. **Nothing in
the story describes creating a user without one** — but S-007 does not render the field for
`Role.Subcontractor`, which can hold no credential at all
[Verified: 2026-08-23 @ `User.cs` -> `StorePasswordHash`], and KAFF-106 rule 10 describes an account
with a null `PasswordHash` as a legitimate state rather than a broken one. QA's `TC-1-047` still
expects `PasswordHash` null on creation, which predates D-049 ruling 4 and now contradicts
`AC-106-A`.

**Optional is the union of both readings and refuses nothing the story requires**: supplied, it is
hashed and forces a change; omitted, the account exists and cannot authenticate. Raised for QA rather
than resolved — `TC-1-047` and `TC-1-048` need rewriting against D-049 ruling 4 either way.

#### Not done

* **`AC-106-H`** — that a user with `MustChangePassword` reaches nothing but the change-password
  endpoint. There is no sign-in door and no password-change endpoint to reach: KAFF-101a and
  KAFF-103. The flag is set and stored; the gate that reads it is not built and **must not be assumed
  to exist**.
* **`AC-106-J`** — Arabic RTL at 390px. Frontend's; no screen was built and `src/Web` was untouched
  apart from the two locale entries above.
* **The `users.*` and `enum.*` UI keys** the story lists. Frontend's, with the screen.
* **A read endpoint.** Q42's warning stands and is not this story's: `UserRead` projects **name and
  role, and stops**.
* **Concurrency beyond the unique index.** The handler pre-checks the name and also maps a unique
  violation on `ux_users_user_name` to the same refusal, so the loser of a race gets a 409 rather
  than a 500 [Verified: 2026-08-23 @ `Handler.cs` -> `IsUserNameCollision`]. Nothing serialises the
  two requests, and nothing needs to.

---

### D-067 · KAFF-108's endpoint shipped with no permission gate at all · **DEFECT, FIXED 2026-08-24**

`PUT /api/users/{userId}/department` was mapped with `.WithName()` and `.WithTags()` and **no
`.RequirePermission(...)`**. Any authenticated caller could move any user between departments.

**Why that is the worst possible endpoint to leave ungated:** a department is one of the two axes a
permission is granted against (§9), so this route hands out capability. `EmployeeManage` and
`ProjectAssignmentManage` were both reachable by department alone at some point in this project's
history — D-035 and D-044 ruling 2 — and KAFF-108's own story says so in as many words: *"this
endpoint can grant capability without touching the role. It is `UserManage`-privileged for that
reason."* An ungated route to it is a privilege-escalation primitive, not a missing check.

**The fix is one line**, and every artefact already agreed on it before the line was written:

* The endpoint's own XML doc said *"The permission check is the `RequirePermission` line below and
  nowhere else"* — **describing a line that was not there.** Four paragraphs of correct reasoning
  about `UserManage`, company-wide scope, and why `ProjectScope.FromRoute()` would refuse even the
  Owner, sitting above a `Map` chain that enforced none of it.
* `AC-108-E` requires HR, Finance and Technical Office to each be refused with 403.
* `Nobody_but_the_owner_can_move_a_user_between_departments` asserted exactly that.

#### What actually caught it, and it was not review

**The test was red.** `Api` stood at **81 total, 1 failed**, and the failure read
`Expected HttpStatusCode.Forbidden {403}, but found HttpStatusCode.NoContent {204}` — a non-Owner
completing the move. Green after the fix: **81 / 81**.

**This is the strongest watched-to-fail evidence this project has produced, and nobody staged it.**
Every previous guard was mutated deliberately to confirm it could go red. This one was red against a
real defect in the real tree, and it named the exact status code and the exact wrong outcome.

**The prose was the least reliable artefact in the file.** A reader checking whether this endpoint was
protected would have read the remarks — which are accurate, detailed, and cite `PermissionCatalogue`
and D-044 correctly — and concluded it was. The comment described the intended code rather than the
code, and it is the one thing here that could not fail. That is D-058's finding arriving in a source
file: **a claim that points at nothing cannot be told it is stale.** `check-citations.ps1` verifies
the identifiers this comment cites and all of them exist; the sentence was still false.

#### What this says about SM-30, which is not the obvious thing

SM-30 requires a new **catalogue row** to ship with a test. This story added no catalogue row — it
reuses `UserManage` deliberately, and correctly. **So SM-30 did not apply and would not have caught
this.** The gap is not a permission without a test; it is an **endpoint** without a permission.

The mechanism that would catch it generically is the same shape as `PermissionCoverageTests`: a test
that enumerates mapped endpoints and fails on any that carries no permission requirement and is not
on a named anonymous allow-list — `GET /api/health` being the only member today. **Not built here**,
because it needs the anonymous list to be a deliberate decision rather than whatever the scan finds
on the day, and sign-in is about to become the second member. Raised for the Architect as **A-04**.

**Found during an independent verification pass**, not by the session that wrote the endpoint —
`agents.md` principle 2, doing the specific job it exists to do.

---

### D-073 · The audit trail attributes acts to the role the token claims, not the role the user has · **CLOSED 2026-08-25 by D-075**

> **Closed, not deferred to KAFF-109.** Both halves are answered in D-075: the trail now takes the
> actor from the row the gate read out of the users table, so it no longer reads the claimed role at
> all, and `ck_audit_records_actor_is_named_completely` refuses the half-named actor this entry
> routed separately. D-075 §6 says why the "reachable when KAFF-109 lands" reasoning does not survive
> the fix. **Read D-075 before acting on anything below.**

> **Renumbered from D-068 to D-073 on 2026-08-25 by the Scrum Master.** It was written as D-068 while
> a D-068 already existed — *"The fourth instance, and why it does not get a fourth rule"* — which is
> cited in `process/agile.md`, `stories/backlog.md`, `KAFF-116` and the execution log, and referenced
> by D-069. **This entry was cited nowhere**, so it moved and the other stayed. **A duplicate D-number
> in the file `CLAUDE.md` sends every agent to first is a worse defect than it looks:** two entries
> answering to one name means a citation resolves to whichever a reader scrolls to.

Raised 2026-08-25 while checking a diagnosis Backend made in passing and did not pursue: *"the audit
actor's role comes from the token claim, not the database."* It is correct, and its consequence is
larger than the test failure that surfaced it.

> ### ⚠️ CORRECTED 2026-08-25 — this entry's original worked example cannot happen
>
> **It led with a department move**: move a user out of Technical Office, they act, the gate decides
> on the new role and the trail records the old one. **That is wrong, and the Verifier caught it.**
> `MoveToDepartment` writes only `Department` and `OperationsSubDepartment`
> [Verified: 2026-08-25 @ `User.cs` -> `MoveToDepartment`], and `AuditRecord` has **no department
> field at all** [Verified: 2026-08-25 @ `AuditRecord.cs` -> `class AuditRecord`]. **A department move
> cannot make `ActorRole` stale, because `ActorRole` records the role and a department move does not
> change it.**
>
> **The only trigger that would is a role change, and `Role` is assigned once in the constructor with
> no mutator anywhere** [Verified: 2026-08-25 @ `User.cs` -> `Role`]. **So the divergence is not
> reachable today.** It becomes reachable the moment **KAFF-109** adds one, which is why this stays
> **OPEN** rather than closing.
>
> **Consequently D-073 does not block KAFF-108**, and the Verifier accepted that story on exactly
> this reasoning.
>
> **The correction is left in place rather than the entry rewritten**, because a `decisions.md` entry
> that quietly loses its wrong argument teaches nobody. **This is the file `CLAUDE.md` sends every
> agent to first, and it led with an example that does not happen for a day.**
>
> **What is real, reachable and separate — routed on its own, not as part of this entry:**
> **`AuditRecord.ActorRole` is nullable, is not `IsRequired()`, and carries no check constraint.** A
> token arriving without the role claim writes a **permanently unattributed row** into an append-only
> table. That needs no role change to happen and it is due **before KAFF-109, and before KAFF-101a
> mints real tokens.**

**The two halves disagree, and only one of them was fixed.**

* **Authority** is read from the database on every request. That is D-048, and it exists precisely
  *because a token's claims go stale* — a deactivated Owner kept `UserManage`, a deactivated Finance
  user kept `TreasuryPostCompany`.
* **The audit actor's role** is read from the token claim
  [Verified: 2026-08-25 @ `HttpContextCurrentUser.cs` -> `Role`, which is
  `ReadEnum<Role>(KaffClaimTypes.Role)`], and flows into `AuditRecord.ActorRole`
  [Verified: 2026-08-25 @ `AuditRecord.cs` -> `ActorRole`].

**So the permission system distrusts the token and the audit trail believes it**, about the same
user, on the same request.

#### Why the gap is reachable rather than theoretical

`SecurityStamp` rotates on `StorePasswordHash`, `ClearPassword` and `Deactivate`
[Verified: 2026-08-25 @ `User.cs`]. **It does not rotate on a department move, and there is no
role-change method on `User` at all** — KAFF-109 is not built yet. So a moved or re-roled user keeps
a working token carrying their *former* role and department.

KAFF-108's endpoint says this deliberately and correctly: *"no claim is re-issued, no token is
minted, no cache is invalidated… The moved user's existing token keeps working and carries different
authority on its next request — which is the behaviour, not a side effect of one."* That reasoning is
right about authority. **Nobody checked what it does to attribution.**

**The concrete case, in a story that is already built:** the Owner moves a user out of Technical
Office. The user acts. The gate correctly refuses or permits them on their **new** role, and the
audit record correctly names *who* they are — and attributes the act to the **old** role. A reader of
the trail sees a role that had no such authority performing the act, or worse, sees a plausible role
that is simply not the one the system decided on.

#### Why it cannot be left for later

`audit_records` is append-only and no-truncate by trigger, and `CLAUDE.md` forbids a delete or update
path without qualification. **A wrong `ActorRole` is wrong permanently.** This is the same argument
as N-19's grant path, which was built before its first consumer for exactly this reason — except this
column already exists and is already being written on every audited request.

#### What is *not* claimed here

**This is not an authorization hole.** Nothing is permitted that should be refused; D-048 holds and
`EndpointPermissionCoverageTests` (D-069) holds. It is a **forensic accuracy** defect, in the one
table whose entire purpose is to be believed later.

Nor is it obviously "read the role from the database too". That is one option and it has a cost — an
extra read on every audited write, on a path that already reads the subject once. The alternatives
worth weighing are recording **both** the claimed and effective role, or rotating the stamp on a role
or department change so a stale claim cannot exist. **That third option contradicts KAFF-108's
documented behaviour**, which was itself a deliberate decision, so it is not a free choice — which is
why this is the Architect's and not Backend's.

**Routed to the Architect. Not fixed here.** `ActorDisplayName` has the same shape and should be
considered in the same decision: a renamed user's later acts are attributed to their old name.

---

### D-068 · The fourth instance, and why it does not get a fourth rule · 2026-08-24

**Scrum Master.** I said on 2026-08-22 that the claim-hardening pattern had three instances and that
I wanted a fourth before legislating it. **D-067 is the fourth and it is the most expensive.** This
entry is the ruling, and the ruling is **not to write the rule.**

#### The four

| # | What was restated instead of pointed at | Cost |
|---|---|---|
| **SM-31** | a **position** — `PermissionCatalogue.cs` line 200 | ~68 citations into one file across 30 line numbers, one still correct |
| `ProjectAccessPolicy` | a **fact** — *"HR holds just two"* | wrong the day `UserRead` landed; still passes every check |
| KAFF-106 / KAFF-113 | a **quotation** — attributed to a sentence I had deleted | a fabricated attribution in two committed stories |
| **D-067** | an **enforcement claim** — *"The permission check is the `RequirePermission` line below and nowhere else"*, above a `Map` chain that had no such line | **a privilege-escalation primitive: any authenticated caller could move any user between departments** |

**The fourth is a different shape from the first three and that matters.** One, two and three are
copies — a copy has no pointer, so nothing can tell it it is stale. **The fourth is a pointer**, to a
syntactic construct in the same file, and it was **false on the day it was written**. It is SM-31's
shape gone intra-file and informal, where no checker can reach it.

**Four paragraphs of accurate reasoning sat above it** — `UserManage`, company-wide scope, why
`ProjectScope.FromRoute()` would refuse even the Owner. All true, all useful, and **a reviewer reading
that file to answer "is this route protected?" would have concluded yes.** `check-citations.ps1`
passes on it: every identifier the comment names exists.

#### The ruling: no SM-32

**A fifth prose law would be the seventh fix of this class in four days, and six of the first were
requests to be careful.** The two that held — `check-citations.ps1` and
`Every_permission_catalogue_row_is_named_in_a_test` — are machines. **A rule saying "do not write a
comment that claims something the code does not do" is unenforceable by construction: it asks the
author to notice exactly the thing they have already failed to notice.**

**The answer to the fourth instance is A-04, and A-04 is a machine.** D-067 raised it: a test that
enumerates the mapped endpoints and **fails on any that carries no permission requirement**, except
members of a **named anonymous allow-list**. It catches this defect and every future one of its shape,
on every build, without anyone reading a comment.

**And note SM-30 would not have caught it, which is the useful diagnostic.** SM-30 requires a new
*catalogue row* to ship with a test. KAFF-108 correctly adds no row — it reuses `UserManage`. **The
gap is not a permission without a test; it is an endpoint without a permission.** Those are different
holes and they need different machines. **Routed to the Architect**, because the allow-list is a
deliberate security decision — `GET /api/health` is its only member today and sign-in is about to be
the second, and an allow-list that grows by accident is the defect wearing the fix's clothes.

**What I am recording instead of a rule**, as an observation for whoever reads this next:

> **Prose that a reviewer would rely on to answer a safety question is not documentation. It is an
> unexecuted assertion.** Either something executes it, or it is decoration that reads like a
> guarantee.

**Revisit if** a fifth instance appears in a shape A-04 and `check-citations.ps1` between them cannot
see. Then the gap is real and mechanical, and it gets a machine — not a paragraph.

---

#### V-J — the Definition of Done line I wrote is blocking work it was not aimed at

The Verifier's finding, and it is against my own rule. `process/agile.md`'s Definition of Done says
`scripts/check-citations.ps1` **passes**. It exits 1 while **97 legacy line-number citations** remain
repo-wide — **76 of them in `meetings/2026-08-21-sprint-1-refinement.md`, which is my debt and none of
it in any file the current work touches.**

**So a pre-existing debt of mine currently blocks acceptance of every story, including two that are
otherwise done.** That is wrong scoping, not a wrong rule: **a Definition of Done is about the change
in front of you.**

**Ruled — the DoD line becomes two, and both are per-change except the one that must be global:**

* **`broken (identifier absent)` is 0 repo-wide.** That is the real safety property — a citation
  pointing at something that does not exist — and it is currently satisfied and must stay satisfied.
* **The change introduces no new legacy line-number citation.** Measured against the count before the
  change, not against zero.

**The 97 remain mine and remain owed.** They are not forgiven by this; they are removed from the path
of work they have nothing to do with. `meetings/` is 64 distinct targets, each needing a real lookup —
recorded in D-059 §13 and still true.

---

#### Story status, ruled

* **KAFF-116 — ACCEPTED.** The Verifier recommends it and I concur. Six criteria, four grant paths
  distinguishable on a written record, the column landed before its first consumer, which was the
  whole argument for building it first.
* **KAFF-106 — NOT ACCEPTED. Held open on V-A**: the 403 carries no `messageKey`, so the Arabic UI has
  nothing to render for the refusal `AC-106-B` requires. `AC-106-J` (Arabic, RTL, mobile width) is
  **carried forward explicitly, not tacitly** — there is no UI yet. `AC-106-H` is correctly deferred.
  **The Verifier's warning is the one to keep: *"the temptation on a green suite is to read 11 of
  11."***
* **KAFF-108 — built, and complete despite appearances.** Its slice has no `Response.cs` and no
  `Validator.cs`, unlike `CreateUser`, and that is **correct rather than interrupted**: the endpoint
  returns **204**, which has no body, and the request is two nullable enums whose only rule is the
  domain's `ValidateDepartment`, refused there with an i18n key. **`CLAUDE.md`'s five-file listing is
  the shape of a full slice, not a quota.** All seven criteria are cited by eleven tests. **Its one
  real gap was the missing gate, and that was D-067.**

---

### D-069 · A-04 built — an endpoint with no permission is a build failure, and the allow-list has one member · 2026-08-24

**Architect.** D-068 routed A-04 here and said why: the allow-list is a security decision, not a
scan result. This entry is the decision, and the machine that carries it is
`tests/Api.Tests/EndpointPermissionCoverageTests.cs`.

#### 1. It enumerates the routes the host built, not the source that describes them

Three facts over one enumeration of `EndpointDataSource.Endpoints`, taken from a started test host
[Verified: 2026-08-24 @ `EndpointPermissionCoverageTests.cs` -> `ShippedEndpoints`].

**Metadata rather than source text, and D-067 is the argument.** The endpoint that shipped ungated
carried a comment reading *"The permission check is the `RequirePermission` line below and nowhere
else"* above a `Map` chain with no such line. A grep over `Endpoint.cs` files would have read what
somebody meant; the metadata is what the pipeline enforces. `RequirePermission` records itself as an
`IAuthorizeData` whose policy name round-trips through `PermissionRequirement.TryParse`
[Verified: 2026-08-24 @ `PermissionPolicyProvider.cs` -> `RequirePermission`], and nothing else in
the pipeline produces such a name — so the test asks for a **`PermissionRequirement` policy
specifically, never for authorization in general.** The fallback policy already requires an
authenticated caller on every route [Verified: 2026-08-24 @ `Program.cs` -> `SetFallbackPolicy`], and
being authenticated is exactly what D-067's attacker was.

**The test host's own routes are excluded by the assembly that declares the handler**
[Verified: 2026-08-24 @ `ProbeEndpoint.cs` -> `Map`], read from the `MethodInfo` in the endpoint's
metadata. **The filter fails closed**: an endpoint whose handler cannot be identified is treated as
shipped surface and must be gated.

#### 2. The allow-list, and why it has one member and not two

| Method | Route | Why it may be reached with no permission |
|---|---|---|
| `GET` | `/api/health` | A liveness probe carries no credentials, and it answers the one operational question an unauthenticated caller must be able to ask — are the PostgreSQL guards installed on this deployment (D-033). It discloses whether the database answers and which guards are missing, and nothing else. |

**Sign-in is deliberately not pre-listed.** KAFF-101a will need the second entry, and adding it now
would pre-authorise a route nobody has reviewed — the allow-list growing ahead of the decision it is
supposed to record. **The test going red the day that route is mapped is the visible act**, and the
reason gets written by whoever is looking at the route.

**A second fact keeps the list from rotting**, which is the other half of "grows by accident":
`Every_allow_list_member_is_mapped_and_says_so_in_its_own_file` fails on an entry no endpoint maps —
a dead exemption silently pre-authorises whatever claims that route next — and fails on a member
whose own slice does not say `AllowAnonymous()`. **The exemption must be legible in the file a reader
opens**, not only in a test they do not.

#### 3. Watched to fail — three mutations, three reds, each naming the route

House standard, and D-061 and D-063 are the reason it is one.

| Mutation | Result |
|---|---|
| `.RequirePermission(Permission.UserManage)` deleted from `MoveUserDepartment` -> `Map` — D-067's exact defect | `Every_mapped_endpoint_carries_a_permission_requirement` red: *"found at least one item {"PUT /api/users/{userId:guid}/department"}"* |
| `CreateUser` -> `Map` changed to `.RequirePermission(Permission.UserManage, ProjectScope.FromRoute())` | `Every_permission_requirement_declares_the_scope_its_catalogue_row_names` red: *"POST /api/users declares UserManage with project scope Route, but its catalogue row is CompanyWide"* |
| `.AllowAnonymous()` deleted from `GetHealth` -> `Map` | `Every_allow_list_member_is_mapped_and_says_so_in_its_own_file` red on the missing `IAllowAnonymous`. Fact 1 stayed **green** — correctly: the route is allow-listed, and the divergence between the list and the file is fact 2's job |

**Every mutation built clean with 0 warnings**, which is the point restated: the compiler had nothing
to say about a route anyone could reach. All three were restored and the build is clean.

#### 4. V-C ruled — decide now, and the machine already built is the fix

The Verifier's W-1 is real today [Verified: 2026-08-24 @ `PermissionAuthorizationHandler.cs` ->
`HandleRequirementAsync`]: the gate calls `GrantedThrough(access.Path)` on `access is not null`, and
a company-wide permission declared with a project scope reaches that line with
`ProjectAccessPath.None`, which the check constraint turns into a 500.

**Ruled: an endpoint declares the scope its catalogue row names, and a test enforces it.** That is
the third fact above, and it makes W-1 **unreachable by construction** rather than by "nothing does
this today" — which is the same sentence that preceded D-067. Note the mismatch is worse in the other
direction: a *project-scoped* permission declared with `ProjectScope.None` would never evaluate the
assignment half of spec.md §9's "role × assignment" at all, and no previous check would have seen it.
One assertion covers both.

**The one-line guard the Verifier proposes — condition on `access.Granted` rather than
`access is not null` — remains correct and is routed to Backend as belt-and-braces.** It is no longer
load-bearing and is not a blocker: with the scopes agreeing, `access is not null` inside the granted
branch already implies `access.Granted`. Not done here, because it is not my file to mutate while
Backend is in it, and a fix nobody watched fail is the thing D-066 exists to discourage.

#### 5. V-D ruled — slice 4, and the trigger is nameable

W-2 is confirmed [Verified: 2026-08-24 @ `AuditSaveChangesInterceptor.cs` -> `ExtractProjectId`]: the
interceptor writes `projectId is null ? null : _auditContext.GrantPath`, pairing the path with the
*presence* of a project rather than its *identity*.

**Deferred, and not because it is small.** The shape that makes it wrong is a single
`SaveChangesAsync` touching entities of two different projects. **Nothing in the system produces one,
and QA cannot write a scenario that fails against a shape that does not exist** — a test that cannot
fail is worse than no test (`agents.md` §3c). The fix is also not one line: the gate would have to
record the project it authorised alongside the path, which changes `IAuditContext` in `Domain`, the
handler in `Api` and the interceptor in `Infrastructure`.

**The trigger, so this is not rediscovered:** the first handler that saves entities belonging to more
than one project in one `SaveChanges`. Slice 4 is where that becomes plausible (opportunity →
project → BOQ). At that point the guard is
`projectId == _auditContext.GrantProjectId ? _auditContext.GrantPath : null`, with the pair recorded
at the gate.

#### 6. A correction to the run-and-build lore, and it is a green-over-stale-binary case D-046 misses

`.claude/skills/run-kaff-erp/SKILL.md` -> `Gotchas` names a running **`Kaff.Api`** as what locks the
build. **A leftover `Kaff.Api.Tests` host does it too, and it fails differently and worse:** the copy
of `Kaff.Api.dll` into the test output failed with eight `MSB3026` warnings, and the build reported
**`Build succeeded`, 0 errors, exit code 0.** D-046's rule — check the build exit code before trusting
a test result — **would have passed a run against a stale binary.** Stop `Kaff.Api.Tests` as well as
`Kaff.Api`, and treat `MSB3026` on a succeeded build as a failed build.

#### Not done

* **The `access.Granted` one-liner in `PermissionAuthorizationHandler`** — §4. Backend's, with its
  mutation.
* **V-D** — §5, slice 4.
* **Nothing under `src/Api/Features/Users/` or elsewhere in `tests/Api.Tests/` was changed**, beyond
  the three mutations above which were applied and reverted. Backend was working in both concurrently.
* **No analyzer, no source generator, no attribute scheme.** One test class, three facts, and the
  brief was right to fix the rung.

---

### D-070 · KAFF-116 built — the grant path column, and the four questions it answers · 2026-08-24

**Backend, closing verification finding V-E.** KAFF-116 shipped with no entry of its own. It added a
column **and** a check constraint to `audit_records`, which is append-only and trigger-protected, and
every other change to that table has one — D-061, D-063, D-066. The Definition of Done requires it.
This is that entry, written after the fact and against the files as they stand today rather than
against the story.

`audit_records.grant_path`, `character varying(64)`, nullable
[Verified: 2026-08-24 @ `20260822210402_AuditGrantPath.cs` -> `AuditGrantPath`;
@ `AuditRecord.cs` -> `GrantPath`].

#### 1. Why the column is nullable, and what the null means

**Null is not "unknown". It means the act had no project, so no access policy ran and there is no
path to name** — creating a user, creating a client, signing in. `Permission.UserManage` is
`CompanyWide`; the request names no project, nothing resolves one, and `ProjectAccessPolicy` is never
called [Verified: 2026-08-24 @ `CreateUser/Handler.cs`, whose remarks say the same;
@ `AuditSaveChangesInterceptor.cs` -> `ExtractProjectId`, which writes the path only when a project
id is present].

**The alternative was a fifth enum member — `CompanyWide` or `NotApplicable` — and it was the wrong
shape.** The four members answer *"how did this actor reach this project?"*. A company-wide act has no
project, so it does not answer that question badly; it does not ask it. A member meaning "the question
does not apply" would be the one value writable for either reason, and the first time somebody forgot
to set the path it would look exactly like a legitimate company-wide act.

**Defaulting to `OwnerGlobal` would have been worse still** and is refused explicitly
[@ `AuditRecord.cs` -> `GrantPath`]: it invents an authority claim for an act that made none.

**Nullable is not the same as unconstrained.** The pairing is enforced in the database, not in the
comment above it: `grant_path IS NULL OR (project_id IS NOT NULL AND grant_path <> 'None')`
[Verified: 2026-08-24 @ `AuditConfiguration.cs` -> `ck_audit_records_grant_path`], driven with raw
SQL so it holds against something other than our C#
[@ `AuditMechanismTests.cs` -> `A_grant_path_is_refused_without_a_project_and_may_never_be_None`].
Two rows are unwritable as a result: a path over no project, and `None` — which is the value a
refusal carries, and **a refusal writes no record at all**, so a row claiming it would be a grant
naming its own absence.

#### 2. The value is taken from the policy that admitted the request, never re-derived

The gate hands the audit context the `Path` off the very `ProjectAccess` it just granted on
[Verified: 2026-08-24 @ `PermissionAuthorizationHandler.cs` -> `HandleRequirementAsync`, calling
`_auditContext.GrantedThrough(access.Path)`; @ `AuditContext.cs` -> `GrantedThrough`].

**Re-deriving it in the interceptor would have been easy, and that is the trap.** The interceptor has
the user and the project id and could look up an assignment row and conclude `Assignment`. That is a
second derivation of an already-decided fact, and the two would disagree — not hypothetically:
`OwnerGlobal`, `HrGlobal` and `PortalClient` all reach a project **with no assignment row to find**,
so a lookup-based derivation would record nothing, or invent `Assignment`, for three of the four
paths. The trail would then describe reach the system did not use. One derivation, at the point of
the decision, is the only version that can be right.

The same single-source argument keeps a refusal from claiming a path: `Granted` is *derived* from
`Path` rather than stored beside it [@ `PermissionEvaluator.cs` -> `record ProjectAccess`], so
"refused, but reached through X" is unrepresentable in the type before the constraint ever sees it.

#### 3. Why the column had to land before its first consumer

**`audit_records` is append-only by database trigger**
[@ `001_guards.sql` -> `trg_audit_records_append_only`], asserted against the live database by
[@ `AuditMechanismTests.cs` -> `An_audit_record_cannot_be_changed_afterwards`]. A column added later
**cannot be backfilled** — there is no `UPDATE` path and there must never be one. Every row written
before the column existed would be permanently silent about how its actor got there, and the gap
would be widest exactly where the trail matters most: the Owner, whose authority leaves no
`ProjectAssignment` row anywhere, so **without this column three of the four paths are invisible**
[@ `PermissionEvaluator.cs` -> `enum ProjectAccessPath`, whose own remarks record this].

That is the general rule this table works under, already stated in D-055 §7 and now paid for a second
time: **on an append-only table a column is cheap before the first write and impossible after it.**

#### 4. Known limits, both the Architect's — not closed here

* **V-C.** The gate calls `GrantedThrough` on `access is not null` rather than `access.Granted`
  [@ `PermissionAuthorizationHandler.cs` -> `HandleRequirementAsync`]. A company-wide permission
  declared with a project scope would hand it `None` and fail the check constraint as a 500.
  Not reachable today — every company-wide `RequirePermission` uses the one-argument overload.
* **V-D.** The path is paired with the *presence* of a project id, not its *identity*
  [@ `AuditSaveChangesInterceptor.cs` -> `ExtractProjectId`]. A save touching a second project would
  label that project's record with the path that admitted the caller to the first.

Neither is edited here: changing the gate's condition while A-04 is being built against the same
files is how two sessions produce one broken tree.

---

### D-071 · Every refusal names an i18n key, and the fix is one callback rather than one per endpoint · 2026-08-24

**Backend, closing verification finding V-A. KAFF-106's `AC-106-B` was held open on it.**

A 403 from the authorization gate carried **no `messageKey`**, so the Arabic UI had nothing to render
for a refusal. `errors.auth.forbidden` existed in both catalogues and was emitted by nothing
[Verified: 2026-08-24 @ `en.json`, `ar.json` -> `errors.auth.forbidden`;
@ `SeparationOfDuties.cs` -> `AuthorizationErrors`].

**The cause is a seam, not an omission.** `ResultExtensions.Problem` attaches `code` and `messageKey`
to everything a *handler* refuses [@ `ResultExtensions.cs` -> `Problem`]. A 401 or a 403 is never
produced by a handler — the authentication and authorization middleware write it, and they know
nothing about `Error`. So **every permission-refused response in the system had this hole, not only
the one endpoint the finding named.**

#### Where the fix went, and why there

One `CustomizeProblemDetails` callback on `AddProblemDetails`
[Verified: 2026-08-24 @ `Program.cs` -> `AddProblemDetails`], filling in `code` and `messageKey` for
401 and 403 when nothing already has.

**That is the point all callers route through.** `IProblemDetailsService` is the single writer behind
`UseStatusCodePages`, `UseExceptionHandler` and `Results.Problem` alike, and the callback runs inside
it. A guard at the endpoint would have to be repeated on every endpoint that exists and every one
that does not exist yet — and the endpoint that forgets it is the one nobody notices, which is D-067's
shape exactly.

**`TryAdd`, not assignment.** A handler that already named a more specific key keeps it — a domain
`Forbidden` such as `AuthorizationErrors.SameActorCreatedAndApproved` is a 403 with a key of its own,
and the callback must not flatten it to the generic one.

**The generic key is deliberate for gate refusals.** The gate knows *why* it refused —
`NotAssignedToProject`, `RoleNotGranted`, `AssignmentLevelTooLow` — and logs it
[@ `PermissionAuthorizationHandler.cs` -> `HandleRequirementAsync`]. It is not returned. `AC-106-B`
asks for `errors.auth.forbidden`, and telling an unauthorised caller which of the two axes they
failed describes the permission model to the person who has just been refused by it.

**401 was fixed in the same switch and is a second defect the Verifier observed in passing** — an
unauthenticated `POST /api/users` returned 401 with no key. Mapping it to
`errors.auth.not_authenticated` costs one arm. **No locale entry was added by this change; both keys
already existed in both catalogues.**

#### What holds it

* [@ `PermissionMechanismTests.cs` -> `A_refusal_from_the_gate_names_a_key_the_ui_can_render`] —
  asserted on **probe routes that carry no feature code**, so a later per-endpoint patch that left the
  siblings silent turns it red. That is the test for the mechanism rather than for a feature.
* [@ `CreateUserTests.cs` -> `Nobody_but_the_owner_can_create_a_user`] — the key is now asserted
  beside the status inside the six-role loop that already existed. That is `AC-106-B` verbatim, and
  the missing line is the whole reason the criterion read as satisfied on a green suite.

---

### D-072 · Nabil's rulings of 2026-08-24 — the last two blockers fall · **APPROVED**

Four rulings. **Two unblock the last two `BLOCKED` stories in the sprint**, one defers a compliance
problem to a slice with a named mechanism, and one sets the shape of the next message to Karim.

---

#### 1. Q47 Case 3 — 423 only when the password is correct. **UNBLOCKS KAFF-101a**

**Decision, verbatim:** *"The system will return **423 Locked only if the provided password is
correct**. If the password is wrong, it must return the generic 401 Unauthorized. This perfectly seals
the enumeration leak. An attacker guessing passwords learns nothing, while the legitimate user
receives the necessary UX feedback that their account is locked."*

**This is the conditional resolution D-065 put to him, accepted.** Q47 is now answered in full — all
five cases at the door. The flat 423 is not built and never was; **the trade-off D-065 flagged is
resolved rather than accepted.**

**⚠️ The ordering constraint this creates, and it must be written into the story explicitly.** The
password has to be **verified before the lockout state decides the response**. That means **a locked
account still performs a full hash comparison** — 600,000 PBKDF2 iterations
[Verified: 2026-08-24 @ `PasswordHasher.cs` -> `Hash`] — and that is deliberate twice over:

* it is the only ordering that can distinguish *"correct password, locked"* from *"wrong password,
  locked"*, which is what the ruling turns on; and
* **it keeps the timing envelope even.** The obvious implementation — check lockout first,
  short-circuit before hashing — **restores the enumeration oracle through timing exactly as the
  status code stops leaking it.** A locked account would answer in microseconds while every other
  path pays for the hash.

**So "check lockout first" is not an optimisation here, it is the defect.** Written down because it is
the shape a later session will "tidy" toward, and the tidy version passes every test that asserts
status codes.

---

#### 2. V-03 — the flag travels in the payload, not a refusal. **UNBLOCKS KAFF-105a**

**Decision, verbatim:** *"Do not refuse the call at the API level. The API must successfully
authenticate the user, issue the session token, and include a `mustChangePassword: true` flag inside
the payload. The Angular frontend will intercept this flag and explicitly route the user to the
mandatory password change screen, preventing the sign-in dead-end loop."*

**Resolved in favour of rule 3; `AC-105a-C` is the side that changes.** It is also what the frontend
already assumes — `AuthService.Session.mustChangePassword` exists and reads it
[Verified: 2026-08-24 @ `auth.service.ts` -> `Session`].

**It is a three-way reconciliation, not a one-story fix.** The Verifier found **KAFF-103 and KAFF-100
taking opposite sides of the same question**. All three stories are corrected together or the
contradiction simply moves.

**🟡 One thing the ruling implies and does not say — raised, not settled.** A token issued to a
must-change-password user is a **full** token. Whether any endpoint beyond the password-change one
should refuse it is **a rule nobody has stated**. The ruling closes the dead-end loop; it does not say
what else that session may reach. **Do not assume either way** — the permissive reading is that the
flag is advisory and the SPA honours it, the strict reading is that the server refuses everything
else, and those differ by whether a hostile client can skip the change screen entirely.

---

#### 3. Q54 — partition by month at slice 9, and the consequence is due now

**Decision, verbatim:** *"We will not solve this in application code, and it does not block this
sprint. Once we reach **Slice 9 (Compliance/Archival)**, we will implement **PostgreSQL table
partitioning by month** on `audit_records`. This allows us to drop entire historical partitions once
the legal retention period expires, effectively deleting the PII without violating the
append-only/no-truncate triggers on the active partitions."*

**One of the two mechanisms the Architect named as compatible with append-only**, chosen over the
keyed hash. It answers how an IP address in a table with no delete path ever expires.

**🟡 What it creates now, and it is the reason this is not simply filed under slice 9.** **Converting
a populated table to a partitioned one is materially harder than creating it partitioned** — in
PostgreSQL it is a new table plus a data migration plus a swap, on a table that is **append-only and
trigger-protected**, which is precisely the kind of table you least want to rewrite. **If
`audit_records` should be partitioned from the start, that decision is due now, not at slice 9.**

**Routed to the Architect as an open question, not settled here.** The deadline is not slice 9 — it is
**before the first production rows exist**, and the cheapest moment is before slice 3 starts writing
real money history.

---

#### 4. Karim's next message — four questions, one theme

**Decision, verbatim:** *"Batch Q-N10-1, Q-N10-2b, and Q-N10-3 into a single message for Karim, as
they all address who can touch a newly created project."*

And on **V-I**: *"This is defensible as a 'placeholder' profile (e.g. for assigning tasks to a worker
who doesn't log in). **Ask Karim if the business logic actually requires these placeholder accounts**
before we write a criterion rejecting them."*

**Note the shape of the V-I instruction and preserve it.** It is a question about whether the
capability is **wanted**, not an instruction to keep it or to remove it. A story criterion rejecting
placeholder accounts would be inventing a rule; so would one permitting them. **Karim decides whether
the business needs them at all.**

---

#### 5. CI — still never run, and the reason is not in this repository

**Recorded so nobody re-investigates it.** All three jobs are annotated *"The job was not started
because your account is locked due to a billing issue."* Two seconds, zero steps, no runner assigned.
**Nothing in the workflow files is wrong, and CI has still never actually executed.** It clears at
`github.com/settings/billing`, then Re-run jobs — **Nabil's, and it is not an engineering task.**

**It is not recorded as attempted-and-failed, because it was not attempted.** The Definition of Done's
*"runs on staging"* and the CI line both remain **untested**, not failed. The distinction matters: a
failed run tells you something, and a run that never started tells you nothing at all.

**Already fixed and pushed:** `deploy-staging.yml` fired on every push to `main` and has no target
(D-023), so it would have been red on every commit — **burying the first real CI failure on a page
that was already red.** Now `workflow_dispatch` only, with a note to restore the push trigger in the
same change that answers D-023. Commit `37fdaa5`.

**And the repository now exists.** `git init`, **252 files tracked, zero build output**
[Verified: 2026-08-24 — no tracked path under `bin`, `obj`, `node_modules`, `dist` or `TestResults`],
working tree clean, pushed to `github.com/AhmedNabil30/ERP`. **Four days of decision history had no
version control until today.** From here the Definition of Done can reasonably include a commit.

---

### D-074 · KAFF-108, KAFF-110 and KAFF-113 built — one entry for three, closing W-7 · 2026-08-25

**Backend, recording after the fact — verification finding W-7.** D-066 covers KAFF-106 and D-070
covers KAFF-116; D-067 is a defect entry for KAFF-108, not a build entry. KAFF-108, KAFF-110 and
KAFF-113 had no entry at all. One entry for three because none of the three added a permission,
a catalogue row, a migration, or anything the other two would need to cross-reference — each is a
short, independent note, and three short entries would have repeated the same shape three times.

#### 1. KAFF-108 — `PUT /api/users/{userId}/department`, and the defect this entry cannot omit

**The permission is `Permission.UserManage`, `CompanyWide`, Owner alone**
[Verified: 2026-08-25 @ `src/Api/Features/Users/MoveUserDepartment/Endpoint.cs` -> `Map`], the same
row KAFF-106 uses (D-044 ruling 1) — moving a department is not a capability of its own, it is
`UserManage`. It sits at company scope rather than project scope because the route names a *user*,
not a project: `ProjectScope.FromRoute()` would find nothing to check and refuse every caller
including the Owner. The handler refuses nothing at the permission level itself — everything past the
gate is a domain refusal from `User.MoveToDepartment`, which calls the same `ValidateDepartment` that
`User.Create` calls, so a department rule has exactly one place to be wrong in
[Verified: 2026-08-25 @ `src/Domain/Identity/User.cs` -> `MoveToDepartment`].

**This endpoint shipped with no permission gate at all.** `.RequirePermission(Permission.UserManage)`
was absent from the `Map` chain, so any authenticated caller — any role — could move any user between
departments, and department is one of the two axes a permission is granted against (§9), so an
ungated move route is a privilege-escalation primitive, not a missing check. **The endpoint's own XML
doc claimed otherwise while the line was not there** — *"the permission check is the
`RequirePermission` line below and nowhere else,"* sitting above a chain that enforced none of it.
D-067 records the discovery (a red test, not a review) and the fix. This entry exists so the story's
own build history — shipped with no gate, caught by `Nobody_but_the_owner_can_move_a_user_between_departments`
going red on 403-expected/204-received, fixed same day — is written down once, in the story's own
build record, rather than only inside a defect entry a future reader might not think to open.

**No `Response.cs` and no `Validator.cs` in this slice, and that is correct, not an oversight.** The
move returns **204 No Content**
[Verified: 2026-08-25 @ `src/Api/Features/Users/MoveUserDepartment/Handler.cs` -> `HandleAsync`] — S-008
re-reads the user it is showing, and the authority the move actually changes is never in a response
body, it is re-read from the database on the moved user's next request (D-048). There is nothing to
shape a `Response` around. And the request's only rule — Operations needs a sub-department, nobody
else may carry one, HR cannot leave HR — is the domain's `ValidateDepartment`, called once, from
`MoveToDepartment`; a `Validator.cs` here would be a second place for a rule that must have exactly
one. **A future session should not "restore" either file.** CLAUDE.md's five-file list is what a
slice needs when it needs all five, not a fixed shape every slice must fill in.

#### 2. KAFF-110 — `POST /api/users/{userId}/deactivate`, and why KAFF-111 has no handler of its own

**Same permission shape as KAFF-108** — `Permission.UserManage`, `CompanyWide`, Owner alone
[Verified: 2026-08-25 @ `src/Api/Features/Users/DeactivateUser/Endpoint.cs` -> `Map`] — and it refuses
HR here even though HR holds `ProjectAssignmentManage`: ending somebody's access company-wide is not
staffing a project, and the two permissions do not overlap by construction. Same file shape too: no
`Response.cs` (204; the act has no result to report and the change is only observable on the next
request the way KAFF-108's is) and no `Validator.cs` (the one optional field, `Reason`, has no rule
today — Q35 is open, and the endpoint's own remarks say where a `Validator.cs` would go if Karim
answers yes: `src/Api/Features/Users/DeactivateUser/Request.cs` -> `Reason`). Not repeating KAFF-108's
full paragraph on this; the reasoning is identical.

**KAFF-111 (revoke every active assignment on deactivation) is built inside KAFF-110's handler and has
no endpoint or handler folder of its own.** The two are one act by rule: *"one request, one correlation
id"* (KAFF-110 rule 7), so the deactivation and every revocation it causes are one `SaveChangesAsync`
[Verified: 2026-08-25 @ `src/Api/Features/Users/DeactivateUser/Handler.cs` -> `HandleAsync`] — one
transaction, so there is no state where a user is switched off but a stale assignment still reads
them as on the team. The revocation is handler work rather than entity work: `User` cannot reach its
own `ProjectAssignment` rows, and giving it a query to do that would put persistence access inside an
entity to satisfy one rule. `ProjectAssignment.Revoke` still does the actual stamping and is called
once per active row; the handler's job is only finding the rows and calling it in a loop, discarding a
`Result` that cannot fail here because every row in the loop is already known to be active. A reader
looking for "the KAFF-111 endpoint" will not find one — it does not exist and should not be built;
KAFF-111's acceptance criteria are exercised through KAFF-110's handler, and that is where its tests
live.

#### 3. KAFF-113 — `POST /api/projects/{projectId}/assignments`, and the race the unique index alone would 500 on

**The permission is `Permission.ProjectAssignmentManage`, `ProjectScoped`, Owner and Hr**
[Verified: 2026-08-25 @ `src/Api/Features/Assignments/AssignUserToProject/Endpoint.cs` -> `Map`],
staying project-scoped even though HR reaches every project with no assignment row of its own — reach
and capability are answered by two different mechanisms on purpose. The scope makes the route require
a real project (`AC-113-C`); HR's global reach is `IProjectAccessPolicy`'s `GlobalReachAsync` branch,
which is itself bounded by the project existing
[Verified: 2026-08-25 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` ->
`GlobalReachAsync`]. Widening the catalogue row to `CompanyWide` to "simplify" HR's reach would delete
the requirement that a real project be named, which is exactly the criterion the scope exists to hold.
**Reach is not capability, either**: HR's global reach admits it to this endpoint and to nothing
financial, because HR is deliberately absent from `Permission.ProjectRead` (D-044 ruling 2) — the same
call that staffs a project cannot open it.

**Past the gate, the handler refuses at three levels, in order, and the order is load-bearing.** (1)
`UserNotFound` if the named user does not exist. (2) `UserIsInactive` if the user exists but is
deactivated — checked in the handler rather than in `ProjectAssignment.Create`, deliberately: the
assignment row is not what makes a leaver safe, the subject read is (D-048), and this refusal is about
the request making a false statement rather than about the entity being invalid. (3) Everything about
*who* may be assigned and *at what level* — external roles, seniority legal only for site engineers —
goes through `ProjectAssignment.Create` and nowhere else
[Verified: 2026-08-25 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Create`], so the handler passes
the request's level through untouched rather than "helpfully" coercing it, the same mutation D-066 §2
recorded on the create-user path.

**The duplicate-assignment refusal is enforced twice, deliberately, and the two do not agree by
coincidence.** The handler pre-checks for an existing active row and returns
`UserAlreadyAssignedToProject` as a friendly 409 before ever touching `SaveChangesAsync`
[Verified: 2026-08-25 @ `src/Api/Features/Assignments/AssignUserToProject/Handler.cs` ->
`HandleAsync`]. **That check is not the enforcement** — two concurrent requests can both pass it — so
the unique index `ux_project_assignments_active` is the real rule, and the handler catches the
resulting `DbUpdateException`, inspects it for `PostgresErrorCodes.UniqueViolation` on that exact
constraint name, and maps the loser of the race to the identical `UserAlreadyAssignedToProject` 409
rather than letting a constraint violation surface as a 500
[Verified: 2026-08-25 @ `src/Api/Features/Assignments/AssignUserToProject/Handler.cs` ->
`IsDuplicateActiveAssignment`]. Matching on the constraint name rather than only the SQL state is
deliberate: a different unique violation on the same table must not be swallowed by this catch and
reported as "already assigned" when it is not.

**No `Validator.cs` here either, and unlike the other two this slice does have a `Response.cs`** — the
created row, because `POST .../assignments` returns **201 Created** with the assignment's id, level
and who assigned it (S-010 needs to show it after the sheet closes)
[Verified: 2026-08-25 @ `src/Api/Features/Assignments/AssignUserToProject/Response.cs`]. The request
carries exactly two fields and every rule about either of them already lives in the domain, so there
is nothing left for a validator to check.

#### What this entry does not do

**It records, it does not decide.** Nothing above changes a permission, adds a catalogue row, or
answers an open question — Q35 and Q51 stay open exactly as the stories leave them. The i18n claim in
the Verifier's report is addressed separately: every `MessageKey` these three slices can emit —
`errors.identity.user_not_found`, `.user_is_inactive`, `.user_already_assigned_to_project`,
`.client_is_not_assignable`, `.assignment_level_not_applicable`, and the gate's own
`errors.auth.forbidden` / `.not_authenticated` / `.not_assigned_to_project` — was already present in
both catalogues with real Arabic before this entry was written
[Verified: 2026-08-25 @ `src/Web/public/locales/en.json`, `src/Web/public/locales/ar.json`], and
`TranslationCatalogueTests` is green in the 75/75 Domain run this session produced. The keys the
verification report flagged as absent — `assignments.action.assign`, `assignments.field.*`,
`enum.AssignmentLevel.*`, `users.action.deactivate`, `users.confirm.deactivate.*` and the rest of the
KAFF-110/KAFF-113 UI bullets — are Angular screen labels with no screen built yet, not `Error`
`MessageKey`s, and `TranslationCatalogueTests` cannot see them because it enumerates `*Errors` static
classes, not arbitrary catalogue keys. They remain Frontend's, with the screens, exactly as the
Verifier routed them.

---

### D-075 · The audit trail takes the actor the gate verified, and the database now says an actor is named completely · 2026-08-25

**Architect. Closes D-073.** The entry the code was already citing — as **D-074**, which is Backend's
build record for KAFF-108/110/113 and says nothing about actors. Thirteen comments across seven files
pointed at it; every one now points here. **F-28's shape, third instance this sprint**, and it
happened the ordinary way: the entry was written into the comments before it was written into the
file, and the session ended in between.

---

#### 1. Why the trail takes the verified actor and not the claim

D-048 stopped the **gate** trusting the token, because claims go stale: a deactivated Owner kept
`UserManage` until his token expired. The **trail** went on believing the same token (the defect, as
raised, is D-073). So the permission system distrusted the token and the audit trail believed it,
about the same user, on the same request.

**Decision: the gate hands the trail the row it just read.** On a grant,
`PermissionAuthorizationHandler` calls `IAuditContext.ActorVerifiedAs` with the `PermissionSubject`
it loaded from the users table, and `AuditSaveChangesInterceptor` prefers that over anything the
token says [Verified: 2026-08-25 @ `PermissionAuthorizationHandler.cs` -> `ActorVerifiedAs`] and
[Verified: 2026-08-25 @ `AuditSaveChangesInterceptor.cs` -> `ResolveActor`].

**Why this and not the alternatives D-073 listed.**

* **Record both the claimed and the effective role.** Two columns, one of which is a value the system
  has already decided not to act on. A reader of an append-only table would have to know which one
  the gate honoured, forever. The trail's job is to say what happened, not to preserve what the token
  guessed.
* **Rotate the security stamp on a role or department change.** It contradicts KAFF-108's documented
  behaviour — *"no claim is re-issued, no token is minted, no cache is invalidated"* — which was a
  deliberate decision, not an oversight. And it fixes attribution by making stale tokens impossible
  rather than by making attribution correct, which leaves the trail still reading a source it should
  not read.
* **A second read of the users table in the interceptor.** An extra query per audited write on a path
  that has already read that exact row. The actor's name rides along on `PermissionSubject` for
  precisely this reason [Verified: 2026-08-25 @ `PermissionSubjectReader.cs` -> `FullName`]; the
  evaluator never reads it.

**There is deliberately no fallback to the claim** when no gate ran. An authenticated request that
reaches a save without passing the gate is the D-067 shape that `EndpointPermissionCoverageTests`
makes a build failure, and attributing that save from an unverified claim would reintroduce the
defect. What is left is a named actor with no role — §3 is what happens to it.

**`ActorDisplayName` is fixed by the same act**, which D-073's closing line asked for: the name comes
from the same row as the role, so a renamed user's later acts are attributed to the name the database
holds. `TestAuthHandler` puts a synthetic name in the name claim and never the user's real name, so
the assertion on the display name can only pass if the value came from the database
[Verified: 2026-08-25 @ `MoveUserDepartmentTests.cs` ->
`The_trail_records_the_role_the_database_holds_not_the_role_the_token_claims`].

---

#### 2. Why there are two actor channels and not one

`IAuditContext` carries two, and they are not redundant.

| Channel | Who calls it | Why it cannot be the other one |
|---|---|---|
| `ActorVerifiedAs` | the authorization gate, on a grant, and nothing else | States what the gate **already decided**. It is a fact the request carries, so it survives `Clear()` — who the caller is is not a property of one save within the request. Same arrangement as `GrantedThrough` (KAFF-116 rule 6) |
| `AttributeTo` | bootstrap (KAFF-100), and nothing else | The request has **no identity to verify** — the endpoint is anonymous by definition and the Owner is created by the very transaction being audited. There is no gate, so there is nothing for `ActorVerifiedAs` to report |

**They are held apart by a refusal, not by convention.** `AttributeTo` on a request that already
carries an identity throws: an authenticated caller naming a different actor is impersonation written
into a table with no correction path [Verified: 2026-08-25 @ `AuditSaveChangesInterceptor.cs` ->
`ResolveActor`]. Collapsing the two into one setter would delete that distinction — the setter could
no longer tell "the gate verified this" from "the handler asserted this".

---

#### 3. When there is genuinely no actor, and the constraint that had been described but not built

**The one legitimate unnamed actor is work outside a request** — migrations, seeding, scheduled jobs.
`SystemCurrentUser` reports no user id and no role [Verified: 2026-08-25 @ `AuditContext.cs` ->
`SystemCurrentUser`], and those rows name nobody, honestly, in both columns.

**Everything else names a user, and a user without the role they acted under is a permanently
unattributed row.** D-073 routed this as the reachable half needing no role change: `ActorRole` was
nullable, was not `IsRequired()`, and carried no check constraint.

**The claim was in the comments and the constraint was not.** `AuditContext.cs` and `IAuditContext.cs`
both named `ck_audit_records_actor_is_named_completely`, and `AuditContext.cs` said *"the constraint
is still the authority."* It existed in no EF configuration and no migration. **D-067's shape — prose
describing a guard that is not there — inside the audit mechanism, on an append-only table.**

**Decision: build it.** `(actor_user_id IS NULL) = (actor_role IS NULL)`
[Verified: 2026-08-25 @ `AuditConfiguration.cs` -> `ck_audit_records_actor_is_named_completely`],
migrated as `AuditActorIsNamedCompletely` [Verified: 2026-08-25 @
`20260825102403_AuditActorIsNamedCompletely.cs` -> `AddCheckConstraint`].

**Three things about its shape.**

* **A check constraint rather than `IsRequired()` on the role.** The column must stay nullable, or
  the system actor — the one row shape that is genuinely roleless — becomes illegal. The rule is a
  pairing, and only a constraint can say a pairing.
* **A biconditional rather than one direction.** The documented rule is that the two are *null
  together*, so the constraint says that, not half of it. The mirror — a role over nobody — is not
  reachable through any code path today, which is exactly the argument the grant-path constraint
  already rejected: the interceptor is one writer today and the table outlives it.
* **No entry in a guard list, because there is no list to enter.** D-064 made
  `FindMissingGuardsAsync` read the check-constraint names **from the design-time EF model**, so
  declaring it with `HasCheckConstraint` is what puts it in the start-up guard check
  [Verified: 2026-08-25 @ `DatabaseInitializer.cs` -> `FindMissingGuardsAsync`]. The brief for this
  work asked for a third step here; D-064 had already removed it.

**Why not simply delete the claim and rely on the application guard.** `AuditContext.FullyNamed`
refuses a half-named actor at both channels [Verified: 2026-08-25 @ `AuditContext.cs` ->
`FullyNamed`], and that is genuinely enforcement — but **only of actors that pass through a channel.**
`ResolveActor`'s fallback constructs a user id, a display name and a null role **directly**, reaching
neither channel, so no application guard has ever seen it. That is not a theoretical bypass; §5 is the
measurement of it.

---

#### 4. The forward consequence, named because it lands on an unbuilt story

**An authenticated endpoint with no permission requirement can no longer write an audit record.** Its
save resolves a named actor with no role and the database refuses it. The candidate is **KAFF-102
sign-out**: authenticated, and its whole mechanism is clearing a cookie, so there is no obvious
permission to require.

This does not create a silent trap — `EndpointPermissionCoverageTests` already goes red the day such
a route is mapped, and the allow-list is a decision rather than a formality
[Verified: 2026-08-25 @ `EndpointPermissionCoverageTests.cs` -> `AllowList`]. It makes the same
requirement twice, once at build time and once at the save. **The requirement is the point:** a
sign-out that records who signed out must know who they are from the database, and an endpoint that
never reads that row cannot say it.

---

#### 5. Watched to fail, twice

**The constraint removed from the model.** With `ck_audit_records_actor_is_named_completely` deleted
from `AuditConfiguration` and the suite rebuilt, `An_actor_is_named_completely_or_not_at_all` failed
on its **first** assertion — *"Expected caught not to be null because the database must refuse this
operation"*. The save **committed**: an authenticated request that reached the interceptor without a
gate wrote `actor_user_id` set and `actor_role` null into an append-only table, and returned success.
**`FullyNamed` was present and unchanged throughout that run.** That is the measurement of §3's last
paragraph — the application guard alone does not catch what this constraint catches, because the
defective actor never passes through it [Verified: 2026-08-25 @ `AuditMechanismTests.cs` ->
`An_actor_is_named_completely_or_not_at_all`]. Restored; 109/109.

**The constraint dropped from the live database**, the way D-064 did with
`ck_postings_amount_positive`. `/api/health` went from `200 healthy … missingGuards: []` to
`503 degraded … missingGuards: ["ck_audit_records_actor_is_named_completely"]`, and a Staging
start-up refused with *"Refusing to start: database guards are missing —
ck_audit_records_actor_is_named_completely."* (exit 82). Restored, and health returned to
`200 healthy … missingGuards: []`.

**The migration was applied against the live `kaff` database before the drop, over its 14 existing
audit rows, and passed.** That is worth recording because it is the one way this migration can fail:
`ALTER TABLE ADD CONSTRAINT` validates existing rows, and `audit_records` is append-only **and**
no-truncate by trigger — a database holding one half-named row could neither pass the migration nor
delete the row. The remedy, if a deployment ever hits it, is to add the constraint `NOT VALID` so it
binds new rows only; nothing needs it today, and it is not written into the migration on speculation.
CI is unaffected: the e2e database is created fresh per run and the test fixture builds from the
model.

---

#### 6. D-073's disposition: **CLOSED**, not deferred to KAFF-109

D-073 stayed open on the reasoning that the divergence becomes reachable when KAFF-109 adds a role
mutator — the role is assigned once in the constructor with no mutator anywhere
[Verified: 2026-08-25 @ `User.cs` -> `Role`]. **That reasoning applied to the symptom. It does not
survive the fix.**

The trail no longer *reads* the claimed role at all. A role mutator changes the database row; the
gate reads the database row; the trail records what the gate read. There is no path by which KAFF-109
reintroduces the divergence, so there is nothing for a deferred entry to wait for. The stamp-rotation
question D-073 raised as its third option is moot for the same reason — rotation was a way to stop
stale claims existing, and attribution no longer consults them.

Its separately routed half — the nullable, unconstrained `ActorRole` — is §3. Both halves are closed
here, which is why this entry closes D-073 rather than partially answering it.

**What is not claimed.** This was never an authorization hole and is not one now; D-048 and D-069
were always holding. It was forensic accuracy in the one table whose entire purpose is to be believed
later.

---

**Revisit if.** An endpoint legitimately needs to write an audit record without a permission gate. The
answer is not to relax the constraint — it is to give that endpoint a verified read of its own
caller, or to name it in the allow-list with the reason, which is where the decision belongs.


---

### D-076 · Staging is real — an ARM64 VPS, and the four defects that had been waiting for it · 2026-08-25

**Answers D-023, open since slice 0.** Nabil: staging is an **Oracle Cloud ARM64 VPS**, reached over
SSH, running the images `deploy-staging.yml` publishes. Operational steps are in `deploy/README.md`;
this entry is the reasoning and the record.

#### What was built

`deploy/docker-compose.staging.yml`, **in the repository rather than on the box**, so what staging
runs is reviewable and a rollback is `git revert` plus a re-run. The deploy job scps it to the host
and runs `docker compose pull && up -d`.

Two choices worth defending:

* **Images are pinned to the commit SHA, not the `staging` tag.** Both are pushed. Deploying the
  mutable tag makes *"what is on staging?"* unanswerable the moment a second build lands.
* **CI never reads or writes the host's `.env`.** `JWT_SIGNING_KEY` and `POSTGRES_PASSWORD` live
  beside the compose file on the host. A secret that passes through a workflow can be printed by any
  step somebody adds later. The compose file uses `${JWT_SIGNING_KEY:?...}` so a missing secret fails
  loudly rather than starting on a placeholder — the same reason `appsettings.json` ships an empty
  key.

#### The four defects, and why they matter more than the deploy job

D-023 predicted *"a handful of lines once the target is known."* The deploy job **was** four steps.
What it was not was the work. **Every one of these had been sitting in a file that nothing had ever
executed:**

1. **The web image served 404 for the entire application.** `COPY --from=build /source/dist/kaff-web`
   into nginx's root, but Angular emits `dist/kaff-web/**browser**/index.html`, so `index.html` sat
   one directory below `root`.
2. **The API image did not build.** `adduser` exits 127 — the .NET 10 runtime image is Azure Linux,
   not Debian. The line was also unnecessary: the base image already ships `app` at uid 1654 as
   `$APP_UID`.
3. **No `.dockerignore` existed**, and that was a correctness bug rather than a slow build:
   `COPY src/ src/` copied the host's `bin/` and `obj/` over the restore output, and the publish
   failed with `NETSDK1064` naming a NuGet package, which reads like a restore problem and is not one.
   Context also dropped from 522 MB.
4. **`IMAGE_PREFIX: ${{ github.repository }}`** expands to `AhmedNabil30/ERP`, and a Docker reference
   must be lowercase — **the client refuses it before the registry is contacted**, so that form could
   never have pushed.

**And the same shape appeared in CI on the same day**: the e2e job's artifact path had the identical
`browser` mistake, and `ci/serve-e2e.mjs` mounted its proxy with `app.use('/api', …)`, which strips
the mount path, so `/api/health` reached the API as `/health` — answered **401, not 404**, because
authorization runs before routing resolves.

**The theme, stated plainly because it will recur:** *a path that only one pipeline exercises, and
that pipeline has never run.* D-054 recorded it about a test-harness default. This is the same fact
at the scale of two container images, a proxy, and an artifact — **six defects, one cause, all found
within hours of the first run.**

#### ARM64: cross-compiled, not emulated

The host rejected the first successful pull with `no matching manifest for linux/arm64/v8`. buildx
defaults to the runner's platform and the runners are amd64.

The reflex fix is `docker/setup-qemu-action` plus a `platforms:` list. It works, and it makes every
push minutes slower — a .NET restore and publish under emulation is not cheap. **Both Dockerfiles
instead pin their build stage to `$BUILDPLATFORM` and target `$TARGETARCH`:** the API restores *and*
publishes with `-a $TARGETARCH`, and the web build stage runs on the runner because Angular's output
is JavaScript and identical whatever emits it. **No QEMU is configured and none is needed** — the
runtime stages only `COPY`, so nothing built for the target executes during the build.

**The restore needs the architecture too.** Without `-a` on the restore, publish restores again and
`--no-restore` fails on assets resolved for the wrong RID.

**`linux/amd64` stays in the platform list.** These images get built and run on a developer machine to
check them, which is how defects 1 and 2 above were both caught before either reached a host.

#### Verified

`docker buildx build --platform linux/arm64` produces `linux/arm64` for both images, checked with
`docker image inspect`. On the host: all three containers up, nginx answering 200, and
`curl http://localhost/api/health` returning
`{"status":"healthy","databaseReachable":true,"guardsInstalled":true,"missingGuards":[]}`.

**`guardsInstalled: true` is the part that matters.** D-033 refuses to start the application when the
PostgreSQL guards are absent, so a staging box that answers this is one where append-only postings and
the non-negative balance rule are actually enforced — not merely a container that stayed up.

#### 🟡 Still open

**The pipeline cannot yet observe staging.** The smoke check curls `STAGING_URL/api/health` from
GitHub's runners and fails; the application is healthy when curled on the box. Oracle Cloud needs
**two** firewalls opened and the second is easy to miss — the VCN security list, *and* the instance's
own iptables REJECT rule, which blocks inbound regardless of the security list. Steps in
`deploy/README.md`.

**So the Definition of Done's *"runs on staging"* is met in substance and should not be ticked yet.**
Those are different claims: the application runs on staging, and the pipeline proves it. The smoke
check exists so the second is true rather than remembered, and holding the tick until it passes is
the whole point of having it.

**Not built, and named so nobody assumes otherwise:** no TLS, no backups of the staging database, no
log shipping, no restart policy beyond `unless-stopped`, and no second environment. Staging is one
box. None of that is required by anything yet, and each is a decision rather than an oversight.

---

### D-077 · D-063 §2 and §3 built — the IP column and the nullable subject · 2026-08-25

**Backend. D-063 is the decision; this is the build entry for it, closing W-7
[qa/slice-1/verification-2026-08-25.md] for these two mechanisms the way D-066 and D-070 did for
KAFF-106 and KAFF-116.** Nothing here is a new ruling — every choice below is D-063 §2 or §3, read and
transcribed.

#### What was built

**§2 — the IP column.**

* `AuditRecord.IpAddress`, `System.Net.IPAddress?`, mapped with no converter and no `HasColumnType`
  call. Npgsql's provider maps the CLR type to PostgreSQL `inet` on its own — confirmed in the
  generated migration, which emits `type: "inet"`
  [Verified: 2026-08-25 @ `20260825172127_AuditIpAddressAndNullableSubject.cs` -> `Up`].
* Populated by **the same call that already populates `RequestPath`**, not a second one:
  `IAuditContext.BindToRequest` gained a third parameter,
  `AuditCorrelationMiddleware.InvokeAsync` passes `context.Connection.RemoteIpAddress`
  [Verified: 2026-08-25 @ `AuditCorrelationMiddleware.cs` -> `InvokeAsync`], and both
  `AuditRecord.For` and `AuditRecord.ForEvent` carry it through to the row. **Never
  `X-Forwarded-For`** — nothing in this change reads request headers at all.
* Null wherever there is no request. The three existing "outside a request" tests in
  `AuditMechanismTests` never bind an `AuditContext`, so they exercise this for free; one now asserts
  it explicitly [Verified: 2026-08-25 @ `AuditMechanismTests.cs` ->
  `An_event_that_changes_no_entity_still_writes_a_record`].

**§3 — the subject that does not exist.**

* `AuditRecord.EntityId` is now `Guid?`; `AuditEvent.SubjectId` is now `Guid?`. `EntityType` stayed
  required — nothing touched it.
* **New check constraint**, `ck_audit_records_entity_change_has_subject` —
  `action = 'Occurred' OR entity_id IS NOT NULL`
  [Verified: 2026-08-25 @ `AuditConfiguration.cs` -> `ck_audit_records_entity_change_has_subject`].
  Shape-level, not vocabulary-level, exactly as §3 specified — it names `action`, not any
  `AuditEventKind` member.
* **`IAuditContext.Record<TSubject>` now takes `Guid? subjectId`.** This is the one place the brief
  asked to be checked before being touched, and it was
  [Verified: 2026-08-25 @ `AuditContext.cs` -> `Record`]: the guard read *"An audited event must name
  its subject"* and threw on `Guid.Empty` before this session touched the file. **It still does** —
  the comparison `subjectId == Guid.Empty` is unchanged, and a lifted `Guid?` compared against
  `Guid.Empty` is false for `null`, so an explicit null now passes where it used to be inexpressible
  and `Guid.Empty` still throws. Both directions are asserted:
  `An_event_may_declare_no_subject` and `Recording_an_event_with_Guid_Empty_as_the_subject_still_throws`
  [Verified: 2026-08-25 @ `AuditMechanismTests.cs` -> `Recording_an_event_with_Guid_Empty_as_the_subject_still_throws`].
* **No `AuditEventKind` member was added.** Nothing in this session names an event; the vocabulary
  stays KAFF-101a's, exactly as §3's closing paragraph said it should.

**One migration for both halves**, generated rather than hand-written —
`20260825172127_AuditIpAddressAndNullableSubject`, following `20260822210402_AuditGrantPath`'s shape
(nullable column plus check constraint) as instructed. `KaffDbContextModelSnapshot` was regenerated by
the same `dotnet ef migrations add` run, not edited by hand.

#### A-01 — checked, not re-fixed

The brief asked whether A-01 (D-063: no check constraint verified at start-up) was closed before
adding anything to a guard list. **It is** — D-064, the same day, made `FindMissingGuardsAsync` read
every check constraint from the EF design-time model rather than a hand-written array
[Verified: 2026-08-25 @ `DatabaseInitializer.cs` -> `FindMissingGuardsAsync`]. `HasCheckConstraint` on
`ck_audit_records_entity_change_has_subject` is therefore picked up with no further change, and it was
— confirmed on a real run: `docker start kaff-db`, the Development API applied the new migration on
boot, and `driver.mjs smoke` reported `database guards installed — []`, i.e. nothing missing,
including the new constraint. No line was added to any guard list, because D-064 removed the list.

#### Tests

Added to `AuditMechanismTests.cs`: `An_event_may_declare_no_subject`,
`Recording_an_event_with_Guid_Empty_as_the_subject_still_throws`,
`An_entity_change_with_no_subject_is_refused_by_the_database` (hits PostgreSQL directly, same shape as
`Only_an_Occurred_record_carries_an_event_type`), and an added assertion on the existing
"outside a request" test for a null `IpAddress`. Added to `PermissionMechanismTests.cs`:
`A_write_through_a_real_request_records_the_connections_address` — the one test in the suite that
goes through a real HTTP request rather than constructing an `AuditContext` by hand.

**One thing this needed that the brief didn't anticipate.** `WebApplicationFactory`'s `TestServer`
never populates `HttpContext.Connection.RemoteIpAddress` — verified empirically: the test failed with
a null IP before anything was added to compensate — so `KaffApiFactory` now registers a small
`IStartupFilter` that sets a fixed, publicly-exposed `TestRemoteAddress` on the connection ahead of
`AuditCorrelationMiddleware`, only when nothing else has set one already
[Verified: 2026-08-25 @ `KaffApiFactory.cs` -> `FakeConnectionStartupFilter`]. This is test
infrastructure standing in for a real socket, not a change to how the middleware decides an address —
the middleware still reads only `Connection.RemoteIpAddress`, exactly as `AuditCorrelationMiddleware.cs`
shows.

#### Verified

Build: 0 warnings / 0 errors. `dotnet format KaffErp.sln --verify-no-changes` exit 0. Domain **75/75**,
Api **113/113** — four new: three in `AuditMechanismTests.cs`, one in `PermissionMechanismTests.cs`.
`scripts/check-citations.ps1`, run with this entry in place: **639 checked, 0 broken, 0 legacy,
exit 0** — up from the 631 baseline. `/run-kaff-erp` smoke: all seven checks passed, including
`guardsInstalled: []`.

#### For Nabil — the premise that moved, not acted on

D-063 §2 gave three reasons to read only `Connection.RemoteIpAddress` and never `X-Forwarded-For`; the
third was that trusting a forwarded header needs `ForwardedHeadersOptions.KnownProxies`, *"a
deployment fact this project does not have — D-023, the staging target, is still open."* **D-023 is
answered now, by D-076: staging is a real ARM64 VPS reached over SSH, behind whatever reverse proxy
that deployment does or does not run.** Whether a `KnownProxies` allowlist is now derivable, and
whether Kaff's staging box even sits behind something that rewrites the header, is the Architect's
question — this session built the connection address exactly as D-063 decided, unconditionally, and
is only flagging that the premise behind one of its three reasons has changed. Not touched here.

#### Also open, unchanged by this session

**Q54** — indefinite retention of a personal-data IP address in a table with no delete path — is still
with Nabil and Karim, per D-063. This session wrote no new IP-bearing rows outside the test suite and
one interactive smoke run against the local `kaff` database, and changes nothing about Q54's answer.

#### Not done, and named so nobody assumes otherwise

No `AuditEventKind` member. No handler calls `IAuditContext.Record` with a null subject — nothing
exists yet that would. No retention or partitioning mechanism for `ip_address` (Q54). No change to
`src/Web/`. No story was built; KAFF-101a still owns the vocabulary this mechanism now has room for.

---

### D-078 · KAFF-114 built — revoking an assignment, and a stale claim the story made about its own 403 · 2026-08-25

**Backend.** `POST /api/projects/{projectId}/assignments/{assignmentId}/revoke`. Almost everything the
story needed already existed in slice 0 — `ProjectAssignment.Revoke`, `IdentityErrors
.AssignmentAlreadyRevoked`, `ProjectAccessPolicy.AssignedAccessAsync`'s per-request `RevokedAt == null`
read — so this entry is the endpoint, the handler, one new `IdentityErrors` member, and the tests.

#### What was built

* **The permission is `Permission.ProjectAssignmentManage`, `ProjectScoped`, `Owner` and `Hr`** — the
  same row `AssignUserToProject` uses, not a new one
  [Verified: 2026-08-25 @ `PermissionCatalogue.cs` -> the `Permission.ProjectAssignmentManage` row].
  The story's permissions bullet names `ProjectScoped`, which is the scope, not the permission; the
  permission that actually appears in the catalogue is `ProjectAssignmentManage`, matched against
  `AssignUserToProject/Endpoint.cs` before writing this one
  [Verified: 2026-08-25 @ `src/Api/Features/Assignments/AssignUserToProject/Endpoint.cs` -> `Map`].
  SM-30 asked whether this change adds a catalogue row: it does not, and this is what checking looked
  like rather than assuming it.
* **The route names both the project (for the scope) and the assignment (the row being closed)**:
  `/api/projects/{projectId:guid}/assignments/{assignmentId:guid}/revoke`
  [Verified: 2026-08-25 @ `src/Api/Features/Assignments/RevokeProjectAssignment/Endpoint.cs` -> `Route`].
  A `POST .../revoke` rather than a `DELETE`, matching `DeactivateUser`'s `POST .../deactivate` shape
  — the row is never removed, only stamped, and a `DELETE` verb on a route that soft-closes a row
  would be the wrong claim for a client to read off the URL.
* **No `Response.cs` and no `Validator.cs`**, for the reason D-074 §1 already gave `AssignUserToProject`
  and `MoveUserDepartment`: 204 has nothing to shape a response around, and the request's only rule —
  refuse a second revocation — lives in `ProjectAssignment.Revoke` and nowhere else
  [Verified: 2026-08-25 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Revoke`]. A `Validator.cs`
  here would be a second place for a rule that must have exactly one.
* **One new `IdentityErrors` member, `ProjectAssignmentNotFound`**, for a route naming an assignment id
  that does not exist on that project — matched on `Id` and `ProjectId` together, whether or not the
  row is already revoked, so an already-revoked row is still found and gets
  `AssignmentAlreadyRevoked` from `Revoke` rather than a false "not found"
  [Verified: 2026-08-25 @ `src/Api/Features/Assignments/RevokeProjectAssignment/Handler.cs` ->
  `HandleAsync`]. Not sourced to an acceptance criterion — KAFF-114's criteria assume the row exists —
  so this is REST plumbing for a route parameter, the same shape as KAFF-108's `UserNotFound`, not a
  business rule read out of a gap.
* **No audit code in the handler.** The revocation is an entity change (`RevokedAt`,
  `RevokedByUserId`), so `AuditSaveChangesInterceptor` writes the `Modified` record in the same
  transaction the handler already opens, naming both properties in `ChangedProperties` and carrying
  the `ProjectId` and whichever `GrantPath` the gate granted on — asserted directly rather than trusted
  [Verified: 2026-08-25 @ `tests/Api.Tests/RevokeProjectAssignmentTests.cs` ->
  `The_revocation_leaves_a_modified_audit_record_naming_what_changed`].

#### `TranslationCatalogueTests` required touching two lines under `src/Web/`, and that is the one
exception to "never touches `src/Web/`" this session made

`ProjectAssignmentNotFound`'s key has no entry in either locale catalogue by default, and
`Kaff.Domain.Tests.TranslationCatalogueTests.Every_domain_error_key_has_an_arabic_and_an_english
_translation` fails the Domain suite on any `*Errors` member with no translation
[Verified: 2026-08-25 @ `tests/Domain.Tests/TranslationCatalogueTests.cs` ->
`Every_domain_error_key_has_an_arabic_and_an_english_translation`] — that test's own remarks call the
two catalogues "the contract's other end," not a screen. `errors.identity.project_assignment_not_found`
was added to both `src/Web/public/locales/en.json` and `ar.json`, one line each, next to
`user_already_assigned_to_project`. **Nothing else under `src/Web/` was touched.** The
`assignments.action.revoke` / `assignments.confirm.revoke.*` / `a11y.revoke_assignment` /
`assignments.revoked_on` family the story's i18n bullet names is Frontend's and there is no screen yet
— confirmed absent from both catalogues, not added.

#### A stale claim in the story, found by running the test rather than trusting the text (SM-31)

**AC-114-A says the refusal on the next request carries `errors.auth.not_assigned_to_project`. The
shipped gate cannot produce that key, for anybody, on any endpoint, today.**
`Program.cs`'s `CustomizeProblemDetails` stamps every 401/403 with one blanket key —
`AuthorizationErrors.NotAuthenticated` / `.Forbidden` — because it is the single place that sees the
status code after the fact, not the specific `PermissionDecision` that produced it
[Verified: 2026-08-25 @ `src/Api/Program.cs` -> `AddProblemDetails`].
`PermissionAuthorizationHandler.HandleRequirementAsync` only declines to call `context.Succeed` on a
refusal — it never reaches a handler that could return a more specific `Problem`, so
`SeparationOfDuties.NotAssignedToProject` is declared, translated in both catalogues, and never
referenced by anything in `src/Api`
[Verified: 2026-08-25 @ `src/Domain/Authorization/SeparationOfDuties.cs` -> `NotAssignedToProject`] —
confirmed by a solution-wide search finding zero call sites, not assumed from the class existing.
`AssignUserToProjectTests`' own 403 assertions already expect `errors.auth.forbidden` for the identical
reason [Verified: 2026-08-25 @ `tests/Api.Tests/AssignUserToProjectTests.cs` ->
`Nobody_but_the_owner_and_hr_can_staff_a_project`]. `AC-114-A` is built and asserts the real key,
`errors.auth.forbidden`, with the discrepancy recorded in the test's own remarks
[Verified: 2026-08-25 @ `tests/Api.Tests/RevokeProjectAssignmentTests.cs` ->
`Access_ends_on_the_next_request_after_revocation`]. **Not fixed here.** Distinguishing 403 reasons at
the HTTP layer is a change to `PermissionAuthorizationHandler` and `Program.cs` — the one gate every
protected route in the application shares — and every existing 403 assertion in both test files would
need re-auditing against it. That is Architect-sized work under a 3-point story, not something to
wire quietly to make one criterion's exact string true. Flagged, not built.

#### `AC-114-E` watched to fail for the right reason before being trusted

Per the brief: `.RequirePermission(Permission.ProjectAssignmentManage, ProjectScope.FromRoute())` was
removed from `Endpoint.cs`'s `Map` chain, the solution rebuilt, and both
`Nobody_but_the_owner_and_hr_can_revoke_an_assignment` (this story) and
`EndpointPermissionCoverageTests.Every_mapped_endpoint_carries_a_permission_requirement` (the
mechanical A-04 gate) went red — the former with a 500 rather than a clean 403 mismatch, because an
unauthorized caller now reached `SaveChangesAsync` with no `GrantPath` recorded, which is the
D-067 shape exactly: a caller that should never have reached the handler, reaching it. The line was
then restored and the full suite re-run green. Not a permanent change; the endpoint always shipped
with the line — this was a check that the test can actually catch its own defect class, done once,
in this session, rather than assumed from the test's name.

#### Verified

Build: 0 warnings / 0 errors, clean `--no-incremental` Release. `dotnet format KaffErp.sln
--verify-no-changes` exit 0. Domain **75/75** (unchanged — no new Domain test; the new `IdentityErrors`
member is exercised through the Api suite). Api **121/121**, up from 113 — eight new:
seven in `RevokeProjectAssignmentTests.cs`, one in `EndpointPermissionCoverageTests.cs`
(`No_endpoint_deletes_a_project_assignment`, `AC-114-F`, enumerating the host's actually-mapped routes
for a `DELETE` verb anywhere under `.../assignments`, rather than asserting one hand-picked URL 404s).
`scripts/check-citations.ps1`: run after this entry was written, **645 checked, 0 broken, 0 legacy,
exit 0** — up from 639, this entry's own citations included; the count did not move when the `.cs`
files above were written, because the checker only walks `*.md` files for citations to verify, not
source — a fact this entry's citations style (`<c>File.cs</c>` in the C# XML doc, backtick-fenced here)
was written to match rather than assumed. `/run-kaff-erp` smoke: all seven checks passed, API and SPA
both started clean.

#### Q49 and Q51 — untouched, as instructed

Neither open question was resolved, softened or extended. Rule 7 (Q49, the last engineer may be
revoked off a project) is exercised nowhere in `RevokeProjectAssignmentTests.cs` on purpose — a test
proving "revoking the last engineer succeeds" would not be wrong, but it would be this session reading
a minimum-team-size question into a settled answer the suite then defends, and Q49 is Karim's to close,
not a test's. Rule 4 / `AC-114-D` (Q51, revoking an already-revoked assignment is refused) is asserted
exactly as built, sourced to slice-0 code as the story already says.

#### Not done, and named so nobody assumes otherwise

No change to `PermissionAuthorizationHandler`, `Program.cs`, or `SeparationOfDuties` — the
`errors.auth.not_assigned_to_project` gap above is flagged, not closed. No change to the audit
mechanism or to any migration; this story needed neither, and none was added. No screen — the
`assignments.action.revoke` family stays Frontend's, unbuilt keys named above rather than filled in.
`AssignUserToProjectTests.cs`'s own `RevokeAsync` helper comment ("the endpoint for it is KAFF-114 and
is not in this sprint") is now stale — KAFF-114 shipped this session — but that file is not this
story's to edit and the comment does not affect what the test does; noted here rather than changed
there.

---

### D-079 · The audited address is the caller's, and the trust that makes it so lives in the compose file · 2026-08-25

**Architect.** D-063 §2 decided the audit trail records `Connection.RemoteIpAddress` and never
`X-Forwarded-For`, and gave one condition under which that would change: *"Reading it becomes
legitimate only once `ForwardedHeadersOptions` is configured with an explicit `KnownProxies` /
`KnownNetworks` allowlist, which is a deployment fact this project does not have — **D-023, the
staging target, is still open**."* D-077 flagged that the premise had moved. **This entry is that
condition being met, not D-063 being reopened.** The prohibition on trusting an unattested header
stands exactly as written; what changed is that one peer is now attested.

#### The premise, re-read rather than taken from D-077

* **Staging puts nginx in front of the API, and nothing else can reach it.** The `api` service
  declares `expose: "8080"` and no `ports` mapping; the `web` service — nginx serving the Angular
  build — is the only one that publishes a host port. So every request the API sees on the only
  deployed environment arrives from the nginx container on the Compose network
  [`deploy/docker-compose.staging.yml`, the `api` and `web` services].
* **nginx already sends the header.** `proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;`
  has been in the template since it was written [`src/Web/nginx.conf.template`, the
  `location /api/` block]. Nothing was added there by this session.
* **`AuditCorrelationMiddleware` passes the connection address**
  [Verified: 2026-08-25 @ `AuditCorrelationMiddleware.cs` -> `InvokeAsync`].

**So the defect is real and total, not partial.** Every audit row written on staging — including the
failed sign-ins KAFF-101a is about to start writing, which are the rows D-063 §2 built the column
for — would carry a Docker bridge address that identifies nothing. Not a degraded value: a value
with no information in it at all, in a table with no delete path and no backfill.

#### Decision

**The source of truth for the audited address is the connection, and the connection is allowed to be
rewritten by `ForwardedHeadersMiddleware` when — and only when — the immediate peer appears in a
configured allowlist.** Three parts, and the third is the one that matters:

1. **`Kaff:TrustedProxyNetworks`**, a list of CIDR networks, empty in `appsettings.json`
   [Verified: 2026-08-25 @ `appsettings.json` -> `TrustedProxyNetworks`]. The trust decision is
   configuration, never a header, which is the constraint D-063 §2 set and this keeps.
2. **`Program.cs` registers `UseForwardedHeaders` only when that list is non-empty**, with the
   framework's loopback defaults cleared and `ForwardedHeaders.XForwardedFor` alone
   [Verified: 2026-08-25 @ `Program.cs` -> `trustedProxyNetworks`]. It is placed before
   `AuditCorrelationMiddleware` and therefore before authentication, because an anonymous failed
   sign-in is exactly the row that needs the caller's address.
3. **`AuditCorrelationMiddleware` is unchanged.** It still reads `Connection.RemoteIpAddress` and
   still reads no header. The audit mechanism has no opinion about proxies; the pipeline hands it a
   connection address that is either the peer's or, where we have said the peer is a proxy we run,
   the peer's peer. **This is what keeps D-063 §2's rule literally true in the code it was written
   about.**

**Staging's allowlist is `172.28.0.0/24`, and the compose file both creates that network and names
it as trusted, in the same file, eleven lines apart.** Compose's default address pool assigns a
subnet at `up` time, which an allowlist cannot name, so the network is pinned. Two files could not
have drifted apart quietly; one file, twice, can be reviewed in one glance.

#### What this rules out

* **Reading `X-Forwarded-For` unconditionally.** Never proposed and still refused. An unattested
  header is a caller-supplied string, and D-063 §2's reasoning about writing one into an append-only
  table is untouched.
* **Reading `X-Real-IP`.** nginx sets it too, and it is the shorter path. Rejected: it is a
  single-value convention with no framework support, so the trust check would have to be hand-written
  beside it — a second implementation of the thing the framework already does, on the security path.
* **Registering `UseForwardedHeaders` unconditionally.** ⚠️ **This is the footgun and it is not
  theoretical.** `ForwardedHeadersMiddleware` treats *no known proxies and no known networks* as
  *check nobody* — that is, trust every peer that sends the header. An empty allowlist would
  therefore mean universal trust, the exact inverse of what an empty `Kaff:AllowedOrigins` means ten
  lines above it. **Measured, not reasoned about:** registered unconditionally, the suite went red
  with the forged address recorded, `Expected object to be 203.0.113.42, but found 198.51.100.7`.
  Left in the conditional, and the conditional is now held by a test.
* **Trusting the RFC1918 private ranges, or "anything on a Docker network".** Broader than the
  deployment, and it would keep passing on the day somebody adds a `ports` mapping to the `api`
  service and puts it on the public internet.
* **Making the allowlist an application constant.** The peer differs per deployment. D-063 §2 said
  the trust decision belongs in configuration and that is where it is.
* **Trusting `ForwardLimit` to be the security control.** It is set to 1, correctly, but it is not
  what stops a forgery — see below.

#### One claim in the first draft of this change was wrong, and it was caught by measuring it

The comment shipped beside `ForwardLimit = 1` originally said that the limit is what defeats a forged
`X-Forwarded-For` entry. **It is not.** Raising it to 2 and re-running the suite left
`Behind_a_trusted_proxy_the_recorded_address_is_the_caller_not_the_proxy` green: the middleware stops
as soon as the next address it would consume is not a known proxy, and a forged entry never is. The
allowlist does the work; `ForwardLimit` is the true hop count and nothing more. Both the code comment
and the test's own remarks now say so, and say that the earlier version said the opposite — a
plausible wrong reason in a test's documentation is what the next session copies.

#### Tests

Two, both in `PermissionMechanismTests.cs`, both watched to fail for the right reason before being
trusted:

* **`A_forwarded_header_is_ignored_when_no_proxy_network_is_trusted`**
  [Verified: 2026-08-25 @ `PermissionMechanismTests.cs` ->
  `A_forwarded_header_is_ignored_when_no_proxy_network_is_trusted`] — the shipped default, with a
  header on the request. This is the guard on the footgun above, and the existing
  `A_write_through_a_real_request_records_the_connections_address` **would not have caught it**,
  because it sends no header. Verified red under unconditional registration.
* **`Behind_a_trusted_proxy_the_recorded_address_is_the_caller_not_the_proxy`**
  [Verified: 2026-08-25 @ `PermissionMechanismTests.cs` ->
  `Behind_a_trusted_proxy_the_recorded_address_is_the_caller_not_the_proxy`] — its own host, peer
  inside a trusted `/24`, and a **two-entry** header in nginx's real shape
  (`forged, real`), asserting the rightmost wins. Verified red with `UseForwardedHeaders` removed:
  `Expected object to be 198.51.100.7, but found 192.0.2.10` — which is the staging defect itself,
  reproduced.

`KaffApiFactory` gained two optional constructor parameters so the second test can have a proxied
host [Verified: 2026-08-25 @ `KaffApiFactory.cs` -> `FakeConnectionStartupFilter`]. The trusted
network is set as an environment variable **always, including to null**, because `Program.cs` reads
it before `Build()` and one factory's trust setting must not survive into the next factory built in
the same process.

#### Verified

Clean `--no-incremental` Release build: **0 warnings / 0 errors**.
`dotnet format KaffErp.sln --verify-no-changes` exit 0. Domain **75/75** (unchanged). Api
**123/123**, up from 121. `/run-kaff-erp` smoke: all seven checks passed, `guardsInstalled: []`.

#### Deadline met, and why it was one

**Before KAFF-101a writes its first row**, per D-063 §2's own N-19 reasoning: the column cannot be
backfilled into a trigger-protected append-only table, so a row written with the proxy's address is
wrong permanently. KAFF-101a is not built. No production row has been written with either value.

#### For Nabil — Q54 just became a real question rather than a theoretical one

**Q54** (indefinite retention of an IP address in a table with no delete path) has been open with
Nabil and Karim since D-063. **This entry does not answer it and does not touch it — but it does
change what it is about.** Until today the column would have recorded a Docker container's internal
address on staging, which is not personal data by any reading. From the next deploy it records a real
end user's address, which is. The retention question is the same question; it now has a subject.
Routed, not decided — it is a data-protection rule and `spec.md` does not state one.

#### Not done, and named so nobody assumes otherwise

No TLS, and this does not add any — staging is still plain HTTP (`deploy/README.md`, "What staging
does not have"). **A TLS terminator or CDN added in front of nginx later would make the nginx-appended
address that terminator's, and this allowlist would then be recording the wrong hop with no test
turning red** — the allowlist and `ForwardLimit` must both be revisited the day anything is put in
front of the box. No change to `AuditCorrelationMiddleware`, `IAuditContext`, `AuditRecord`, the
schema, or any migration; none was needed. No change to `src/Web/`. No retention or partitioning
mechanism for `ip_address` (Q54). No story was built.


---

### D-080 · The blanket 403 key satisfies D-071. Four acceptance criteria and three test cases are what is wrong · 2026-08-25

**Architect, answering the question D-078 refused to answer from inside a 3-point story.** That
refusal was right and this entry does not overturn it — it answers it.

**The question:** `Program.cs` -> `CustomizeProblemDetails` stamps every 403 with
`errors.auth.forbidden`; `AuthorizationErrors.NotAssignedToProject` is declared, translated, and
called by nothing. `AC-114-A` commands a key the shipped pipeline cannot produce. Is D-071 satisfied
by the blanket key, or is the flattening a defect?

**Answer: D-071 is satisfied, and it already said so in as many words.** *"The generic key is
deliberate for gate refusals. The gate knows why it refused — `NotAssignedToProject`,
`RoleNotGranted`, `AssignmentLevelTooLow` — and logs it. It is not returned … telling an unauthorised
caller which of the two axes they failed describes the permission model to the person who has just
been refused by it."* D-071 gave every refusal **a** key, not a specific one. The pipeline built is
the pipeline D-071 decided.

#### The premises, re-read today rather than taken from D-078

All four hold [Verified: 2026-08-25]:

* One callback stamps every 401 and 403 after the fact, with no access to the decision that produced
  it [@ `Program.cs` -> `CustomizeProblemDetails`].
* The handler only declines to call `context.Succeed`; it reaches nothing that could return a
  `Problem` of its own [@ `PermissionAuthorizationHandler.cs` -> `HandleRequirementAsync`].
* `AuthorizationErrors.NotAssignedToProject` has **zero call sites anywhere in the solution** —
  confirmed by a repository-wide search, not inferred [@ `SeparationOfDuties.cs` ->
  `NotAssignedToProject`]. The name that *is* used everywhere is the enum member
  `PermissionDecision.NotAssignedToProject` [@ `PermissionEvaluator.cs` -> `NotAssignedToProject`],
  which is a decision the gate logs, never an `Error` it returns. **Two different things with one
  name**, and D-078's brief called the `Error` "`SeparationOfDuties.NotAssignedToProject`" — that is
  the file, not the owner; the member belongs to `AuthorizationErrors`.
* Existing 403 assertions expect the blanket key [@ `AssignUserToProjectTests.cs` ->
  `Nobody_but_the_owner_and_hr_can_staff_a_project`; @ `RevokeProjectAssignmentTests.cs` ->
  `Access_ends_on_the_next_request_after_revocation`].

#### Why the Q47 reasoning does apply, even though the caller is authenticated

The brief asked whether **Q47** and **D-072 §1** — a role-specific sign-in refusal tells an attacker
the account exists — carry over to a caller who already holds a session. The obvious objection is
that they should not: this caller is known, and can read their own role and their own assignment list
from their own profile, so a specific key tells them nothing they cannot already look up.

**The objection fails, and the reason is in the evaluator's ordering rather than in an analogy.**
`PermissionDecision.NotAssignedToProject` is only reachable **after** `matching.Count == 0` has
already been ruled out [@ `PermissionEvaluator.cs` -> `Evaluate`]. So the two keys do not distinguish
two facts about the *caller*; they distinguish two facts about the **endpoint**:

| Key | What the caller learns |
|---|---|
| `errors.auth.forbidden` (from `RoleNotGranted`) | their role does not hold the permission this route requires |
| `errors.auth.not_assigned_to_project` | **their role does hold it** — the route is project-scoped and only the assignment stopped them |
| `errors.auth.assignment_level_too_low` | their role holds it, they are assigned, **and the grant carries a seniority floor** |

That is the `PermissionCatalogue` row, read off a refusal. A caller cannot look that up; it is the
security-relevant map of which permission each route requires, on which axis, at what seniority — and
it is enumerable one endpoint at a time by anyone holding any session. **The value of the specific
key and its cost are the same disclosure.** Q47's shape holds: what varies with the answer is what
leaks, and being authenticated changes who is doing the enumerating, not what is enumerated.

**Stated narrowly, because the wider claim is not true.** This is *not* an existence oracle for
projects. For every role but the Owner, `AssignedAccessAsync` matches an assignment row, so a
non-existent project and an unassigned one are indistinguishable. For the Owner, `GlobalReachAsync`
is "bounded by the project existing" [@ `ProjectAccessPolicy.cs` -> `GlobalReachAsync`], so a refusal
does imply non-existence — to the one role that can list every project anyway. Immaterial, and named
so nobody cites this entry for a leak it does not claim.

#### What the specific key would have bought, priced honestly

Not nothing. A refused user sees one Arabic string and cannot tell "ask HR to assign me" from "this
is not my job". **That is a real cost and this decision accepts it**, for two reasons: the gate
already logs the decision with its reason [@ `PermissionAuthorizationHandler.cs` ->
`HandleRequirementAsync`], so support can answer the question from the log rather than from the
screen; and the audience for the distinction is the assigned user's manager, not the refused request.

**And the build cost, so the trade is visible rather than asserted:** carrying the decision to the
response means `PermissionAuthorizationHandler` writing its reason onto `HttpContext` and the
`CustomizeProblemDetails` callback reading it — perhaps 15 lines. It is not expensive. **It is
refused on the disclosure, not the price.** D-078 was right that it is Architect-sized work under a
3-point story; it is now Architect-answered, and the answer is no.

#### Consequence: what is wrong is the story text, and it is the BA's to fix — not mine, not a test's

**Four acceptance criteria command a key the gate is decided never to send.** Every one re-read in
its own file today:

| Criterion | File | What it says |
|---|---|---|
| `AC-101a-K` | `stories/slice-1-foundation/KAFF-101a-sign-in-api.md` | "Then the request is refused with `errors.auth.not_assigned_to_project`" |
| `AC-109-E` | `stories/slice-1-foundation/KAFF-109-change-a-users-role.md` | "Then it is refused with `errors.auth.not_assigned_to_project`" |
| `AC-112-B` | `stories/slice-1-foundation/KAFF-112-reactivate-a-user.md` | "And a request against any of those three projects is refused with `errors.auth.not_assigned_to_project`" |
| `AC-114-A` | `stories/slice-1-foundation/KAFF-114-revoke-a-project-assignment.md` | "Then it is refused with `errors.auth.not_assigned_to_project`" — **already built against the real key**, discrepancy recorded in the test (D-078) |

**Three QA test cases inherit it**, in `qa/slice-1/test-cases.md`: **TC-1-013**, **TC-1-113**,
**TC-1-257** (the last is a slice-4 case, so it is a correction not a rework).

**One story bullet that is not a criterion:** KAFF-113's i18n list names
`errors.auth.not_assigned_to_project` among the keys its screen needs. The screen does not need it.

**Two UX documents assert it as server behaviour**, and one of them already hedges correctly:
`ux/slice-1-flows.md` S-016 says the key "or the more specific … **when the server sends it**", which
survives this ruling unchanged; the same file's per-screen error table and `ux/navigation.md` state it
flatly, including `errors.auth.assignment_level_too_low`, and do not.

**The recommended correction, offered to the BA rather than made here.** In every case the criterion's
real content is behavioural — *access ended*, *nothing was restored*, *the session grants nothing by
itself* — and the key was decoration that hardened into a false claim about the wire. The fix is to
assert **403 and `errors.auth.forbidden`**, which is what the gate sends and what the existing
assertions already expect. Naming a specific key in a criterion is what made a UI string into an
acceptance gate; the criteria are better without it.

#### What this rules out

* **Wiring `PermissionDecision` through to the response body.** Refused above, on disclosure.
* **A per-endpoint refusal that returns its own key.** D-071's own reasoning: a guard the endpoint
  must remember is the guard the endpoint forgets, which is D-067's shape.
* **Editing the four criteria, the three test cases or the two UX documents in this session.** They
  belong to the BA, QA and UX. `agents.md`: "Architect — never change a business rule to make the
  architecture cleaner", and an agent editing another's story to make its own ruling true is the same
  move in a smaller frame. Routed, per principle 8.
* **Deleting `AuthorizationErrors.NotAssignedToProject`, `.AssignmentLevelTooLow` and
  `.ProjectNotSpecified` as dead code.** They are unreachable **from the gate**, which is not the same
  as unreachable. A later handler that takes a project id from a request body rather than the route
  refuses it itself, as a domain `Result` — and that is a handler's refusal, which D-071's `TryAdd`
  deliberately preserves. `errors.auth.role_cannot_log_in` is the standing precedent: KAFF-101a
  records it as "not deleted; it stops being reachable from this door." Same disposition. **Nobody
  should wire one of them into the gate to satisfy a criterion** — that is the move this entry exists
  to forbid.

#### Nothing here went to Nabil, and that is deliberate

Neither half of this turns on a rule `spec.md` does not state. `spec.md` §9 says enforcement is
server-side and that permission is role × assignment; it says nothing about what a refusal tells the
person refused, which is a security-disclosure question and therefore the Architect's. **Karim is not
asked.** Q47 and D-072 §1 are cited as the reasoning Nabil has already applied to this exact shape,
not as authority delegated back to him.

#### Not done

No code changed by this entry — not `Program.cs`, not `PermissionAuthorizationHandler`, not
`SeparationOfDuties.cs`, not a test, not a locale catalogue. No story, test case or UX document was
edited. `AC-114-A` remains built and green against `errors.auth.forbidden`, with D-078's note in the
test's own remarks now upgraded from "flagged" to "answered here."

---

### D-081 · KAFF-112 built — reactivating a user, and the one path D-051 (N5) named that still did not rotate · 2026-08-25

**Backend.** `POST /api/users/{userId}/reactivate`. Same permission shape as `DeactivateUser` and
`CreateUser` — `Permission.UserManage`, `CompanyWide`, `Owner` alone
[Verified: 2026-08-25 @ `PermissionCatalogue.cs` -> the `Permission.UserManage` row], D-044 ruling 1,
KAFF-112 rule 1. No new catalogue row; SM-30 asked and the answer is the same as KAFF-108/110/114's.

#### 1. The domain fix rule 9a named, and it is one line

`User.Reactivate()` did not rotate `SecurityStamp` — `Deactivate`, `ClearPassword` and the private
`StorePasswordHash` behind both password setters all did, and `Reactivate` was "the one path that
should rotate and does not" (decisions.md D-051 N5). Fixed
[Verified: 2026-08-25 @ `src/Domain/Identity/User.cs` -> `Reactivate`]. **Deliberately independent of
whatever the handler does with the credential**: the entity's own invariant is that reactivating an
account must not leave a pre-existing token able to authenticate against it, and that must hold even
if a future reading of Q50 stops clearing the credential on reactivation at all. Proven as its own
fact rather than assumed from the handler's behaviour —
[Verified: 2026-08-25 @ `tests/Domain.Tests/UserTests.cs` ->
`Reactivate_rotates_the_security_stamp_on_its_own`] calls only `Deactivate` and `Reactivate`, touches
no credential method, and would fail on its own if the fix were reverted. `ReactivateUserTests` proves
the same fact again at the API layer under reassignment
[Verified: 2026-08-25 @ `tests/Api.Tests/ReactivateUserTests.cs` ->
`A_token_minted_before_deactivation_is_still_refused_even_after_reassignment`], which is the one that
can distinguish "refused because the token is stale" from "refused because there is no assignment"
(AC-112-B would otherwise mask AC-112-E — every project the leaver held was revoked, so a stale token
and a valid-but-unassigned one look identical unless the person is reassigned first).

#### 2. Rules 3 and 4, built under the readiness waiver, in the order the story reads them

`User.ClearPassword()` runs unconditionally in the handler — the old credential does not survive a
reactivation whether or not a new one replaces it. `User.SetTemporaryPassword` runs afterwards only
when the request carries a `TemporaryPassword`, mirroring `CreateUser.Request`'s identical field for
the identical reason: KAFF-106 rule 10 makes "no credential" a legitimate state, and nothing forces
the Owner to issue one in the same request as the reactivation
[Verified: 2026-08-25 @ `src/Api/Features/Users/ReactivateUser/Handler.cs` -> `HandleAsync`]. **This
is the story's own reading of D-049 ruling 5, not Karim's** — the ruling says only "a new password."
Built under the readiness waiver of decisions.md D-062 §1; **Q50 stays open**, exactly as the story
leaves it.

#### 3. Rule 5/6 — the handler that must not query `ProjectAssignment`, and does not

No line in `Handler.cs` reads or writes `ProjectAssignment`. This is the central rule of the story
(D-049 ruling 5: "zero project assignments — nothing is restored automatically") and the safest way
to build it was to give the handler no path to the table at all, rather than a loop that runs zero
times by construction the way `DeactivateUser`'s revocation loop does. `AC-112-C` asserts the three
rows KAFF-111 revoked are bit-for-bit unchanged — same `RevokedAt`, same `RevokedByUserId` — after the
reactivation, not merely still present
[Verified: 2026-08-25 @ `tests/Api.Tests/ReactivateUserTests.cs` ->
`Reactivation_restores_no_assignment_and_leaves_the_revoked_rows_exactly_as_they_were`].

#### 4. D-080 applied, not re-litigated

`AC-112-B`'s messageKey was corrected to `errors.auth.forbidden` in the story and in
`qa/slice-1/test-cases.md` by the BA/QA session that ran concurrently with this one (commit
`5a2c282`, visible in `git log` before this entry was written) — D-080 had already ruled the blanket
key correct for this exact criterion by name. `ReactivateUserTests` was written against the same key
independently and agrees with the corrected story rather than the one this session's brief quoted.
`AC-112-H`'s refusal (HR and Finance) is asserted the same way, for the same reason `DeactivateUserTests`
and `RevokeProjectAssignmentTests` already do.

#### 5. AC-112-D and AC-112-F — named as not fully built, not silently dropped

Neither KAFF-101a (sign-in) nor KAFF-103 (change password) exists yet
[Verified: 2026-08-25 — no `SignIn` or `ChangePassword` folder under `src/Api/Features`, no `Verify`
method on `PasswordHasher`]. `AC-112-D` ("attempt to sign in with the old password") and `AC-112-F`
("can reach only the change-password endpoint," sourced by the criterion itself to `AC-103-B`, which
D-072 §2 left partly open) both name a mechanism this story cannot reach. Built instead, and asserted
as what makes each criterion true once its dependency lands: `AC-112-D` as the stored hash changing to
a value produced from a fresh salt, which is what makes the old plaintext unable to verify
[Verified: 2026-08-25 @ `tests/Api.Tests/ReactivateUserTests.cs` ->
`The_stored_credential_changes_when_a_temporary_password_is_issued_on_reactivation`]; `AC-112-F` not
asserted at all — there is no session-reach gate in this codebase to assert against, and a test
asserting `MustChangePassword == true` alone would not be `AC-112-F`, it would be rule 4 again. Routed
forward: `AC-112-F` is KAFF-103's to close, the same story its own criterion already points at.

#### What this entry does not do

It records, it does not decide. Q50 and Q51 stay open exactly as the story leaves them. No permission
row, no migration, no i18n key was added — every `MessageKey` this slice can emit
(`errors.identity.user_not_found`, `.user_already_active`, `errors.auth.forbidden`,
`errors.auth.password_too_short`) already existed in both catalogues before this session
[Verified: 2026-08-25 — `TranslationCatalogueTests` green in the 78/78 Domain run this session
produced, with no new `*Errors` member]. Nothing under `src/Web/` was touched; the
`users.action.reactivate` / `users.confirm.reactivate.*` family stays Frontend's, unbuilt.

#### Verified

Build: 0 warnings / 0 errors, clean `--no-incremental` Release. `dotnet format KaffErp.sln
--verify-no-changes` exit 0. Domain **78/78**, up from 75 — three new, in `UserTests.cs` (rule 7, rule
8, rule 9a). Api **132/132**, up from 123 before this session's two new files — nine new, in
`ReactivateUserTests.cs`. `Nobody_but_the_owner_can_reactivate_a_user` and
`EndpointPermissionCoverageTests.Every_mapped_endpoint_carries_a_permission_requirement` were both
watched to fail before being trusted: `.RequirePermission(Permission.UserManage)` removed from
`Endpoint.cs` -> `Map`, both went red — the coverage test naming
`POST /api/users/{userId:guid}/reactivate` directly, the permission test with a 500 rather than a
clean 403 mismatch, the exact D-078 shape (no gate ran, so no actor was ever verified before the
save, and the audit check constraint refused the row) — then the line was restored and the full
suite re-run green. `scripts/check-citations.ps1`: **654 checked, 0 broken, 0 legacy, exit 0** before
this entry's own citations were added. `/run-kaff-erp` smoke: all seven checks passed against the real
app title ("كف"), not the earlier false pass this session caught and re-ran — the SPA dev server had
not finished its first build when `smoke` was first invoked, and Chromium's own offline error page,
which is itself Arabic and RTL, passed every check the first time. Re-run once the dev server's
`Local: http://localhost:4200/` line appeared; genuinely green after that.

---

### D-082 · KAFF-109 built — changing a role, and the reversal D-051 (Q27) already ruled · 2026-08-25

**Backend.** `PUT /api/users/{userId}/role`. Same permission shape as `CreateUser`, `MoveUserDepartment`,
`DeactivateUser` and `ReactivateUser` — `Permission.UserManage`, `CompanyWide`, `Owner` alone
[Verified: 2026-08-25 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> the `Permission.UserManage`
row], D-044 ruling 1. No new catalogue row; SM-30 asked and the answer is the same as the four stories
before it.

**Not D-049 ruling 6.** That ruling — refuse a role change while the user actively supervises a
project — was reversed the next day by D-051 (Q27), and the story was rewritten to say so loudly, with
the superseded block left visible in `spec.md` §9 rather than edited away. This build implements Q27:
the change always succeeds and always revokes every active `ProjectAssignment` the user holds —
Supervisor, Junior and Standard alike — never refuses on that ground, and re-assignment afterwards is a
separate, deliberate act through `AssignUserToProject` (KAFF-113).

#### 1. `User.ChangeRole` — one new entity method, reusing `ValidateDepartment`

Added [Verified: 2026-08-25 @ `src/Domain/Identity/User.cs` -> `ChangeRole`]. Reapplies exactly the
invariants `Create` applies — department compatibility through the existing private `ValidateDepartment`
(the same reuse `MoveToDepartment` already established), the client-id rule for `Role.Client`, and the
no-department rule for external roles — against the account's *existing* `Department`,
`OperationsSubDepartment` and `ClientId`, none of which this method touches. Rule 8 (a request naming
the role already held is not a change) is **not** special-cased inside it: re-validating state that was
already valid cannot fail, so the call simply succeeds, and the handler is what compares the role before
and after to decide whether there is anything left to revoke. Five new `Domain.Tests` cases pin this
directly [Verified: 2026-08-25 @ `tests/Domain.Tests/UserTests.cs` ->
`ChangeRole_reapplies_the_hr_department_rule`].

#### 2. The revocation is handler work, the KAFF-111 shape, not a second mechanism

`Handler.cs` loads every active `ProjectAssignment` for the user and calls `ProjectAssignment.Revoke` in
a loop, discarding the `Result` for the same reason `DeactivateUser`'s handler does: every row in the
loop is already known to be active, so `AssignmentAlreadyRevoked` cannot occur
[Verified: 2026-08-25 @ `src/Api/Features/Users/ChangeUserRole/Handler.cs` -> `HandleAsync`]. One
`SaveChangesAsync` call carries the role change and every revocation together — CLAUDE.md's "If two
features need the same thing, it moves to Domain/", applied to the mechanism rather than to a rule: the
mechanism itself was already written once, for KAFF-111 inside KAFF-110's handler (D-074 §2), and this
story calls the same `ProjectAssignment.Revoke`, not a copy of the loop's reasoning.

#### 3. The one shape difference from its four siblings: 200 with a body, not 204

KAFF-109 rule 6 requires the response to name every project the change took the user off, so whoever
re-assigns them afterwards knows what to re-assign. `CreateUser`, `MoveUserDepartment`, `DeactivateUser`
and `ReactivateUser` all return 204 because none of them has anything to report that is not already
re-readable from the user row on the next request. This one does, so it returns
`Response(UserId, Role, RevokedProjectIds)` and 200
[Verified: 2026-08-25 @ `src/Api/Features/Users/ChangeUserRole/Response.cs`]. No `Validator.cs`, for the
same reason `MoveUserDepartment` has none: the request carries one field, and every rule about it is
`ChangeRole`'s.

#### 4. AC-109-K — not independently fault-injected, and that is recorded rather than hidden

> ### ⚠️ CORRECTED 2026-08-26 — the premise below is **false**. The conclusion survives on other evidence.
>
> **"Structurally unreachable through the public surface of this codebase" was wrong when it was
> written.** `PUT /api/users/{userId}/role` with `{"role":"Subcontractor"}`, against a departmentless
> staff account holding a credential — every `Role.Owner`, including the one KAFF-100's setup screen
> mints — passed every check in `User.ChangeRole` and then violated
> `ck_users_subcontractor_cannot_log_in` at `SaveChangesAsync`, **with the role change and every
> revocation in the change tracker together.** That is a real fault at exactly the point this section
> says nothing can fail. See `qa/slice-1/verification-2026-08-26.md` §3 `V-26-A` and §4.1.
>
> **The conclusion — the batch is atomic — is now on evidence rather than on the argument.** The
> Verifier injected that real fault against a target holding two active assignments and measured
> `roleBefore=Owner roleAfter=Owner assignments=2 stillActive=2` (PROBE-6). EF Core's implicit
> transaction around the single `SaveChangesAsync` did the work, exactly as the handler claims.
>
> **Read the sections that cite this one accordingly.** KAFF-109, KAFF-110 and KAFF-111 all lean on
> "one `SaveChangesAsync`, therefore one transaction", and that part is true and demonstrated. What
> must not be inherited is the second sentence — *"a fault-injection test would pass regardless"*. A
> handler that opens its own transaction, or saves twice, breaks the invariant, and the recorded
> reason for having no test would still have read "unreachable". **The reachable door named above is
> closed as of D-088** — `ChangeRole` now refuses the conversion as a `Result` before the revocation
> loop starts, pinned by
> [Verified: 2026-08-26 @ `tests/Api.Tests/ChangeUserRoleTests.cs` ->
> `Converting_an_account_that_holds_a_credential_into_a_subcontractor_is_refused`], which asserts both
> assignments are still active. **Closing one door is not the same as there being none**, and this
> correction stands rather than being deleted for that reason.
>
> **No source file restates the false half.** `DeactivateUser.Handler`'s and `ChangeUserRole.Handler`'s
> remarks claim only "one `SaveChangesAsync`, therefore one transaction" and that
> `AssignmentAlreadyRevoked` cannot occur because the loading query filters `RevokedAt == null`
> [Verified: 2026-08-26 @ `src/Api/Features/Users/DeactivateUser/Handler.cs` -> `HandleAsync`;
> @ `src/Api/Features/Users/ChangeUserRole/Handler.cs` -> `HandleAsync`] — both still true. The
> unreachability argument lives here and nowhere else, which is why the correction is here.


`AC-109-K` asks for a case where "the third revocation fails" mid-batch and the whole request rolls
back. As built, that specific failure is structurally unreachable through the public surface of this
codebase: the query that loads the revocation loop filters to `RevokedAt IS NULL`, so
`ProjectAssignment.Revoke` cannot fail on any row in it, and EF Core's single `SaveChangesAsync` call is
already the atomicity boundary CLAUDE.md names ("EF Core's `DbContext` is the unit of work") — the same
boundary KAFF-110/KAFF-111 relied on for the identical "one transaction" claim without a dedicated
fault-injection test (D-074 §2's evidence for that claim is the shared-correlation-id test, not an
induced failure). Producing a genuine mid-batch DB-level failure here would need fault-injection test
scaffolding — a `SavingChangesAsync` interceptor able to corrupt one tracked row deterministically
between the read and the flush — that exists nowhere else in this suite. Not built for one criterion
without precedent. What **is** built and proven: the refusal half (`AC-109-G`) — a domain-level failure
before the revocation loop starts leaves the role and every assignment untouched — and the
one-transaction structural guarantee via code review and the single `SaveChangesAsync` call. Routed
forward as a genuine coverage gap, not a silent pass: a fault-injection harness for this class of claim,
if Nabil wants one, is Verifier-sized work that would also retroactively strengthen KAFF-110/111's
identical claim.

#### 5. The question this entry does not answer

Whether the Owner may change their own role is unaddressed by every source cited to this story —
spec.md §9's "nobody creates and approves the same movement" governs financial movements, and a role
change moves no money. `Endpoint.cs` says so in its own remarks rather than deciding either way; no test
refuses a self-change and none asserts one succeeds beyond what the general suite already exercises
incidentally. **Raised for Nabil, not decided.**

#### What this entry does not do

It records, it does not decide. No permission row, no migration, no i18n key was added — every
`Error` this slice can emit (`identity.hr_role_requires_hr_department`,
`.external_role_cannot_hold_department`, `.client_user_requires_client`,
`.non_client_user_cannot_carry_client`, `.operations_requires_sub_department`,
`.sub_department_only_for_operations`, `.user_not_found`, plus the gate's own `errors.auth.forbidden`)
already carried real Arabic and English before this session
[Verified: 2026-08-25 @ `src/Web/public/locales/en.json`, `src/Web/public/locales/ar.json`]. KAFF-109
rule 12's withdrawal (`errors.identity.role_change_blocked_by_supervision` must not be added) is
observed by omission — grep confirms it is absent from `IdentityErrors.cs` and both locale files, and
nothing in this session added it. The one touch under `tests/Api.Tests/Infrastructure/` is
`ProbeEndpoint.cs`'s new `TreasuryPostRoute`, added because `AC-109-F` names `Permission.TreasuryPostProject`
by name and no existing probe route was gated on it. Nothing under `src/Web/` was touched; `enum.Role.*`
and the `users.confirm.change_role.*` family stay Frontend's, unbuilt.

#### Verified

Build: 0 warnings / 0 errors, clean `--no-incremental` Release. `dotnet format KaffErp.sln
--verify-no-changes` exit 0. Domain **83/83**, up from 78 — five new, in `UserTests.cs`. Api **143/143**,
up from 132 — eleven new, in `ChangeUserRoleTests.cs`. `Nobody_but_the_owner_can_change_a_role` and
`EndpointPermissionCoverageTests.Every_mapped_endpoint_carries_a_permission_requirement` were both
watched to fail before being trusted: `.RequirePermission(Permission.UserManage)` removed from
`Endpoint.cs` -> `Map`, both went red — the coverage test naming the ungated route directly, the
permission test with a 500 rather than a 403, the same D-078 shape (no gate ran, so no actor was ever
verified before the save, and the audit check constraint refused the row) — then the line was restored
and the full suite re-run green: Domain 83/83, Api 143/143.
`scripts/check-citations.ps1`: **657 checked, 0 broken, 0 legacy, exit 0**. `/run-kaff-erp` smoke: all
seven checks passed, `kaff-root present=true`, against the real app title ("كف").

---

### D-083 · KAFF-100 built — the setup screen, and the constraint the whole story turns on · 2026-08-26

**Backend.** `GET /api/setup` and `POST /api/setup` — the second and third anonymous endpoints the
system will ever have (the first is `GET /api/health`). Both are named in
`EndpointPermissionCoverageTests`'s allow-list rather than left to the fallback policy
[Verified: 2026-08-26 @ `EndpointPermissionCoverageTests.cs` -> `AllowList`], each with its own reason —
D-069's rule that the list grows by decision, not by accident.

#### 1. Rule 6 — the atomic guard is a unique index, not a read-then-write

**The mechanism is `User.IsBootstrapOwner`, a column only `User.CreateBootstrapOwner` ever sets,
paired with `ux_users_bootstrap_owner_once` — a unique index filtered to `is_bootstrap_owner = true`**
[Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `IsBootstrapOwner`, `CreateBootstrapOwner`;
@ `IdentityConfigurations.cs` -> `UserConfiguration`].

The rule this story states — "the check and the insert are one atomic operation, enforced by the
database" — could not be built as a table-wide uniqueness rule the way `ux_users_user_name` is,
because a table-wide "at most one row, ever" constraint would also forbid the ordinary second, third
and fortieth user KAFF-106 creates. Nothing in `spec.md` or a ruling says the Owner may exist only
once forever, either — `CreateUser` can mint a second `Role.Owner` account today, and nothing here
should quietly stop that. **The flag names the one row the setup screen itself produces, and the index
is scoped to that flag, not to the role.** A second Owner created through the ordinary path carries
`IsBootstrapOwner = false` and is untouched by the index
[Verified: 2026-08-26 @ `tests/Domain.Tests/UserTests.cs` ->
`An_owner_created_through_the_ordinary_path_does_not_carry_the_bootstrap_flag`].

`CreateOwner/Handler.cs`'s `Users.AnyAsync()` check is the courtesy path, exactly the shape
`CreateUser/Handler.cs` already established for `ux_users_user_name`: it saves hashing a password for
a request that cannot succeed, and it is not what the atomicity claim rests on. What actually decides
two concurrent requests is the unique index — a real Postgres `23505` on `SaveChangesAsync`, caught and
translated to the same `SetupErrors.AlreadyCompleted` a plain second call gets
[Verified: 2026-08-26 @ `src/Api/Features/Setup/CreateOwner/Handler.cs` -> `IsBootstrapRace`].

**Watched to fail, not assumed.** `ux_users_bootstrap_owner_once` was commented out of
`IdentityConfigurations.cs`, the solution rebuilt, and
`CreateOwnerTests.Two_concurrent_requests_produce_exactly_one_owner_and_one_refusal` went red —
`Expected … to be 1 … but found 2`, two `201`s and two Owner rows. The index was restored and the same
test, and the full suite, re-run green. The race is fired as two genuinely concurrent
`HttpClient.SendAsync` calls against the same running host and the same database
(`Task.WhenAll`), not simulated — decisions.md asked for a test that actually races it, and this one
does, against a real PostgreSQL server rather than a provider that would not enforce the index at all
(decisions.md D-022's reasoning, applied to a new class of guard).

#### 2. The audit actor is D-061's mechanism, exercised for the first time

`Handler.cs` calls `IAuditContext.AttributeTo(new AuditActor(owner.Id, owner.FullName, owner.Role))`
before `SaveChangesAsync` [Verified: 2026-08-26 @ `src/Api/Features/Setup/CreateOwner/Handler.cs` ->
`HandleAsync`] — the one caller `IAuditContext.cs`'s own remarks already named when D-061 built the
mechanism. No handler constructs an `AuditRecord`; the interceptor writes the `Created` row exactly as
it does for `CreateUser`, with the new Owner naming itself as actor
[Verified: 2026-08-26 @ `tests/Api.Tests/CreateOwnerTests.cs` ->
`The_creation_leaves_an_audit_record_naming_the_new_owner_as_its_own_actor`].

#### 3. AC-100-G's waived half, built as waived

`IdentityErrors.UserNameReserved` refuses `admin`, `root` and `kaff`, case-insensitively, in
`CreateOwner/Validator.cs` — not in `User.Create`, because the rule is this one screen's, not a
company-wide username policy (nothing stops an ordinary `CreateUser` call naming somebody `admin`).
Still uncited to a ruling; **Q45 stays open**, exactly as the story's readiness waiver (D-062 §1) left
it. The empty-full-name half needed no new code — `IdentityErrors.FullNameRequired` already refuses it
through `User.Create`, reused rather than duplicated.

#### 4. What AC-100-D is, and why it is not a test

AC-100-D asks the codebase to be searched for a setup flag, a `SetupComplete` column, a configuration
switch or an environment variable that re-opens the screen. There is none — `GetSetupAvailability`
and `CreateOwner` both compute their answer from `Users.AnyAsync()` and nothing else
[Verified: 2026-08-26 @ `src/Api/Features/Setup/GetSetupAvailability/Endpoint.cs` -> `HandleAsync`;
@ `src/Api/Features/Setup/CreateOwner/Handler.cs` -> `HandleAsync`]. This is a claim about the absence
of a mechanism, not a behaviour an HTTP test can provoke, so it is recorded here rather than encoded as
a test that would only ever pass — the same reasoning `agents.md` §3c gives for QA's "a scenario that
cannot fail is worse than no scenario."

#### 5. AC-100-F and part of AC-100-I are out of reach, and are named rather than assumed

Neither `KAFF-101a` (sign-in) nor `KAFF-105a` (`GET /api/auth/me`) exists yet — the same gap D-081
recorded for KAFF-112. What is built and proven instead: the created row's `MustChangePassword` is
`false` [Verified: 2026-08-26 @ `tests/Api.Tests/CreateOwnerTests.cs` ->
`The_owner_is_not_forced_to_change_the_password_he_typed`], which is the fact those endpoints will read
once they exist. The Arabic/RTL screen (AC-100-I) is Frontend's; no screen exists under `src/Web` yet,
and nothing beyond the two locale lines below was added there.

#### 6. `errors.setup.already_completed` and `errors.identity.username_reserved`

Both added to `src/Web/public/locales/en.json` and `ar.json`, next to their nearest siblings —
`TranslationCatalogueTests` requires it and the story's own i18n bullet names the first by exact
string. **Nothing else under `src/Web/` was touched.**

#### A test-infrastructure correction, made and then re-corrected in the same session

`CreateOwnerTests`'s first shape created and dropped a fresh `PostgresDatabase` per `[Fact]` — thirteen
`CREATE DATABASE`/`DROP DATABASE` cycles, run in xUnit's own collection (no `[Collection(...)]`
attribute means its own, parallel to `postgres`). That churn measurably destabilised an unrelated test:
`AssignUserToProjectTests.Assigning_a_user_who_does_not_exist_is_refused` intermittently saw a `403`
where it expected `404`, reproduced twice with the thirteen-database shape present and absent zero
times in three back-to-back runs without it. Replaced with a single `IClassFixture` database, reset
between tests by truncating `users` alone — **not `audit_records`**, which refused the `TRUNCATE`
outright: `23001: KAFF_APPEND_ONLY … TRUNCATE is not permitted`, the guard working exactly as designed
against a test that reached for it as a shortcut. The two tests that needed an audit count were
rewritten to assert the *change* their own call made, not the table's total, once it was clear the
total is shared and cumulative across every test method in the class. Both mistakes were caught by
running the suite repeatedly rather than once, per the standard KAFF-112, 114 and 109 all met.

#### Not done

* **Q45 and Q46** — untouched. `AC-100-G`'s blocklist is built under the waiver exactly as written; rule
  2's no-department reading is unchanged.
* **No change to `PermissionAuthorizationHandler`, `Program.cs`'s `CustomizeProblemDetails`, or any
  other endpoint's gate.** `POST /api/setup` needed no permission line — its gate is rules 4/5/6, not
  `RequirePermission` — and nothing else in the pipeline was touched.
* **No sign-in, no `GET /api/auth/me`.** KAFF-101a and KAFF-105a are unbuilt; §5 above names exactly
  what this story could and could not prove as a result.

#### Verified

Build: 0 warnings / 0 errors, clean `--no-incremental` Release, `-warnaserror`. `dotnet format
KaffErp.sln --verify-no-changes` exit 0. Domain **86/86**, up from 83 — three new, in `UserTests.cs`.
Api **156/156**, up from 143 — thirteen new, in `CreateOwnerTests.cs`.
`Two_concurrent_requests_produce_exactly_one_owner_and_one_refusal` and
`EndpointPermissionCoverageTests`'s two existing facts were watched to fail before being trusted, per
§1 above and per the allow-list additions being new, reason-carrying entries rather than a bare route
string. `scripts/check-citations.ps1`: **660 checked, 0 broken, 0 legacy, exit 0** before this entry's
own citations were added. `/run-kaff-erp` smoke: all seven checks passed, `kaff-root present=true`,
against the real app title ("كف"); `GET /api/setup` confirmed live against the running dev database,
returning `{"available":true}`.

---

### D-084 · KAFF-101a built — the staff door, and the ordering that no status code can prove · 2026-08-26

**Backend.** `POST /api/auth/sign-in`. The story was `Ready to start` rather than `Ready` because
`AC-101a-O` sat behind two decided-but-unbuilt mechanisms; both landed on 2026-08-25 (D-077, D-079),
so this session had nothing left to wait for. Nothing below is a new ruling except where it says so.

#### The one thing to read if you read nothing else

**`PasswordHasher.Verify` runs before the lockout, the role and the active flag decide the response,
and it runs for every caller including one whose username matches no row.** KAFF-101a rules 14a and
16a, decisions.md D-072 §1 and D-063 §1. Moving any of those checks earlier is **the defect, not the
optimisation** — it re-opens the user-enumeration oracle as a clock at the exact moment the status
code stopped leaking it.

**How that is held in place, since a comment is not a mechanism.**
`PasswordHasher.Verify` takes a **nullable** stored hash and has no early return: a null falls back
to a random salt and 32 random bytes at the shipped iteration count
[Verified: 2026-08-26 @ `PasswordHasher.cs` -> `Absent`]. **There is no branch in the handler to
tidy** — the absent case does the same 600,000 iterations the present case does, because there is
nowhere to put the shortcut. That is the design decision this entry most wants to survive.

#### The route, which nothing named

**`POST /api/auth/sign-in`.** The story, `qa/slice-1/test-cases.md` and `ux/slice-1-flows.md` all
describe the endpoint and none of them names its path. `/api/auth/me` is fixed (KAFF-105a) and the
frontend already calls it [Verified: 2026-08-26 @ `auth.service.ts` -> `Session`], so the door is its
sibling. **Recorded because it is now a wire contract KAFF-101b and KAFF-102 must match.**

#### What was built

* **`PasswordHasher.Verify`** — parameters read out of the stored string rather than from the
  constants, so raising the work factor never invalidates a credential issued before it;
  `CryptographicOperations.FixedTimeEquals` for the compare
  [Verified: 2026-08-26 @ `PasswordHasher.cs` -> `Verify`].
* **`AuthorizationErrors.InvalidCredentials`** (401) and **`AuthorizationErrors.AccountLocked`** (423)
  [Verified: 2026-08-26 @ `SeparationOfDuties.cs` -> `InvalidCredentials`]. Both keys added to
  `en.json` and `ar.json` together; nothing else under `src/Web/` was touched.
* **`ErrorType.Locked` and its `StatusFor` row** [Verified: 2026-08-26 @ `Error.cs` -> `Locked`;
  @ `ResultExtensions.cs` -> `StatusFor`]. A bare 423 in one handler would have put a status mapping
  outside the one place `ResultExtensions` keeps it.
* **`StaffSessionMinter`** — rule 16b and D-063 §1's *"one guard there refuses `Role.Client` for all
  of them"*. It throws; it is a programmer-error guard and never the user-facing path
  [Verified: 2026-08-26 @ `StaffSessionMinter.cs` -> `Issue`]. `Role.Subcontractor` is refused beside
  it, on spec.md §9's *"record only, no login"* — the same guarantee, and not a new rule.
* **`SlidingSessionMiddleware`** — rule 5's *sliding*, which is a separate mechanism from the expiry
  and had no home [Verified: 2026-08-26 @ `SlidingSessionMiddleware.cs` -> `InvokeAsync`].
  Cookie-borne sessions only, and never on an `AllowAnonymous` endpoint.
* **Three `AuditEventKind` members** — `SignInFailed`, `SignInFailedUnknownUser`, `AccountLockedOut`
  [Verified: 2026-08-26 @ `IAuditContext.cs` -> `SignInFailedUnknownUser`].
* **No migration.** Nothing about this story changes the schema — which is what D-077 and D-079
  landing first bought.

#### Four decisions this session made rather than transcribed

**1. `AuditEventKind` gets three members, not six.** D-063 §3 delegated the vocabulary here and said
the unknown-username case *"arguably implies another"*. It implies exactly one — the subjectless
`SignInFailedUnknownUser`. Every refusal against an account that **exists** is one `SignInFailed`,
because the subject already names the row and the row already carries the role and the active flag;
splitting by reason would put the door's decision into the trail for no reader. `AccountLockedOut` is
the third, and it is the story's own audit paragraph asking for the lockout as a searchable fact
rather than as a property diff somebody has to notice.

**2. The success response is `204`, not `200` with an empty object.** D-050: the body carries no
token in any field under any name. A `204` has no body for one to be added to later.

**3. `nbf` is not on the token.** It was, and it broke the sliding session: the framework validates
lifetimes against its own clock, so a token renewed by a host whose clock is ahead is refused as
not-yet-valid on the very next request. Measured, not reasoned about. `exp` is the whole lifetime.

**4. The handler discards the caller's identity before doing anything.** Signing in while already
holding a cookie is an ordinary act — the SPA submits the form, the browser attaches the old cookie —
and without this it is a **500**: no gate runs on an anonymous endpoint, so `ResolveActor` builds an
actor from the token's claims with no verified role beside it
[Verified: 2026-08-26 @ `AuditSaveChangesInterceptor.cs` -> `ResolveActor`], and
`ck_audit_records_actor_is_named_completely` refuses a half-named actor outright. It is also what the
act means: a sign-in replaces the session, it does not extend it. Held by
[Verified: 2026-08-26 @ `SignInTests.cs` -> `Signing_in_again_while_holding_a_session_replaces_it`].

#### How the ordering was proved, since this is the claim that matters

**Three defects were injected, built, and watched.** Not one of them is hypothetical; each is the
shape a later session tidies toward.

| Injected defect | What went red | What stayed green |
|---|---|---|
| `if (storedHash is null) return false;` as `Verify`'s first line | the hasher timing test — **0 ticks against 4,458,099** | the three other `Verify` tests |
| the lockout check hoisted above `PasswordHasher.Verify` | `A_locked_account_answers_423_to_the_right_password_and_401_to_a_wrong_one` **and** `Five_different_refusals_are_one_answer` — *"Expected 401, but found 423 Locked"* | everything else, 19 of 21 |
| `if (user is null) return Unauthorized;` before the hash | `No_refusal_is_faster_than_the_hash_it_should_have_paid_for` — **61,475 ticks against a 4,653,877 baseline, 1.3% of the work** | **every other test in the file, 20 of 21** |

**The third row is the whole argument for keeping a timing test in this suite, and it answers the
question honestly.** The lockout-first defect *is* caught deterministically, by a status code — D-072
§1's own ruling makes it impossible to answer 401 and 423 correctly without knowing the password
first. The unknown-username defect is **not**, by anything except a clock, and it is the more likely
of the two to be written.

**The timing assertion is not a micro-benchmark.** The statistic is the **minimum** of three runs, the
threshold is **half the baseline**, and the margin between doing the work and skipping it is three
orders of magnitude — no scheduler noise reaches that from either side. The same property is asserted
once more against the pure function with no HTTP in the way
[Verified: 2026-08-26 @ `PasswordHasherTests.cs` -> `Verifying_against_no_stored_hash_costs_what_verifying_against_one_costs`].
**If it ever flakes, the answer is a wider margin or a longer sample — not deleting it**, because
deleting it leaves the one defect nothing else can see.

#### 🟡 For Nabil — three questions this build raises and does not answer

**1. The reach of a `mustChangePassword` session. Not answered here, deliberately.** D-072 §2 issues
a **full** token and puts the flag in `/api/auth/me`'s payload. Rule 8, `AC-101a-F` and `AC-103-B` all
assert the strict reading and all three cite D-049 ruling 4, **which names no endpoint**. The
criterion says in its own text that it must not be resolved by whoever writes the handler, so it is
not: **sign-in succeeds and consults the flag for nothing**, which is what D-072 §2 rules, and no gate
was added anywhere. The two readings differ by whether a hostile client can skip the change screen.
**`AC-101a-F` is therefore not covered by a test and is reported uncovered rather than quietly
dropped.**

**2. The inactive account has no ruled refusal shape, and it now has a built one.** The story's i18n
bullet names `errors.auth.account_inactive`; **no criterion reaches it** — `AC-101a-H` says only
*"refused"*. This build answers with the **generic 401**, folded in with the client and the
subcontractor, because a distinct answer is reachable **from the username alone** and announces that
the account exists, which is precisely what D-065 case 5 refused for the subcontractor. **That is an
extension of Nabil's reasoning to a case he was not asked about, and it is flagged rather than
buried.** `errors.auth.account_inactive` is consequently **not** in either catalogue: adding an
unreachable key would have been the more misleading half of the choice. One line in the handler and
one assertion in
[Verified: 2026-08-26 @ `SignInTests.cs` -> `An_inactive_account_is_refused_like_a_stranger`] are what
change if he rules the other way.

**3. Q28 is still open and is now observable.** The lockout is per account, so anybody who knows a
site engineer's username can hold him out fifteen minutes at a time, from anywhere, indefinitely —
and the test suite does it in five HTTP requests. Nothing here anticipates the second reading.

#### What the story and the test cases got wrong

**Named, not edited — `qa/` and `stories/` belong to QA and the BA.**

* **`TC-1-011` commands `errors.auth.role_cannot_log_in` for the subcontractor.** Superseded by
  **D-065 case 5**; the door answers the generic 401 and the case as written would fail a correct
  implementation. `AC-101a-G` was corrected on 2026-08-23 and the test case was not.
* **`TC-1-015` requires the failure record to carry *"the attempted username"*.** Superseded by
  **D-062 §3**, which strikes exactly that. The story's audit paragraph carries the strike; the test
  case still commands the thing Nabil forbade.
* **`TC-1-009` and `TC-1-016` predate D-065 and D-072 §1** — three cases where there are now five, and
  *"sign-in succeeds"* for a `Role.Client`, which D-062 §2 reverses outright.
* **`TC-1-018` omits the `/api/auth/me` carve-out** that D-072 §2 added to `AC-101a-F`.

#### Not done, and named so nobody assumes it exists

* **No `GET /api/auth/me`** (KAFF-105a), **no sign-out** (KAFF-102), **no change-password** (KAFF-103),
  **no reset** (KAFF-104), **no sign-in screen** (KAFF-101b). The cookie this mints is what all five
  build on.
* **No `mustChangePassword` gate on any endpoint.** See question 1.
* **No `errors.auth.account_inactive` and no `errors.auth.password_change_required`** in either
  catalogue. Both are named by the story's i18n bullet and neither is reachable from anything built;
  the second belongs to KAFF-103.
* **`AC-101a-M` is not covered here.** It is a browser assertion about `localStorage` and
  `sessionStorage` and belongs to the E2E suite with KAFF-101b's screen. What this story can guarantee
  — that nothing is handed to JavaScript to store — is covered by the empty `204` body and the
  `HttpOnly` assertion.
* **No rehash-on-sign-in.** `Verify` reads the work factor from the stored string, so a credential at
  an older factor keeps working; nothing upgrades it. Add it beside `Hash` when the factor first moves.
* **No change to `AuditCorrelationMiddleware`, `IAuditContext`, `AuditRecord`, the schema or any
  migration.** None was needed.

#### Verified

Clean `--no-incremental` Release build: **0 warnings / 0 errors**. `dotnet format KaffErp.sln
--verify-no-changes` exit 0. Domain **86/86** (unchanged). Api **181/181**, up from 156 — twenty-one
in `SignInTests.cs` and four in `PasswordHasherTests.cs`. `/run-kaff-erp` smoke: all eight checks
passed, `kaff-root present=true`, `guardsInstalled: []`. The endpoint was exercised against the
running Development stack, not only the test host: `POST /api/auth/sign-in` for an unknown username
returned `401` / `errors.auth.invalid_credentials`, and the row it wrote reads
`SignInFailedUnknownUser | User | <no entity_id> | <no actor> | ::1 | /api/auth/sign-in` — a subject
that is absent, an address taken from the connection, and no trace of what was typed.

---

### D-085 · KAFF-102 built — sign-out, and the no-op the story left open · 2026-08-26

**Backend.** `POST /api/auth/sign-out`
[@ `src/Api/Features/Auth/SignOut/Endpoint.cs`, `Handler.cs`]. Re-estimated 3 → 2 on the strength of
D-051 (N5): no session table, so the whole mechanism is clearing the `__Host-kaff-auth` cookie and
writing one `AuditEventKind.SignedOut` — a member D-061 shipped on 2026-08-22 and no feature had
consumed since. **No migration, no session store, no revocation list.** The story's own guardrail
against a session table was not tested by tempting circumstance — nothing about this build wanted one.

#### What was built

* **`Endpoint.cs`** — `AllowAnonymous()`, the fifth member of
  `EndpointPermissionCoverageTests.AllowList`
  [@ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `AllowList`]. Rule 7 forces this: behind
  the fallback policy an unauthenticated caller is refused `401` before the handler runs at all, which
  is exactly the refusal rule 7 says signing out twice must not get.
* **`Handler.cs`** — reads the caller's identity from `http.User` **before** discarding it, loads the
  row fresh from `Users` (never the token's claims — D-075's discipline extended here on the same
  reasoning: a stale role written into an append-only trail is wrong permanently), then discards the
  identity and calls `AttributeTo` exactly as `SignIn.Handler` does (D-084 point 4) — required because
  no permission gate runs on an `AllowAnonymous` endpoint to populate `VerifiedActor`, and an
  authenticated request may not `AttributeTo` a different actor without it.
* **`StaffSessionMinter.CookieAttributes()` went from `private` to `internal`**
  [@ `src/Api/Identity/StaffSessionMinter.cs` -> `CookieAttributes`], and `SignOut.Handler` calls it
  rather than building its own `CookieOptions`. The brief's own warning — "any clear you write must
  match the attributes the mint used or the browser will not remove it" — is a fact about two literals
  agreeing forever, and the only mechanism that guarantees that is one literal, not a second one kept
  in sync by hand.

#### One decision this session made rather than transcribed

**An already-unauthenticated caller writes no audit record.** Rule 7 rules out refusing the call; it
does not say whether a call that changed nothing — no cookie existed to clear, no actor to name —
still owes CLAUDE.md's "every state change writes an audit record" a row. The story's audit paragraph
describes only the signed-in case. A `SignedOut` row with a null actor is legal at the database (D-063
§3 built exactly that shape), but it would assert "somebody signed out" with no way to say who, which
nothing asks for. **Not written, and flagged rather than decided silently** — asserted by
`Signing_out_with_no_session_writes_no_audit_record`
[@ `tests/Api.Tests/SignOutTests.cs` -> `Signing_out_with_no_session_writes_no_audit_record`], so a
later session that starts writing one does so on purpose. **Question for Nabil:** should a no-op
sign-out leave a trace at all, and if so, naming whom?

#### AC-102-F could not be exercised through a door that exists

**`StaffSessionMinter.Issue` throws for `Role.Client` by construction** (decisions.md D-063 §1) — the
client portal is a separate host with its own door (D-051 Q33) that no story has built yet. There is
therefore no way, today, for a `Role.Client` caller to hold a cookie this endpoint would ever see.
`A_client_role_session_can_sign_out_too` hand-signs a token with the same key, issuer and audience
`KaffApiFactory` configures for every test host — the same shape `StaffSessionMinter.Mint` produces,
built independently since that class refuses to build one for this role — and asserts sign-out handles
it identically. **That proves the handler is role-agnostic; it does not prove a client can reach it
today**, and the gap is named here rather than papered over with a mechanism that does not ship.

**Watched red, the way it was meant to be caught.** The first run of that test failed with
`System.InvalidOperationException: Sequence contains no elements` — not the assertion failing, the
audit query finding no row at all. Cause: the hand-minted token's `Expires` was computed from the
suite's fixed `Now` (`2026-05-01`), but this factory runs no `TestClock`, so the shipped JWT bearer
scheme validated lifetime against the real system clock and the token read as already expired — the
handler correctly treated the caller as unauthenticated and wrote nothing. Fixed by minting from
`DateTimeOffset.UtcNow` instead. Left in `SignOutTests.cs`'s remarks so the next session does not
reintroduce it.

#### Verified

Clean `--no-incremental` Release build: **0 warnings / 0 errors**. `dotnet format KaffErp.sln
--verify-no-changes` exit 0. Domain **86/86** (unchanged — no Domain code touched). Api **191/191**,
up from 181 — ten new facts in `SignOutTests.cs` and the fifth allow-list entry.
`check-citations.ps1`: **679 checked, 0 broken, 0 legacy** (unchanged — this story cited existing
identifiers rather than adding bracketed ones). `/run-kaff-erp` smoke: all seven checks passed against
the running Development stack, `kaff-root present=true`, `guardsInstalled: []`.

#### Not done, and named so nobody assumes it exists

* **No session table, no revocation list, no token blacklist.** The story's own re-estimation says
  this is the whole point; nothing here second-guesses D-051 (N5).
* **No i18n catalogue change.** No new domain error key: the story's `auth.action.sign_out` and
  `auth.signed_out` are UI-facing action/message keys for the sign-in screen's owner, not a backend
  refusal key, and this build introduces no refusal of its own.
* **No client-portal sign-in door.** See above — `AC-102-F` is exercised against a hand-minted token,
  not a shipped mechanism.
* **The no-op audit question above is unresolved**, deliberately, pending Nabil.

---

### D-086 · KAFF-103 built — changing your own password, and the third shape a permission-coverage test needed · 2026-08-26

**Backend.** `POST /api/auth/change-password`
[@ `src/Api/Features/Auth/ChangePassword/Endpoint.cs`, `Handler.cs`]. `User.SetOwnPassword`,
`SetTemporaryPassword`, `MustChangePassword` and the `StorePasswordHash` guard that refuses
`Role.Subcontractor` all existed already, cited by the story as built — this session's own work is the
API surface, the `MustChangePassword` gate (`AC-103-B`), and the tests, not the domain method.

#### The shape the coverage test did not have

The story's line — "authenticated as the user themselves. Not `UserManage`" — names a permission model
`EndpointPermissionCoverageTests` had no room for. Every mapped route until now was either
`RequirePermission(...)` or a named `AllowList` member requiring `AllowAnonymous()`
[@ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `AllowList`,
`Every_allow_list_member_is_mapped_and_says_so_in_its_own_file`]. Neither fits an endpoint that must
refuse an unauthenticated caller but grants no role anything — there is no catalogue `Permission` for
"act on your own row alone", and inventing one would misstate the rule: it is not a grant any role holds
over anyone.

**Built a third category, `SelfOnlyEndpoints`**
[@ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `SelfOnlyEndpoints`], narrow by construction
the same way `AllowList` is (D-069): one member, one named reason, and a mirror test asserting the entry
is mapped, carries **no** `IAllowAnonymous` (an unauthenticated caller must still be refused) and **no**
`RequirePermission` of its own
[@ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
`Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own`]. Watched
red before being named: with the list emptied to `[]`,
`Every_mapped_endpoint_carries_a_permission_requirement` failed on the new route, exactly the shape
D-067 exists to catch. Restored; Api **200/200**.

#### `AC-103-B`: one check in the one evaluator, not a filter bolted beside it

**Decision.** `PermissionSubject` gains `MustChangePassword`
[@ `src/Domain/Authorization/PermissionEvaluator.cs` -> `PermissionSubject`], read fresh from the users
table on every request by `PermissionSubjectReader`
[@ `src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`] — the same freshness
discipline D-048 and D-053 already hold every other authorization fact to. `PermissionEvaluator.Evaluate`
refuses with the new `PermissionDecision.PasswordChangeRequired` immediately after the `Role.Subcontractor`
check and before the catalogue is consulted, in both overloads
[@ `src/Domain/Authorization/PermissionEvaluator.cs` -> `Evaluate`], so a temporary credential blocks a
company-wide, unconditionally-granted permission (`UserManage` for the Owner) exactly as it blocks a
project-scoped one — proved directly against the pure function
[@ `tests/Domain.Tests/PermissionEvaluatorTests.cs` ->
`A_caller_who_must_change_their_password_is_refused_before_the_catalogue_is_consulted`].

**One route, every `RequirePermission` endpoint at once, by construction — not a second rule layered on
top of the first.** The check lives in the one place every gated request already passes through, so
"every endpoint except the change-password one" needs no enumeration of endpoints. The change-password
endpoint is exempt for the same reason it needed `SelfOnlyEndpoints` above: it carries no
`RequirePermission`, so the gate this decision lives inside never runs on it at all.

**The refusal needed its own key, and D-071/D-080 do not hand out a mechanism for that on purpose.**
Both entries flatten every gate refusal to the blanket `errors.auth.forbidden` deliberately, because
telling a caller which axis of role × assignment failed is a disclosure question. A must-change-password
refusal is not that disclosure — it tells the caller nothing beyond what holding the credential already
implies, and the shell needs the distinct key to route to the change-password screen rather than treat
it as an ordinary 403 (D-072 §2's own reasoning against a dead-end loop, applied one endpoint over).
Built `SpecificRefusal` [@ `src/Api/Authorization/SpecificRefusal.cs`], the ~15-line mechanism D-080
priced and declined for the axis-disclosure case: the gate stashes the specific `Error` on
`HttpContext.Items`, and `CustomizeProblemDetails` reads it ahead of the generic 401/403 switch
[@ `src/Api/Program.cs` -> `AddProblemDetails`]. Watched red: with the `MustChangePassword` check
removed from both `PermissionEvaluator` overloads,
`Until_the_password_is_changed_every_other_endpoint_refuses_it_and_this_one_does_not` failed
[@ `tests/Api.Tests/ChangePasswordTests.cs`]; restored.

#### The endpoint re-checks its own caller — nothing else will

No permission gate runs on a `SelfOnlyEndpoints` route, so `Handler.cs` reads the user id and security
stamp from the token's own claims, loads the row fresh, and refuses (`errors.auth.forbidden`, the same
generic key a stale token gets everywhere else) when the account is inactive or the stamp no longer
matches [@ `src/Api/Features/Auth/ChangePassword/Handler.cs` -> `HandleAsync`] — the same freshness
`PermissionSubjectReader` applies to every `RequirePermission` route, reapplied by hand because nothing
upstream of this handler will apply it here. Proved by
[@ `tests/Api.Tests/ChangePasswordTests.cs` -> `A_deactivated_account_cannot_change_its_own_password`].

The actor is declared, not read from a grant, for the identical reason KAFF-101a's sign-in and
KAFF-102's sign-out declare theirs (D-075): the handler discards the inbound identity and calls
`IAuditContext.AttributeTo` itself before saving, because no gate populated `VerifiedActor`.

#### A second `Set-Cookie`, and it is not a bug

`SlidingSessionMiddleware` renews the session with the request's own **pre-change** stamp before the
handler runs, because this endpoint is authenticated, not anonymous
[@ `src/Api/Common/Middleware/SlidingSessionMiddleware.cs` -> `InvokeAsync`]. `SetOwnPassword` then
rotates the stamp, and the handler mints a **second** cookie with the new one so the calling device is
not signed out by its own change (`AC-103-A`) — the same two-`Set-Cookie` shape that middleware's own
remarks already document for the sign-in case, last one wins in a real browser. The test suite's cookie
helper takes the last header for the same reason
[@ `tests/Api.Tests/ChangePasswordTests.cs` -> `Cookie`].

#### What was not generalised, on purpose

The brief for this story warned against inventing a wider rule than `AC-103-B` states, and nothing here
does. `PasswordChangeRequired` applies to every `RequirePermission`-gated route because that is the one
mechanism CLAUDE.md asks for — not because this session decided how far a `mustChangePassword` session
should reach in the abstract. `GET /api/auth/me` (KAFF-105a) is still unbuilt, so `AC-103-B`'s carve-out
for it could not be exercised either way; nothing here assumes an answer for it. The open question D-084
and D-072 §2 both raised — what a full token issued to a must-change-password user may reach beyond the
change endpoint and `/api/auth/me` — stays open, for Nabil, not decided sideways by this build.

#### Verified

Clean `--no-incremental` Release build, `-warnaserror`: **0 warnings / 0 errors**.
`dotnet format KaffErp.sln --verify-no-changes` exit 0. Domain **90/90**, up from 86 — four new, in
`PermissionEvaluatorTests.cs` (two) and `UserTests.cs` (two, `AC-103-H`'s subcontractor refusal and
rule 4's stamp rotation, exercised at the entity since a subcontractor can never hold the session the
API endpoint requires). Api **200/200**, up from 191 — nine in `ChangePasswordTests.cs` and one in
`EndpointPermissionCoverageTests.cs`. `check-citations.ps1`: **682 checked, 0 broken, 0 legacy**.
`/run-kaff-erp` smoke: all seven checks passed, `kaff-root present=true`, `guardsInstalled: []`.

#### Not done, and named so nobody assumes it exists

* **`AC-103-I` — Arabic, RTL, at mobile width.** A screen, owned by the Frontend agent under
  `src/Web/`. Nothing here touches it.
* **No `auth.password.*` / `auth.field.*` / `action.save` UI keys added.** The story's i18n bullet
  names them for the screen that does not exist yet; only the two server-owned refusal keys —
  `errors.auth.current_password_incorrect`, `errors.auth.password_change_required` — were added, to
  both catalogues, per CLAUDE.md's rule that a domain error key needs both lines and nothing else under
  `src/Web/`.
* **`GET /api/auth/me` was not built and not touched.** KAFF-105a's, not this story's.
* **Q37 and Q48 stay open** — no expiry on a temporary password, and whether the current password is
  genuinely required. Rule 5 is built under the story's own readiness waiver (D-062 §1); nothing here
  answers Q48.
* **The reach of a `mustChangePassword` session beyond this story's own criterion is not decided.** See
  above.

---

### D-087 · KAFF-105a built — `GET /api/auth/me`, and the trap the brief named by name · 2026-08-26

**Backend.** `GET /api/auth/me`
[@ `src/Api/Features/Auth/WhoAmI/Endpoint.cs`, `Handler.cs`, `Response.cs`]. Last story in sprint 1.
Folder is `WhoAmI`, not `Me` — `Me` is a reserved word in a CLS-consuming language and CA1716 refuses
the namespace outright the first time the solution builds with `-warnaserror`; the route itself is
still `/api/auth/me`, fixed by decisions.md D-084, and nothing about the wire contract moved.

#### The trap named in the brief, and how it is closed

**Every field in the response is read from a fresh `Users` row, never from the token's claims.**
`StaffSessionMinter.ClaimsFor` issues four claims — user id, display name, role at mint time, security
stamp — and department is not among them at all
[Verified: 2026-08-26 @ `StaffSessionMinter.cs` -> `ClaimsFor`]. A role changed by KAFF-109 does not
rotate the stamp (decisions.md D-051 Q27, D-082), so the claim would go on reading the old role for as
long as the token lives. The handler loads `User` by id and builds the response from that row's
`Role`, `Department` and `OperationsSubDepartment`
[Verified: 2026-08-26 @ `Handler.cs` -> `HandleAsync`] — there is no code path here that ever reads
`KaffClaimTypes.Role`. Proved rather than merely built: `A_role_changed_after_sign_in_is_reported_fresh_not_from_the_stale_token`
[@ `tests/Api.Tests/MeTests.cs`] signs in, rewrites the row's role directly (the KAFF-109 shape, no
stamp rotation), and asserts the response follows the row. **Watched red first** — with the handler
temporarily made to read `claimedRole` off `http.User.FindFirst(KaffClaimTypes.Role)` instead, this one
test failed and no other did; reverted.

#### The second trap: the projection is the control, not the permission

Rule 4 restricts the payload to `PermissionScope.CompanyWide` rows, and `Role.Client` must see an empty
set even though the catalogue does grant it two rows (`PortalRead`, `PortalApprove`) — both
`ProjectScoped` (decisions.md D-035, the KAFF-105a/105b split). Rather than special-case `Role.Client`,
`PermissionEvaluator.CompanyWidePermissionsHeld(PermissionSubject)`
[@ `src/Domain/Authorization/PermissionEvaluator.cs` -> `CompanyWidePermissionsHeld`] filters
`PermissionCatalogue.All` to `CompanyWide` rows and runs the ordinary `Evaluate` once per row — the
same function every `RequirePermission` route already calls, not a second matcher written for this
endpoint. A client's set is empty as a consequence of the catalogue shape, not a role check in the
handler; the same is true of the story's rule 5 (a catalogue addition needs no change here), held by
`A_permission_the_test_adds_to_the_catalogue_would_appear_with_no_change_to_this_method`
[@ `tests/Domain.Tests/PermissionEvaluatorTests.cs`].

**`CompanyWidePermissionsHeld` does not special-case `MustChangePassword` either.** `Evaluate` already
refuses every permission with `PermissionDecision.PasswordChangeRequired` while the flag is set
(D-086), so a forced-change caller's permission list is empty — an honest "nothing yet", not a second
rule invented for this endpoint. Pinned by
`A_caller_who_must_change_their_password_holds_no_company_wide_permission_either`
[@ `tests/Domain.Tests/PermissionEvaluatorTests.cs`]. Nobody asked for this reading and nobody asked
against it either; it is the reading that falls out of reusing `Evaluate` rather than writing a second
rule, and is named here so a future session sees it was a choice.

#### Why the endpoint carries no `RequirePermission` — `AC-105a-C`'s whole mechanism

There is no catalogue `Permission` for "read your own profile" — the story's own line, "authenticated,
any role, no assignment" — so this route needed the same shape D-086 built for `change-password`:
authenticated, but gated by nothing `PermissionAuthorizationHandler` would refuse on. Added as the
second member of `EndpointPermissionCoverageTests.SelfOnlyEndpoints`
[@ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `SelfOnlyEndpoints`], with its own mirror
assertion: no `IAllowAnonymous` (an unauthenticated caller is still refused 401 by the fallback policy
— `AC-105a-D`) and no `RequirePermission` of its own.

**That absence is `AC-105a-C`'s whole mechanism, not incidental to it.** D-072 §2 requires a
`mustChangePassword` caller to get a `200` and a full profile rather than a refusal.
`PermissionEvaluator.Evaluate`'s `PasswordChangeRequired` short-circuit only ever runs inside the
`RequirePermission` pipeline (D-086); an endpoint with no `RequirePermission` never reaches it, by
construction, the same way `change-password` never reaches it. **Watched red**: with
`.RequirePermission(Permission.UserRead)` temporarily added to the `Map` chain,
`A_forced_password_change_is_announced_as_a_field_on_a_200_not_a_refusal` failed — 403 instead of
200 — and no other `MeTests` case did; reverted.

#### The freshness re-check nothing upstream applies here

> **⚠️ CORRECTED 2026-08-26 by D-089 — this re-check was two of three, and the missing one was a
> security defect.** What a `RequirePermission` route gets is `IsActive`, the stamp, **and** the role
> bar `PermissionEvaluator` applies before the catalogue is consulted. This section describes copying
> the first two by hand and says so accurately; it does not notice the third, so `GET /api/auth/me`
> answered `Role.Subcontractor` with a `200` and their name — spec.md §9, "record only, no login".
> See `qa/slice-1/verification-2026-08-26.md` `V-26-B`. **The hand-copy is gone**: all three now live
> in `LiveSession` and are applied by `RequireLiveSession()`, which is also the only thing that marks
> a route as exempt, so the two acts cannot come apart again. The paragraph below stands as written
> because the reasoning that produced it — "leaving it out here would make this the one exception" —
> was right, and reached for the wrong half of the answer: **the pattern, not the instance.**

No permission gate runs on this route, so nothing re-validates `IsActive` or the security stamp the way
`PermissionSubjectReader` does for every `RequirePermission` route (D-048, D-053). The handler
reapplies both by hand, refusing with the ordinary `errors.auth.forbidden` a stale token gets everywhere
else (D-071, D-080) rather than answering a deactivated account, or a token a later password change
already superseded, with a profile as if the session were live
[@ `src/Api/Features/Auth/WhoAmI/Handler.cs` -> `HandleAsync`]. Not commanded by any acceptance
criterion — the story is silent on what a stale token does here — and built anyway on the strength of
the identical shape `ChangePassword.Handler` already carries for the same reason (D-086), rather than
leaving this the one `SelfOnlyEndpoints` route that answers a dead session as a live one. **Watched
red twice**: with the check narrowed to `user is null` alone,
`A_deactivated_accounts_token_is_refused_not_answered_with_a_profile` and
`A_password_changed_on_another_device_ends_this_endpoints_answer_too`
[@ `tests/Api.Tests/MeTests.cs`] both failed and nothing else did; reverted.

#### What the response projects, and what it deliberately does not

**Projects:** `userId`, `displayName`, `role`, `department`, `operationsSubDepartment`,
`mustChangePassword`, `permissions` (flat `CompanyWide` set) — exactly rule 1 plus rules 3 and 4, no
more [@ `src/Api/Features/Auth/WhoAmI/Response.cs` -> `Response`].

**Does not project:** `clientId`, `email`, `phone`, `employeeId`, `isActive`, `failedSignInAttempts`,
`lockedOutUntil`, `createdAt`, `deactivatedAt`, `userName` — none of these is named by rule 1, and Q42's
ruling (D-055 §2, cited in the catalogue's own `UserRead` remarks) is the standing warning against
shipping the row instead of the projection the story asked for. No money field, no cost, no margin
(rule, "money" bullet). No `PasswordHash`, no `SecurityStamp` — `AC-105a-G`.

#### Two things this session decided rather than transcribed, both narrower than they could have been

1. **The freshness re-check above.** Not asked for by name; built because `SelfOnlyEndpoints` already
   established the pattern for exactly this shape of route and leaving it out here would make this the
   one exception.
2. **Reusing `PermissionEvaluator.Evaluate` unmodified for the `MustChangePassword` and `Role.Subcontractor`
   short-circuits**, rather than writing a permission-list builder that bypasses them. Discussed above.
   Both are read-time consequences of reuse, not new rules; flagged so a future session does not read
   an empty permission list under `mustChangePassword` as a bug.

Neither widens what a `mustChangePassword` session may **reach** — the 🟡 question D-084 and D-072 §2
both raised, about every endpoint beyond this one and `change-password`, is untouched here exactly as
the story's own text requires ("Handed back to Nabil, not settled here").

#### What the story got wrong

Nothing. Every criterion was buildable as written; the one open question (`mustChangePassword` reach
beyond this endpoint and `change-password`) was already correctly identified in the story as Nabil's,
not Karim's, and not this session's to answer.

#### Verified

Clean `--no-incremental` Release build, `-warnaserror`: **0 warnings / 0 errors**.
`dotnet format KaffErp.sln --verify-no-changes` exit 0. Domain **94/94**, up from 90 — four new in
`PermissionEvaluatorTests.cs`, and `PermissionCoverageTests.cs`'s `NamedInNoTestYet` list lost
`Permission.SupplierManage`, now named by one of them (SM-30 — a row stops being a known gap the day a
test names it, and leaving the line in would itself have failed the file's own check). Api **209/209**,
up from 200 — nine in `MeTests.cs` and the second `SelfOnlyEndpoints` entry.
`check-citations.ps1`: **692 checked, 0 broken, 0 legacy** — unchanged by the build itself; this entry's
own citations are what raise it from here. `/run-kaff-erp` smoke: all eight checks passed,
`kaff-root present=true`, `guardsInstalled: []`; `GET /api/auth/me` exercised against the running
Development stack with no cookie returned `401` / `errors.auth.not_authenticated`.

#### Not done, and named so nobody assumes it exists

* **`KAFF-105b` — the per-project list and a portal client's two permissions.** Rule 4 and `AC-105a-H`
  are exactly the boundary that story starts from; nothing here builds any part of it.
* **No i18n catalogue change.** Both refusal keys this handler emits (`errors.auth.not_authenticated`,
  `errors.auth.forbidden`) already existed in both catalogues before this session.
* **No audit record.** A read; CLAUDE.md requires one on a state change, and this is not one.
* **The reach of a `mustChangePassword` session beyond this endpoint and `change-password` is still
  undecided.** D-084 and D-072 §2 both raised it; this entry does not narrow it in either direction.

---

### D-088 · `V-26-A` fixed — the reachable 500, and the seeding that made the whole suite vacuous · 2026-08-26

**Backend, defect-fix session.** `qa/slice-1/verification-2026-08-26.md` rejected KAFF-109, KAFF-105a
and KAFF-102. This entry is the first of three and covers `V-26-A` (HIGH) and the correction to
D-082 §4 above.

#### What was wrong

`User.ChangeRole` re-applied the *creation* invariants and nothing about the *transition*
[Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `ChangeRole`]. spec.md §9 — *"Subcontractor
— record only, no login"* — is enforced by the entity on the way **in**
[Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `StorePasswordHash`] and by the database
outright [Verified: 2026-08-26 @
`src/Infrastructure/Persistence/Configurations/IdentityConfigurations.cs` ->
`ck_users_subcontractor_cannot_log_in`], and by nothing at all on the way **round**. A departmentless
staff account holding a credential — every `Role.Owner`, including the one `CreateBootstrapOwner`
mints [Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `CreateBootstrapOwner`] — passed every
check and violated the constraint at `SaveChangesAsync`. The `DbUpdateException` was unhandled and the
caller got a `500` carrying no `code` and no `messageKey`, which the Arabic shell cannot render.

**KAFF-100 already had the pattern this was missing** — its handler catches the unique-violation by
constraint name and returns `setup.already_completed` rather than a `500`
[Verified: 2026-08-26 @ `src/Api/Features/Setup/CreateOwner/Handler.cs` -> `IsBootstrapRace`]. The fix
here takes the other route available: state the rule in the domain, where a `Result` can carry it,
rather than catch the database's refusal in a handler.

#### The decision, and it is half a decision

**Refuse, do not clear.** `ChangeRole` refuses `Role.Subcontractor` while `PasswordHash is not null`
and returns the `IdentityErrors.SubcontractorCannotLogIn` the entity already uses for the same rule
[Verified: 2026-08-26 @ `src/Domain/Identity/IdentityErrors.cs` -> `SubcontractorCannotLogIn`] — a
`409` with a key that already carries real Arabic and English
[Verified: 2026-08-26 @ `src/Web/public/locales/ar.json` ->
`errors.identity.subcontractor_cannot_log_in`]. No new error, no new key, no catalogue change.

**This is not a new business rule and is deliberately not one.** It is
`ck_users_subcontractor_cannot_log_in` — *"`role <> 'Subcontractor' OR password_hash IS NULL`"* —
restated where a `Result` can carry it. An account holding **no** credential still converts, which is
exactly what the constraint permits, pinned by
[Verified: 2026-08-26 @ `tests/Api.Tests/ChangeUserRoleTests.cs` ->
`Converting_an_account_with_no_credential_into_a_subcontractor_succeeds`] so that nobody reads the
refusal as *"a user may never become a subcontractor"*, a rule no source states.

#### 🟡 For Nabil — the half this session refused to decide

**Should converting a user to `Role.Subcontractor` (a) refuse, or (b) succeed and clear the
credential?** KAFF-109 does not say. spec.md §9 says only *"record only, no login"*, which both
readings satisfy. KAFF-109's own **Q41** raises the sibling question for `Role.Client` and is open.

**Refusing was built because it is the reversible half.** A later ruling can relax it to "convert and
clear" and nothing has been lost in the meantime; clearing a credential the Owner did not ask to clear
destroys it, kills every session the account holds, and cannot be undone by a ruling that arrives
afterwards. CLAUDE.md: *"If `spec.md` doesn't answer a business question, stop and ask. Do not
decide."* — this is the safe build plus the question, not the decision.

**What (b) would look like if Karim wants it:** `ChangeRole` calls the existing `ClearPassword`
[Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `ClearPassword`], which nulls the hash and
rotates the stamp — so it would also close `V-26-B`'s production reachability as a side effect. That
is an argument for (b) and it is **not** why (b) should be chosen; `V-26-B` is closed on its own terms
in D-089, so this question is free to be answered on the business merits alone.

#### The durable half: a constraint that was satisfied vacuously by the entire suite

`ChangeUserRoleTests` seeded every user through `User.Create` alone, so **no row in the file held a
`PasswordHash` and `ck_users_subcontractor_cannot_log_in` could not be violated by any case in it.**
The suite was green and the endpoint answered `500`. `MakeUser` now stores a credential unless the
caller asks for one without
[Verified: 2026-08-26 @ `tests/Api.Tests/ChangeUserRoleTests.cs` -> `MakeUser`], and the two `V-26-A`
targets are departmentless `Role.Owner` accounts differing in exactly that.

**The seeded value is a literal, not a `PasswordHasher.Hash` call**, because nothing in that file
verifies it — every request there authenticates through `TestAuthHandler` — and 600,000 PBKDF2
iterations per seeded row for a string nobody reads is cost with no assertion behind it.

**The other suites were surveyed and are not vacuous in the same way.** Four seed no credential at all
— `AssignUserToProjectTests`, `DeactivateUserTests`, `MoveUserDepartmentTests`,
`RevokeProjectAssignmentTests` — and none of them changes a role or writes a credential, so the
constraint is not a rule they claim to cover and leave untested. It is not a general "seed everything"
rule: a credential on a seeded row earns its place where a rule reads it, and nowhere else.

#### Watched red before being trusted

The guard was deleted from `ChangeRole`, the solution rebuilt clean, and the two suites run:
`ChangeRole_refuses_a_subcontractor_conversion_while_a_credential_is_stored` failed on
`changed.IsFailure` being false, and
`Converting_an_account_that_holds_a_credential_into_a_subcontractor_is_refused` failed with
**`Expected HttpStatusCode.Conflict {409} … but found HttpStatusCode.InternalServerError {500}`** —
the Verifier's PROBE-1 reproduced by the suite. Nothing else went red. Restored; Domain **96/96**
(up from 94), Api **211/211** (up from 209).

#### Not done, and named so nobody assumes it exists

* **`ChangeRole` still does not rotate `SecurityStamp`.** Deliberate, D-051 Q27, and unchanged here.
  The session a role change leaves alive is `V-26-B`'s subject and is closed in D-089 at the door
  rather than by rotating a stamp this story rules must not rotate.
* **Nothing catches `DbUpdateException` generically.** Every other check constraint on `users` remains
  unmapped to a `Result`; this fixes the one the Verifier found reachable, not the class. A generic
  translator would be guessing which constraint means which business rule.
* **Q41 is untouched.** Whether a staff account may become a `Role.Client` portal login at all is
  still nobody's decision but Karim's.

---

### D-089 · `V-26-B` and `V-26-C` fixed — what an endpoint outside the gate owes, applied by construction · 2026-08-26

**Backend, defect-fix session.** Second of three. `V-26-B` is HIGH and is a security defect against a
rule Nabil stated absolutely; `V-26-C` is MEDIUM and falls out of the same mechanism.

#### The finding is the category, not the route

`GET /api/auth/me` answered `Role.Subcontractor` with a `200` and their name. spec.md §9: *"record
only, no login."* **Adding the missing check to that one endpoint would have left the hole open**, and
the Verifier said so in as many words: two endpoints are exempt today, the list is designed to grow,
each entry records why it is exempt and none records what it therefore owes.

**What a gated route gets is three things, not two.** `PermissionSubjectReader` establishes
`IsActive` and the security stamp in one `WHERE` clause
[Verified: 2026-08-26 @ `src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`],
and `PermissionEvaluator.Evaluate` refuses `Role.Subcontractor` before the catalogue is consulted at
all [Verified: 2026-08-26 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `Evaluate`].
`StaffSessionMinter.Issue` bars both external roles by construction
[Verified: 2026-08-26 @ `src/Api/Identity/StaffSessionMinter.cs` -> `Issue`]. D-086 and D-087 each
re-applied two of the three by hand on a route the gate does not run on, and dropped the third;
`SignOut` re-applied none.

#### The fix: one mechanism, and declaring the exemption is the same act as paying for it

**`LiveSession`** [Verified: 2026-08-26 @ `src/Api/Authorization/LiveSession.cs` -> `ResolveAsync`] is
the one place the three checks are written. `RequireLiveSession()` applies them in an endpoint filter
**and** stamps the route with `LiveSession.Marker`
[Verified: 2026-08-26 @ `src/Api/Authorization/LiveSession.cs` -> `RequireLiveSession`], and nothing
else adds that metadata. `EndpointPermissionCoverageTests.IsSelfOnlyListed` exempts a route only when
it is both named on the list **and** carries the marker
[Verified: 2026-08-26 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `IsSelfOnlyListed`], so
a new self-only endpoint that skips the checks is not exempt at all — it falls through to
`Every_mapped_endpoint_carries_a_permission_requirement` as an ungated route, D-067's own failure.

**The anonymous half cannot take a refusing filter, and is covered by a different assertion.**
Sign-out must answer `204` to a caller holding no session (KAFF-102 rule 7), so it calls
`LiveSession.ResolveAsync` directly and writes its audit row only on a live answer
[Verified: 2026-08-26 @ `src/Api/Features/Auth/SignOut/Handler.cs` -> `HandleAsync`]. What is checkable
there is the hand-roll itself: all three defective handlers each carried a private
`ReadUserId(ClaimsPrincipal)` over `KaffClaimTypes.UserId`, so
`No_feature_handler_reads_the_callers_identity_from_the_token_itself`
[Verified: 2026-08-26 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
`No_feature_handler_reads_the_callers_identity_from_the_token_itself`] fails when any file under
`src/Api/Features/` names a claim type. **Its ceiling is named rather than hidden:** a handler could
still load its own caller's row through `ICurrentUser.UserId` without naming a claim. That is the
reviewer's, and it is not the shape any of the three defects had.

**The role bar itself moved to Domain** as `StaffSessionRules.MayHoldStaffSession`
[Verified: 2026-08-26 @ `src/Domain/Identity/Role.cs` -> `MayHoldStaffSession`], now called by
`StaffSessionMinter.Issue`, `SignIn.Handler` and `LiveSession` — CLAUDE.md, *"if two features need the
same thing, it moves to `Domain/`"*. **It deliberately does not replace `PermissionEvaluator`'s bar**,
which is a narrower statement: the evaluator refuses `Role.Subcontractor` a permission and says nothing
about `Role.Client`, because a client legitimately holds `PortalRead` and `PortalApprove` on their own
project (D-035) — through the portal door, when it ships. This predicate is about the staff session.

#### The refusal shape did not move, and must not

Every failure here answers `403` / `errors.auth.forbidden`, the blanket pair of D-071 and D-080.
`AuthorizationErrors.RoleCannotLogIn` is **not** used, and `SpecificRefusal` (D-086) is **not** used —
Nabil: *"If we return a specific `errors.auth.role_cannot_log_in`, we are explicitly telling the
attacker: 'This account exists and belongs to a subcontractor.' That is a security breach."* Asserted
both ways: the body must contain `errors.auth.forbidden` and must not contain `role_cannot_log_in` or
the role's name [Verified: 2026-08-26 @ `tests/Api.Tests/MeTests.cs` ->
`A_subcontractor_session_is_refused_not_answered_with_a_profile`].

#### `V-26-C` is covered by construction, not by a second fix

Sign-out asks `LiveSession` the same question every other exempt route asks. A cookie the global kill
of D-053 already ended gets the same `204` and the same cleared cookie — rule 7 unchanged, nothing
disclosed — and writes no row into a table that is append-only and trigger-protected, where a wrong
row can never be corrected by anyone
[Verified: 2026-08-26 @ `tests/Api.Tests/SignOutTests.cs` ->
`A_cookie_the_global_kill_already_ended_writes_no_audit_row`].

#### ⚠️ A correction to the Verifier's report: `V-26-B`'s production reachability is overstated

The report says the subcontractor half is *"reachable in production, through KAFF-109"*, because
`ChangeRole` does not rotate the stamp. **Re-derived against the files, that path does not close.**
Holding a live staff session means having signed in, which means holding a credential; a credential is
exactly what blocks the conversion (a `500` before D-088, a `409` after it); and the only way to remove
it — `User.ClearPassword` — rotates the stamp
[Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `ClearPassword`], which kills the session.
**Both halves of `V-26-B` need a hand-issued identity today**, exactly as the report already concedes
for `Role.Client` in its §8. Its own PROBE-2 is consistent with this: that account converted with a
`200`, which after D-088 means it held no credential, which means its session was not obtained by
signing in.

**This changes nothing about the fix and is recorded because the reasoning is what a later session
inherits.** The defect is real and was demonstrated: the endpoint answers a subcontractor with a `200`
and their name. The bar belongs there because *"no staff session exists for this role"* is a property
of the door — the argument `StaffSessionMinter` already makes for itself — and not because a path to
it happens to be open today. That is the D-082 §4 mistake, and this session declines to repeat it in
the opposite direction.

#### 🟡 Two things for Nabil, both changes to what an accepted criterion is proved against

1. **`AC-102-F`'s audit half is reversed.** `A_client_role_session_can_sign_out_too` asserted a
   `SignedOut` row with `ActorRole == Role.Client`; it now asserts the `204`, the cleared cookie, and
   **no** row [Verified: 2026-08-26 @ `tests/Api.Tests/SignOutTests.cs` ->
   `A_client_role_session_can_sign_out_too`]. The criterion's own text — a portal user can sign out —
   is unchanged and still passes. The alternative was a per-route list of which of the three checks
   each exempt endpoint owes, which is the hand-copy that produced `V-26-B`.
2. **`GET /api/auth/me` now refuses `Role.Client`.** `AC-105a-H`'s substance is untouched and is proved
   where it is a fact about the rule rather than about this route
   [Verified: 2026-08-26 @ `tests/Domain.Tests/PermissionEvaluatorTests.cs` ->
   `A_client_holds_no_company_wide_permission`]. When the portal door of D-051 Q33 ships,
   whether it reuses this endpoint is that story's question — it must widen one line in Domain
   deliberately, which is the point of it being one line.

#### Watched red — four mutations, each reverted, each naming what it proves

| Mutation | Red | What it proves |
|---|---|---|
| Delete `MayHoldStaffSession()` from `LiveSession.ResolveAsync` | `MeTests` -> `A_subcontractor_session_is_refused_not_answered_with_a_profile` and `A_hand_minted_portal_client_session_is_refused_by_the_staff_door` (**both `200`** — the Verifier's PROBE-4 and PROBE-5 reproduced), plus `SignOutTests` -> `A_client_role_session_can_sign_out_too` | The role bar is live, on both roles and on both route shapes |
| Delete `.RequireLiveSession()` from `WhoAmI.Endpoint` | `Every_mapped_endpoint_carries_a_permission_requirement` naming `GET /api/auth/me` **and** `Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own` on the null marker | A self-only route that skips the checks is not exempt — it is ungated, which is D-067's own failure |
| Reintroduce a `KaffClaimTypes` reference in a feature handler | `No_feature_handler_reads_the_callers_identity_from_the_token_itself` naming the file | The hand-roll that produced all three defects cannot come back quietly |
| Delete `IsActive` and the stamp comparison from `ResolveAsync` | `SignOutTests` -> `A_cookie_the_global_kill_already_ended_writes_no_audit_row` (**2 rows, expected 1** — PROBE-3 reproduced), `MeTests` -> both freshness cases, `ChangePasswordTests` -> `A_deactivated_account_cannot_change_its_own_password` | The two checks D-087 did copy are still live, and now cover sign-out too |

#### Verified

Clean `--no-incremental` Release build, `-warnaserror`: **0 warnings / 0 errors**.
`dotnet format KaffErp.sln --verify-no-changes` exit 0. Domain **96/96**, Api **214/214** — three new
in `EndpointPermissionCoverageTests`, `MeTests` and `SignOutTests`.

#### Not done, and named so nobody assumes it exists

* **`ChangeRole` still does not rotate the stamp**, and this entry does not propose that it should.
  D-051 Q27 rules that a role change takes effect on the next request through the gate's re-read, not
  by ending the session; the door is the right place for the bar.
* **No new i18n key.** Every refusal here is `errors.auth.forbidden`, which both catalogues carry.
* **`AllowList` members other than sign-out were checked and owe nothing.** `GET /api/health`,
  `GET /api/setup` and `POST /api/setup` never read the caller's identity, and `POST /api/auth/sign-in`
  discards it before doing anything [Verified: 2026-08-26 @ `src/Api/Features/Auth/SignIn/Handler.cs`
  -> `HandleAsync`]. Sign-in's own role bar is the shared predicate now, in the same statement and the
  same position — the ordering D-072 §1 turns on did not move.
* **`V-26-G` is untouched.** `TC-1-042` lives under `qa/`, which this session must not edit; it is the
  Scrum Master's per SM-30, and `V-26-B`'s fix makes it more wrong, not less — the endpoint now refuses
  the caller that case describes.

---

### D-090 · `V-26-F` fixed — the statement whose position is the whole of the safety · 2026-08-26

**Backend, defect-fix session.** Third of three, and the smallest: two tests and no production change.

#### What was unpinned

D-086 built `SpecificRefusal` so the shell can tell *"you must change your password"* apart from an
ordinary refusal, carrying `errors.auth.password_change_required` past the blanket `401`/`403` that
D-071 and D-080 give every other gate refusal. **It is safe for exactly one reason, and it is not the
reason its own remarks give.** They argue from *what is disclosed*; the actual guarantee is *where the
check sits*. `PermissionEvaluator.Evaluate` returns `PasswordChangeRequired` **before**
`PermissionCatalogue` is consulted at all
[Verified: 2026-08-26 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `Evaluate`], so a caller
receiving that key learns nothing about whether they hold the permission — the evaluator never looked.

Move it below the grant match and the same key becomes a *"you would have been allowed"* oracle on
every endpoint in the system: the axis disclosure D-080 declined to make, arriving through a
`messageKey` instead of a status code, **changing no status code on the way**. Nothing in the suite
pinned it. `AC-101a-P` has the identical shape and has `TC-1-258`; this had nothing.

#### Pinned at both levels, because the leak is observable at both

* **The rule, as a pure function** [Verified: 2026-08-26 @
  `tests/Domain.Tests/PermissionEvaluatorTests.cs` ->
  `The_password_change_refusal_is_identical_for_a_caller_who_holds_the_permission_and_one_who_does_not`]:
  an Owner (holds `UserManage` company-wide, unconditionally) and a Finance user (holds no grant on it
  at all), both with the flag set, must receive the **same** decision. The non-holder's lack of the
  grant is asserted separately with the flag off, so the comparison cannot pass vacuously.
* **The wire** [Verified: 2026-08-26 @ `tests/Api.Tests/ChangePasswordTests.cs` ->
  `The_forced_change_refusal_is_the_same_for_a_caller_who_holds_the_permission_and_one_who_does_not`]:
  the same two roles against `/probe/company` (`Permission.ClientManage`, granted to `Role.Owner` and
  `Role.MarketingSales` and nobody else) must get the same status **and** the same `messageKey`. This
  is the oracle a caller would actually use.

#### The test that was already there does not prove what its name says

`A_caller_who_must_change_their_password_is_refused_before_the_catalogue_is_consulted`
[Verified: 2026-08-26 @ `tests/Domain.Tests/PermissionEvaluatorTests.cs` ->
`A_caller_who_must_change_their_password_is_refused_before_the_catalogue_is_consulted`] uses a subject
that **holds** the permission, so the swap leaves it green — measured, not reasoned. It catches the
check being *deleted*, which is worth having. A one-paragraph remark now says so on the test itself,
rather than leaving the next reader to inherit the name as evidence.

#### Watched red

Both `MustChangePassword` blocks were moved below the catalogue lookup and the grant match in
`PermissionEvaluator.Evaluate`'s two overloads; the solution rebuilt clean. **Exactly two tests went
red — the two above, and nothing else in either suite** (Domain 96/97 with the new one, Api's
`ChangePasswordTests` 8/9). The Domain failure reads `PermissionDecision.RoleNotGranted` where
`PasswordChangeRequired` is required, and the Api failure reads two `messageKey`s differing at index
12 — `errors.auth.forbidden` against `errors.auth.password_change_required`, the leak itself. Reverted.

**That "nothing else went red" is the finding, restated as a measurement.** The swap changes no status
code, breaks no assertion about which error a permission holder receives, and reintroduces the axis
disclosure.

#### Not done

* **`SpecificRefusal`'s own remarks still argue from disclosure rather than from position.** They are
  not wrong, they are incomplete, and D-086 §`SpecificRefusal` plus this entry are the record. No
  source change: the guarantee now has a test, which is what it was missing.
* **No production code changed in this defect.** The ordering was already correct.

---

### D-091 · KAFF-101b — the first screen, and the conventions it sets · 2026-08-28

**Nothing had ever rendered before this.** Every choice here becomes the pattern later screens copy,
so they are recorded rather than left to be inferred from one file.

**Signal forms, not signals with `(input)` handlers.** The first version used plain signals and two
event handlers — smaller for two fields, and wrong as a precedent. **Nabil overturned it on
2026-08-28**, and he was right: CLAUDE.md mandates signal forms, this is the file every later screen
will be read against, and "it was fewer lines for this one case" is exactly how a codebase ends up
mixing Angular eras. The form is `form(model, schema)` from `@angular/forms/signals`, bound through
the `FormField` directive (`[formField]`), with validity and submitting read from
`loginForm().valid()` and `loginForm().submitting()`.

**The API was verified against the installed types, not written from memory.** `@angular/forms`
v22.1.2's own declarations gave `form`, `schema`, `submit`, `required`, `minLength` and the directive's
real selector. Worth keeping as a habit: the signal-forms surface is new enough that a plausible
guess compiles into something else entirely, or not at all.

**The whole password policy is two lines of schema.** `required` and `minLength(8)` — no `pattern`, no
strength meter, D-049 ruling 3. Expressing it as a schema puts `AC-101b-E`'s "and nothing else" in one
place a reviewer can check, instead of spread across a template attribute and a `computed`.

**⚠️ The server's refusal is held beside the form, never as a field error.** A field-level error
renders next to the input it belongs to, and *that placement is itself an answer*: "wrong password"
under the password box says the user name was found. That is precisely the distinction D-065 and
D-072 §1 refuse to make, and Nabil's reasoning on the subcontractor case applies unchanged. One
page-level message, one key from the server, no field named, and no `switch` on status anywhere in
the component. Signal forms make attaching it to a field the easy thing to do, which is why this is
written down rather than left to taste.

**`$any` stays out of templates.** `[formField]` binds value and events both ways, so there is no
`[value]`/`(input)` pair and no cast at all — strictly better than the first version, which only
moved the cast into TypeScript where the compiler could still see it.

**Two rules are implemented as a held message rather than a redirect, and that is deliberate.**
Rule 8 sends a `mustChangePassword` user to the change-password screen; **that screen is KAFF-103's
`AC-103-I` and does not exist.** Navigating to `/change-password` today falls through
`app.routes.ts`'s wildcard onto the landing page — a user with an Owner-set password inside the
application, the one outcome rule 8 exists to prevent, reached by code that reads as correct. The
screen holds them with the server's own `messageKey` instead. The server is what actually stops them
(D-086 put the check inside `PermissionEvaluator`); this is only where they are told.

**The wildcard route is now the hazard to remove.** It makes an unbuilt route indistinguishable from
the landing page. When KAFF-103's screen and KAFF-105b's shell land, `path: '**'` should become a 404
rather than a redirect, so a missing route fails loudly. Noted in `app.routes.ts` at the wildcard.

**Deferred, and to which story:** `AC-101b-A` and `AC-101b-D` (the staff shell and HR's Project Team
landing) move with **KAFF-105b** and **KAFF-115**; `AC-101b-F` moves with **KAFF-103**. Everyone
currently lands on `/`, and that is not a decision that HR and Finance share a landing page — it is
the statement that there is one page.

**Verified by running it, not by building it.** Production build clean (250.69 kB initial, 0 errors,
0 warnings); `smoke` all eight checks; the screen driven end to end against the live stack — Owner
bootstrapped through `POST /api/setup`, a wrong password refused with
`اسم المستخدم أو كلمة المرور غير صحيحة.`, the correct one landing on `/` with the cookie carrying the
session; screenshot at **390px in Arabic**, RTL, `scrollWidth - clientWidth = 0`.

**The driver gained a width argument** — `shot <url> <out.png> [width]`. It was fixed at 1280, and
CLAUDE.md requires testing at mobile width: an RTL row that fits on a desktop and overflows on a phone
looks correct in every screenshot taken at the default.

**One thing found while driving it, not fixed here and routed to Backend:** a malformed JSON body to
`POST /api/setup` returns **500**, not 400. `BadHttpRequestException` from the body reader escapes the
handler, so a client bug is reported as a server fault and lands in the log as an unhandled exception.
Reproduced deliberately; the endpoint's own behaviour on well-formed input is unaffected.

---

### D-092 · KAFF-103's screen built, and `AC-101b-F` closed with it · 2026-08-29

**Frontend.** The change-password screen `AC-103-I` names, plus `AC-101b-F` — the forced-change reach
question D-091 deferred here — in the same session, because the screen is what `AC-101b-F` needed to
redirect to.

#### What was built

`ChangePasswordPage` [@ `src/Web/src/app/features/auth/change-password/change-password-page.ts`]
follows D-091's conventions exactly rather than re-deriving them: signal forms
(`form`/`schema`/`submit` from `@angular/forms/signals`), `FormField` binding with no `$any`, one
page-level refusal region, and the same `messageKey`-only rendering discipline `sign-in-page.ts`
established. Three fields — current password, new password, confirm — where `confirmPassword` is a
client-side-only cross-field check via `validate()` in the schema
[@ `change-password-page.ts` -> `changePasswordSchema`] and never reaches the network: the request
body is exactly `{ currentPassword, newPassword }`, matching `ChangePassword.Request` on the API side.
The same shape the setup screen's own confirm field already established (`CreateOwner.Request`'s own
remark that the server never sees a second copy to compare) — not invented here, followed.

`AuthApi.changePassword` [@ `src/Web/src/app/core/auth/auth.api.ts` -> `changePassword`] is the one
new HTTP call, added beside `signIn` and `me` rather than putting `HttpClient` into `AuthService` —
that class still holds no credential and no HTTP, per D-050.

`mustChangePasswordGuard` [@ `src/Web/src/app/core/auth/must-change-password.guard.ts`] is
`AC-101b-F`'s reload half: a `CanActivateFn` on the landing route (`''` in `app.routes.ts`) that
resolves the session via `GET /api/auth/me` when nothing has asked yet, and redirects to
`/change-password` when the flag is set. **Written as convenience throughout, not security** — the
guard's own doc comment says so, and CLAUDE.md's line is quoted in it. Nothing here is the
enforcement; D-086's `PermissionEvaluator` check already refuses a `mustChangePassword` session on
every permission-gated route by construction, guard or no guard.

`sign-in-page.ts`'s hold is now a real redirect
[@ `src/Web/src/app/features/auth/sign-in/sign-in-page.ts` -> `navigateByUrl`]: the `⚠️` block D-091
wrote — "there is nowhere to redirect to" — is gone, because there now is. The server-side
enforcement this redirect merely announces is unchanged.

Nine new i18n keys, both catalogues: `auth.password.title`, `.must_change`, `.rule_min_length`,
`.hint.ends_other_sessions`, `.mismatch`, `.changed`; `auth.field.current_password`, `.new_password`,
`.confirm_password`; `action.save`. All under namespaces the frontend owns (`ux/rtl-and-i18n.md` hard
rule 1 reserves `errors.*` for the backend) — `auth.password.mismatch` is new relative to the story's
own list, because that list predates a confirm-password field existing at all; it names a purely
client-side condition the server never sees, the same reasoning that keeps `confirmPassword` off the
wire in the first place.

#### The wildcard route — left as a redirect, and this is the check D-091 asked for

D-091 named two conditions together: *"when KAFF-103's screen and KAFF-105b's shell arrive"*. Only the
first is true after this session. Flipping `path: '**'` to a 404 now would fail loudly on a typo'd
route today, at the cost of failing loudly on every URL that will legitimately exist once KAFF-105b's
shell ships and does not yet. That trade is not an improvement over the redirect it would replace —
it moves the same hazard from one direction to the other rather than closing it — so the wildcard is
unchanged, and the comment in `app.routes.ts` now explains why in the present tense rather than
gesturing at both stories landing together.

#### Verified, and what could not be

Angular production build: clean, 0 errors, 0 warnings, `change-password-page` its own lazy chunk
(5.42 kB raw). `/run-kaff-erp` smoke: **8/8**, run against the API started from its existing Release
binary with `Kaff__ApplyMigrationsOnStartup=false` — **not a rebuild**, deliberately: a Backend agent
was concurrently editing `src/Infrastructure/Persistence/DatabaseInitializer.cs`,
`IdentityConfigurations.cs` and `tests/Api.Tests/SchemaInvariantTests.cs`, and the checked-in binary
already on disk has a model the current migration history does not match — `dotnet build` or
`SchemaStrategy.Migrate` against it throws `PendingModelChangesWarning` (reproduced, not this
session's defect). Overriding the migrate-on-startup flag runs `ApplyGuardsAsync` instead, against the
schema the long-lived `kaff-db` container already has, and starts clean. The API process was stopped
again afterward so it does not hold `Kaff.Domain.dll`/`Kaff.Infrastructure.dll` against the other
agent's build (the SKILL.md gotcha this project has already paid for once).

`check-citations.ps1`: **918 checked** (meets the 915+ target), **0 broken, 0 legacy**. A first run
mid-session reported 4 broken — all four the same pre-existing citation of
`ck_users_subcontractor_cannot_log_in` in
`src/Infrastructure/Persistence/Configurations/IdentityConfigurations.cs`, read while the concurrent
Backend session had that exact file open. Neither the file nor the citations belong to this session;
a re-run once this entry was written found the identifier present again and the count clean. Left
untouched throughout, per the boundary this session was given (`src/Web/` and this file only).

**Live end-to-end drive, completed once the credential blocker cleared.** `GET /api/setup` reporting
`available: false` was not a stale artefact — `nabil`/the Owner created while verifying KAFF-101b on
2026-08-28 is a real, working credential on this local, disposable `kaff-db`. A raw `INSERT` was never
needed: the forced-change user was created **through the application**, the same route production
uses — signed in as `nabil` through this session's own sign-in screen, then `POST /api/users`
(KAFF-106) called from that signed-in page with `temporaryPassword` set, exercising the create-user
endpoint as a side effect rather than writing a row behind its back. Driven with a scratch CDP script
(not committed — one Chrome tab held open for the whole sequence, since `driver.mjs` launches a fresh
browser per command and would drop the session cookie between steps):

1. Signed in as `nabil` through `/sign-in` — landed on `/`.
2. `POST /api/users` from that session — `201`, `MarketingSales`/`Marketing`, `mustChangePassword: true`.
3. `POST /api/auth/sign-out` — `204` — then signed in as the new user through `/sign-in`.
4. **Redirected to `/change-password`** — `AC-101b-F`'s in-session half, `sign-in-page.ts`'s new
   `navigateByUrl`.
5. **A fresh top-level navigation to `/` — a cold reload with no in-memory `AuthService` state —
   redirected back to `/change-password`.** This is the half the sign-in redirect alone cannot prove:
   `mustChangePasswordGuard` resolved the session itself via `GET /api/auth/me` and returned the
   `UrlTree`, exactly as designed.
6. A hard reload of `/change-password` itself stayed put and re-showed the must-change banner —
   confirming the component's own constructor fetch (for callers who land here without the guard
   having run first) — after allowing for the fetch's own round trip; the first pass checked the
   banner before that promise resolved and read as absent, corrected by polling rather than by
   changing the component.
7. Submitted current/new/confirm, landed on `/`, and `GET /api/auth/me` confirmed
   `mustChangePassword: false` — `AC-103-A`/`AC-103-F`'s observable effect.

**Screenshot taken at step 4**, 390×844, Arabic, RTL — looked at directly. Title
"تغيير كلمة المرور", the forced-change banner, three labelled fields (current / new / confirm) each
right-aligned with the input below, the 8-character hint and the ends-other-sessions hint both
present, one disabled "حفظ" button (form empty at that point — `canSubmit()` correctly false), no
horizontal overflow, no untranslated key visible anywhere on the page.

Two disposable rows now exist on `kaff-db` as a result (`qa.kaff103`, superseded mid-session by
`qa.kaff103b` once a banner-timing question in the *test script* — not the app — needed a fresh
forced-change session to re-check against); left in place, this being confirmed as a free-to-use local
dev database.

#### Criteria, and how each is covered — now observed, not only reviewed

| Criterion | Covered by | Observed |
|---|---|---|
| `AC-103-D` — current password required | `changePasswordSchema`'s `required(path.currentPassword)`; it is the first field, not a third "new password" box | Partially observed: the field exists, is first, and the correct value (`temp1234`) was accepted. **Not observed:** submitting a missing or wrong current password and watching the refusal render — the drive only took the happy path. Code-reviewed only for that half (`Handler.cs`'s `PasswordHasher.Verify` check, `AuthorizationErrors.CurrentPasswordIncorrect` rendered by the same page-level `refusalKey` region `sign-in-page.ts` uses) |
| `AC-103-E` — 8 characters, nothing more | `minLength(path.newPassword, MINIMUM_PASSWORD_LENGTH)` and `required` — no `pattern`, no strength meter | Partially observed: `temp1234` and `NewPass123` (8 and 10 chars) both accepted with no complexity prompted anywhere in the UI. **Not observed:** a 7-character password actually refused (client-side block or server's `password_too_short`) — not tried during the drive. Code-reviewed only for that half |
| `AC-103-F` — ends every other session | `SetOwnPassword`'s stamp rotation (D-086, tested there — `ChangePasswordTests.cs`) does the ending; not re-tested here | **Not observed this session** — only one device was driven, so there was no second session to watch get refused. What *was* observed: this device's own session survived its own change (landed on `/`, `mustChangePassword: false` on the next `GET /api/auth/me`), which is `AC-103-A`, a different half |
| `AC-103-I` — Arabic, RTL, 390px | `change-password-page.css` — logical properties only | Screenshot at 390px, looked at: RTL, Arabic, no overflow |
| `AC-101b-F` — nothing else reachable, reload returns here | `mustChangePasswordGuard` on the landing route, plus the real redirect in `sign-in-page.ts` | Both the in-session redirect (step 4) and the cold-reload redirect via the guard (step 5) observed directly |

Refusals: one page-level region, `role="alert"`, no field named, no `switch` on status — the same
shape `sign-in-page.html` uses, for the same reason (D-091, D-065, D-072 §1: a field-level error on a
two-password-field form says which field is wrong).

---

### D-093 · `V-27-A` fixed — a required list that cannot be edited by the edit it is guarding against · 2026-08-29

**Backend. `V-27-A`, both halves.** qa/slice-1/verification-2026-08-27.md §2 and §5.

**What was wrong, and it was not the constraint.** `ck_users_subcontractor_cannot_log_in` was covered
by nothing — delete it from `IdentityConfigurations` and both suites stayed green. That is the first
half and it is the smaller one. The second half is that **the mechanism built to notice exactly this
could not**: `FindMissingGuardsAsync` derived its required check-constraint list from the EF model
(D-064), so deleting `HasCheckConstraint` deleted the expectation in the same edit. `missingGuards`
stayed `[]`, `/api/health` went on reporting `guardsInstalled`, `smoke` went on passing, and **D-033's
refusal to start cannot fire for a guard the model no longer declares.**

**D-064 was not wrong; it was one-directional.** Its reasoning — *"a hand-written list of 28 names is
a list somebody forgets to extend"* — is true, and the derived list genuinely catches the case it was
built for: a **database** that drifted from the model. What it cannot catch is a **model** that lost a
rule. The triggers, which D-064 left as a hand-written list and whose comment worries about exactly
that, are the half that works: `MUT-G4` showed removing one stops the host booting.

**Decision. Both lists, and a test that they agree.**

1. `DatabaseInitializer.RequiredCheckConstraints` — all **30** names, written out, grouped by the
   configuration file that declares them
   [Verified: 2026-08-29 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` ->
   `RequiredCheckConstraints`].
2. `FindMissingGuardsAsync` requires the **union** of that list and the model's, so a name in either
   and absent from the database is a missing guard
   [Verified: 2026-08-29 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` ->
   `FindMissingGuardsAsync`].
3. `ModelCheckConstraints` exposes the derived half so a test can compare the two
   [Verified: 2026-08-29 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` ->
   `ModelCheckConstraints`].
4. `The_written_out_check_constraints_and_the_model_agree` fails in **both** directions — a constraint
   in the model and not in the list is D-064's forget-to-extend; a constraint in the list and not in
   the model is `V-27-A`
   [Verified: 2026-08-29 @ `tests/Api.Tests/SchemaInvariantTests.cs` ->
   `The_written_out_check_constraints_and_the_model_agree`].
5. `Thirty_check_constraints_are_required` states the count, because deleting a rule from **both**
   places satisfies (4) and the deliberate two-file act should still be loud
   [Verified: 2026-08-29 @ `tests/Api.Tests/SchemaInvariantTests.cs` ->
   `Thirty_check_constraints_are_required`].

**Watched failing, and the reading is the point.** `MUT-A` re-applied — the four lines deleted from
`IdentityConfigurations` exactly as the Verifier deleted them. Before: `97/97`, `215/215`, nothing
red. After this change: **`180 of 217` failed**, and 178 of them fail with

```
System.InvalidOperationException : Refusing to start: database guards are missing —
ck_users_subcontractor_cannot_log_in.
```

**The host does not boot.** That is the trigger-class coverage the check constraints did not have, and
it is what `MUT-G4` produced for `trg_postings_append_only`. The other two are the new tests, failing
on their own terms. Reverted; `git status` clean; `217/217` and `97/97` restored.

**How many of the 30 are covered now: 30.** Not by 30 behavioural tests — by one mechanism that does
not depend on which of them somebody happened to write a test for. The Verifier sampled four and found
one covered, one covered by accident of hard-coded naming, two not at all; that distribution no longer
decides anything. `ck_postings_amount_positive`, `ck_postings_distinct_accounts` and
`ck_postings_not_self_reversing` — the slice-3 money rules §5 names — are three of the thirty.

**What this does not do.** It does not verify the constraint's *expression*, only its name. A
migration that keeps `ck_postings_amount_positive` and changes its predicate to `amount >= 0` passes
every check here. That is a real gap and a different, larger mechanism (D-064's "Not done" paragraph
already scopes the schema-wide comparison); recorded rather than built, because nothing has asked for
it and the name-level gap was the one that was live.

**Revisit if.** A slice adds check constraints — the count in (5) moves in the same commit, which is
the intended friction.

---

### D-094 · `V-27-B` fixed — the marker is now unforgeable, and the test no longer explains how to forge it · 2026-08-29

**Backend. `V-27-B`, closed at the compiler.** qa/slice-1/verification-2026-08-27.md §3.

**What was wrong.** D-089 claims `RequireLiveSession()` applies the three checks *"by construction"*,
because it stamps the route with `LiveSession.Marker` and *"nothing else adds that metadata."*
`Marker` was `public` with an `internal static readonly Instance`, and **every feature slice compiles
into `Kaff.Api`** — so `internal` named exactly the place endpoints are written. Writing
`.WithMetadata(LiveSession.Marker.Instance)` in place of `.RequireLiveSession()` compiled, and the
suite reported **215 / 215** against a route reachable by any authenticated caller, acting on the
caller's own row, applying none of the three checks. **The guarantee was conventional, not
structural.**

**And the failing test was the instruction manual.** An author adding a self-only route saw a message
saying `RequireLiveSession()` *"is the only thing that adds this metadata"*. That sentence was false,
`Instance` was one dot away in the same assembly, and attaching it turned the red test green while
applying nothing — D-046's green light inside the mechanism written to prevent it.

**Decision. The type is private; the question is public.**

* `Marker` is a **private nested type** of `LiveSession`. A private nested type cannot be **named**
  from outside its containing class, so `RequireLiveSession` is now the only *ordinary* expression
  that can produce this metadata
  [Verified: 2026-08-29 @ `src/Api/Authorization/LiveSession.cs` -> `RequireLiveSession`].
  **⚠️ Amended 2026-09-01, decisions.md D-098: this sentence overstated what `CS0122` proves.**
  Reflection still constructs the type from outside the class — see D-098.
* `LiveSession.IsApplied(Endpoint)` is the read half — a test can ask whether a route paid, and is
  not handed the means to answer dishonestly
  [Verified: 2026-08-29 @ `src/Api/Authorization/LiveSession.cs` -> `IsApplied`].
* The two test call sites ask through it rather than through `GetMetadata<Marker>()`
  [Verified: 2026-08-29 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `IsSelfOnlyListed`].
* **The message is corrected**, and now says what is true: add `.RequireLiveSession()`, there is no
  other way to satisfy this, the metadata is a private nested type
  [Verified: 2026-08-29 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
  `Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own`].

**Watched failing, twice, in the two different ways this can now break.**

1. **`MUT-E` re-applied first**, to establish the defect rather than take the Verifier's word:
   `src/Api/Features/Auth/VerifierProbe/` with `.WithMetadata(LiveSession.Marker.Instance)` and no
   checks, named in `SelfOnlyEndpoints`. **Built clean, and `EndpointPermissionCoverageTests` reported
   6 / 6.** With the fix applied and the same probe unchanged, the build fails:
   `error CS0122: 'LiveSession.Marker' is inaccessible due to its protection level`. **The probe was
   then deleted** — an unpaid exemption must not exist in this repository even as a fixture.
2. **The accessibility itself is pinned.** A compiler error is evidence only while the accessibility
   stands, and widening `private` to `internal` is a one-word edit. Done: the build stays clean and
   `LiveSession_exposes_no_metadata_type_wider_than_private` (renamed 2026-09-01, D-098 — it was
   `Nothing_outside_LiveSession_can_produce_the_metadata_that_proves_a_route_paid`) fails with
   `found at least one item {"Marker"}`
   [Verified: 2026-08-29 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
   `LiveSession_exposes_no_metadata_type_wider_than_private`]. Restored;
   `218 / 218`.

**What is still true and is not closed by this.** The Verifier's §3.1 gap stands unchanged: a handler
can still resolve its caller through `ICurrentUser.UserId` rather than a claim type, and
`POST /api/auth/sign-out` is `AllowAnonymous` and can carry no refusing filter, so one source grep
remains the whole mechanical cover for that route. This entry narrows who can *claim* the exemption,
not who can *hand-roll around* it.

**Revisit if.** A second assembly ever needs to declare a self-only route. `private` would then be too
narrow, and the answer is not `internal` — it is that `RequireLiveSession` moves with it.

---

### D-095 · `V-27-C` fixed — a role that is not a role, and two predicates that failed open · 2026-08-29

**Backend. `V-27-C`.** qa/slice-1/verification-2026-08-27.md §6.

**What was wrong, in two layers.** `PUT /api/users/{userId}/role` answered **`200`** to `-1`, `0` and
`99` and persisted them — the Verifier read `role = '99'` back out of the users table. Reproduced here
before anything was changed, as three theory cases, all three green against HEAD.

**A C# enum is not a closed set at run time.** `(Role)99` is a legal cast, enums are stored as text
(D-002) so the column takes whatever arrives, `JsonStringEnumConverter` accepts integers, and no
check constraint refuses it: `ck_users_client_scope` and `ck_users_operations_sub_department` are both
satisfied by a value that is neither `'Client'` nor `'Operations'`.

**The second layer is the one that mattered.** Both role predicates were **deny-lists**, so they
answered *permitted* for every value outside the nine:

* `MayHoldStaffSession` was `role is not (Role.Client or Role.Subcontractor)` — so `(Role)99` **may
  hold a staff session**, and `GET /api/auth/me` would answer it.
* `PermissionEvaluator` barred `subject.Role == Role.Subcontractor` — same shape, so `(Role)99`
  reached the catalogue.

Neither is wrong for the nine roles that exist. Both are the wrong default for a predicate whose whole
job is to refuse, and **an enum member added later would be admitted by silence.**

**Decision.**

1. **Validation in the domain, at the join both entry points already use.** `ValidateDepartment` is
   called by `User.Create` **and** `User.ChangeRole`, so `!Enum.IsDefined(role)` sits there and covers
   both — a validator in the `ChangeUserRole` slice would have left `CreateUser` open
   [Verified: 2026-08-29 @ `src/Domain/Identity/User.cs` -> `ValidateDepartment`]. New error
   `IdentityErrors.UnknownRole`, `errors.identity.unknown_role`, `400`
   [Verified: 2026-08-29 @ `src/Domain/Identity/IdentityErrors.cs` -> `UnknownRole`].
2. **Both predicates inverted to allow-lists**
   [Verified: 2026-08-29 @ `src/Domain/Identity/Role.cs` -> `MayHoldStaffSession`;
   @ `src/Domain/Identity/Role.cs` -> `MayHoldPermissions`], and the evaluator asks the second one
   [Verified: 2026-08-29 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `Evaluate`].
3. **Two lists, not one with an exception.** They differ by exactly `Role.Client`, who holds
   `PortalRead` and `PortalApprove` on their own project (spec.md §12, D-035) and must therefore be
   grantable by the evaluator while being refused by the staff door (D-062 §2). Folding them together
   is the D-035 shape.
4. **`Enum.IsDefined` is deliberately not the predicate.** It answers "is this a member", which admits
   a tenth role by silence — exactly the failure being fixed. It is right for *validation* (1), where
   a new member genuinely is a role, and wrong for a *door* (2).

**Watched failing, three times.**

| Mutation | Result |
|---|---|
| Nothing — the reproduction, before any fix | `A_role_outside_the_enum_is_refused_and_never_persisted` red for `-1`, `0` and `99`, each `found True` on `IsSuccessStatusCode` |
| `MayHoldStaffSession` restored to the deny-list | `A_role_outside_the_enum_is_refused_at_every_door` red — *"Expected unknown.MayHoldStaffSession() to be False … but found True"* |
| Both restored, after the fix | build clean, Domain `107/107`, Api `221/221` |

**The §7 gap this also closes.** The Verifier observed that `MUT-H` — `MayHoldStaffSession` made to
answer true for every role — left the **Domain** suite at 97/97, because the rule lives in `Domain/`
and was covered entirely from the Api suite: *"the cheapest possible test of the predicate itself does
not exist."* It does now, as a nine-row table
[Verified: 2026-08-29 @ `tests/Domain.Tests/UserTests.cs` -> `The_two_role_doors_admit_exactly_these`].

**One locale key added, and it is the error-catalogue contract, not frontend work.**
`errors.identity.unknown_role` in `ar.json` and `en.json` — one line each, nothing else in
`src/Web/` touched. `TranslationCatalogueTests` fails the build for a domain error whose key is absent
from either catalogue, so the key is not optional.

**🟡 Not decided here, and routed rather than assumed.**

* **What counts as a valid role is spec.md §9's to say.** This refuses a value that names *no* role;
  it does not add, remove or reinterpret one. If Karim adds a tenth, `MayHoldStaffSession` must be
  edited to admit it — deliberately, which is the point.
* **The existing `role = '99'` row on the Verifier's database is not migrated.** No data fix ships
  here: this is a development database, the row was created by the sweep itself, and a migration that
  rewrites a role is a business decision about what that account should become. **Architect / Nabil**
  if any environment that matters turns out to hold one.
* **`W-5` is untouched.** The refusal shape for a malformed body is still the Architect's open scope
  question, which is why the new Api test asserts "not success" and the value never reaching the
  table, rather than a specific status code.

**Not renamed, deliberately.** `ValidateDepartment` now also checks that the role is a role, and the
name was widened to `ValidateRoleAndDepartment` and then reverted: four historical records cite the
old identifier under SM-31 and live in `meetings/`, `qa/`, `proposals/` and `stories/` — documents the
agent doing the rename must not edit. The checker caught all four. Recorded in the method's own
summary instead.

---

### D-096 · Scrum Master — what makes an acceptance lapse, and two process rules bought at a cost · 2026-08-30

**Scrum Master, opening sprint 2.** `meetings/2026-08-30-sprint-2-open.md` is the reasoning; this is
the part that is a decision rather than a report.

#### 1. The lapse rule needs a scope, or it voids every sprint

`meetings/2026-08-27-sprint-1-retrospective.md` §3 change 3: *"An acceptance is a claim about a
commit. When a later commit touches that story's files, the acceptance lapses and must say so out
loud."* **Today is the first time it has been applied rather than argued about, and applying it
literally would have voided the entire sprint** — `ca4db6c` changed `PermissionEvaluator.Evaluate`,
which every permission-checked endpoint runs through, and `45a939d` changed the request pipeline for
every JSON-binding endpoint. A rule that lapses everything decides nothing.

**Decision, and the line is behavioural, not file-based:**

* **A story lapses** where a commit changed behaviour **that story's own acceptance criteria assert.**
  Five did: KAFF-109 (`User.ValidateDepartment` gained an `Enum.IsDefined` refusal — its own path),
  KAFF-101a (`MayHoldStaffSession`, its own role bar, deny-list → allow-list), and KAFF-105a, 102 and
  103 through the gate they route through.
* **A story is carried with the exposure named** where a commit changed a **shared mechanism** whose
  behaviour *for that story* is unchanged — **and "unchanged" must be pinned by a test, not argued.**
  Here it is [Verified: 2026-08-30 @ `tests/Domain.Tests/UserTests.cs` ->
  `The_two_role_doors_admit_exactly_these`]: both predicates are allow-lists of exactly the roles that
  exist, the deny-list restored goes red (D-095's `MUT-H` row), so for the nine roles the gate is
  *measured* identical and the change is confined to inputs no criterion names.

**This sits deliberately close to the argument change 4 forbids** — *"this cannot have changed,
therefore no test is possible"* — **and it is on the right side of that line only because the
equivalence was fault-injected by another session and re-run today.** Without that test the honest
answer is that every story lapses. **The test is the whole of the licence**, and the next session that
wants to carry a story past a shared-mechanism change must produce the equivalent or lapse it.

**What this does not fix, and it is the larger thing.** KAFF-101a and KAFF-103 have now lapsed
**twice** — `f807364`, then `ca4db6c`. Certifying five stories every time `LiveSession` or the role
doors move is unsustainable at slice 1 and impossible at slice 5. **The thing worth verifying on a
cadence is the mechanism, not the stories sitting behind it.** Flagged, not decided: it changes what
`ACCEPTED` means, and that is not the Scrum Master's to redefine alone.

#### 2. Disjoint file ownership is not sufficient — one agent per machine

`process/agile.md` ceremony 2, amended. On 2026-08-29 Frontend and Backend ran concurrently with
**genuinely disjoint** ownership — `src/Web/` against `src/Infrastructure/` and `tests/Api.Tests/` —
satisfying `agents.md` principle 3 throughout, and collided anyway on **port 5080** (hardcoded in
`src/Web/proxy.conf.json`, so both need the same one) and on `Kaff.Domain.dll` /
`Kaff.Infrastructure.dll`, which a running API holds open against the other's build. One agent killed
the other's API host by PID. **Two stalls.** D-092 records the workaround — start from a checked-in
binary with `Kaff__ApplyMigrationsOnStartup=false`, stop the API afterwards — which is a workaround,
not a fix.

**The machine is the shared resource, not the files.** Principle 3 stops agents overwriting each
other's work; this stops them being unable to build at all. Both must hold, and the second is a hard
serial constraint until the stack can be brought up twice on one box.

**Revisit if** the API's port becomes configurable end to end — `proxy.conf.json` is the binding
constraint, not the API.

#### 3. A check that could not detect what it checked for

`.claude/skills/run-kaff-erp/SKILL.md`'s stop-the-API gotcha said to run
`Get-Process -Name Kaff.Api`. **That does not match the process the same file's §1 tells you to
start:** `dotnet run --project ...` executes the app through `dotnet.exe`, so the process name is
`dotnet` and the check throws *"Cannot find a process with the name"* **while the DLLs are held
open**. It matches only the apphost form, which does exist [Verified: 2026-08-30 — `Kaff.Api.exe` is
present in `src/Api/bin/Release/net10.0/`] but is not what the skill instructs. Replaced with a
`Win32_Process` command-line match, which catches both launch forms.

**This is the retrospective's §1 pattern, three days after it was written**: a passing check and an
absent check produced identical output. The check reported *"not running"* for the very thing it was
checking for.

#### 4. §M's split has a second half, and this session proved it by skipping it

**§M is right and it held again**: the eighteen-file status sweep ran on a small model against a
dictated table, and the judgement did not. **But a small model executing a dictated table has nothing
to check the table against**, so a wrong fact in the brief is transcribed faithfully and arrives
looking like work.

It happened. The dictated table said `ACCEPTED` for **KAFF-106 and KAFF-110**, which have never been
accepted — `meetings/2026-08-27-sprint-1-close.md` §1 puts both in *"built and verified with a
criterion still held"*, and `AC-106-H` and `AC-110-D` have never been examined by any Verifier pass.
The model transcribed it exactly. **The defect was the Scrum Master's**; caught on reading the sweep's
diff, corrected in `aa8a9ca` with the correction written into both status lines.

**A second one followed, in the Scrum Master's own hand:** the sprint-1 final table dropped KAFF-108
from the accepted bucket and reported **20 of 57** with a paragraph built on the difference. The
figure is **25 of 57**. Caught by re-deriving the arithmetic rather than re-reading the prose,
corrected in `4fe4936`; the wrong figure survives in `601ac04`'s commit message and is corrected
there loudly rather than rewritten.

**Decision: reading the mechanical agent's diff is not review, it is the other half of the split, and
a sweep is not finished until it is done.** `agents.md` principle 7 puts an invitation to correct the
brief in every one, and it works — two wrong facts in briefs last sprint were both caught by the
agents receiving them. **It cannot work here.** A small model given a dictated table has no standing
to doubt it and no context to doubt it from, which is exactly what makes the split cheap. The saving
is real; the review is what pays for it.

**Three wrong facts in three sprints of Scrum Master briefs, and this is the first that reached a
file.** The difference was not care — it was that the two earlier ones went to agents that could
argue back.

#### 5. Not decided here

* **`AC-101b-A`'s reading.** KAFF-105b's ten criteria are all payload criteria; none renders anything,
  so D-091's deferral of the staff shell onto it cannot be discharged by that story as written. Three
  readings are costed in the sprint-2 opening §4.2 and **none is taken. Scope is Nabil's.**
* **`KAFF-118`'s cut**, the **`Role.Subcontractor` conversion**, the **`mustChangePassword` reach**,
  and **Q54/N11's retention consequence** — all four still stand with Nabil, none has moved, and none
  may be answered by any agent.

---

### D-097 · Scrum Master — the sprint-2 refinement: staging proven, SM-33, and a sprint refused in its proposed shape · 2026-09-01

**Scrum Master.** `meetings/2026-09-01-sprint-2-refinement.md` is the reasoning and the record; this is
the part that is a decision rather than a report. The ceremony `agents.md` principle 6 requires and the
sprint-1 close recorded as owed — *"no agent was asked 'what do you not know?'"* — was run: six agents,
read-only, no build, no stack.

#### 1. Staging is tickable for the API and not for the screens, and the pipeline said so, not a message

Nabil reported staging fixed. **The application running there and the CI smoke check reaching it are
different claims**, and only the second is the Definition of Done line. It was measured rather than
believed: the **same commit `dc76fe7`** ran `.github/workflows/deploy-staging.yml` -> `Smoke check`
twice — attempt 1 **failed** after exhausting the full 30 × 10s retry loop, attempt 2 **passed in
eleven seconds** — with nothing in the tree changed between them. The step ran rather than being gated
away: attempt 1's failure could not have come from an unset `STAGING_URL`.

**Decision: the line is ticked per story, by surface.** Because only the `web` service publishes a port
in `deploy/docker-compose.staging.yml`, one external 200 carrying `guardsInstalled: true` proves both
Oracle firewalls open, nginx serving, the API reaching PostgreSQL, and D-033's guards installed. **It
proves nothing about a screen** — the check curls `/api/health` and never fetches the SPA. So *"runs on
staging"* is **✅ for every API-surface story** and **⬜ for KAFF-101b's and KAFF-103's screens**.
`meetings/2026-08-27-sprint-1-close.md` §4 said *"the pipeline cannot see it"*; that was true when
written, is now false, and is corrected there in place with the date rather than left stale.

#### 2. SM-33 — the Test Naming Law

Ruled in full in `process/agile.md` -> `The Test Naming Law — SM-33`, and added to the Definition of
Done.

> **A name that is merely *narrow* stays. A name the change makes *false* is renamed in that same
> change, and its citations move with it in the same commit. A test name must not encode a count that a
> legitimate future change falsifies.**

**It does not contradict D-095, it draws the line D-095 needed and did not have.** D-095 reverted
`ValidateDepartment` → `ValidateRoleAndDepartment` because historical records cite the old name, and
that was right: the name became **narrow**, not untrue. `Hr_holds_exactly_three_permissions_and_none_touches_money`
becomes **untrue** the moment `ProjectTeamRead` lands, and
`Nothing_outside_LiveSession_can_produce_the_metadata_that_proves_a_route_paid` is untrue **today**
(`V-30-A`). **The Scrum Master moves the citations in `meetings/`, `qa/` and `proposals/`** — the files
the implementing agent may not edit, which is precisely the constraint that made D-095 choose the other
way.

#### 3. Neither candidate story is Ready, and the shell hole is four screens rather than one story

**KAFF-105b: `BLOCKED` on six Definition of Ready lines**, five of them repairable by the BA and QA
without any ruling, and **one that is Karim's**: rule 6 and `AC-105b-C` assert HR receives a project's
**code**, both citing D-051 (Q32), which grants *"the project name and the list of assigned engineers"*
and says nothing about a code. That is **`Q43`, registered and open**. The story bakes an unasked answer
into a criterion and a test.

**KAFF-115: `BLOCKED`**, transitively and on its own account. **Re-estimated 3 → 8** — it births a
permission row (5) and spans backend and frontend through `AC-115-J` (8); take the higher, not the sum.
Frontend, asked independently, returned 8 with the same reasoning. **KAFF-105b re-estimated 3 → 5** for
the permission row alone; as written it renders nothing and is not a frontend story.

**And the shell.** `ux/navigation.md` -> `Landing summary` names a slice-1 landing for every role. **No
story builds S-004, S-005, S-009a or S-011**, and the entire API exposes **three GET routes** —
`/api/auth/me`, `/api/health`, `/api/setup` — so three of the four landings have no data to render.
**Growing KAFF-105b to 8 does not produce a shell**; it produces a chrome that lands five of the nine
roles on a blank page. **All three readings costed in `meetings/2026-08-30-sprint-2-open.md` §4.2
understate it, and `AC-101b-D` fails the same arithmetic as `AC-101b-A`** — HR lands on **S-009a**, the
project list, while KAFF-115 builds **S-009b**, one project's team panel. **Scope is Nabil's and no
reading is taken here.**

#### 4. The largest technical finding is not in the sprint, deliberately

**`CLAUDE.md`'s flagship database rule — the safe balance that can never go negative — is verified by
nobody, and it is not one of the thirty check constraints `V-30-D` measured.** It is a constraint
trigger running a plpgsql function
[Verified: 2026-09-01 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` -> `kaff_check_non_negative_balance`],
checked by `tgname` alone, and **which accounts are floored is data, not code** — the trigger reads
`accounts.enforce_non_negative`, and no test asserts that flag on any row in a database. A database
whose Safe row carries `false` passes every guard check and floors nothing.

**Harmless today** — no `Posting`, no account set, no money. **Due before the first posting endpoint
ships, not before slice 9.** Routed to the **Architect** as owner with **Backend**. Deliberately **not**
proposed for sprint 2, where it would be rushed.

> **⚠️ Amended 2026-09-02 by the Architect, decisions.md D-101 — two of the three sentences above are
> false and were measured false the same week.** **(a)** *"verified by nobody"*:
> `TreasuryGuardTests.The_safe_balance_cannot_go_negative` verifies the rule behaviourally against a
> real PostgreSQL, and gutting the trigger's body while keeping its name turns it — and only it — red,
> 1 of 227 [Verified: 2026-09-02 @ `tests/Api.Tests/TreasuryGuardTests.cs` ->
> `The_safe_balance_cannot_go_negative`]. **(b)** *"no account set"*: `AccountTreeSeeder` inserts the
> main safe and thirteen other company-level accounts on every start-up
> [Verified: 2026-09-02 @ `src/Infrastructure/Persistence/Seeding/AccountTreeSeeder.cs` ->
> `MainSafeCode`]. **What survives, and it is the sentence that mattered:** which accounts are floored
> is data, no test could assert it on a row, and D-101 §3 closes that. The paragraph is left standing
> rather than rewritten — what this entry claimed, and when, is the record.

#### 5. Not done

* **No story file was edited.** KAFF-105b and KAFF-115 still carry every defect §3 of the meeting names.
* **`Q54`'s register row is still `"Not settled by any agent"`.** The BA was told to close it against
  D-072 §3 and correctly declined: **D-072 §3 ruled the mechanism and never gave a retention period**,
  which the original question asked for in as many words. Answered as to mechanism, **open as to
  period, and the period is Karim's.**
* **Nothing was built, run, or measured on the machine.** Suite figures here are inherited from
  `qa/slice-1/verification-2026-08-30.md`. Three questions that need the machine are named in the
  meeting §2.3 and serialised.
* **`V-30-A`, `V-30-C`, `V-30-D`, `V-30-G`, `V-30-H`, `W-2`, `W-5` and the citation checker's source
  blind spot are all still open**, each with a named owner in the meeting §4.2.
* **No sprint scope is locked.** A repair-and-unblock sprint is **proposed** in the meeting §5 and
  carries no story points. **Seven questions stand with Nabil** and none was answered here.

---

### D-098 · `V-30-A` fixed — the mechanism was already sound; the prose overstated it in six places and a test name · 2026-09-01

**Backend. `V-30-A`.** `qa/slice-1/verification-2026-08-30.md` §2.

**What was wrong, and what was not.** D-094 fixed the real defect — `Marker` went from `public` with
an `internal Instance` to a `private` nested type, so the one-dot forge `.WithMetadata(LiveSession
.Marker.Instance)` is now `CS0122` at compile time, and widening the accessibility back to `internal`
turns a pinning test red. **That mechanism is unchanged by this entry and stays.**

What D-094 got wrong was the *sentence* describing it: *"a private nested type cannot be named or
constructed from outside its containing class, so `RequireLiveSession` is now the only expression in
the language that can produce this metadata."* `CS0122` proves the type cannot be **named** from
outside `LiveSession`. It does not prove the metadata cannot be **produced** —
`Activator.CreateInstance(typeof(LiveSession).GetNestedType("Marker", BindingFlags.NonPublic)!,
nonPublic: true)` is one expression, reachable from any feature slice because they all compile into
`Kaff.Api`, and it constructs the value. The Verifier built exactly that into a probe route, listed it
in `SelfOnlyEndpoints`, applied none of the three checks, and the suite reported **227/227**
[Verified: 2026-09-01 @ `qa/slice-1/verification-2026-08-30.md` -> the `V-30-A` finding].

**The false sentence was not written once. It was written six times, plus a test name that asserts it
as its own claim** — found by re-reading every file the Verifier's finding touches rather than trusting
the brief's earlier count of three:

1. `src/Api/Authorization/LiveSession.cs` -> `Marker`'s `<summary>`: *"Added by `RequireLiveSession` and
   by nothing else."*
2. `src/Api/Authorization/LiveSession.cs` -> `Marker`'s `<remarks>`: *"so `RequireLiveSession` is now
   the only expression in the language that can produce this metadata."*
3. `src/Api/Authorization/LiveSession.cs` -> `IsApplied`'s `<remarks>`: *"nothing outside this class can
   make a route claim it did."*
4. `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
   `Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own`'s
   failure message: *"There is no other way to satisfy this ... no expression outside LiveSession can
   produce it."*
5. `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> the renamed test's `<summary>` (item 7 below):
   *"The exemption marker cannot be obtained by anything but `RequireLiveSession()`."*
6. `decisions.md` D-094 itself: *"so `RequireLiveSession` is now the only expression in the language
   that can produce this metadata"* — amended in place above, at D-094, rather than silently rewritten.
7. The test name **was the claim, restated as an identifier**:
   `Nothing_outside_LiveSession_can_produce_the_metadata_that_proves_a_route_paid`. SM-33
   (`process/agile.md`) rules exactly this shape: a name that is merely narrow stays, a name the change
   makes false is renamed in the change that finds it false.

**This is the second false sentence found in this file. D-094 replaced the first — the claim that
`.WithMetadata(LiveSession.Marker.Instance)` was the only forge worth guarding against.** Two
absolute claims about the same mechanism, each written by the session that had just made the
mechanism stronger, each overstated in the same direction: from "this is the strongest attack I
tested" to "this is the only attack that exists." **The shape recurs in this file specifically**,
because every fix here narrates its own sufficiency, and a narrated sufficiency is exactly the sentence
that outruns its evidence.

**Decision.**

1. **The six sentences are rewritten to say what `CS0122` actually establishes**, not what a stronger
   claim would be convenient to write: the type cannot be *named* from outside `LiveSession`, which
   closes the accidental forge; reflection can still *construct* it, which no test here catches, and a
   route built that way cannot be mistaken for correct usage the way the old one-dot forge could — it
   is a deliberate act, not a plausible mistake
   [Verified: 2026-09-01 @ `src/Api/Authorization/LiveSession.cs` -> `Marker`;
   @ `src/Api/Authorization/LiveSession.cs` -> `IsApplied`;
   @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
   `Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own`].
2. **The test is renamed** from `Nothing_outside_LiveSession_can_produce_the_metadata_that_proves_a_
   route_paid` to `LiveSession_exposes_no_metadata_type_wider_than_private`
   [Verified: 2026-09-01 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
   `LiveSession_exposes_no_metadata_type_wider_than_private`] — the property the test's own body checks
   (`GetNestedTypes` filtered to non-private), not the impossibility its old name asserted. Per SM-33,
   the two citations of the old name inside this file are moved in this same entry (D-094, above); the
   one citation in `qa/slice-1/verification-2026-08-30.md` is the Scrum Master's to move, not Backend's
   — that file is out of bounds for this agent this sprint.
3. **The mechanism itself is not changed.** Whether to close the reflection door — asserting the
   *behaviour* a stale session gets refused, rather than the *metadata* a route carries (`V-30-B`) — is
   a cost judgement for the Architect and Nabil, not decided here.

**Watched, not merely reasoned.** The build stays clean and `dotnet format --verify-no-changes` stays
clean after the rewrite; the renamed test's own assertion is unchanged in substance
(`reachable.Should().BeEmpty()`), so it still fails exactly when `Marker` — or any future nested type
of `LiveSession` — is widened past `private`, which is the property it now honestly claims. The
Verifier's reflection forge (`MUT-B2` in the verification report) is unaffected by this entry and
remains open, by design: closing it is `V-30-B`'s question, not this one's.

**What this does not do.** It does not make the reflection forge harder to write, and it does not add
a test that fails against it. **`V-30-A` is closed as a documentation defect. `V-30-B` — whether the
door is worth closing — is untouched and stays with the Architect.**

**Revisit if.** The Architect rules on `V-30-B` and a behavioural assertion is added; the prose above
should then say a stronger thing is caught, not merely that a weaker thing is honestly described.

---

### D-099 · `V-30-C` — the missing entry for `45a939d`, and how far it actually reaches · 2026-09-01

**Backend. `V-30-C`.** `qa/slice-1/verification-2026-08-30.md` §5; raised again at the sprint-2
refinement, `meetings/2026-09-01-sprint-2-refinement.md` §4.2.

**What changed, and it shipped with no entry.** `45a939d` touches exactly one file,
`src/Api/Program.cs`, in two places [Verified: 2026-09-01 @ `src/Api/Program.cs` ->
`ThrowOnBadRequest`; @ `src/Api/Program.cs` -> the `ExceptionHandlerOptions` block]:

1. `builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);` —
   the framework default is `true` in Development and `false` everywhere else, so a malformed JSON body
   produced a `BadHttpRequestException` in Development and a clean `400` in Staging. Set explicitly, so
   every environment now agrees.
2. `app.UseExceptionHandler(new ExceptionHandlerOptions { StatusCodeSelector = ... })` — without a
   selector, `UseExceptionHandler` reports every exception, `BadHttpRequestException` included, as
   `500`. The selector reads the exception's own status code (`400` for an unreadable body, `413` for
   one over the size limit) instead of flattening all of them to a server fault.

**Why it has no entry, corrected now.** It was written and merged as a bug fix for the defect found at
`POST /api/setup`, and the commit's own comments say plainly that the fix is not scoped to that route —
*"Every endpoint that binds a JSON body, not `POST /api/setup` where it was found"*
[Verified: 2026-09-01 @ `src/Api/Program.cs` -> `ThrowOnBadRequest`]. A fix with that reach is
structural under `CLAUDE.md`'s Definition of Done and should have carried an entry the day it merged.
It did not, and this is that entry, written under D-057 §4's rule for outstanding work rather than
waiting on `W-5` to close first.

**Blast radius — this is not an `/api/setup` fix.** Both changes are registered once, globally, in
`Program.cs`, ahead of every feature slice's endpoint mapping. **Every JSON-binding endpoint the API
has today, and every one it will ever add, answers a malformed body with a plain client-error status in
every environment**, not only the route the defect was noticed on. Driven live in Development by the
Verifier across `POST /api/setup`, `POST /api/auth/sign-in` and `POST /api/auth/change-password` with
nine malformed bodies: zero `500`s, and zero `fail:`-level log entries where each used to log an
unhandled exception [Verified: 2026-09-01 — `qa/slice-1/verification-2026-08-30.md` §5].

**What it rules out.** An environment-dependent status code for the same malformed request (Development
`500`, Staging `400`) is no longer possible without deliberately reverting the `Configure<
RouteHandlerOptions>` line — and `The_bad_request_behaviour_is_set_explicitly_rather_than_by_environment`
fails the moment that line is removed
[Verified: 2026-09-01 @ `tests/Api.Tests/MalformedRequestTests.cs` ->
`The_bad_request_behaviour_is_set_explicitly_rather_than_by_environment`]. It also rules out a client's
malformed body being logged as a genuine unhandled-exception fault — the two are different events and
must not read alike in the log.

**What `W-5` became, and this is the honest consequence rather than a decision about it.** Before this
commit, a `messageKey`-less `400` was a Development-only artefact of the framework throwing on a body
the client got wrong — an edge a developer saw and a client, in practice, did not, because Staging
answered `400` without a body-shaped `ProblemDetails` extension either way. **After this commit, the
`messageKey`-less `400` is what every JSON-binding endpoint returns, in every environment, for any
malformed body** — `CustomizeProblemDetails` (D-079 / the block above it in `Program.cs`) only fills in
`code` and `messageKey` for a bare `401` or `403`, and a framework-thrown `400` is neither. D-095 §7
already declined to assert a specific status code in `A_role_outside_the_enum_is_refused_and_never_
persisted` for exactly this reason. **No user-visible defect exists today** — the SPA's `toProblem`
falls back to `errors.unknown`, which is a real Arabic key
[Verified: 2026-09-01 @ `src/Web/src/app/core/api/problem-details.ts` -> `toProblem`] — but the shape
is now load-bearing everywhere rather than nowhere, and whether that is the API's permanent refusal
contract, and what a `413` should carry when a site engineer's photo upload trips the size limit
(slice 6), is **the Architect's and UX's to rule**, not decided here.

**Not done, and named so it is not mistaken for closed.**

* **Regression cover is name-level, not surface-level.** `V-30-G` stands: every assertion in
  `MalformedRequestTests` runs against test-host probe routes in the `Testing` environment, where the
  framework default already matched half of this fix before it existed. No test in the suite exercises
  a *shipped* route or a *Development* host; the Verifier established the reach by driving the running
  API by hand, not by a test that fails on a regression. Closing it needs the machine and is Backend's,
  separately, not folded into this entry.
* **`W-5`'s refusal contract is not ruled here.** This entry records what changed and what it now
  means for `W-5`; it does not decide whether a `messageKey`-less `400` is an acceptable permanent
  shape, and it does not touch `413`.

**Revisit if.** The Architect rules a `messageKey` is required on every refusal shape, including a
framework-thrown `400`/`413` — `CustomizeProblemDetails`'s `switch` would then need a third arm, and
`W-5` would close at the same time.

---

### D-100 · Scrum Master — Nabil's three rulings applied, `KAFF-125` cut, and the demo question put back to him · 2026-09-02

**Scrum Master.** `meetings/2026-09-02-sprint-2-locked.md` is the reasoning and the record; this is the
part that is a decision rather than a report. Nabil ruled on **three of the seven** questions standing
after the 2026-09-01 refinement (D-097 §5, meeting §6). The other four are untouched and none was
answered by any agent.

#### 1. `Q43` is ANSWERED, both halves, and the format is part of the ruling

> *"The Reference Code is mandatory alongside the project name (format: `[RefCode] Project Name`). In
> construction/engineering ERPs, project names frequently overlap (e.g. 'Capital Site - Phase 1' vs
> 'Phase 2'). The RefCode is the hard identifier that prevents HR from misallocating staff to the wrong
> site."*
>
> *"Team Size: Yes, displaying the current headcount is required. It serves as the primary visual
> indicator, allowing HR to spot unstaffed sites at a glance without drilling down."*

**Decision, and the distinction is load-bearing: the payload carries three fields; `[RefCode] Project
Name` is a display format and belongs to the rendering stories, never to the JSON.** A pre-formatted
display string in an API response is a translation decision taken on the server, which
`problem-details.ts` and the i18n rule both forbid. **Team size is the count of active
`ProjectAssignment` rows** — the set KAFF-115 rules 1 and 4 already define — and it is derived on read,
never stored, for the same reason a balance is never stored.

Applied, and verified after it happened: the register row moved to the answered table
[Verified: 2026-09-02 @ `stories/questions-for-karim.md` -> the `Q43` row]; KAFF-105b rules 6 and 6a,
`AC-105b-C` and `AC-105b-F` carry the code and the team size
[Verified: 2026-09-02 @ `stories/slice-1-foundation/KAFF-105b-api-me-project-list.md` -> `AC-105b-C`];
KAFF-115 carries the format on its rendering criteria
[Verified: 2026-09-02 @ `stories/slice-1-foundation/KAFF-115-project-team-panel.md` -> `AC-115-H`].

**`Q43` was KAFF-105b's sole remaining Definition of Ready failure, so the story is Ready at 5.**
KAFF-115's transitive block cleared with it, and its own three failures were repaired the same day —
including **`AC-115-G`, which had been passing for the wrong reason**: a `Role.Client` is refused at the
staff door by `MayHoldStaffSession` before any handler runs, so the criterion would have stayed green if
the rule it names were deleted. It is Ready at 8.

**One thing the brief that opened this run got wrong, and the BA reported it rather than working around
it: KAFF-113 has no project picker.** Its criteria are backend and permission logic throughout; the only
picker in that flow is a **user** picker on S-010, by which point the project is already chosen. The
ruling was applied to the two stories that render a project and to no third one.

#### 2. `AC-101b-A` and `AC-101b-D` cannot be discharged by a payload, and the ticket is cut

> *"KAFF-105b (Backend) remains the API payload ticket. It technically satisfies the backend portion of
> the ACs the moment it returns the correct role/permission data structure. A dedicated frontend ticket
> must be cut for the visual shell itself — the layout, sidebar, header, and role-based routing.* **You
> cannot discharge a UI rendering dependency with a JSON response.**"

This settles the arithmetic failure D-097 §3 found twice — once for `AC-101b-A`, and once, invisibly
until that ceremony, for `AC-101b-D`. **`KAFF-125` is cut at 3 points**, carrying both criteria
[Verified: 2026-09-02 @ `stories/slice-1-foundation/KAFF-125-staff-shell.md` -> `AC-125-A`], and
KAFF-101b's deferrals are re-pointed at it in a dated amendment rather than a silent edit.

**Its criteria are bounded by what an endpoint can feed, and the story says so in a table rather than in
prose.** The API exposes three `GET` routes. S-005's identity half renders today; **S-006 has no
list-users route and S-011's clients are KAFF-119…124, deferred out of sprint 1**, so neither is
asserted by any criterion — `agents.md` §3c's hard rule cuts both ways, and a criterion that cannot pass
is as bad as one that cannot fail. **Where a ruled landing has nothing to render, the story raises a
question and does not invent an interim one**; an invented landing is exactly the plausible fill
`agents.md` calls this project's most expensive failure mode.

**The estimate is 3 and it rests on evidence I want visible, because it is lower than I expected.**
`AuthService`'s three session signals and `mustChangePasswordGuard` already exist
[Verified: 2026-09-02 @ `src/Web/src/app/core/auth/auth.service.ts` -> `AuthService`], so the story is
chrome, dispatch and per-role routes on top of a service that is built, not a second implementation of
it. **Frontend has not confirmed the number independently**, which KAFF-115's 8 had and this does not.

#### 3. Sprint 2 stays a repair sprint, and `Ready` is not the same as pulled

> *"An answer to Q43 does not change its shape. If we build new features on a porous foundation, the
> Zero-Trust posture collapses. Pay the technical debt first."*

**Decision: KAFF-105b and KAFF-115 are Ready and are not in sprint 2.** Making a story Ready and pulling
it are different acts, and this entry records them as different acts so a later session does not read
`Ready` as committed.

**And the repair list Nabil ruled against was itself four items stale — the correction is in the meeting
§2 and it is the reason this sprint was short.** `POST /api/setup`'s `500` was fixed at `45a939d` and now
has its entry (D-099); `V-27-A`, `V-27-B` and `V-27-C` were fixed and **independently accepted** — all six
commits `ACCEPT` in `qa/slice-1/verification-2026-08-30.md` §11; `V-30-A`, `V-30-C` and `V-30-H` were
repaired on 2026-09-01 at `93fa417`, `e93029b` and `f47416d`; the staging SPA assertion landed at
`3d98fa1`; and the QA block was six of ten closed with two needing no QA at all, not eight looping items.
**What actually remained is `V-30-D`, the safe-balance layer under it, `V-30-B` and `V-30-G`** — done in
this sprint, D-101 and D-102 — **plus two QA rows blocked on named questions.**

#### 4. What I did not decide, and one of them is Nabil's and was put back to him today

* **Whether `KAFF-125` is built in sprint 2.** Nabil's closing words this run were *"we are still in
  sign in page we want to move to have demo to client"*, and that pulls against his own ruling 3. **He
  ordered the ticket cut; cutting is not building.** Scope is his lock and not the Scrum Master's, so it
  is put to him as one question with the trade priced — meeting §5. **I did not widen the sprint to
  accommodate it and I did not assume the demo overrides the ruling.**
* **The four business questions**, all unchanged and none answerable by any agent: `KAFF-118`'s cut,
  `Q56` (the `Role.Subcontractor` conversion), the `mustChangePassword` reach beyond `/api/auth/me`
  (`V-30-I` — `AC-106-H` and `AC-105a-C` contradict each other in committed text and both behaviours have
  been observed), and **`Q54`'s retention period, which is Karim's.**

#### 5. Not done

* **Nothing built today has been independently verified.** D-101 and D-102 both changed
  `FindMissingGuardsAsync`, which decides whether the host starts; `agents.md` §7 and `CLAUDE.md` both
  say the author does not certify its own work. **A Verifier pass is owed before this sprint closes.**
* **`ux/navigation.md` is still stale** on `mustChangePassword` — it describes the refusal reading D-072
  §2 replaced on 2026-08-24. Routed to UX at the 2026-09-01 refinement, routed again here, still not
  done. **A shell story written against it would command a defect**, which is why KAFF-125 is written
  against D-072 §2 and says so in its own text.
* **`src/Web/src/app/app.routes.ts` still attributes the staff shell to *"KAFF-105b's shell"*** — the
  exact confusion ruling 2 corrects. Found by the BA, left for Frontend, not fixed.
* **`AC-101b-D` now sits on a story that cannot yet say where HR lands.** KAFF-125's open question 3 —
  whether S-009a renders from the shared `/api/auth/me` or needs the dedicated HR API `ux/` describes —
  is unanswered, and no reading was picked. It is UX's and Nabil's.
* **The staging SPA smoke step at `3d98fa1` has not been observed passing by me.** It is committed and
  pushed; whether the workflow has run green is unverified in this session.

---

### D-101 · Architect — the safe floor is data, and the half of it nothing read · 2026-09-02

**Architect, with the machine to itself.** Raised at the sprint-2 refinement,
`meetings/2026-09-01-sprint-2-refinement.md` §2.1, and routed to me by D-097 §4. **Measured before it
was fixed**, per the retrospective's change 4 — *a self-sealing argument needs a demonstration*.

#### 1. The finding as it was stated is half wrong, and the wrong half was the headline

D-097 §4 and the refinement both say *"`CLAUDE.md`'s flagship database rule — the safe balance that
can never go negative — **is verified by nobody**"*, and both say it is *"harmless today"* because
there is *"no `Posting`, no account set and no money."* **Neither sentence survived the measurement.**

**a. The rule's behaviour is verified, and it was verified before this entry.**
`TreasuryGuardTests.The_safe_balance_cannot_go_negative` creates a real `Safe` account against a real
PostgreSQL, funds it 1,000, spends 5,000 and requires `KAFF_NEGATIVE_BALANCE`
[Verified: 2026-09-02 @ `tests/Api.Tests/TreasuryGuardTests.cs` -> `The_safe_balance_cannot_go_negative`].
**`MUT-1`:** `kaff_check_non_negative_balance`'s body replaced with an immediate `RETURN NULL`, the
name and the registration untouched. Build clean, **1 of 227 failed** — that test, and only that test,
with *"the database must refuse this operation with KAFF_NEGATIVE_BALANCE."* Reverted.

**So the trigger body is not name-only cover**, and the finding as routed would have bought a test
that already exists. Recorded plainly because the brief that carried it invited its own correction and
this is the third sprint running in which that has paid (`agents.md` principle 7).

**b. There is an account set, seeded on every start-up, and `SAFE-MAIN` is in it.**
`AccountTreeSeeder` inserts the main safe on every boot and never rewrites an existing row
[Verified: 2026-09-02 @ `src/Infrastructure/Persistence/Seeding/AccountTreeSeeder.cs` -> `MainSafeCode`].
Fourteen company-level accounts exist on this machine's database today. *"No account set"* was wrong,
and it is the sentence that made this look deferrable.

#### 2. What is real, and it is the layer underneath — measured

**Which accounts are floored is data.** `kaff_check_non_negative_balance` loops over
`accounts WHERE a.enforce_non_negative`, so the trigger can be present under its required name, fire
on every insert, and floor nothing
[Verified: 2026-09-02 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` -> `kaff_check_non_negative_balance`].
The guard file says so in its own words: *"a database seeded before 2026-08-20 therefore keeps the old
floors."*

**`MUT-2`.** A `Safe` row `INSERT`ed with `enforce_non_negative = false`, funded 1,000, then overdrawn
by 5,000:

| | Result |
|---|---|
| The overdrawing posting | **accepted**, 1 row |
| The safe's signed balance afterwards | **-4,000.0000** |
| `FindMissingGuardsAsync` | **`[]`** |
| `/api/health` | `healthy`, `guardsInstalled: true` |

**`MUT-2b` — and this is what makes it worse rather than better.**
`UPDATE accounts SET enforce_non_negative = false` on a correctly-created row is refused:
*"23001: KAFF_ACCOUNT_IMMUTABLE: account … configuration cannot be changed after creation"*
[Verified: 2026-09-02 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` -> `kaff_accounts_configuration_is_immutable`].
The refinement asked me to weigh what that trigger closes. **It closes the flip and not the value.** It
is `BEFORE UPDATE`, so an `INSERT` never meets it — and on a row that is already wrong it is the
mechanism that makes the wrong value *permanent*. **An immutable wrong value is worse than a mutable
one**, and this is the one guard in the file whose correctness makes a defect harder to repair.

**Why no test could see any of it.** Every test in this repository builds its accounts through
`Account.Create`, which copies the flag from `AccountTypes` — so the row always agrees with the
catalogue by construction. `SchemaInvariantTests.Stored_account_metadata_matches_the_domain_catalogue`
asserts it on an `Account` constructed from metadata
[Verified: 2026-09-02 @ `tests/Api.Tests/SchemaInvariantTests.cs` -> `Stored_account_metadata_matches_the_domain_catalogue`]
and `CatalogueCompletenessTests.Exactly_three_account_types_are_floored_at_zero` asserts it on the
metadata itself
[Verified: 2026-09-02 @ `tests/Domain.Tests/CatalogueCompletenessTests.cs` -> `Exactly_three_account_types_are_floored_at_zero`].
**Both assert the code against the code.** The exposure is a row written by a *past* catalogue, and no
test in this repository can produce one.

#### 3. Decision — the check goes where D-033 already refuses to start, and it is one query

`FindMissingGuardsAsync` now also compares every `accounts` row's `enforce_non_negative` against the
floored set in `AccountTypes`, in both directions, and reports each disagreement as
`accounts.enforce_non_negative on <code>`
[Verified: 2026-09-02 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` -> `FindMissingGuardsAsync`].

**Why here and not in a new mechanism.** This is already the one place that answers *"is this database
enforcing what it must?"*, and it already feeds both the D-033 start-up refusal and `/api/health`'s
`guardsInstalled` — which `.github/workflows/deploy-staging.yml` greps for. **One query therefore makes
the staging pipeline assert the safe floor is real, on every deploy, with no second mechanism to keep
alive.** It is the rung `CLAUDE.md`'s own *"enforced by a database constraint, not application code"*
points at: the deployment is checked, not the caller.

**Both directions, deliberately.** A floor missing lets an account overdraw (spec.md §6.1). A floor
*added* refuses a legitimate posting with an opaque `KAFF_NEGATIVE_BALANCE` mid-extract — the second
half of `Exactly_three_account_types_are_floored_at_zero`'s own comment, and the shape a database
seeded before Karim's 2026-08-20 ruling actually carries, with `Hold`, `FirmAdvance` and
`MaterialAdvance` still floored.

**Watched failing at both levels, not merely written.**

1. **The test.** `An_account_row_whose_floor_disagrees_with_the_catalogue_is_reported_as_a_missing_guard`
   inserts an unfloored `Safe` row raw, requires it reported, and removes it
   [Verified: 2026-09-02 @ `tests/Api.Tests/SchemaInvariantTests.cs` -> `An_account_row_whose_floor_disagrees_with_the_catalogue_is_reported_as_a_missing_guard`].
   **`MUT-3`:** the new predicate neutered to `WHERE false AND …` — build clean, that test red,
   *"Expected collection {empty} to contain …"*. Reverted.
2. **The running stack.** An unfloored `Safe` row inserted into this machine's live `kaff` database:
   `/api/health` went from `200 healthy … missingGuards: []` to
   **`503 degraded … missingGuards: ["accounts.enforce_non_negative on PROBE-UNFLOORED"]`**. Row
   deleted; `200 healthy` restored; the fourteen seeded rows all agree with the catalogue and
   `SAFE-MAIN` carries `true`.

#### 4. What this does not cover, stated at the level it actually holds

**The brief asked for behaviour over data, and the honest answer is that the behaviour half already
existed and the data half is what was missing.** The two together are the rule; neither alone is:

* `The_safe_balance_cannot_go_negative` fails when the **trigger stops flooring what the flag marks** —
  `MUT-1`, 1 of 227.
* the new test fails when a **row stops carrying the flag** — `MUT-3`.

**Neither is a test of a posting endpoint, because there is none**, and I did not invent one. What
still has no cover is the composition: no test asserts that *the account a real payment flow reaches*
is a floored one, because nothing reaches an account yet. That is slice 3's, and its gate — *"the
worked example reconciles"* — is where it belongs.

#### 5. The refinement's open question, answered with a measurement — §2.3 item 2

**Whether comparing a checked-in expression against PostgreSQL's own re-printed definition is stable,
or false-positives on formatting alone.** This decides the shape of `V-30-D`, which is Backend's and
runs after me. Measured on PostgreSQL 16:

| Authored, in a configuration file | `pg_get_constraintdef` |
|---|---|
| `amount > 0` [Verified: 2026-09-02 @ `src/Infrastructure/Persistence/Configurations/TreasuryConfigurations.cs` -> `ck_postings_amount_positive`] | `CHECK ((amount > (0)::numeric))` |
| `role <> 'Subcontractor' OR password_hash IS NULL` [Verified: 2026-09-02 @ `src/Infrastructure/Persistence/Configurations/IdentityConfigurations.cs` -> `ck_users_subcontractor_cannot_log_in`] | `CHECK ((((role)::text <> 'Subcontractor'::text) OR (password_hash IS NULL)))` |

**Answer, in three parts.**

1. **Comparing the *authored* text to the re-print is not stable — it is not even close.** PostgreSQL
   adds parentheses and explicit casts. Neither of the two above matches, and nothing suggests any of
   the thirty would. **A checker built that way reports thirty failures on a correct database on the
   day it ships**, and *a checker that cries wolf gets muted, which is D-046's green light by another
   name.* **Do not build that one.**
2. **The re-print is itself a stable normal form, and that is the shape to build.** Four constraints
   created on the same server: `CHECK (amount > 0)` and `CHECK ((( amount   >   0 )))` re-print
   **identically**; `CHECK (amount >= 0)` — exactly `V-30-D`'s mutation — re-prints **differently**.
   So a checked-in snapshot of PostgreSQL's *own output*, compared against the live `pg_constraint`,
   is blind to formatting and loud about the predicate. That is what `V-30-D` should compare.
3. **One residual, named so Backend is not surprised by it.** `CHECK (0 < amount)` re-prints as
   `CHECK (((0)::numeric < amount))` — a semantically identical rewrite that the comparison flags.
   That is a deliberate edit to a money guard, and re-approving the snapshot in the same commit is
   D-093's own two-file friction rather than a false positive. **It is not the formatting noise the
   question was about.**

#### 6. `V-30-B` — the reflection door stays open, and here is the condition that reopens the question

**Routed to me by `qa/slice-1/verification-2026-08-30.md` -> the `V-30-B` finding and by D-098's
closing paragraphs, and decided by neither. It is a cost judgement, and the ruling is the deliverable.**

**The mechanism, re-established rather than taken from the brief.** `RequireLiveSession` does two
separable things — it adds an endpoint filter and it stamps a `Marker` — and `IsApplied` reads only the
second
[Verified: 2026-09-02 @ `src/Api/Authorization/LiveSession.cs` -> `RequireLiveSession`;
@ `src/Api/Authorization/LiveSession.cs` -> `IsApplied`]. `SelfOnlyEndpoints` has exactly **two**
members, `POST /api/auth/change-password` and `GET /api/auth/me`
[Verified: 2026-09-02 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
`Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own`].

**And the property a sweep would assert is already asserted, per member, established today rather than
taken on the brief's word:**

* `MeTests.A_password_changed_on_another_device_ends_this_endpoints_answer_too` signs in twice, changes
  the password on one device and requires **`403`** on the other
  [Verified: 2026-09-02 @ `tests/Api.Tests/MeTests.cs` -> `A_password_changed_on_another_device_ends_this_endpoints_answer_too`],
  with `A_deactivated_accounts_token_is_refused_not_answered_with_a_profile` covering the `IsActive`
  half and `A_subcontractor_session_is_refused_not_answered_with_a_profile` the role bar.
* `ChangePasswordTests.The_change_ends_every_other_session` requires the other device to stop getting
  `200`
  [Verified: 2026-09-02 @ `tests/Api.Tests/ChangePasswordTests.cs` -> `The_change_ends_every_other_session`],
  and `A_deactivated_account_cannot_change_its_own_password` requires **`403`**
  [Verified: 2026-09-02 @ `tests/Api.Tests/ChangePasswordTests.cs` -> `A_deactivated_account_cannot_change_its_own_password`].

**Ruling: not now.** A behavioural sweep over `SelfOnlyEndpoints` would today assert, generically and
therefore more weakly, what two hand-written tests already assert concretely for the only two members
that exist. Its whole value is over **members that do not exist yet** — and a sweep must construct a
live session and a stale one for an arbitrary route it knows nothing about, which for a `POST` means
inventing a body. **Buying that for two members, against a list that has grown by one in a month, is
paying now for a property nothing currently lacks.**

**The trigger condition, concrete enough that a future session can tell whether it has fired — and any
one of the three is enough:**

1. **`SelfOnlyEndpoints` reaches a third member.** Two hand-written tests per member is a pattern; three
   is a hand-copy, and `LiveSession`'s own remarks say what a hand-copy is: *"one item short
   eventually."*
2. **A self-only route is added whose per-member tests do not cover all three checks** — `IsActive`,
   the security stamp, `MayHoldStaffSession`. The two present members cover all three each; the day one
   does not, the generic assertion is the cheaper way to get it than a third hand-copy.
3. **A self-only route touches money or a posting.** Both current members act on the caller's own
   credential. A self-only route that moves value changes the cost of being wrong, and §M's rule —
   never downgrade anything deciding who may touch money — applies to the *test* strategy as much as to
   the model.

**What I am not ruling.** The reflection forge itself stays open and is unchanged by this — D-098 §3
already says so, and closing it is the same work as the sweep, not a separate cheaper option. **The
honest statement of today's position: the metadata proves a declaration, the behaviour is proved
per member, and nothing proves the two agree for a member nobody has written yet.**

#### 7. Not done

* **No posting endpoint, no money, and no §15 assertion.** Nothing here tests the worked example, and
  the suite totals in this entry must not be read as coverage of it.
* **`V-30-D` itself is not built.** §5 answers the question that decides its *shape*; the
  expression-level comparison is Backend's and is not in this change. The thirty predicates remain
  verified by name only.
* **The other 29 constraint predicates and the 7 other required triggers were not mutated.** `MUT-1`
  covers `kaff_check_non_negative_balance` alone.
* **No behavioural sweep over `SelfOnlyEndpoints` was written** — §6 rules it out for now, with the
  condition that reopens it.
* **The staging database was not inspected.** §3's live evidence is this machine's `kaff` database.
  **If any deployed database carries a row seeded before 2026-08-20, this change will refuse that
  host's start-up** outside Development — which is D-033 working as designed, and is the point. **The
  repair is not an `UPDATE`:** `MUT-2b` shows the immutability guard refuses one. Such a row must be
  closed and the account reopened, which is `kaff_accounts_configuration_is_immutable`'s own `HINT`.
  Named here rather than discovered on a deploy.
* **Two sentences in files I may not edit are now false** — `meetings/2026-09-01-sprint-2-refinement.md`
  §2.1's *"verified by nobody"* and its *"no account set"*. **Reported to the Scrum Master, whose files
  those are under SM-33.** D-097 §4 carries the same two and is amended in place above.

**Revisit if.** A slice adds an account type, or Karim changes which accounts are floored. The floored
set then moves in `AccountTypes`, `Exactly_three_account_types_are_floored_at_zero` moves with it — and
**every already-seeded row keeps the old flag and cannot be updated**, so the check in §3 will report
them. That is the intended alarm, not a bug in it, and the migration it demands is real work that must
be planned rather than discovered.

---

### D-102 · Backend — `V-30-D` closed at expression level, and `V-30-G`'s regression cover written · 2026-09-02

**Backend, with the machine to itself.** Two items from the sprint-2 refinement brief, routed by
`qa/slice-1/verification-2026-08-30.md` §3 (`V-30-D`) and §5 (`V-30-G`).

#### §1 — `V-30-D`: check constraints are now verified by predicate, not only by name

**What was wrong.** `RequiredCheckConstraints` (D-093) and the model-derived list both verify a check
constraint exists under a required name. Neither reads what the name actually guards.
`ck_users_subcontractor_cannot_log_in`, kept under its name with its predicate replaced by `1 = 1`
(`MUT-C3`), passed every gate this repository had: build clean, Api suite 227/227, D-033's start-up
refusal silent, `/api/health` reporting `guardsInstalled: true`. D-093's own words named the exposure
that mattered: *"`ck_postings_amount_positive`, `ck_postings_distinct_accounts` and
`ck_postings_not_self_reversing` — the slice-3 money rules — are three of the thirty. Those three have
no domain guard in front of them today."*

**The design question the Architect answered first, re-verified rather than taken on trust.**
`meetings/2026-09-01-sprint-2-refinement.md` §2.3 item 2 asked whether comparing a checked-in
expression against PostgreSQL's own re-print is stable. D-101 §5 measured it on PostgreSQL 16:
comparing the *authored* SQL (`amount > 0`) against the live re-print (`CHECK ((amount > (0)::numeric))`)
is not stable — PostgreSQL adds parentheses and casts to every predicate — but PostgreSQL's *own*
re-print **is** a stable normal form: two constraints created with equivalent but differently
formatted predicates re-print identically, and a genuinely different predicate re-prints differently.
Re-derived independently today by querying every one of the 30 required constraints on the live `kaff`
database rather than trusting the two worked examples in D-101 §5
[Verified: 2026-09-02 — `docker exec kaff-db psql -U kaff -d kaff -c "SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint WHERE contype='c'"`]; all 30 matched D-101's predicted shape.

**Decision.** A checked-in snapshot of PostgreSQL's own re-print for all 30 required constraints,
compared against the live database on every call to `FindMissingGuardsAsync` — the same method that
already feeds D-033's start-up refusal and `/api/health`'s `guardsInstalled`, so the staging pipeline
gets this for free on every deploy, exactly as D-101 §3 did for the account floor.

1. `RequiredCheckConstraintDefinitions` — all 30 names mapped to PostgreSQL's re-printed definition,
   hand-written in `DatabaseInitializer.cs`, not in `Persistence/Configurations`
   [Verified: 2026-09-02 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` ->
   `RequiredCheckConstraintDefinitions`]. **This has `RequiredCheckConstraints`'s own property, for the
   same reason**: it lives in a different file from the configuration that declares the predicate, so
   editing the predicate cannot also update its own snapshot in the same keystroke. A predicate change
   — even under an unchanged name — is now a deliberate edit across two files, the same friction D-093
   built for a constraint's existence, now built for its content.
2. `FindMissingGuardsAsync` fetches each present constraint's `conname` **and**
   `pg_get_constraintdef(oid)` in the one query that already ran, and reports a mismatch as
   `"<name> predicate changed: expected …, found …"` for any constraint present under its required name
   with a definition that disagrees with the snapshot
   [Verified: 2026-09-02 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` ->
   `FindMissingGuardsAsync`]. Compared only for constraints found present — an absent one is already
   reported by the existing name check, and reporting it twice would obscure which defect occurred.
3. `Every_required_check_constraint_has_a_recorded_definition` closes the same forget-to-extend risk
   D-093 built `The_written_out_check_constraints_and_the_model_agree` for, one level down: a required
   name with no recorded definition would have its predicate compared against nothing
   [Verified: 2026-09-02 @ `tests/Api.Tests/SchemaInvariantTests.cs` ->
   `Every_required_check_constraint_has_a_recorded_definition`].
4. `A_check_constraints_predicate_changed_while_its_name_did_not_is_reported_as_a_missing_guard`
   reproduces `MUT-C3` permanently, plus the money case Nabil named directly —
   `ck_postings_amount_positive` widened from `amount > 0` to `amount >= 0`
   [Verified: 2026-09-02 @ `tests/Api.Tests/SchemaInvariantTests.cs` ->
   `A_check_constraints_predicate_changed_while_its_name_did_not_is_reported_as_a_missing_guard`].

**Watched failing, not merely written — at the test level and live, both mutations from the brief.**

| Mutation | Test suite | Live `/api/health`, API on 5080, Development, before the fix would have been silent |
|---|---|---|
| `MUT-C3` — `ck_users_subcontractor_cannot_log_in` kept, predicate → `1 = 1` | Red, exact message asserted | `503 degraded`, `missingGuards: ["ck_users_subcontractor_cannot_log_in predicate changed: expected \"CHECK ((((role)::text <> 'Subcontractor'::text) OR (password_hash IS NULL)))\", found \"CHECK ((1 = 1))\""]` |
| The money case — `ck_postings_amount_positive` kept, predicate → `amount >= 0` | Red, exact message asserted | `503 degraded`, `missingGuards: ["ck_postings_amount_positive predicate changed: expected \"CHECK ((amount > (0)::numeric))\", found \"CHECK ((amount >= (0)::numeric))\""]` |

Both driven against the live `kaff` database with the API running (`ASPNETCORE_ENVIRONMENT=Development`,
port 5080), not only in the test suite: `docker exec kaff-db psql` dropped and re-added each
constraint under its own name with the weakened predicate, `/api/health` answered `503` with the
mismatch named, both restored, `/api/health` returned to `200 healthy … missingGuards: []`. `git status`
confirmed no drift; the live database carries exactly the predicates it did before this entry.

**What this does not do — the residual D-101 §5.3 already named, confirmed rather than newly found.**
A semantically identical rewrite re-prints differently and is flagged as a mismatch — `0 < amount`
would re-print as `CHECK (((0)::numeric < amount))`, not as this snapshot's `amount > 0` entry. That is
D-093's two-file friction working as designed for a deliberate edit to a money guard: re-approve the
snapshot in the same commit. It is not formatting noise to be normalised away, and no attempt was made
to build a normaliser for it — D-101 §5.3 already ruled that out.

**Not done.**

* **The count did not move.** Thirty required constraints, unchanged — no `SM-33` rename applies to
  `Thirty_check_constraints_are_required`, because this work adds a second dictionary alongside the
  existing list rather than changing what either counts.
* **Triggers and indexes are unchanged, per the brief's explicit instruction.** `V-30-D` and this entry
  are check constraints only. The safe-floor trigger's *body* is D-101's, already covered by
  `TreasuryGuardTests.The_safe_balance_cannot_go_negative`; nothing here widens into trigger bodies or
  index definitions.
* **The other 28 constraints' predicates were watched failing only through the permanent test and the
  two live mutations above**, not individually re-derived by hand beyond the one `psql` query that
  pulled all 30 at once. The dictionary values themselves were not hand-typed against memory — they are
  copied verbatim from that query's output.

**Revisit if.** A slice adds or changes a check constraint. `RequiredCheckConstraints`,
`RequiredCheckConstraintDefinitions` and the migration all move in the same commit, or
`Every_required_check_constraint_has_a_recorded_definition` (a missing definition) or
`The_written_out_check_constraints_and_the_model_agree` (a missing name) catches the omission.

#### §2 — `V-30-G`: the fix is global, and now the regression cover is too

**What was wrong.** `45a939d`'s malformed-body fix (D-099) touches `Program.cs` globally, ahead of
every endpoint. Every assertion in `MalformedRequestTests` ran against test-host probe routes —
`ProbeEndpoint.BodyBindingRoute` and `ProbeEndpoint.BadRequestThrowRoute` — in the `Testing`
environment, where `ThrowOnBadRequest` was already `false` by framework default even before the fix
existed. No test named a shipped route, and none ran the host as `Development` — the one environment
where the original defect (`500` instead of `400`) was actually found. The Verifier closed the gap by
hand, driving nine malformed bodies against three shipped routes live; nothing in the suite would
notice a regression.

**The open question this needed the machine for, tried rather than reasoned about.**
`meetings/2026-09-01-sprint-2-refinement.md` §2.3 item 1: can the Api test host run as `Development`
without tripping `Program`'s start-up guard refusal? **Yes.** The refusal is conditioned on
`missingGuards.Count > 0 && !app.Environment.IsDevelopment()`
[Verified: 2026-09-02 @ `src/Api/Program.cs` -> `missingGuards`] — Development is the one environment
the refusal never fires in, regardless of guard state. Confirmed empirically, not only read: a factory
built with `environment: "Development"` against the shared test database boots and answers requests.

**Decision.**

1. `KaffApiFactory` gained an `environment` constructor parameter, defaulting to `"Testing"` — every
   existing call site is unaffected, and the class remarks explaining *why* `Testing` is the default
   (D-033's refusal must still fail the build here) stand unchanged
   [Verified: 2026-09-02 @ `tests/Api.Tests/Infrastructure/KaffApiFactory.cs` -> `_environment`].
2. `A_malformed_json_body_on_the_shipped_sign_in_route_is_refused_as_a_client_error` posts malformed
   bodies to the shipped, `AllowAnonymous` `POST /api/auth/sign-in` — not a probe route — through the
   existing `Testing`-environment factory, closing the Verifier's own suggested case
   [Verified: 2026-09-02 @ `tests/Api.Tests/MalformedRequestTests.cs` ->
   `A_malformed_json_body_on_the_shipped_sign_in_route_is_refused_as_a_client_error`].
3. `A_malformed_json_body_is_refused_as_a_client_error_when_the_host_runs_as_development` builds a
   second factory with `environment: "Development"` and repeats the malformed-body assertion there —
   the one environment where `ThrowOnBadRequest`'s framework default used to disagree with the fix, so
   this is the one test in the file that would notice the fix being deleted **and** the environment
   reverting to deciding, together
   [Verified: 2026-09-02 @ `tests/Api.Tests/MalformedRequestTests.cs` ->
   `A_malformed_json_body_is_refused_as_a_client_error_when_the_host_runs_as_development`].

**Watched passing on the machine, both halves.** Domain 107/107, Api 235/235 (228 at this session's
baseline plus 7 new: 3 for the shipped-route case, 1 for the Development-host case, 3 for `V-30-D`
above). `dotnet build -c Release --no-incremental`: 0 warnings, 0 errors.
`dotnet format --verify-no-changes`: exit 0. `driver.mjs smoke`: 8/8.

**Not done, and named so it is not mistaken for closed.**

* **`W-5`'s refusal contract is untouched.** A framework-thrown `400` still carries no `messageKey`, in
  every environment, exactly as D-099 left it. Not this entry's to rule.
* **`413` is still unexamined.** D-099 and the sprint-2 refinement (`B3-7`) both named it as open with
  the Architect and UX; this entry adds no coverage for it.
* **The `KaffApiFactory` environment parameter is not exercised anywhere but this one new test.** Every
  other suite still takes the `Testing` default deliberately, per the class remarks — a broken guard
  must still fail the build in every suite that is not specifically testing what Development changes.

**Revisit if.** A future fix to `Program.cs` is meant to behave differently across environments on
purpose — the Development-host test above would need to move from asserting agreement to asserting the
deliberate difference, and should say so in its own remarks when that happens.

**Report anything in this brief that was wrong, applied to itself.** The brief's own citations were
re-verified against the files today rather than repeated: D-101 §5's `pg_get_constraintdef` examples
were re-derived independently (§1 above) rather than copied, and the `missingGuards.Count > 0 &&
!app.Environment.IsDevelopment()` gate (§2 above) was read directly from `Program.cs` rather than taken
from the meeting file's characterisation of it. Both held.

---

### D-103 · Backend — KAFF-105b built: `ProjectTeamRead` born, and the per-project list added to `GET /api/auth/me` · 2026-09-03

**Backend.** `stories/slice-1-foundation/KAFF-105b-api-me-project-list.md`, Ready at 5, sprint-2 demo
scope alongside KAFF-125 (built after this, on the same machine, serialised per Nabil).

#### The permission, born once, exactly where Q43 and D-051 said it would be

`Permission.ProjectTeamRead` [@ `src/Domain/Authorization/Permission.cs`] is new — `PermissionCatalogue`
had no such row before this session
[Verified: 2026-09-03 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Build`, before this
change — neither named it]. One row, `ProjectScoped`, granted to `Role.Owner` and `Role.Hr`
[@ `src/Domain/Authorization/PermissionCatalogue.cs` -> the `Permission.ProjectTeamRead` row],
`TouchesMoney: false`. No change to `IProjectAccessPolicy`: Owner and HR already receive global reach
for *any* `ProjectScoped` permission through `EvaluateAsync`'s role switch
[@ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `EvaluateAsync`], so this row rides the
same mechanism `ProjectAssignmentManage` already uses rather than adding a second one.

**SM-30 paid**, not merely cited: `Owner_and_hr_alone_hold_ProjectTeamRead_and_it_touches_no_money`
[@ `tests/Domain.Tests/CatalogueCompletenessTests.cs`] exists before the row's comment cites it, and
`Hr_holds_no_permission_that_touches_money`'s expected set now includes it.

#### SM-33 paid in the same commit, not merely acknowledged

`Hr_holds_exactly_three_permissions_and_none_touches_money` is renamed to
`Hr_holds_no_permission_that_touches_money`
[@ `tests/Domain.Tests/CatalogueCompletenessTests.cs`], named for the property rather than the count —
the story's own text supplied the replacement name. **This session's own citations moved**: the
`PermissionCatalogue.cs` class remarks and `ProjectAccessPolicy.cs`'s remarks, both of which are this
agent's source, and this entry uses the new name throughout. **What did not move, per the brief's own
boundary and per SM-33 as the story states it**: the citations in `decisions.md` D-056 §2 (`:2438-2441`)
and D-097 §2 (`:7386-7389`), and in `meetings/`, `qa/questions.md`,
`stories/slice-1-foundation/KAFF-107-hr-role-is-bound-to-the-hr-department.md` and
`proposals/N10-project-creation.md`, still name the old identifier. The story text names only
`meetings/`, `qa/` and `proposals/` as the Scrum Master's to move; the two `decisions.md` entries above
are older entries this session did not author and did not edit, on the same reasoning D-090 and D-101
§5 use elsewhere in this file for a citation nobody currently maintaining the entry can silently correct
— **flagged here rather than touched**, so the Scrum Master (or whoever owns a `decisions.md` citation
sweep) knows two more remain.

#### The payload: two CLR types, not one filtered, decided by role rather than by which grant matches

`WhoAmI.Response` gained `Projects` (`IReadOnlyList<ProjectEntry>`) and `TeamProjects`
(`IReadOnlyList<TeamProjectEntry>`) [@ `src/Api/Features/Auth/WhoAmI/Response.cs`]. `ProjectEntry`
carries `ProjectId`, `Name`, `Code`, `AccessPath`, `Level` and the caller's
`ProjectScoped` permissions on that project. `TeamProjectEntry` carries exactly `Name`, `Code` and
`TeamSize` — **no `ProjectId`**, deliberately: `AC-105b-F` fixes HR's type's whole allowed surface to
{name, code, team size, (per-member fields KAFF-115 owns)}, copied verbatim from D-051/D-100's own
"carries" wording, and an internal row key is not in that set. **Flagged rather than assumed**: how
KAFF-115's frontend routes from a project's row to its team screen without an id — by `Code`, which
D-100 calls "the hard identifier" — is a question this story's payload does not answer and KAFF-115
does not raise either. Somebody building that screen's routing needs to decide it.

`Handler.HandleAsync` branches on `user.Role == Role.Hr` directly, not on which catalogue grant
matches, so rule 9 ("HR … does not receive the project dashboard's payload under any circumstance")
holds even if a future catalogue edit blurred the two [@ `src/Api/Features/Auth/WhoAmI/Handler.cs` ->
`HandleAsync`]. **Watched red**: swapping the branch condition to `Role.Owner` (MUT-105b-1, this
session) turned exactly `Hr_gets_names_codes_and_team_sizes_including_an_unstaffed_project_and_nothing_financial`
and `The_owners_reach_needs_no_assignment_row` red — `projects` came back empty for the Owner and
`teamProjects` leaked to HR — nothing else in either suite moved. Reverted.

**The projection, not the permission, is what rule 8 leans on** — the same warning D-055 §2 records for
`UserRead` and this story's own text repeats for `ProjectTeamRead`. Nothing in the catalogue stops a
`ProjectEntry` from growing a financial field later; `HR_and_staff_project_entries_are_distinct_types_with_no_financial_field`
[@ `tests/Api.Tests/MeTests.cs`] is the reflection test that would catch it, on either type, the day it
happens — proved by construction, not exercised against a mutation, because there is no financial field
in the source today to remove and re-add.

#### The per-project permission list is the catalogue, run once more, not a second list

`PermissionEvaluator.ProjectScopedPermissionsHeld(subject, projectId, projectAccess)`
[@ `src/Domain/Authorization/PermissionEvaluator.cs`] is `CompanyWidePermissionsHeld`'s sibling: it runs
`Evaluate` once per `ProjectScoped` catalogue row and reports what agrees, so `AC-105b-J` — a new
`ProjectScoped` grant appears on a project with no change to this endpoint — holds by construction.
Pinned the same way the company-wide method already was
[@ `tests/Domain.Tests/PermissionEvaluatorTests.cs` ->
`A_project_scoped_permission_the_catalogue_grants_agrees_with_evaluate_for_every_row`].

#### The project list itself is queried directly, not through `IProjectAccessPolicy`, and that is deliberate

`IProjectAccessPolicy.EvaluateAsync` answers "may this caller reach *this one* project" — the question a
route with a project id already asks. This endpoint needs every project a caller reaches at once, and
calling the policy once per project would be exactly the N+1 shape it exists to avoid. `Handler`'s
`ProjectsAsync` and `TeamProjectsAsync` [@ `src/Api/Features/Auth/WhoAmI/Handler.cs`] query directly,
mirroring the policy's own two branches — the Owner's `OwnerGlobal` / `Supervisor` pair and the
assignment's own `Assignment` / stored `Level` pair — rather than reimplementing a third rule. `AC-105b-H`
and `AC-105b-I` (a revoked or role-emptied assignment disappears) hold for free from the same
`RevokedAt == null` filter the policy uses, and `AC-105b-I` specifically holds because KAFF-109's
`ChangeUserRole` handler already revokes every active assignment on a role change
[@ `src/Api/Features/Users/ChangeUserRole/Handler.cs` -> `HandleAsync`] — nothing new was built for it,
and the test exercises the real endpoint rather than hand-simulating the revocation
[@ `tests/Api.Tests/MeTests.cs` -> `A_role_change_to_technical_office_empties_the_project_list_on_the_next_call`].

**Team size** is `COUNT` of active `ProjectAssignment` rows per project, grouped once and looked up by
dictionary rather than N+1 counted — derived on every read, never stored, per D-100 and CLAUDE.md's
never-store-a-balance rule extended to any derived count.

#### `AC-105b-G` asserted against the reason it actually holds, not decorated further

The story's own trap: `AC-105b-G` was passing for the wrong reason in a sibling story until 2026-09-01.
This session added no new refusal mechanism — `RequireLiveSession()`'s existing `MayHoldStaffSession`
bar (D-089, D-094) still refuses `Role.Client` before `Handler.HandleAsync` runs at all — and only
extended `A_hand_minted_portal_client_session_is_refused_by_the_staff_door`'s existing assertions to
check that none of this story's three project codes appear in the refusal body
[@ `tests/Api.Tests/MeTests.cs`], rather than adding a second, differently-reasoned test.

#### `EndpointPermissionCoverageTests` — unchanged, and correctly so

`GET /api/auth/me` was already the second `SelfOnlyEndpoints` member (D-086, D-089, D-094); this story
only widens what its handler returns on the same route, under the same `RequireLiveSession()` filter.
No new endpoint, no new row in `SelfOnlyEndpoints` or `AllowList`, no change to `EndpointPermissionCoverageTests`.

#### Watched passing

Clean `--no-incremental` Release build, `-warnaserror`: 0 warnings, 0 errors. `dotnet format
KaffErp.sln --verify-no-changes`: exit 0. Domain **111/111**, up from 107 (four new:
`Owner_and_hr_alone_hold_ProjectTeamRead_and_it_touches_no_money`,
`A_junior_engineers_project_scoped_set_does_not_carry_DraftSubmit_but_a_supervisors_does`,
`A_project_scoped_permission_the_catalogue_grants_agrees_with_evaluate_for_every_row`,
`A_caller_who_must_change_their_password_holds_no_project_scoped_permission_either`). Api **241/241**,
up from 235 (six new, listed in `tests/Api.Tests/MeTests.cs` under the KAFF-105b heading, plus extended
assertions on two existing tests). `scripts/check-citations.ps1`: **1097 checked, 0 broken, 0 legacy**
(up from 1088, this entry's own citations included).

**Watched red twice, against the two rules that matter most here**, both reverted after observing the
expected failure and nothing else:

| Mutation | Red | What it proves |
|---|---|---|
| `Handler`'s role branch swapped `Role.Hr` → `Role.Owner` | `Hr_gets_names_codes_and_team_sizes_including_an_unstaffed_project_and_nothing_financial` (`projects` empty, expected ≥3) and `The_owners_reach_needs_no_assignment_row` (`projects` empty, expected to contain three ids) | The HR/staff separation is enforced by the role check, not accidental, and both directions of the leak are covered |
| `Permission.ProjectTeamRead`'s grants narrowed to `[owner]` | `Owner_and_hr_alone_hold_ProjectTeamRead_and_it_touches_no_money` and `Hr_holds_no_permission_that_touches_money` | SM-30's citation and the renamed SM-33 test both actually exercise the row, not merely name it |

Ran against `kaff_verify` per this session's instructions; `kaff` was not touched. A stray, unrelated
`503 degraded` was observed once against `kaff_verify` directly
(`ck_babs_not_own_parent predicate changed … found "CHECK (true)"`) when this session probed
`/api/health` outside the test suite — **not reproduced inside any of the 241 Api.Tests runs**, which
each boot a fresh host against the same database and passed throughout, including the D-102 guard tests.
Likely a live mutation from concurrent work on the shared `kaff_verify` instance, caught mid-flight;
**not touched, not diagnosed further, and not this story's constraint** — flagged for whoever owns that
database's state next, on the same "do not fix what you did not break" reasoning `CLAUDE.md` states for
`kaff`.

#### What Q43 answered that this build did not have to decide again

Rule 6a fixes `[RefCode] Project Name` as a display format belonging to the rendering stories. Nothing
here concatenates it — `TeamProjectEntry` carries `Name` and `Code` as separate fields, full stop.

#### Not done, and named so nobody assumes it exists

* **The two `decisions.md` citations of the old test name (D-056 §2, D-097 §2) were not edited** — see
  the SM-33 section above. Routed, not fixed.
* **`kaff_verify`'s `ck_babs_not_own_parent` drift was not investigated or repaired** — see the watched-
  passing section above. Unrelated to this story's tables.
* **No frontend work.** `src/Web/` was not touched; KAFF-125 (the staff shell) is the next story on this
  machine and renders this payload.
* **KAFF-115's own route (the per-project team roster — per-member name, role, level) was not built.**
  This story's `TeamProjectEntry` carries no member list; that is KAFF-115's dedicated,
  `ProjectTeamRead`-gated endpoint, named but not built here.
* **No audit record.** `GET /api/auth/me` is a read; CLAUDE.md requires a record on a state change, and
  none occurred.
* **`AC-105b-E`'s wire-level reinforcement is additive, not a replacement.** The rule itself is proved
  where D-055 §2's own reasoning says it must be — against the catalogue
  (`Hr_cannot_reach_a_project_through_ProjectRead`, pre-existing) — and this session's HR test adds a
  same-response check that `ProjectRead` is absent from HR's `permissions` array, not a new mechanism.

#### Questions handed back

None new. The routing-by-code-or-id question above is a flag for KAFF-115's frontend, not a business
rule for Karim — `spec.md` and `decisions.md` are silent on it because it is an implementation detail of
a screen this story does not build.

**Report anything in this brief that was wrong, applied to itself.** The brief's claim that
`ProjectAssignment.Create`'s per-row `AssignmentLevel` "is never flattened to one value" (rule 11,
`AC-105b-A`) and the brief's account of KAFF-109's revocation-on-role-change behaviour were both
re-verified against `src/Domain/Identity/ProjectAssignment.cs` and
`src/Api/Features/Users/ChangeUserRole/Handler.cs` rather than taken on the brief's word, and both held
exactly as stated — the second is what makes `AC-105b-I` true with no new code. The brief's framing of
`AC-105b-F`'s allowed field set as excluding a `ProjectId` was checked against the story text itself
(`AC-105b-F`, KAFF-105b) rather than assumed, and held: the set the story names has no id in it.

---

### D-104 · Frontend — KAFF-125 built: the staff shell, S-004's dispatch, and D-103's payload rendered · 2026-09-03

**Frontend.** `stories/slice-1-foundation/KAFF-125-staff-shell.md`, cut at 3 points and not pulled into
a sprint by name, built on the same machine immediately after D-103 (KAFF-105b) per Nabil's demo
option C. The story's own text is unchanged by this entry — it still reads "Cut… not marked Ready or
BLOCKED" — this record is what actually shipped against it, one session later, once the payload it was
waiting on existed.

#### `AuthService.Session` grew the two fields KAFF-105b added, and nothing re-derives them

`Session` now carries `projects: readonly ProjectEntry[]` and `teamProjects: readonly TeamProjectEntry[]`
[@ `src/Web/src/app/core/auth/auth.service.ts`], typed against the exact CLR shapes D-103 recorded —
`ProjectAccessPath`, `AssignmentLevel`, `Department`, `OperationsSubDepartment` all became real union
types rather than the `string | null` the fields were typed as before this session, because rendering
them as text (`ux/rtl-and-i18n.md`'s `enum.<Type>.<Member>` convention) needs a closed set to switch on
exhaustively. `src/Web/src/app/core/i18n/enum-keys.ts` is one function per enum, each ending in an
`assertNever` default — rtl-and-i18n.md hard rule 4, verbatim: "build the key in the component with an
exhaustive switch so a new enum member is a compile error."

#### S-004 split into a service and a guard, because the shell needed the same fetch from three places

`SessionResolver` [@ `src/Web/src/app/core/auth/session-resolver.ts`] is the one place `GET
/api/auth/me` is called from now. `App`'s constructor calls it once on boot (so a direct load of
`/sign-in` or `/change-password` — routes with no guard of their own — still resolves); the new
`sessionGuard` [@ `src/Web/src/app/core/auth/session.guard.ts`] calls it on the landing route before
deciding whether to bounce to `/sign-in` (`AC-125-B`); `mustChangePasswordGuard` was rewritten to call
it too instead of fetching a second time. All three share one in-flight promise. `AuthService` itself
still holds no `HttpClient` — D-050's discipline, unchanged.

**Sign-out is a new method on `AuthService`, `reset()`, not a second call to `clear()`.** `AC-125-E`'s
own wording — sign-out "returns the shell to resolving" — is literal, not colloquial: `reset()` sets
`asked` back to `false`, the same state a fresh page load starts in, rather than jumping straight to
the `signed-out` resting state `clear()` produces (used only when a `GET /api/auth/me` call itself
fails). `SessionResolver.signOut()` calls the sign-out endpoint, then `reset()`, then immediately
re-resolves — observed directly: the boot spinner is not merely inferred from source, see below.

#### The chrome lives in `App`, not a second wrapper component

`app.ts`/`app.html`/`app.css` now render the three session states directly (`@if (!resolved())` → boot
surface; else `@if (showStaffNav())` → side nav + `router-outlet`; else bare `router-outlet`) rather
than adding a `StaffShellComponent` around a nested route outlet. Slice 1 has exactly one landing
route, so a second router-outlet layer bought nothing yet — noted here so a later slice that adds a
second staff route doesn't read the single-outlet shape as an oversight.

**The side nav's one entry is computed from role through `core/navigation/landing.ts`, shared by `App`
(which nav item) and `LandingPage` (which content).** One function, `landingFor`, decided by role —
not a `switch (role)` menu of the kind rule 6 forbids, because the server itself decided `Projects` vs
`TeamProjects` by role, not by catalogue grant (D-103's own reasoning, applied here rather than
re-derived from a permission that would need to be `ProjectScoped` and therefore invisible at this
company-wide layer anyway).

#### `AC-125-C` is rendered against 2026-09-03's payload, not against the criterion's own 2026-09-02 text — flagged, not silently changed

The criterion as written says a profile-only role sees "no project or assignment… because
`/api/auth/me` carries neither today." That predicate is now false — KAFF-105b shipped `Projects`
hours before this session started, and the brief that opened it said plainly "you render it." `S-005`
(`ux/screen-inventory.md`) has always required "the projects I am assigned to with my level"; this
session renders that field now that it exists, rather than holding to a criterion whose reasoning no
longer applies. **This is a judgement call, not a rule follow, and it is recorded as one rather than
folded into the story silently.** If Nabil or a later Verifier reads `AC-125-C` literally and expects
an empty projects section on this landing, that is the discrepancy to reconcile — not a defect to
silently patch back.

#### Owner and MarketingSales render an honest "not built yet," never an invented dashboard

Both title the real ruled destination (`ux/navigation.md`: S-006 "User list", S-011 "Client list") and
say only that it has not been built — no invented tiles, no date, no placeholder table. The story's own
open questions 1 and 2 (what either role lands on until their screen exists) are **not answered by
this entry** — this is the "stated 'not built yet' message" option the story itself named as one of
three, not a decision that this is the permanent answer. HR's landing renders `TeamProjects` in
D-100's `[RefCode] Name` format with team size as a visually distinct badge (red border/text at zero,
per D-100: "the primary visual indicator… at a glance") — this is **not** S-009a itself: no row is
clickable, because D-103 flagged the row-to-team-screen routing as unresolved and this session does not
invent an answer either.

#### The wildcard route flips to a 404, and the comment naming KAFF-105b as "the shell" is corrected

D-091 named the exact trigger — "when KAFF-103's screen and KAFF-105b's shell arrive" — and D-092 left
it a redirect because only the first was true. Both are true now, so `path: '**'` loads a new
`NotFoundPage` [@ `src/Web/src/app/features/not-found/`] instead of redirecting to `/`. Only the
"not found" third of S-016 — "access denied" and "failed" are server refusals rendered where a request
is actually made, not a routing concern.

#### Watched, not just built

Angular production build: 0 errors, 0 warnings, both before and after the wildcard change. Driven live
against `kaff_verify` end to end, not reasoned about — see the report to Nabil for the full observed/
code-reviewed breakdown per criterion; the short version: **`AC-125-A` was caught on screen** by
throttling the network and screenshotting mid-resolution (`جارٍ التحميل…`, app name and locale switch,
nothing else); a real Owner, a Finance user and an HR user were signed in through the actual sign-in
form (native value-setter + `input` events, the same technique D-100/D-103's own Verifier used for
change-password), forced through `/change-password` and landed on their real screens; `localStorage`
and `sessionStorage` were read before and after every sign-in and every sign-out across the whole run
and were empty every time; the side nav's drawer was confirmed by computed style to sit flush at the
**right** edge under `dir="rtl"` (`left: 134px, right: 390px` in a 390px viewport) and flush at the
**left** edge after switching to English (`left: 0, right: 256`) — logical properties actually flipping,
not a mirror kept correct by luck.

**Test users left on `kaff_verify` for a later session to find:** `karim` / Owner, `sara_finance` /
Finance, `hend_hr` / Hr (all with a changed, non-temporary password by the end of the run). Not
cleaned up — there is no user-delete endpoint and CLAUDE.md does not want one; flagged here on the same
reasoning D-103 flagged its own stray `503` on this shared database.

#### Not done, and named so nobody assumes it exists

* **S-006 and S-011 themselves.** Nothing renders for them beyond the honest "not built" message —
  no endpoint exists for either, and the story's own rule (`agents.md` §3c) forbids asserting a
  criterion that cannot pass.
* **S-009a's real screen and the row-to-team-screen routing question.** Rendered only as far as the
  shared `/api/auth/me` payload goes; the dedicated-HR-API reading `ux/` describes was not picked, and
  no row is clickable.
* **`ux/navigation.md`'s stale `mustChangePassword` refusal paragraph.** Still not this agent's file to
  fix (D-091, D-100 both already flagged it); flagged a third time so it does not keep being found and
  dropped.
* **The two stray `decisions.md` citations of the pre-rename SM-33 test name** (D-056 §2, D-097 §2),
  named again in D-103, still not moved — outside this story's file list either.
* **A dropdown-style account menu.** The header shows the display name and a sign-out button inline
  rather than a popover menu — the substance of "account menu" (rule 1) without building interaction
  slice 1 has no second item to justify.

---

### D-105 · QA/Backend — the E2E suite repaired against what the app is now, and the demo's real ceiling found · 2026-09-03

**QA/Backend, with the machine to itself, per `process/agile.md`.** Two jobs from the brief: repair
`tests/E2E.Tests/SmokeTests.cs`, and build a repeatable client demo. Baseline first, both suites: build
clean (0 warnings, 0 errors, `-warnaserror`), `dotnet format --verify-no-changes` exit 0, Domain
**111/111**, Api **241/241** (against `kaff_verify` — `kaff` still will not boot, untouched per the
brief), citations **1104 checked, 0 broken, 0 legacy** — all four unchanged from the brief's own
baseline, confirmed rather than assumed.

#### 1. The E2E suite: red 2/5, green after — the real figure, not rounded

**Before, measured against a live stack, not inferred:** `Kaff.E2E.Tests.exe` reported **5 total, 2
failed, 3 succeeded** — `The_status_page_reports_the_database_guards_are_installed` and
`The_page_does_not_scroll_sideways_at_phone_width` both timed out waiting on `data-testid="status-guards"`
and `"status-panel"`, exactly as the brief said, because KAFF-125 (D-104) replaced the status page at
`/` with the role-based landing and nothing routes to the old component any more. **After: 6 total, 0
failed, 6 succeeded** — one test added, not merely two repaired.

**Both options in the brief were taken, not one.** The guards assertion now hits `GET /api/health`
directly with a plain `HttpClient`, reading `status`, `guardsInstalled` and `missingGuards` off the JSON
body — the same endpoint `driver.mjs smoke` already asserts, and the fact CLAUDE.md actually cares about
(D-033's database-enforced safety) was never a screen's job to prove.
`An_unauthenticated_visit_to_the_landing_route_is_sent_to_sign_in` is new: it asserts the landing
route's own current surface — `sessionGuard` bouncing a signed-out visitor to `/sign-in`
(`AC-125-B`) — which is what a smoke test should assert about `/` now that it dispatches by role
instead of being a page of its own. The scroll-width test keeps its assertion and only its wait target
changes, from the deleted `status-panel` to `app-title` — rendered in every session state
[@ `src/Web/src/app/app.html`], so it is a wait target the next slice is unlikely to delete out from
under this test the way KAFF-125 did the last one.

**Every assertion can still fail** (agents.md §3c): a genuinely missing guard turns the health check
red with the real missing-guard names in the message (not rounded to a boolean); a broken
`sessionGuard` leaves the page on `/` or sends it somewhere else and the URL assertion misses; a
deleted `app-title` or a reintroduced physical CSS property both still fail the tests that depend on
them. None of the four repaired/added tests passes independent of the property it names.

**One correction to the brief, applied rather than worked around.** It describes "five tests navigating
to `/`" in `SmokeTests.cs`; the file has **four** methods, and five is the whole suite's total once
`SuiteConfigurationTests.cs`'s one `[Fact]` is counted — [Verified: 2026-09-03 @
`tests/E2E.Tests/SmokeTests.cs` -> `SmokeTests`; @ `tests/E2E.Tests/SuiteConfigurationTests.cs` ->
`SuiteConfigurationTests`]. Read directly rather than assumed from the brief's count, per agents.md's
evidence rule.

`E2EEnvironment` gained `ApiBaseUrl`, reading `KAFF_API` with the same name and default
(`http://localhost:5080`) driver.mjs already uses [@ `tests/E2E.Tests/E2EEnvironment.cs` -> `ApiBaseUrl`]
— deliberately the same variable, so a health check pointed at a different host from the driver's own
never becomes a second source of truth to disagree with the first.

#### 2. The orphaned `features/status/status-page.*` — deleted, not routed

**Decided: delete.** `status-page.ts`'s own template already referenced `status.*` i18n keys that were
not in either catalogue any more [checked: `src/Web/public/locales/{ar,en}.json` have no `status`
key], which means the component would have rendered raw untranslated keys the moment anything reached
it — evidence it had already started rotting, not merely gone unrouted. Nothing else in `src/Web`
imported it [Verified: 2026-09-03 — grep for `status-page`/`StatusPage` across `src/` found only the
component's own three files]. Its entire reason to exist — proving the API/DB/guards chain is wired —
is now `driver.mjs smoke`'s job and, inside the app, `GET /api/health`'s own consumer if one is ever
built; giving it a route back would resurrect a screen nothing in `ux/` asks for, which is the
plausible-invention failure mode CLAUDE.md and agents.md both name. Angular's production build is clean
before and after (0 errors, 0 warnings both times) — nothing depended on the three deleted files.

#### 3. Whether a project can be created through the API: no, and it is load-bearing for the demo

**Checked directly, not assumed.** `src/Api/Features/` has exactly five feature folders —
`Health`, `Setup`, `Auth`, `Users`, `Assignments` — no `Projects`, no `Clients`
[Verified: 2026-09-03 — directory listing of `src/Api/Features/`]. `Kaff.Domain.Projects.Project` has a
complete `Create` factory and state machine [@ `src/Domain/Projects/Project.cs` -> `Create`], and
`POST /api/projects/{projectId}/assignments` (KAFF-113) exists to staff a project that must already
exist [@ `src/Api/Features/Assignments/AssignUserToProject/Endpoint.cs` -> `Route`] — but nothing maps a
route that creates the `Project` row the second endpoint's own route parameter names. `scripts/seed-demo.ps1`
proves this live on every run, not only by reading source: its final step `POST`s to `/api/projects` and
gets **404**, because the route is not mapped at all — confirmed against a running host, watched
directly, not inferred.

**Consequence, stated in `deploy/DEMO.md` §1 rather than worked around with SQL.** No project, no
client, no team roster with a nonzero size, no unstaffed-site indicator — D-100's "primary visual
indicator" requirement is unmeetable through real endpoints today, full stop. A raw-SQL project insert
was considered and rejected: `Project.Create` requires a `ClientId` and there is no `Clients` endpoint
either, so a fabricated project would need a fabricated client under it — compounding exactly the
"SQL-inserted data proves nothing and can violate an invariant silently" problem the brief itself warns
against. The demo instead shows the honest empty state each landing already produces on a project-less
database: Hr's "لا توجد مشاريع بعد" and Finance's "لست مُسنداً إلى أي مشروع حتى الآن" are the system
telling the truth, not a placeholder standing in for a feature.

#### 4. The demo: a dedicated `kaff_demo` database, not `kaff_verify`

**`kaff_verify` was ruled out, measured rather than assumed clean.** `GET /api/setup` against it returns
`{"available":false}` — an Owner already exists there (D-104's `karim`/`sara_finance`/`hend_hr`, left
with "a changed, non-temporary password" nobody recorded) — so `POST /api/setup` cannot run again and
the "known credentials, works every time" requirement fails on the first step. The brief's own text
offered the alternative ("`kaff_verify` … or provision your own"); this session provisioned
`kaff_demo` — a plain `CREATE DATABASE … OWNER kaff` alongside the existing two, migrated and guarded
by the API's own `Development` boot sequence, so it can be dropped and recreated from nothing before
every demo. `kaff` and `kaff_verify` are both untouched by this entry.

**Seeding went through `POST /api/setup` → `POST /api/auth/sign-in` → three `POST /api/users` calls,
never through `DbContext` or raw SQL** — `scripts/seed-demo.ps1`, checked in and re-run verbatim during
this session against a freshly recreated `kaff_demo` with the exact output recorded in
`deploy/DEMO.md` §4. Four accounts, covering every landing kind `KAFF-125`'s shell renders today
(Owner and MarketingSales both "not built yet"; Hr and Finance the two data-bearing landings) — credentials
in `deploy/DEMO.md` §4.4, chosen rather than left to be guessed, with `mustChangePassword` on every
account but the Owner (who sets their own password at setup, rule 7).

**Two PowerShell 5.1 traps found and worked around, recorded in `deploy/DEMO.md` §4.3 so the next
session does not rediscover them at cost.** `Invoke-WebRequest`'s own charset guessing corrupted the
Arabic full names in transit — confirmed with `octet_length` vs `length` on the stored row in Postgres
directly (43 bytes for a name whose correct UTF-8 encoding is far shorter), not merely a display
artefact, and this is the same class of bug SKILL.md already documents for editing files with
PowerShell string replacement (D-056 §5), now shown to reach HTTP bodies too. And the auth cookie is
`Secure` [@ `src/Api/Identity/StaffSessionMinter.cs` -> `CookieAttributes`], which .NET's
`CookieContainer` refuses to attach to a plain `http://` request even to `localhost` — a real browser
exempts `localhost` from that rule, a scripted `HttpClient` does not, so the seed script forwards the
`Set-Cookie` value by hand rather than relying on the framework's own cookie handling. **Neither trap
touches the real demo path**: a person driving the app through an actual browser hits neither, and
`scripts/screenshot-demo.mjs` — which drives Chromium exactly the way a person would, native
value-setter plus a real `input` event, the technique D-104 already verified for this exact
signal-forms stack — confirms it: all four accounts signed in, cleared their forced password change
where present, and landed correctly.

**Screenshots taken and looked at, not merely generated** — `deploy/DEMO.md` §5 records what is in each
of the four, at 390×844, Arabic, RTL: Owner and MarketingSales each show the honest "لم يُبنَ هذا
الجزء من النظام بعد" placeholder under their respective "not built yet" heading; Hr shows "لا توجد
مشاريع بعد" under "المشاريع"; Finance shows a correctly-populated profile panel and "لست مُسنداً إلى
أي مشروع حتى الآن" under "مشاريعي". No horizontal scroll at 390px on any of the four. `localStorage`
and `sessionStorage` were read before sign-in and after landing on every run and were empty in both
directions every time — D-050's rule holds under an actual browser, confirmed again rather than taken
on D-104's earlier word.

#### 5. Not done, named so nobody assumes it exists

* **No project-creation endpoint was built.** Deciding whether to build one is an Architect/Nabil scope
  question, not something to invent under a demo brief — the exact failure mode CLAUDE.md and
  agents.md both name as the most expensive one in this project. Flagged in `deploy/DEMO.md` §1 with
  the live 404 as evidence, for whoever picks it up next.
* **Screenshots are not checked into the repository** — binary, and they go stale the moment a screen
  changes. `deploy/DEMO.md` §5 records what was seen in prose; `scripts/screenshot-demo.mjs` regenerates
  them on demand.
* **`kaff_verify`'s existing state was not touched or cleaned up.** Its own leftover accounts (D-104)
  are a separate concern from this entry's; this session's databases are `kaff_demo` (created) and
  `kaff_verify` (read-only, for the Api.Tests baseline and the final E2E confirmation).
* **The demo runbook was not run against staging** (`deploy/README.md`). `deploy/DEMO.md` §7 names the
  parameter changes that would make `scripts/seed-demo.ps1` and `scripts/screenshot-demo.mjs` work
  there, untested.
* **Two pre-existing `decisions.md` citations of the pre-rename SM-33 test name** (D-056 §2, D-097 §2),
  already flagged twice by D-103 and D-104, are still unmoved. Not this entry's file list either — named
  a third time only because it was still true.

**Report anything in this brief that was wrong.** The tests/E2E.Tests file count ("five tests") — §1
above. Everything else in the brief — the E2E numbers, the KAFF-113/D-100 project-picker absence, the
`kaff` outage, `kaff_verify`'s dirty state — was re-verified against the files and the running stack
rather than taken on the brief's word, and held.

---

### D-106 · Scrum Master — `V-32-A` closed: the anti-leak guarantee on `/api/auth/me` is a whitelist on both types, not a blocklist on one · 2026-09-04

**The defect, reproduced before it was fixed.** `AC-105b-F`'s guarantee is carried by one reflection
test [Verified: 2026-09-04 @ `tests/Api.Tests/MeTests.cs` ->
`Hr_and_staff_project_entries_are_distinct_types_with_no_financial_field`]. HR's half pinned an exact
field set. The staff half was a blocklist of seven words — `Value`, `Cost`, `Margin`, `Balance`,
`Budget`, `Status`, `Client`. `Amount`, `Total`, `Price`, `Rate`, `Retention`, `Hold` and `Advance`
were on none of them, and **several of those are `spec.md` §14's own mandated vocabulary**, so the
terminology `CLAUDE.md` requires everyone to use was disproportionately the terminology the guard
could not see.

**Watched failing, in the order that proves the fix rather than asserts it.**

| Step | State of `ProjectEntry` | Api suite |
|---|---|---|
| 1 · reproduce | a `decimal RetainedAmount` property added, blocklist test unchanged | **241 / 241 green** — the defect, exactly as `V-32-A` describes it |
| 2 · fix applied, mutation still present | whitelist | **240 / 241, 1 red** — and the failure message names `RetainedAmount` verbatim |
| 3 · mutation reverted | the six shipped fields | **241 / 241 green** |

Build was **0 warnings, 0 errors** at every step, `-warnaserror`, and `dotnet format
--verify-no-changes` exits 0. Step 1 matters as much as step 2: the old test was watched *passing*
over a money field, so the new one is known to be an improvement rather than merely different.

**The fix is the shape that already existed ten lines above the defect** — `BeEquivalentTo` over the
exact allowed surface, `["ProjectId", "Name", "Code", "AccessPath", "Level", "Permissions"]`
[Verified: 2026-09-04 @ `src/Api/Features/Auth/WhoAmI/Response.cs` -> `ProjectEntry`]. Any added
property fails the test, whatever it is called.

**The blocklist was deleted rather than kept alongside.** Once both types are pinned to an exact set,
a list of bad words can never fire. Keeping it would have left a second, weaker mechanism for a future
session to trust — and its weakness is the whole of this finding.

**Why now.** Slice 3 is Treasury. `ProjectEntry` has no money field to leak today, which is precisely
why the hole stayed green across two sprints. The moment money fields exist, the most natural change
in the world — adding a figure to the project a user is already looking at — ships past a green suite.
This lands before the first Treasury field is written.

**Two things deliberately not fixed, named so they are not assumed done.**

* The raw-string sweep in [Verified: 2026-09-04 @ `tests/Api.Tests/MeTests.cs` ->
  `Hr_gets_names_codes_and_team_sizes_including_an_unstaffed_project_and_nothing_financial`] carries
  the same seven-word omission and runs only on HR's response, which by rule 9 always has an empty
  `projects` array — it is structurally incapable of ever seeing a `ProjectEntry`. It is now redundant
  defence rather than the guarantee, so it was left alone.
* A second blocklist of the same shape exists at [Verified: 2026-09-04 @
  `tests/Domain.Tests/CatalogueCompletenessTests.cs` ->
  `There_is_no_posting_type_or_document_type_for_a_free_form_journal_entry`] — `Manual`, `Other`,
  `JournalEntry`, `Misc` over `PostingType` and `SourceDocumentType`. Same failure shape: an escape
  hatch named `Adjustment` or `General` slips past. Different subject, and not `V-32-A`. **Routed to
  the Architect as an open question, not fixed here** — unlike a DTO's field set, an enum's allowed
  membership is a domain judgement and a whitelist there would have to be maintained by whoever owns
  the account tree.

**Report anything in this brief that was wrong.** The brief called this fix "one line". It is one
assertion replacing five, in one file — the count was the only thing about it that was off, and the
diagnosis, the location and the remedy were all exactly right.

---

### D-107 · Architect — N6 answered, the duplicate-warning contract defined, and `AC-119-E` solved by a mechanism that already exists · 2026-09-04

**Decided by the Architect on the strongest model** per `agents.md` §M — all three are unbackfillable
or decide what the audit trail records. **Recorded here by the Scrum Master; making the decision was
not mine, and none of the three business questions underneath them was answered by anybody.**

#### 1. N6 — the client code is drawn from a PostgreSQL sequence declared on the EF model

`HasSequence<long>("client_code_seq").StartsAt(10001)`, declared in `OnModelCreating`, read by the
create handler immediately before `SaveChangesAsync` and after every validation has passed. Format
`C-{value}`, no zero padding. The generator lives in the handler — **no `IClientCodeGenerator`**: one
implementation, one caller, and `CLAUDE.md` forbids an interface that exists for a second caller
nobody has.

**Declaring it on the model rather than in hand-written migration SQL is the load-bearing part, and it
is not obvious.** The Api migrates on boot while the test harness builds the schema from the model
[Verified: 2026-09-04 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` -> `FindMissingGuardsAsync`
is in the same initialiser that runs both paths]. **A sequence created by migration SQL would exist in
production and not exist under the test harness**, and the entire Api suite would fail on the first
client registration.

**The counter row under `SELECT … FOR UPDATE` was rejected**: it costs a table, a seeded row, a lock
held across the audit interceptor, and serialised registrations — to buy consecutiveness **nobody has
stated a need for.** Read-max-and-retry was rejected on its failure mode: `ux_clients_code` is unique
[Verified: 2026-09-04 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` ->
`ux_clients_code`], so it loses the race as a failed insert, and a retry loop under load is a livelock.

**The cost is gaps, and it is the whole cost.** A sequence is non-transactional, so a rolled-back
insert burns a number and `C-10002` never exists. Drawing `nextval` **last** is the cheap half of the
mitigation: every validation, domain and permission failure happens before a number is taken.

**Reversible in the mechanism, irreversible in the history** — switching later is a migration and every
code already issued stays valid, but **gaps already burned are permanent.** That asymmetry is why
question ① below is worth asking now rather than at acceptance.

**Two consequences that land on QA, not on Backend.** `AC-119-B`'s *"the last client carries `C-10001`
… then it carries `C-10002`"* is **not assertable as literal values** against a shared, gappy test
database — it must be tested as format plus strict successor inside one dedicated test. And **no
fixture may seed a literal `C-1xxxx` code**, or it collides with the generator's range the moment the
sequence reaches it, presenting as an unexplained 500 in an unrelated suite.

#### 2. The duplicate-phone warning — two endpoints, and the warning is never a `Problem`

**The Scrum Master's brief presented this as two open candidates and it was wrong: half was already
foreclosed.** `ux/slice-1-flows.md` S-013 states *"the check still fires on blur of the phone field,
which is why phone is still the first field."* **A check that fires on blur cannot be `POST /api/clients`**,
so a side-effect-free lookup is required, not a candidate.

* **`POST /api/clients/phone-check`** — side-effect-free, gated `ClientManage`, returns **all** matches
  with `isArchived`. `POST` rather than `GET` so the phone stays out of URLs, logs and the audit
  trail's request path, and so a stale warning cannot be cached. **It is not KAFF-124's search**: that
  is a fuzzy search across name, code and phone with an archived filter; this is exact equality on the
  normalised phone including archived. Conflating them means a later change to search ranking silently
  changes what warns.
* **`POST /api/clients`** gains `acknowledgedDuplicatePhone: bool`. The handler **re-runs the match
  server-side** and: no match, the flag is ignored (never record a duplicate that was not there);
  matched and acknowledged, `201`; matched and not acknowledged, **`409 Conflict`**, which
  `ErrorType.Conflict` already maps to [Verified: 2026-09-04 @ `src/Api/Common/Results/ResultExtensions.cs`
  -> `StatusFor`].
* **The edit path carries the identical field, and its match query excludes the client being edited** —
  otherwise resubmitting an unchanged phone warns the operator about themselves. Nothing in KAFF-121,
  S-014 or `ux/components.md` §13 says this; it is engineering, recorded so it is not discovered by a
  tester.

**The `409` does not block the save — it asks, which is what the amendment says in the same breath.**
It is retryable by the same actor with the same data plus one flag; a refusal is not. Without it, a
caller that never checks creates a duplicate and **the trail is silent about it, permanently, in an
append-only table.** This is the shape `CreateUser` already uses: the friendly pre-check is not the
enforcement [Verified: 2026-09-04 @ `src/Api/Features/Users/CreateUser/Handler.cs` -> `HandleAsync`].

**The warning cannot be delivered as a `Problem`**, because the client-side shape is
`{ status, code, messageKey }` and everything else in the body is discarded
[Verified: 2026-09-04 @ `src/Web/src/app/core/api/problem-details.ts` -> `toProblem`] — so a `Problem`
could not name the matched client, and §13's own rule is that a count without a name cannot be
rendered as ruled. The **warning** is a `200` body; the **`409`** is a genuine refusal of an
under-specified request and carries no match data at all.

**Multiple matches: a boolean, not an id.** *"I saw a warning about this number and chose to proceed"*
is sufficient — the audit link is server-derived (§3 below), a single id cannot express N, and §13's
dialog takes a **singular** match input and has no multi-match rendering. **Deliberate simplification,
named:** a new client can appear on that number between the blur check and the save; the acknowledgement
is about the number, not a set of ids, and the audit records what was actually there at save time.

**The shared query and wire type live in `src/Api/Features/Clients/`, not `Domain/`** — Domain has no
EF Core reference and a leak there is a project-file defect. `CLAUDE.md`'s "it moves to `Domain/`" is
satisfied on its own terms: the only *domain* logic here is normalisation, and that is already shared
and uncopied [Verified: 2026-09-04 @ `src/Domain/Common/PhoneNumber.cs` -> `Normalise`]. One static
query returning data, called directly — **not a repository and not a service layer.**

#### 3. `AC-119-E` — neither free text nor a new column

**The Scrum Master put two options to the Architect and it took neither.** The answer is
`IAuditContext.Record<Client>(AuditEventKind.DuplicatePhoneAcknowledged, matchedClientId)`, **one call
per match** — the mechanism D-061 already built for exactly this class of fact, which neither candidate
had used.

The event row's entity id **is** the matched client's id and its entity type is `Client`
[Verified: 2026-09-04 @ `src/Infrastructure/Auditing/AuditContext.cs` -> `Record`], so *"list every
client created as an acknowledged duplicate"* is a join on keys, not prose parsed out of a text column.
`Events` is a list, so N matches are native — the same 1+N shape `DeactivateUser` already writes in one
save, under one correlation id.

**And the unbackfillable part is already in the ground.** `IAuditContext`'s own doc comment says it:
*"Adding a member is a one-line, backfill-free change — the column that stores it is
`AuditRecord.EventType` and it lands with the mechanism, which is the part that cannot be added after
the first consumer"* [Verified: 2026-09-04 @ `src/Domain/Auditing/IAuditContext.cs` -> `AuditEventKind`].
The mechanism landed 2026-08-22. **KAFF-116's `GrantPath` argument does not apply here, because there is
nothing left to add late** — which is the finding, and it is better than either option that was offered.

**Free text was rejected** for putting a server-composed English string into a column whose only
precedent is operator-typed prose, in a system where `CLAUDE.md` forbids the server sending
user-facing strings. **A `MatchedClientId` column was rejected as the wrong shape**: a single `uuid`
cannot hold the N matches D-049 ruling 8 made normal, so the honest column is an array or a child
table — a change to the one audit mechanism in `Domain/`, for one feature.

**What it costs if this is wrong:** one enum member. Today's `AuditEventKind` members are all
authentication events and this is the first master-data one; if the stretch is wrong the cost is a
member that confuses a reader, not a schema. **One residual risk, named:** the event's subject is the
**matched** client, not the created one — the `SignInFailed` precedent exactly, and the enum member's
doc comment must say so.

#### 4. `AC-119-B` is settled structurally, not by a test

*"Ignored or refused"* is two behaviours a test cannot both assert. **The answer is refused
structurally:** `CreateClient`'s request type carries **no `Code` member at all**, so a supplied code is
dropped by the JSON binder and no code path could store one. That matches S-012's *"the UI must never
send the field at all"* and makes the criterion unbreakable rather than merely tested. **The BA owes
`AC-119-B` a one-line rewrite** to say so; the ambiguity is resolved, the story text is not yet.

#### Business questions that remain — named, unanswered, and not any agent's

1. **May client codes have gaps?** For Karim, in his terms: *"A code is drawn the moment a registration
   is saved. If a save fails at the last step that number is used up and never appears — the codes read
   C-10001, C-10003, C-10004. Is a missing number acceptable, or must the sequence be unbroken?"* **The
   mechanism is reversible; the gaps already burned are not.** If he says unbroken, the answer is a
   counter row under lock and every registration serialises.
2. **Must the operator type a reason when proceeding past a duplicate warning?** The amendment says the
   system *asks* and rule 7 says it *records that it was taken* — neither asks for a *why*. **Batch it
   with Q35**, which is the identical question about deactivating a user.
3. **Does ruling 8 cover editing a phone, or only registering a client?** KAFF-121's F-19 already flags
   this as the story's own inference. It is routed because **this decision hardens that inference into
   one shared mechanism** across create and edit — if Karim says edit should refuse, that is a change to
   the shared thing, not to one handler. Non-blocking; the story's reading is the reasonable one.

**Not for Karim, and it needs an owner:** `ux/components.md` §13's match input is **singular** and no
multi-match rendering is specified anywhere. The API returns the list regardless, but the dialog needs
an answer before `AC-119-D`'s second-duplicate case can be demonstrated. **Routed to UX.**

#### What Backend must not start without — four of six worth repeating here

* **KAFF-121's headline capability does not exist.** There is still no setter for `Name`, for the
  primary phone, or for `Kind` [Verified: 2026-09-04 @ `src/Domain/MasterData/Client.cs` ->
  `SetContactDetails` — with `SetTaxRegistration` and `Archive` the only other public mutators]. That is
  the **first** work in KAFF-121, not an assumption under it, and rule 6's Corporate→Individual guard
  must live **with the kind setter**, not in a validator.
* **A new i18n key no story lists** — the `409`'s message key, in **both** catalogues. And the harder
  half: **Frontend must map that `409` to re-opening the dialog, not to an error banner.** A `409`
  rendered as a red banner is a refusal wearing a status code, which is the exact thing ruling 8
  reversed.
* **`phone-check` returns client names and must be gated `ClientManage`.** A "check" endpoint reads as
  innocuous and is precisely where `Role.Client` gets forgotten — `AC-119-G` and `spec.md` §12.
* **`AC-124-C` works only because `Client.Create` upper-cases the code**, so the search term must be
  upper-cased before comparison for `c-10001` to find `C-10001`.

#### The one thing not observed, stated rather than implied

**That `EnsureCreated` materialises a model-declared sequence was reasoned from EF's model differ, not
watched.** It is the one line of this decision worth confirming under `/run-kaff-erp` before the build
order goes out. Its failure mode is loud — `42P01` on the first registration — not silent, which is why
it is acceptable to record it unverified and say so.

**Report anything in this brief that was wrong.** Four corrections came back into it: the blur check
already forecloses half of §2; `SetReason`'s precedent is operator-typed and **nothing in this
repository composes that text itself**, so "already uses it exactly that way" overstated it; there is no
sequence anywhere in this database, which is stronger than the brief claimed; and multi-match on create
follows from `AC-119-D`, not `AC-119-C` as the brief cited. All four are absorbed above.
