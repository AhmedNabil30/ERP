# KAFF-306 · The Treasury can post and reverse, but nothing reads

<!-- kaff id=KAFF-306 slice=3 points=5 state=READY -->

**Slice:** 3 — Treasury · **Points:** 5.
**Why now:** `src/Api/Features/Treasury/` holds exactly `PostMovement` and `ReversePosting`.
**Nothing reads.** There is no posting list and no derived-balance query, although
`Domain/BalanceQueries.cs` exists. `S-033` account statement, `S-034` posting detail, `S-036` safe and
bank balances and `S-040` the project five-ledger view all have no API.
**Files:** new `src/Api/Features/Treasury/ListPostings/*`, `GetBalances/*`.
**Screens this unblocks:** `Money` and the ledger rail on `Project` —
https://claude.ai/artifact/KmuT1UYyXMiSqocN2MCkgY

## What already exists — do not rebuild it

- `Domain/BalanceQueries.cs`, `Domain/AccountBalance.cs`, `Domain/Posting.cs`,
  `Domain/PostingType.cs`, `Domain/PostingAccountLegality.cs`.
- `Infrastructure/KaffDbContext.cs` exposes `DbSet<Posting>` and `DbSet<Account>`, both migrated.
  **This story is read-only. It must not add a migration and must not write a single row.**

## Endpoints

### `GET /api/treasury/postings` — the statement
- Filters: account, project, date range, posting type. Cursor-based paging.
- Each row: date, description, posting type, debit, credit, **running balance**, the source document,
  and `reversesId` where the posting reverses another.
- **A reversed posting is still returned.** It renders struck through with its reversal beside it.
  `CLAUDE.md`: *"Never update or delete a posting… Corrections are new reversing postings that
  reference the original through `ReversesId`."* Both stay visible, forever.

### `GET /api/treasury/balances` — derived, never stored
- Per account, and per project across the **five ledgers**: client advance · hold · firm advance ·
  عهدة · owner current account.
- **Computed by summing postings on every request.** `CLAUDE.md`: *"Never store a balance… If you find
  yourself adding a `Balance` column, stop — that's the bug."*
- **The five never net against each other.** No calculation in this story may offset one against
  another, and the response must not contain a field that invites it.

## The rules this story must not break

- **`decimal` only**, `HasPrecision(18, 4)` where anything is configured. No `float`, no `double`.
- **Money crosses the wire as a JSON string** — D-135.
- **Permission is role × assignment**, server-side, every request. A project-scoped balance is
  refused to a caller with the role but no assignment. **HR sees no financial field at all**
  (D-051 Q32).
- **No endpoint that edits or deletes a posting.** Not even for an admin, not even for test data.
  If you find yourself writing one, you have misread the story.
- The safe's non-negative floor is a **database constraint**, not application code. Do not duplicate
  it in a handler.

## Acceptance criteria

**AC-306-A** Both endpoints exist as vertical slices, read-only. Zero writes, zero migrations —
proven by the diff.
**AC-306-B** The running balance is correct across a reversal: a test posts a movement, reverses it,
and asserts the balance returns to its prior value **with both postings still present in the list**.
**AC-306-C** `GET /api/treasury/balances` derives every figure by summing postings. No `Balance`
column exists anywhere after this story — grep the diff and the model.
**AC-306-D** The five ledgers are returned side by side and no response field nets any pair.
**AC-306-E** A permission test per role: a Site Engineer assigned to project A is refused project B's
balances; HR is refused both, with no financial field present in any payload.
**AC-306-F** Every money field serialises as a JSON string, proven against the raw JSON.
**AC-306-G** `dotnet build` clean with warnings as errors. Domain and Api suites green.

## Not in this story

Creating or reversing postings — `PostMovement` and `ReversePosting` already exist and are not
touched. The account tree screen. Collections and cheques. Any frontend. Anything under
`Features/Projects/`.

## Questions for Karim

None — every rule above is quoted from `CLAUDE.md` or `spec.md` §6. If a case is unanswered,
**stop and ask**.
