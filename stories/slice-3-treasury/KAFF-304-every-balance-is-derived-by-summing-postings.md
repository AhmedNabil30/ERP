# KAFF-304 · Every balance is derived by summing postings

<!-- kaff id=KAFF-304 slice=3 points=5 state=READY verdict=none at=- on=2026-09-14 -->

**Slice:** 3 (Treasury) · **Epic:** Treasury · **Points:** 5 (`stories/backlog.md` slice-3 table) · **Status:** NOT-BUILT.
**Spec:** §6.1 ("Balances are always derived by summing postings and MUST NOT be stored"), §6.4 (the five ledgers reported separately, never netted)
**Register:** none of `Q14`/`Q15`/`Q16`/`Q29` gate this story. New: `Q90` — **blocking**, see Definition of Ready
**Owner:** Backend
**Depends on:** the `account_balances` database view and the `AccountBalance`/`LedgerBalances` domain
types already exist and are already mapped as a keyless read-only EF entity
[Verified: 2026-09-14 @ `src/Infrastructure/Persistence/Sql/002_views.sql`,
`src/Domain/Treasury/AccountBalance.cs`, `src/Infrastructure/Persistence/KaffDbContext.cs` ->
`DbSet<AccountBalance> AccountBalances`]. `KAFF-300`'s fixture reads balances by summing `Posting` rows
directly for its own scenario, not through this view or through any reusable query
[Verified: 2026-09-14 @ `tests/Domain.Tests/` — `KAFF-300`'s own AC-300-D discussion].

## Why this story exists, and what is actually new here

**What already exists and is not rebuilt by this story:** the derivation mechanism itself. There is no
balance column anywhere in the schema; `account_balances` computes `inflow`, `outflow`, `raw_balance`
and `signed_balance` as a SQL view over an index, and `AccountBalance` maps it read-only. This is the
thing `CLAUDE.md` and `spec.md` §6.1 require, and it is done.

**What is new:** nothing today *calls* this view for a purpose other than `KAFF-300`'s own fixture math,
and no general-purpose, reusable read exists that another feature (an extract screen, a dashboard, a
عهدة balance check) can call to get one account's balance or one project's five-ledger snapshot without
writing its own LINQ against `context.AccountBalances`. This story is that reusable mechanism — a
`Domain` query object and/or a read endpoint that every later Treasury story (`KAFF-306`–`318`) and
every non-Treasury feature that needs to show a balance calls, instead of each writing its own query
against the view.

**Do not duplicate `KAFF-300`.** That story's fixture is a citation of §15's own numbers, asserted by
walking postings by hand for one scenario; it is not, and does not need to become, this story's
mechanism. This story exists so the *next* feature that needs a balance does not write a sixth copy of
the same query.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Balances are always derived by summing postings, never stored | `spec.md` §6.1 · `CLAUDE.md` |
| 2 | The five ledgers are reported as five separate figures; no `Total` property, no netting | `spec.md` §6.4 · [Verified: 2026-09-14 @ `AccountBalance.cs` -> `LedgerBalances` record and its remarks — "There is no `Total` property and there must never be one"] |
| 3 | تشوينات is reported alongside the five but is not one of them and is not subject to the netting prohibition | `spec.md` §6.4, §15 · [Verified: 2026-09-14 @ `AccountBalance.cs` -> `LedgerBalances` remarks] |
| 4 | `RawBalance` is inflow minus outflow with no regard to direction; `SignedBalance` multiplies by the account's normal direction, so a liability with money owed on it reads positive | [Verified: 2026-09-14 @ `AccountBalance.cs` -> property remarks; `002_views.sql`] |
| 5 | The read must stay a single aggregate query over an index, not a client-side sum over materialised postings, so a balance check stays cheap enough to run inline (e.g. before a posting) | [Verified: 2026-09-14 @ `AccountBalance.cs` remarks — "the summation happens in PostgreSQL over an index... rather than materialising a project's postings into memory"] |

## Permissions, money, audit, i18n

- **Permissions.** **⛔ Open question, not answered by `spec.md` §9 or by the permission catalogue:**
  no `Permission.TreasuryRead`, `BalanceRead` or equivalent exists today
  [Verified: 2026-09-14 — `PermissionCatalogue.cs` has no permission whose name contains `Balance`,
  `Ledger`, or a Treasury-scoped `Read`]. Reading a project's ledger balances is money-adjacent and must
  not be reachable by `Role.Client` (D-035: a portal user must never see costs or margins) or by HR
  (D-044 §2: "HR... cannot see project costs, margins, or the safe"), but which of the internal roles —
  Finance, Owner, Technical Office, Site Engineer — may read which ledger is not stated anywhere read
  for this story. **Not guessed here.**
- **Money.** No money moves. Every figure returned is `decimal`, matching the view's `numeric(18,4)`
  columns exactly — rule 4, `CLAUDE.md`'s money rules apply to a read as much as to a write.
- **Audit.** None. A read writes no audit record.
- **i18n.** No new user-facing string from this story alone; a permission refusal uses the existing
  generic 403 path.

## Acceptance criteria

**AC-304-A — a single account's balance is returned as inflow, outflow, raw and signed**
Given an account with a known posting history
When its balance is requested through the new mechanism
Then `Inflow`, `Outflow`, `RawBalance` and `SignedBalance` all match a hand-summed total over that account's postings, to four decimal places
*Rule: 1, 4.*

**AC-304-B — a project's five ledgers are returned as five figures, never one**
Given a project with postings against several of its five ledgers
When `LedgerBalances` is requested for that project
Then the response carries `ClientAdvance`, `Hold`, `FirmAdvance`, `PettyCashAdvance` and `OwnerCurrentAccount` as five independent figures, plus `MaterialAdvance` alongside them
And there is no field anywhere in the response that sums or nets any two of the five
*Rule: 2, 3.*

**AC-304-C — the mechanism is reusable, not duplicated per caller**
Given two different features that both need a project's hold balance (for example, a hold-release check and a dashboard)
When each calls the mechanism this story builds
Then both go through the same query object/endpoint, not two independently written LINQ queries against `account_balances`
*This is the story's own reason for existing — see "What is actually new here."*

**AC-304-D — a balance query never materialises postings into application memory**
Given a project with a large posting history
When its balance is read
Then the query executes as a single aggregate against the database (the `account_balances` view or an equivalent index-backed aggregate), not a fetch-then-sum in C#
*Rule: 5.*

**AC-304-E — an account with no postings returns a zero balance, not an error**
Given a newly created account with no postings yet
When its balance is requested
Then `Inflow`, `Outflow`, `RawBalance` and `SignedBalance` are all zero
*Consistency requirement — `account_balances`' own `COALESCE(..., 0)` columns already guarantee this at the SQL level; the criterion pins that the reusable mechanism does not reintroduce a null-handling bug on top.*

## Definition of Ready

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ |
| Stable `AC-304-<LETTER>` ids | ✅ |
| Every business rule cites a `spec.md` section, a D-number, or a `[Verified: ...]` citation | ✅ |
| No uncited rule | ✅ |
| Permissions named explicitly | ⚠️ **Not named for the HTTP surface — `Q90`, open.** `READY` is scoped to the `Domain`-level derivation query only; the endpoint that exposes it over HTTP is held on `Q90` and is **not** built by this pass |
| Money behaviour named explicitly | ✅ |
| Arabic UI strings as i18n keys | ✅ — n/a |
| The audit record it writes is stated | ✅ — none |
| QA has written at least one scenario that fails if the rule is broken | ✅ **Met, 2026-09-14.** `qa/slice-3/test-cases.md` `TC-3-046`…`051` — `051` HELD on `Q90` for the HTTP permission gate |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⚠️ **The permission gap blocks the endpoint shape, not the underlying query object** — the `Domain`-level query can be built and tested against `AC-304-A`, `B`, `D`, `E` today; only the HTTP-facing permission gate on `AC-304-C`'s callers is held |

## Not in this story

- **Domain-level query object built 2026-09-14** (`src/Domain/Treasury/BalanceQueries.cs`,
  `tests/Domain.Tests/BalanceQueriesTests.cs`, AC-304-A/B/D/E). **The HTTP endpoint remains blocked
  on `Q90`** and is not built.
- **Any specific screen or report that displays a balance** — an extract's ledger summary, an owner
  dashboard, a عهدة balance check. Those are `KAFF-306`–`318` and later slices; this story is the one
  mechanism they all call.
- **The posting endpoint and the reversal endpoint.** `KAFF-301`, `KAFF-303`.
- **§15's own worked-example fixture.** `KAFF-300`, already built, asserted by hand for its one scenario.
- **Trial balance / statement generation.** `spec.md` §6.6, slice 7.

## Questions for Karim

| # | |
|---|---|
| **`Q90`** | **"Who is allowed to see a project's ledger balances — the client advance, the hold, the firm advance — day to day? Finance and the Owner, obviously. Does Technical Office or a Site Engineer assigned to the project see any of it, or is it Finance-and-Owner only?"** Say why it is being asked: `spec.md` §9 names roles and their financial *approval* powers in detail but never states who may *read* a ledger balance, and no `Permission` in the catalogue is named for it. D-035 and D-044 §2 rule out the Client and HR by name, which narrows the answer but does not give it. **Blocks `KAFF-304`'s endpoint shape — the query object itself does not need this answer, but nothing can be exposed over HTTP until it is.** |
