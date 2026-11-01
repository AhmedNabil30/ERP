# KAFF-802 · Handover releases the hold, once, in full

<!-- kaff id=KAFF-802 slice=8 points=8 state=NOT-BUILT -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Closure · **Points:** 8 (absorbs `KAFF-308`).
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §5.1 (hold rules — MUST), §11, §6.4 (and its amendment on the hold), §15 (the "Handover — hold release" row).
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7. The study's refusal list applies: **no hold-release endpoint before the handover state check.**

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q109`, `Q110`, `Q111`) and on "QA has written at least one scenario that fails". This story **moves money**: one posting, the largest single posting on a project.

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| The extract chain, `KAFF-500` | The hold has nothing in it until extracts accrue it | **not built** |
| Practical completion and the snag list, `KAFF-800` | A major snag blocks the handover this story acts on | **not built** |
| `PostingProjectStatePolicy` | `HoldRelease` is allowed only in `Handover` and `UnderWarranty` | **done** — default table, unruled [Verified: 2026-11-01 @ `src/Domain/Rulings/PostingProjectStatePolicy.cs` -> `BuildDefault`] |
| The hold ledger and its type guard | Only `HoldAccrual` and `HoldRelease` may touch it | **done** [Verified: 2026-11-01 @ `src/Domain/Treasury/PostingAccountLegality.cs` -> `RestrictedLedgerAccounts`] |
| Collections, `KAFF-316`, and withholding, `KAFF-317` | The client then **pays** the released hold | **not built** |

## Story

At handover the project enters `Handover`, the warranty clock starts, and the whole accumulated hold
is released in one posting. Nothing before handover can take from the hold, and nothing after the
release leaves anything in it.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | The hold releases **once, in full, at handover**, even with minor snags open | `spec.md` §5.1, §11 |
| 2 | Nothing may be taken out of the hold mid-project — **not a snag, not a debit note, not an adjustment** | `spec.md` §5.1 · `CLAUDE.md` |
| 3 | `HoldRelease` is the only posting type that may move value out of a hold account | `PostingType.HoldRelease` · `PostingAccountLegality` |
| 4 | The amount is **the hold balance, summed from postings**. It is never typed in the request and never stored | `CLAUDE.md` · `spec.md` §6.1 |
| 5 | The release must leave the hold at **exactly zero** | `spec.md` §6.4 amendment (D-044 §8) |
| 6 | The release is refused unless the project is in `Handover` or `UnderWarranty`; the domain checks it, not the handler | `PostingProjectStatePolicy` · the journey study, refusal list |
| 7 | Major snags block handover; minor ones do not | `spec.md` §11 · `KAFF-800` |
| 8 | Entering `Handover` stamps the handover date and sets the warranty end four months later | `spec.md` §11 · `Project` [Verified: 2026-11-01 @ `src/Domain/Projects/Project.cs` -> `WarrantyEndsOn`] |
| 9 | §15: after the release, accumulated hold paid out equals 20% of contract value and total client cash equals contract value | `spec.md` §15 |
| 10 | Whether the release carries withholding at source is asked of `RulingSet.WithholdingBase` (`HoldReleaseCarriesWithholding`), not decided here | `WithholdingBaseRule` · `KAFF-317` |
| 11 | The five ledgers never net against each other; the release does not touch the client advance or تشوينات | `CLAUDE.md` · `spec.md` §6.4 |
| 12 | A Design project has no hold and no handover | `spec.md` §11 |
| 13 | Who hands over and who releases, and whether the Owner approves the release as a financial movement, is unruled. Where an approval exists, nobody creates and approves the same movement | `spec.md` §9 · `Q109` |
| 14 | Role **and** assignment, server-side | `spec.md` §9 |
| 15 | Every state change and the release write an audit record | `CLAUDE.md` |

## Acceptance criteria

**AC-802-A — §15's hold release, derived**
Given the §15 acceptance project after its three extracts
When the project is handed over and the hold released
Then one `HoldRelease` posting of 200,000 is written, the request carried no amount, the hold ledger reads exactly zero, and with the final collection total client cash equals contract value
*Rules: 1, 4, 5, 9.*

**AC-802-B — refused before handover**
Given projects in `Setup`, `Active`, `HandoverPending` and `Stopped`
When a hold release is attempted on each
Then every one is refused by the domain, whatever the role
*Rule: 6.*

**AC-802-C — once, and only once**
Given a project whose hold has been released
When a second release is attempted
Then it is refused and the hold is still zero
*Rule: 1.*

**AC-802-D — minor snags open, hold still releases**
Given a project at handover with open minor snags
When the hold is released
Then the whole hold is released
*Rule: 1.*

**AC-802-E — nothing else debits the hold**
Given a project in `Active`
When a snag cost, a debit note or an adjustment names the hold
Then each is refused
*Rule: 2. The same refusal as `AC-512-D`, asserted here against the hold ledger.*

**AC-802-F — what moves Handover to UnderWarranty, and when the release happens — HELD**
Given a project that has just been handed over
When time passes or someone acts
Then the project reaches `UnderWarranty` and the hold is released as the ruling says
*Rule: none yet. **HELD on `Q110`.***

**AC-802-G — a mistaken release — HELD**
Given a hold released in error
When it is corrected
Then the correction follows the ruling
*Rule: none yet. **HELD on `Q111`.** Any correction is a reversing posting through `ReversesId`, never an edit.*

**AC-802-H — the warranty clock starts**
Given a project entering `Handover` on a date
When the transition completes
Then the handover date is stamped and the warranty end date is four months after it
*Rule: 8.*

**AC-802-I — the ledgers do not net**
Given a client advance and تشوينات outstanding at handover
When the hold is released
Then neither balance moves
*Rule: 11.*

**AC-802-J — a Design project cannot hand over**
Given a Design project
When handover or a hold release is attempted
Then both are refused
*Rule: 12.*

**AC-802-K — role and assignment, direct to the endpoint**
Given a user with the role and no assignment, and a user assigned with the wrong role
When each calls the release endpoint
Then both are refused, server-side
*Rule: 14. The role is HELD on `Q109`.*

**AC-802-L — nobody approves their own release**
Given the user who requested the release
When they attempt to approve it
Then it is refused server-side
*Rule: 13.*

**AC-802-M — the release writes an audit record**
Given a released hold
When the trail is read
Then it carries who, when, the amount and the project state
*Rule: 15.*

## Not in this story

Collecting the released hold (`KAFF-316`) and withholding on it (`KAFF-317`). The snag list
(`KAFF-800`). Warranty and callbacks (`KAFF-804`). Subcontractor retention (`KAFF-806`). The client
portal's view. Every screen — no Frontend story is cut, so by `D-117` no UI criterion is written.

## Questions for Karim

1. **`Q109`** — who hands over and who releases; does the Owner approve the release?
2. **`Q110`** — what moves `Handover` to `UnderWarranty`, and the day of the release.
3. **`Q111`** — can a mistaken release be undone and made again?
4. **`Q114`** — whether the release carries withholding (shared with `KAFF-317`).
