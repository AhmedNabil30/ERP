# KAFF-302 · Create a project's account set when a project is created

<!-- kaff id=KAFF-302 slice=3 points=5 state=BUILT verdict=none at=- on=2026-09-14 -->

**Slice:** 3 (Treasury) · **Epic:** Treasury · **Points:** 5 (`stories/backlog.md` slice-3 table) · **Status:** BUILT.
**Spec:** §6.3 (account tree, project × party), §6.4 (the five ledgers), §5.1 (Lump Sum: hold, تشوينات), §5.2 (Cost Plus: "No hold. No تشوينات"), §5.3 (Design: "no hold, no تشوينات") · **Decisions:** D-034 (تشوينات is a liability)
**Register:** none of `Q14`/`Q15`/`Q16`/`Q29` gate this story. `Q88` answered — Karim via Nabil, 2026-09-14, `decisions.md` D-161.
**Owner:** Backend
**Depends on:** `Account.Create` and `AccountTypes` metadata (built, `KAFF-300`'s evidence table). `AccountTreeSeeder` is the company-level precedent this story extends per-project — it explicitly does **not** seed project accounts, "because they are created with the project and the party they belong to" [Verified: 2026-09-14 @ `src/Infrastructure/Persistence/Seeding/AccountTreeSeeder.cs` header remark]. `Project.Create` exists in `src/Domain/Projects/Project.cs`.

## Why this story exists

Today nothing creates a project's ledger accounts. `AccountTreeSeeder` seeds only company-level
accounts (Safe, company control, owner current account, equity, withholding, overheads) and says so
in its own remarks. A project created right now has nowhere for `KAFF-301`'s posting endpoint to post
against — `Account.Create` with `AccountScope.ProjectRequired` needs a `projectId` nobody has supplied
yet, and (for the party-carrying types) a client id.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | A project's accounts are project × party, not a free-form chart | `spec.md` §6.3 — "Two dimensions only: project × party. This is not an open-ended chart of accounts." |
| 2 | Lump Sum projects get a hold ledger and a تشوينات (`MaterialAdvance`) account | `spec.md` §5.1 — hold and تشوينات both named as part of Lump Sum billing |
| 3 | Cost Plus projects get **no hold and no تشوينات** account | `spec.md` §5.2, verbatim: "No hold. No تشوينات." |
| 4 | Design projects get **no hold and no تشوينات** account | `spec.md` §5.3, verbatim: "no hold, no تشوينات" |
| 5 | Cost Plus opens "Project Operating Costs" and "Management/Supervision Revenues"; Design opens "Design Revenues" and "Consulting Costs" — both pairs reuse `ProjectCost`/`ContractRevenue`, named differently per contract type. Accounts can be adjusted from the chart of accounts later. | Karim via Nabil, 2026-09-14 — `decisions.md` D-161 (`Q88`) |
| 6 | Every project account created follows the fixed metadata in `AccountTypes` — class, normal balance, floor — the factory does not let a caller override what the type dictates | [Verified: 2026-09-14 @ `src/Domain/Treasury/Account.cs` -> `Create`, `ValidateScope`, `ValidateParty`] |
| 7 | تشوينات is a liability, not an asset, and carries no floor | D-034 · [Verified: 2026-09-14 @ `src/Domain/Treasury/AccountTypeMetadata.cs` -> `AccountType.MaterialAdvance` row] |
| 8 | Seeding a set of accounts must be additive and idempotent — creating the set twice for the same project must not duplicate or edit an existing account | `CLAUDE.md` — "Never update or delete a posting" extends to "never re-derive an account row that already exists"; pattern already established in `AccountTreeSeeder.AddIfMissingAsync` |
| 9 | Architecture: no domain-event bus, no MediatR exists in this codebase, so the account set is created synchronously inside the `CreateProject` handler, not via a fired-and-forgotten event | `CLAUDE.md` — "Do not introduce... MediatR"; [Verified: 2026-09-14 — no `IDomainEvent`/`DomainEvents`/`INotification` type exists anywhere under `src/Domain`] |

## Permissions, money, audit, i18n

- **Permissions.** No new permission. Creating the account set is a side effect of `Permission.ProjectCreate`
  (Owner and Technical Office, company-wide, D-055 §1) — the same act that already governs whether a
  project may be created at all. This story adds no endpoint of its own.
- **Money.** No money moves. Opening an account is not a posting (`AccountManage`'s own remark: "Opening
  an account is not moving money through it" [Verified: 2026-09-14 @ `PermissionCatalogue.cs` line 474]).
  Every account is created with `EnforceNonNegative` exactly as `AccountTypes` dictates — rule 6.
- **Audit.** `CLAUDE.md` requires an audit record for every state change. Opening a project's account
  set is a state change; it must be attributable to the same actor and timestamp as the project's
  creation, not written as an anonymous seeding step.
- **i18n.** No new user-facing string. Account names are stored bilingually (`NameAr`/`NameEn`) exactly
  as `Account.Create` already requires.

## Acceptance criteria

**AC-302-A — a Lump Sum project gets a client advance, hold, firm advance and تشوينات account, plus a client sub-ledger**
Given a Lump Sum project is created for a client
When creation succeeds
Then accounts of type `ClientAdvance`, `Hold`, `FirmAdvance`, `MaterialAdvance` and `ClientReceivable` exist, scoped to that project and that client, each carrying the class/normal-balance/floor `AccountTypes` dictates
*Rule: 1, 2, 6, 7.*

**AC-302-B — a Cost Plus project gets no hold and no تشوينات account, and gets a cost and a revenue account**
Given a Cost Plus project is created
When creation succeeds
Then no `Hold` account and no `MaterialAdvance` account exist for that project, and a `ProjectCost` account named "Project Operating Costs" and a `ContractRevenue` account named "Management/Supervision Revenues" both exist
*Rule: 3, 5.*

**AC-302-C — a Design project gets no hold and no تشوينات account, and gets a cost and a revenue account**
Given a Design project is created
When creation succeeds
Then no `Hold` account and no `MaterialAdvance` account exist for that project, and a `ProjectCost` account named "Consulting Costs" and a `ContractRevenue` account named "Design Revenues" both exist
*Rule: 4, 5.*

**AC-302-D — عهدة is not opened at project creation**
Given any project of any contract type is created
When creation succeeds
Then no `PettyCashAdvance` account is created as part of the set — عهدة is per-employee (`AccountScope.ProjectRequired` **and** `PartyType.Employee`) and there is no employee to attach one to yet
*Rule: 1. `KAFF-311` opens a عهدة account when a specific request names its holder.*

**AC-302-E — a project's account set is created in the same transaction as the project, or the project creation fails**
Given a project creation request
When account creation would fail (a metadata defect, a database error)
Then the project is not left in a state where it exists with no accounts to post against
*Rule: 8, 9. Consistency requirement — not a specific rollback mechanism, since none is prescribed by spec.md.*

**AC-302-F — creating the set twice for the same project is a no-op the second time**
Given a project whose account set already exists
When the creation step runs again (a retried request, a re-run migration path)
Then no duplicate account is created and no existing account is edited
*Rule: 8.*

**AC-302-G — every created account passes the same validation `Account.Create` already enforces**
Given the account set for any contract type
When each account is created
Then scope, party and metadata validation all pass exactly as they would for a hand-built `Account.Create` call — no bypass, no direct database insert
*Rule: 6.*

## Definition of Ready

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ |
| Stable `AC-302-<LETTER>` ids | ✅ |
| Every business rule cites a `spec.md` section, a D-number, or a `[Verified: ...]` citation | ✅ |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — none new; rides on `ProjectCreate` |
| Money behaviour named explicitly | ✅ — none moves |
| Arabic UI strings as i18n keys | ✅ — n/a, no new string |
| The audit record it writes is stated | ⚠️ Named as a requirement (rule per `CLAUDE.md`); the exact mechanism (shared `Domain/Auditing` writer vs. bespoke) is not verified against a concrete `AuditRecord` call site for Treasury and is left to Backend to wire against the existing mechanism |
| QA has written at least one scenario that fails if the rule is broken | ✅ **Met, 2026-09-14.** `qa/slice-3/test-cases.md` `TC-3-033`…`039` |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ |

## Not in this story

- **The posting endpoint.** `KAFF-301`.
- **Which permission gates opening an *individual* account outside a project's creation** (`AccountManage`,
  already catalogued) — this story only concerns the automatic set tied to `ProjectCreate`.
- **عهدة's own account lifecycle, ceiling, and per-request cap.** `KAFF-311`.
- **Linked-project (`design_to_execution`, `parent_child`) account wiring.** `spec.md` §5.4 is not
  addressed here; a linked execution project's 30% design credit is an `Adjustment` posting, not an
  account-creation concern, and is out of scope for slice 3a.
- **Bank accounts.** Company-level, seeded separately, blocked on `Q15`.

## Questions for Karim

None open. `Q88` answered — Karim via Nabil, 2026-09-14, `decisions.md` D-161.
