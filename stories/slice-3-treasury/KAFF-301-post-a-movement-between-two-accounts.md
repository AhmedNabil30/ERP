# KAFF-301 · Post a movement between two accounts, append-only

<!-- kaff id=KAFF-301 slice=3 points=8 state=BUILT verdict=none at=- on=2026-09-14 -->

**Slice:** 3 (Treasury) · **Epic:** Treasury · **Points:** 8 (`stories/backlog.md` slice-3 table) · **Status:** BUILT, 2026-09-14 (`5605539`, `04147e0`).
**Spec:** §6.1 (posting model), §6.2 (non-cash from day one), §6.4 (five ledgers), §6.10 (project/company tag), §9 (permissions) · **Decisions:** D-033 (guarded database refuses to start), D-044 §6 (four-decimal precision) and §8 (which three accounts are floored)
**Register:** `stories/questions-for-karim.md` — none of `Q14`/`Q15`/`Q16`/`Q29` gate this story. New: `Q87` (see *Questions for Karim* below)
**Owner:** Backend
**Depends on:** nothing new in `Domain/`. `Posting.Create`, `Account`, the eight database guards and the `PostingType`/`AccountType` catalogues are already built (`KAFF-300`'s own evidence table). This story is the **endpoint** — request, validator, handler, response — that calls the domain method that already exists, plus the permission wiring `KAFF-300` explicitly left undone (*"This story writes a fixture and nothing that serves a request. Any Treasury endpoint... is `KAFF-301` and after."*).

## Why this story exists

`KAFF-300`'s fixture proves the domain arithmetic reconciles. Nothing today lets a user, or another
feature, actually create a `Posting` through an HTTP request — `Posting.Create` and `Posting.Reverse`
are called only from test code
[Verified: 2026-09-14 @ `tests/Domain.Tests/PostingRuleTests.cs`, `tests/Api.Tests/TreasuryGuardTests.cs`].
This story is the first Treasury endpoint: `POST` a movement between two named accounts, validated by
the domain, persisted append-only, re-checked by the database guards regardless of what the domain
already caught.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Every financial event is a `Posting`. Balances are always derived by summing postings, never stored | `spec.md` §6.1 · `CLAUDE.md` |
| 2 | Postings are append-only — no update path, no delete path. The database refuses `UPDATE`, `DELETE` and `TRUNCATE` on the table | `spec.md` §6.1 · [Verified: 2026-09-14 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` -> `trg_postings_append_only`, `trg_postings_no_truncate`] |
| 3 | Amount is always positive; direction lives in the from/to account pair, never in the sign | `spec.md` §6.1 · [Verified: 2026-09-14 @ `src/Domain/Treasury/Posting.cs` -> `Validate`, `TreasuryErrors.AmountMustBePositive`] |
| 4 | Four decimals in storage and calculation; `decimal(18,4)` throughout, no `float`/`double` | D-044 §6 · `spec.md` §6.1 amendment · [Verified: 2026-09-14 @ `src/Infrastructure/Persistence/Configurations/TreasuryConfigurations.cs` -> `PostingConfiguration` — precision comes from the `Money` convention in `KaffDbContext.ConfigureConventions`] |
| 5 | The five ledgers never net against each other | `spec.md` §6.4 · [Verified: 2026-09-14 @ `001_guards.sql` -> `kaff_postings_validate`, error `KAFF_LEDGER_NETTING`] |
| 6 | The hold only grows; nothing may post out of it except `HoldRelease`, and a reversal is exempt because it corrects an accrual, not a withdrawal | `spec.md` §5.1 · [Verified: 2026-09-14 @ `001_guards.sql` -> `kaff_postings_validate`, error `KAFF_HOLD_DEBIT`] |
| 7 | A posting names its project or is company-level — never both, never neither, and the tag must match the accounts it moves between | `spec.md` §6.10 · [Verified: 2026-09-14 @ `Posting.cs` -> `ValidateProjectTag`; `001_guards.sql` errors `KAFF_PROJECT_TAG`, `KAFF_CROSS_PROJECT`] |
| 8 | Exactly three accounts carry a hard non-negative floor — the Safe, the client advance and عهدة. A payment that would breach one fails and, for the Safe, prompts an owner injection | D-044 §8 · `spec.md` §6.1, §15 · [Verified: 2026-09-14 @ `001_guards.sql` -> `kaff_check_non_negative_balance`, error `KAFF_NEGATIVE_BALANCE`] |
| 9 | A closed accounting period is immutable; no posting may date inside one | `spec.md` §6.6 · [Verified: 2026-09-14 @ `001_guards.sql` -> `kaff_postings_validate`, error `KAFF_CLOSED_PERIOD`] |
| 10 | The domain check and the database guard both exist for the same rules. If they ever disagree, the database wins and the domain has a bug — this endpoint must not skip the domain call to save a round trip | [Verified: 2026-09-14 @ `src/Domain/Treasury/TreasuryErrors.cs` header remark] |
| 11 | The application refuses to start when a database guard is missing, so this endpoint always runs against a guarded schema | D-033 · [Verified: 2026-09-14 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` -> `FindMissingGuardsAsync`] |

## Permissions, money, audit, i18n

- **Permissions.** `Permission.TreasuryPostProject` (project-scoped, Finance) posts a project-tagged
  movement; `Permission.TreasuryPostCompany` (company-wide, Finance) posts a company-tagged one
  [Verified: 2026-09-14 @ `src/Domain/Authorization/PermissionCatalogue.cs` lines 464-465]. Per
  `CLAUDE.md`, a project-scoped grant still needs the assignment row — role alone is not enough.
  **⛔ Open question, see below:** whether this endpoint also requires `Permission.FinancialMovementApprove`
  (Owner) before it executes, or whether approval belongs entirely to the calling feature (an extract,
  a collection, a عهدة settlement) and this endpoint is the low-level primitive those features call
  once they already hold approval. `spec.md` §9's *"nobody creates and approves the same movement"*
  and *"Owner approves all financial movements"* do not say which layer a bare two-account posting
  sits at. **Not decided here.**
- **Money.** `decimal(18,4)` throughout (rule 4). No `float`/`double` anywhere in the request, the
  handler or the response. The response echoes the stored amount, never a recomputed one.
- **Audit.** `CLAUDE.md`: every state change writes an audit record — who, when, what changed. A
  `Posting` is itself an immutable audit trail of the movement (who: `CreatedByUserId`; when:
  `CreatedAt`; what: the account pair, amount, type), but `spec.md` §9's audit trail is a separate,
  Owner-only read surface (`Permission.AuditRead`, D-049 §1) and this story does not build a second
  mechanism — it writes into the existing one if a shared `Domain/Auditing` writer already exists for
  financial handlers. **⛔ Not verified here whether such a shared writer exists for Treasury handlers
  specifically** — see *Questions for Karim* / hold at Ready check.
- **i18n.** Every `TreasuryErrors` code the domain can return already carries an `errors.treasury.*`
  key [Verified: 2026-09-14 @ `src/Web/public/locales/en.json` lines 228-252]. This endpoint returns
  `Problem(error)` for every domain failure, never a raw string. A refusal that reaches the database
  guard instead of the domain check (a race, or a rule the domain did not pre-validate) is `KAFF-319`'s
  problem, not this one's — see *Not in this story*.

## Acceptance criteria

**AC-301-A — a valid posting is created and is retrievable by its id**
Given two active, postable accounts of the same currency, in the same ledger scope, with matching project tags
When Finance posts a movement between them with a positive amount, a `PostingType` and a `SourceDocument`
Then the posting is created, `IsReversal` is false, and its id, date, accounts, amount and type are returned
*Rule: 1, 3. `spec.md` §6.1's shape — nothing added, nothing dropped.*

**AC-301-B — the amount is stored and returned at four decimal places, never as a float**
Given an amount with four decimal places
When the posting is created and then read back
Then the amount matches to the fourth decimal exactly, and no floating-point type appears anywhere in the request/response contract
*Rule: 4. D-044 §6.*

**AC-301-C — a negative or zero amount is refused with a translated message**
Given an amount that is zero or negative
When the posting is attempted
Then it is refused with `errors.treasury.amount_must_be_positive`, and no row is written
*Rule: 3.*

**AC-301-D — a posting that would net two of the five ledgers is refused**
Given a from-account in the client advance ledger and a to-account in the hold ledger
When the posting is attempted
Then it is refused with `errors.treasury.ledgers_must_not_net`
*Rule: 5.*

**AC-301-E — a posting out of the hold ledger before handover is refused, unless it is `HoldRelease`**
Given a hold account with an accrued balance and a project not yet at handover
When a posting of any type other than `HoldRelease` attempts to move value out of it
Then it is refused with `errors.treasury.hold_only_grows`
*Rule: 6. `CLAUDE.md`: "If you write code that debits the hold ledger before handover, you have misread the spec."*

**AC-301-F — a payment that would breach the Safe's floor is refused, not silently clamped**
Given the Safe's derived balance is less than the posting amount
When a posting would draw the Safe below zero
Then it is refused with `errors.treasury.negative_balance`, and the response names that an owner injection is the next step
*Rule: 8. `spec.md` §6.1: "A payment that would breach this fails and prompts an owner injection instead."*

**AC-301-G — a posting whose project tag does not match its accounts is refused**
Given one account belonging to project A and a `projectId` naming project B (or naming none, or naming one where the accounts are company-level)
When the posting is attempted
Then it is refused with `errors.treasury.project_tag_required` or `errors.treasury.project_tag_forbidden` as appropriate
*Rule: 7.*

**AC-301-H — a posting dated inside a closed accounting period is refused**
Given an `AccountingPeriod` closed for the posting's date
When the posting is attempted
Then it is refused with `errors.treasury.closed_period`
*Rule: 9.*

**AC-301-I — role without project assignment is refused**
Given a Finance user holding `TreasuryPostProject` but with no assignment row on the target project
When they attempt a project-tagged posting
Then the request is refused server-side with a 403, before any domain or database check runs
*`CLAUDE.md`: "Every endpoint checks two things: role and assignment... Server-side, always."*

**AC-301-J — every rule the domain checks is also enforced by the database — HELD, cross-cutting with `KAFF-319`** *(HELD)*
Given a request that could bypass the domain layer (a direct write, a future caller that forgets to call `Posting.Create`)
When the row is attempted
Then the database guard refuses it independently of whether the domain checked first
*Rule: 10. This criterion is satisfied by the guards already in `001_guards.sql` — it is listed to make the property explicit for this endpoint's tests, not to build new SQL.*

## Definition of Ready

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ |
| Stable `AC-301-<LETTER>` ids | ✅ |
| Every business rule cites a `spec.md` section, a D-number, or a `[Verified: ...]` code citation | ✅ |
| No uncited rule | ✅ |
| Permissions named explicitly | ⚠️ `TreasuryPostProject`/`TreasuryPostCompany` are named; whether `FinancialMovementApprove` also gates this endpoint is **open** — see Questions for Karim |
| Money behaviour named explicitly | ✅ — rule 4, `AC-301-B` |
| Arabic UI strings as i18n keys | ✅ — reuses existing `errors.treasury.*` keys, no new string coined here |
| The audit record it writes is stated | ⚠️ Partially — the posting is itself the record of what moved; whether a separate `Domain/Auditing` write also fires is **not verified** |
| QA has written at least one scenario that fails if the rule is broken | ✅ **Met, 2026-09-14.** `qa/slice-3/test-cases.md` `TC-3-023`…`032` — A–I cased, J HELD cross-cutting `KAFF-319` |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — the open question narrows one permission detail, it does not block the endpoint's core criteria |

**QA cases landed 2026-09-14 (`TC-3-023`…`032`); trailer flipped to `READY`.** `Q87` (whether a bare posting needs `FinancialMovementApprove`) stays open and gates nothing built here — Backend builds against `TreasuryPostProject`/`TreasuryPostCompany` alone, per the open-question row below.

## Not in this story

- **The posting-type × account-pair legality table.** Whether a `SupplierPayment` may legally run
  between a `Safe` and a `SubcontractorPayable` account is `KAFF-305`. This endpoint accepts any
  `PostingType` the domain and database do not already refuse.
- **Reversal.** `KAFF-303`.
- **The reusable balance-derivation query.** `KAFF-304`.
- **Translating a raw database-guard exception (as opposed to a domain `Result` failure) into an i18n message.** `KAFF-319`.
- **Any ledger-specific posting flow** — client advance recovery, hold accrual timing, تشوينات
  schedule, firm advance cap, عهدة lifecycle, owner current account, collections, withholding.
  `KAFF-306`–`318`. This story builds the generic primitive; it asserts nothing about *when* a
  particular ledger's postings should happen.
- **Project account creation.** `KAFF-302`.

## Questions for Karim

| # | |
|---|---|
| **`Q87`** | **"Does a plain two-account posting need the Owner's approval before it is written, the way an extract or a change order does — or is approval something the feature calling this endpoint (an extract, a عهدة settlement, a collection) is responsible for having already obtained?"** Say why it is being asked: `spec.md` §9 says "Owner approves all financial movements" and "nobody creates and approves the same movement," and separately grants `TreasuryPostProject`/`TreasuryPostCompany` to Finance alone with no paired approval permission on this endpoint. Both readings are defensible — a generic primitive with approval pushed to callers, or a primitive that itself blocks on `FinancialMovementApprove` — and they produce different endpoints. **Not inferred here; affects `AC-301-A` and every later story that calls this endpoint.** |
