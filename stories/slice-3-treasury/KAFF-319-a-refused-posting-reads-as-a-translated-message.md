# KAFF-319 · A refused posting reads as a translated message, not a 500

<!-- kaff id=KAFF-319 slice=3 points=3 state=BUILT verdict=none at=- on=2026-09-15 -->

**Slice:** 3 (Treasury) · **Epic:** Treasury · **Points:** 3 (`stories/backlog.md` slice-3 table) · **Status:** NOT-BUILT.
**Spec:** §1 (no scope for a bare error page — implicit in "no hardcoded user-facing strings," `CLAUDE.md`) · **Decisions:** D-033 (guarded database refuses to start — the sibling problem: this story is about what happens when a *running*, guarded database's own guard fires)
**Register:** none of `Q14`/`Q15`/`Q16`/`Q29` gate this story
**Owner:** Backend
**Depends on:** `KAFF-301`, `KAFF-303`, `KAFF-305` (they produce most of the refusals this story must
translate). The generic `Result` → `Problem` pipeline already exists and already carries an i18n
`messageKey` for every domain-level refusal
[Verified: 2026-09-14 @ `src/Api/Common/Results/ResultExtensions.cs`]. The precedent for catching a
raw database exception and mapping it to a translated domain error already exists for a unique-index
collision elsewhere in the codebase
[Verified: 2026-09-14 @ `src/Api/Features/Babs/CreateBab/Handler.cs` -> `IsCodeCollision`, matching
`PostgresException.SqlState == PostgresErrorCodes.UniqueViolation` and `ConstraintName`].

## Why this story exists, and precisely what gap it closes

**What is already solved.** Every `TreasuryErrors` member the *domain* can raise — `AmountMustBePositive`,
`LedgersMustNotNet`, `HoldOnlyGrows`, `NegativeBalance`, and so on — already carries a matching
`errors.treasury.*` i18n key [Verified: 2026-09-14 @ `en.json` lines 228-252], and `ResultExtensions`
already turns any `Result` failure into a `ProblemDetails` body carrying that key, never raw prose
[Verified: 2026-09-14 @ `ResultExtensions.cs` remarks: "the API must not send prose... for the client
to display"]. A domain-level refusal already reads as a translated message today, for any handler that
uses this pipeline correctly.

**What is not solved.** The eight database guards raise `RAISE EXCEPTION` with a fixed message
template and a machine-readable prefix (`KAFF_NEGATIVE_BALANCE`, `KAFF_LEDGER_NETTING`,
`KAFF_HOLD_DEBIT`, `KAFF_CLOSED_PERIOD`, `KAFF_REVERSAL_MISMATCH`, `KAFF_REVERSAL_OF_REVERSAL`,
`KAFF_REVERSAL_TARGET_MISSING`, `KAFF_CROSS_PROJECT`, `KAFF_PROJECT_TAG`, `KAFF_ACCOUNT_NOT_POSTABLE`,
`KAFF_ACCOUNT_INACTIVE`, `KAFF_CURRENCY_MISMATCH`, `KAFF_ACCOUNT_MISSING`)
[Verified: 2026-09-14 @ `001_guards.sql` -> `kaff_postings_validate`, `kaff_check_non_negative_balance`].
**Nothing today catches the `PostgresException` these raise and maps it back to a `TreasuryErrors`
member.** When a database guard fires without the domain having already refused the same thing — a
concurrency race the advisory lock does not fully close, a future caller that skips `Posting.Create`,
or (until `KAFF-305` ships) any posting-type/account-pair mismatch the domain does not yet check — the
`PostgresException` propagates to `UseExceptionHandler` and comes back as a bare 500
[Verified: 2026-09-14 @ `src/Api/Program.cs` line 348 area — `UseExceptionHandler` is the generic
catch-all "without a selector... calls all of them 500"]. This story closes exactly that gap, using the
same catch-by-`SqlState`-and-detail pattern `CreateBab` already established for a different guard.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | No hardcoded user-facing strings; everything through i18n from the first commit | `CLAUDE.md` |
| 2 | The database is the authority when domain and database disagree; the domain check exists so the user gets a translated, actionable message, not to be the only place refusal is possible | [Verified: 2026-09-14 @ `TreasuryErrors.cs` header remark] |
| 3 | Every raw exception a Treasury database guard can raise must map to an existing (or newly added) `TreasuryErrors` member and its i18n key, not reach the generic exception handler | [Verified: 2026-09-14 @ `001_guards.sql` — the twelve `RAISE EXCEPTION` call sites]; precedent [Verified: 2026-09-14 @ `CreateBab/Handler.cs` -> `IsCodeCollision`] |
| 4 | A refusal is still refused with the correct HTTP status family (400/409 as appropriate), not always 500, once translated | [Verified: 2026-09-14 @ `ResultExtensions.cs` -> `StatusFor`] |

## Permissions, money, audit, i18n

- **Permissions.** None of its own — this story changes how a refusal already produced by `KAFF-301`,
  `KAFF-303` or `KAFF-305`'s permission-gated endpoints is reported, not who may call them.
- **Money.** No money moves; a refused posting still writes no row.
- **Audit.** No new audit record. A refused attempt is not a state change.
- **i18n.** This is the story's entire content. Every guard-raised exception in the list above needs a
  `TreasuryErrors` member (most already exist for the domain-level equivalents — `KAFF_NEGATIVE_BALANCE`
  maps to `TreasuryErrors.NegativeBalance`, etc.) and a catch site that recognises it by `SqlState` and
  message prefix, the same way `CreateBab` recognises a unique-violation by constraint name.

## Acceptance criteria

**AC-319-A — a database guard firing without a prior domain refusal still returns a translated `ProblemDetails`, never a 500**
Given a posting that the domain check would have allowed but the database guard refuses (simulated by a race or a direct scenario that bypasses the domain pre-check)
When the request completes
Then the response is a `ProblemDetails` carrying `code` and `messageKey` matching the guard that fired, with the correct 400/409 status — not `Status500InternalServerError`
*Rule: 1, 3, 4.*

**AC-319-B — every guard's exception in `001_guards.sql` has a named mapping**
Given the twelve `KAFF_*` exception prefixes the guards can raise
When the mapping table is reviewed
Then each one resolves to an existing or new `TreasuryErrors` member with an `errors.treasury.*` key — none are left to fall through to the generic handler
*Rule: 3. A test enumerates the twelve prefixes against the mapping, the same exhaustiveness discipline `KAFF-305`'s `AC-305-F` and `PostingTypes.NatureOf` already use elsewhere in this codebase.*

**AC-319-C — the mapped message is in the user's language via the existing i18n mechanism, not a new one**
Given a mapped refusal
When it reaches the Angular client
Then it resolves through the existing `errors.treasury.*` catalogue the same way any domain-level refusal does — this story adds no second translation mechanism
*Rule: 1.*

**AC-319-D — an exception this story does not recognise still fails safely, not silently**
Given a database exception that is not one of the twelve named guards (a genuine unexpected failure)
When it propagates
Then it still reaches the generic exception handler and returns 500 — this story narrows the set of things that reach that path, it does not remove the fallback
*Consistency requirement — `CLAUDE.md`'s "Exceptions are for genuinely exceptional cases" still holds for the truly unexpected ones.*

## Definition of Ready

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ |
| Stable `AC-319-<LETTER>` ids | ✅ |
| Every business rule cites a `spec.md`/`CLAUDE.md` line or a `[Verified: ...]` citation | ✅ |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — none |
| Money behaviour named explicitly | ✅ — none moves |
| Arabic UI strings as i18n keys | ✅ — reuses `errors.treasury.*`; any genuinely new key (e.g. for `KAFF_REVERSAL_OF_REVERSAL`, which has no domain `Error` yet per `KAFF-303`'s note) is named there, not invented here |
| The audit record it writes is stated | ✅ — none |
| QA has written at least one scenario that fails if the rule is broken | ✅ **Met, 2026-09-14.** `qa/slice-3/test-cases.md` `TC-3-060`…`063` |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ |

## Not in this story

- **The refusals themselves** — the business rules that make a posting illegal are `KAFF-301`, `KAFF-303`, `KAFF-305`. This story only concerns how a refusal is *reported* when it originates in the database rather than in the domain.
- **Non-Treasury exception mapping.** Whatever pattern this story establishes for Treasury's twelve guard exceptions is not a mandate to retrofit every other feature's database exceptions — that is a separate, larger decision for `decisions.md` if it is ever proposed.
- **A new i18n mechanism or a new response envelope.** This story adds mappings into the existing pipeline; it does not change `ResultExtensions` or `ProblemDetails`' shape.

## Questions for Karim

None. Every rule this story needs is already settled by `CLAUDE.md`'s i18n requirement and by the
existing `ResultExtensions`/`TreasuryErrors` mechanism; the work is mechanical enumeration and mapping,
not a business decision.
