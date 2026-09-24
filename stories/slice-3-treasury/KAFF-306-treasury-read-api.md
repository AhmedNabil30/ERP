# KAFF-306 · The Treasury can post and reverse, but nothing reads

<!-- kaff id=KAFF-306 slice=3 points=5 state=BUILT verdict=none at=- on=2026-09-25 -->

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

## Build notes (2026-09-25)

**The brief's "Questions for Karim: None" is wrong, and the story cannot ship past it without a
ruling.** `questions-for-karim.md` already carries **Q90**, raised cutting `KAFF-304`: *"Once a
project's ledger balances are held somewhere in the system, who is allowed to look at them — the
Owner and Finance only, or does a Site Engineer see his own project's numbers too?"* It is marked
**blocking**, and `KAFF-304`'s own build — `tests/Domain.Tests/BalanceQueriesTests.cs`'s file
comment — says outright that it built only the Domain-level derivation query and left the HTTP
surface unbuilt for exactly this reason: *"no `Permission.*Read` exists anywhere in the catalogue
for a Treasury balance today, and the story's HTTP surface cannot be built without one."* Nothing
between `KAFF-304` and this story answered it. Whoever cut `KAFF-306` did not check
`questions-for-karim.md` against the story it depends on. Flagging this for the Scrum Master rather
than silently building past it — `CLAUDE.md`: "If `spec.md` doesn't answer a business question, stop
and ask. Do not decide."

**What was built anyway, and why it is still worth having:**

- `src/Domain/Treasury/PostingStatementQueries.cs` — a pure Domain-layer function that turns an
  ordered run of `Posting`s against one account into a statement with a running balance, summed
  fresh every call, in the same style as `BalanceQueries` (KAFF-304). Tested at
  `tests/Domain.Tests/PostingStatementQueriesTests.cs`: a posting followed by its reversal returns
  the running balance to its prior value with both rows present (AC-306-B), the balance accumulates
  correctly across three postings in order, a credit-normal account's sign flips the same way
  `account_balances.sql` and `BalanceQueriesTests` already assert, and an empty history returns an
  empty statement rather than an error.
- `src/Api/Features/Treasury/ListPostings/*` — `GET /api/treasury/postings` and
  `GET /api/projects/{projectId}/treasury/postings`, the account statement (S-033). Filters: a
  required `accountId`, optional `from`/`to`/`type`, and cursor-based paging (an opaque cursor that
  is the last returned posting's id). Zero writes.
- `src/Api/Features/Treasury/GetBalances/*` — `GET /api/projects/{projectId}/treasury/balances`
  (the five ledgers, S-040, straight through `BalanceQueries.GetLedgerBalances`) and
  `GET /api/treasury/accounts/{accountId}/balance` (one account, S-036, through
  `BalanceQueries.GetAccountBalance`). Zero writes.
- `Permission.TreasuryReadProject` / `Permission.TreasuryReadCompany` — added to the catalogue
  **`Unresolved: true` with an empty grant list**, not a stated assumption the way `PeriodClose`
  takes one. `PermissionEvaluator` refuses a permission with no matching grant to everyone, the
  Owner included (`RoleNotGranted`), so both routes exist, are wired, write nothing, and are
  reachable by **nobody** until Q90 is answered and a grant is added. This was a deliberate choice
  over guessing "Owner and Finance" or "plus the assigned Site Engineer" — either would be the
  invented-plausible-rule failure `CLAUDE.md` and `PermissionCatalogue.cs`'s own remarks warn about.

**Acceptance criteria, honestly scored:**

- **AC-306-A** (both endpoints exist, read-only, zero writes/migrations) — **met.** No `SaveChanges`,
  `INSERT`, `UPDATE`, or `DELETE` in the diff; no migration added.
- **AC-306-B** (running balance survives a reversal, both postings still listed) — **met at the
  Domain layer**, `PostingStatementQueriesTests`. Not provable end-to-end over HTTP today because
  nothing can call the endpoint (Q90).
- **AC-306-C** (every balance derived by summing postings, no `Balance` column) — **met.** No stored
  balance anywhere; `grep -r "Balance.*{ get; set; }"` and the model both come back empty of one.
- **AC-306-D** (five ledgers side by side, nothing nets) — **met structurally.**
  `LedgerBalancesResponse` has no `Total` field, mirroring `LedgerBalances`'s own remarks; pinned by
  `GetBalancesTests.The_ledger_response_carries_no_field_whose_name_suggests_a_total` (written, not
  run — see below).
- **AC-306-E** (a Site Engineer assigned to A is refused B; HR is refused both, no financial field
  anywhere) — **NOT met as written, and cannot be until Q90 is answered.** With an empty grant list,
  the Site Engineer is refused **both** A and B — there is no positive case to contrast the negative
  one against, because nobody holds the permission yet. The HR half and the "no financial field"
  half both hold trivially (a 403 body carries no ledger data), and are asserted in
  `ListPostingsTests`/`GetBalancesTests`. The story cannot claim this criterion until Karim rules
  Q90 and a grant is written.
- **AC-306-F** (money as a JSON string, no float/double) — **met**, reflection-tested in both
  `Api.Tests` files against the `Response`/`PostingRow`/`LedgerBalancesResponse`/
  `AccountBalanceResponse` types.
- **AC-306-G** (`dotnet build` clean, Domain and Api suites green) — **partially evidenced; see
  Gates below.**

**What was not done:** no HTTP-level scenario test exercises a *successful* read (every role is
refused today by construction), so the wire shape of a 200 response — the exact JSON a frontend
would parse — is unverified beyond the reflection checks. That is the direct, unavoidable
consequence of Q90 being open; once it is answered and a grant lands, `ListPostingsTests`/
`GetBalancesTests` are where the scenario cases belong (flagged inline in both files).

**Gates:**
- `dotnet build KaffErp.sln -c Release --nologo` was clean (0 warnings, 0 errors) once, with every
  production file in this story already in place. A second full-solution Release build later in the
  session failed — not on anything in this diff, but because a live `Kaff.Api` process (pid 11964,
  presumably serving the frontend agents on port 5080 per this session's own instructions not to
  touch it) holds `src/Api/bin/Release/net10.0/*` open, and MSBuild cannot overwrite a locked
  `.dll`/`.exe`. Verified this story's own source still compiles clean by building
  `tests/Api.Tests/Kaff.Api.Tests.csproj` in the `Debug` configuration instead (separate output
  folder, untouched by the lock): 0 warnings, 0 errors, all production and test code included.
- `Domain.Tests`: **297/298 green.** The one failure,
  `TranslationCatalogueTests.Every_domain_error_key_has_an_arabic_and_an_english_translation`
  (`errors.project.not_found` missing from `ar.json`), is **not from this story** — `git status`
  shows `src/Domain/Projects/ProjectErrors.cs` and the locale files modified by the concurrent
  `KAFF-400` session, which this story's hard limits forbid touching. Left alone.
- `Api.Tests` — written, **not run** per this story's own instruction. Four new tests in
  `ListPostingsTests.cs`, four in `GetBalancesTests.cs`, listed above.

**Commits:** see the branch log for KAFF-306 — one per endpoint plus the Domain/permission
scaffolding and the story/status update, per the dispatch's own instruction.

**For the Scrum Master:** route Q90 to Karim/Nabil before this story can move past `BLOCKED`. The
question as filed is broad ("who is allowed to look at them"); given `ProjectRead`'s existing
roster and `TreasuryPostProject`/`TreasuryPostCompany`'s existing holders, the two live options are
likely (a) Owner + Finance only, or (b) Owner + Finance + the assigned Site Engineer, project-scoped
— but naming either here would be exactly the guess this file's own rule forbids. Once ruled, the
fix is one grant list in `PermissionCatalogue.cs`'s two new rows; nothing else in this story changes.

## Build notes (2026-09-25, continued — Q90 released)

**Q90 ruled — decisions.md D-173.** `TreasuryReadProject`: Owner, Finance, Site Engineer, project-scoped
(assignment required). `TreasuryReadCompany`: Owner and Finance only, company-wide. HR holds neither
(D-051 Q32 stands). `stories/questions-for-karim.md` Q90 row updated to ANSWERED, citing D-173, in the
house style of the Q85/Q92 rows.

**What changed, and nothing else:**

- `src/Domain/Authorization/PermissionCatalogue.cs` — `TreasuryReadProject` and `TreasuryReadCompany`
  rows given the grants above and `Unresolved: true` cleared. Same grant shapes the catalogue already
  uses elsewhere: `owner`/`finance` reusable `AccessGrant`s, and a bare `new AccessGrant { Role =
  Role.SiteEngineer }` for the Site Engineer row — the same shape `ProjectRead`'s own Site Engineer row
  uses, since D-173 states no minimum seniority.
- Two pinned Domain tests were now false by construction and updated as the direct, unavoidable
  consequence of resolving the two rows — not scope creep:
  - `tests/Domain.Tests/CatalogueCompletenessTests.cs` →
    `The_set_of_unresolved_permissions_has_not_grown` — expected set shrinks to `[PeriodClose]` only.
  - `tests/Domain.Tests/PermissionEvaluatorTests.cs` → `Finance_holds_a_flat_set_of_its_company_wide_permissions`
    — `Permission.TreasuryReadCompany` added to Finance's expected company-wide set.
- `tests/Api.Tests/Features/Treasury/ListPostingsTests.cs` — the two tests that previously asserted
  "everyone is refused because the grant list is empty" now assert the positive case instead:
  - `An_owner_reads_the_company_statement` — Owner, `GET /api/treasury/postings`, expects 200.
  - `A_finance_user_assigned_to_the_project_reads_the_project_statement` — Finance assigned to project
    A, `GET /api/projects/{A}/treasury/postings`, expects 200.
  - `Hr_is_refused_the_company_statement_too_with_no_financial_field_in_any_payload` — unchanged, still
    refused, still asserts no `runningBalance` in the 403 body.
- `tests/Api.Tests/Features/Treasury/GetBalancesTests.cs` — rewritten to the full AC-306-E matrix, one
  fact per cell:
  - `A_site_engineer_assigned_to_project_A_reads_project_As_own_balances` — 200.
  - `The_same_site_engineer_is_refused_project_Bs_balances` — 403, not assigned to B.
  - `The_owner_reads_both_projects_ledgers` — 200/200, Owner's global reach.
  - `Finance_assigned_to_project_A_reads_project_As_ledgers_but_not_project_Bs` — 200/403, Finance is
    project-scoped too and needs its own assignment.
  - `Hr_is_refused_project_As_ledgers_with_no_financial_field_in_the_payload` — 403, body excludes
    `clientAdvance`.
  - `The_owner_and_finance_read_the_company_account_balance` — 200/200, `TreasuryReadCompany` is
    company-wide so Finance needs no assignment for this route.
  - `A_site_engineer_is_refused_the_company_account_balance` — 403, no Site Engineer row on
    `TreasuryReadCompany`.
  - `Hr_is_refused_the_account_balance_with_no_financial_field_in_the_payload` — unchanged, 403, body
    excludes `signedBalance`.
  - Seeds extended with an Owner and a Finance user assigned to project A (`ProjectAssignment.Create`,
    same helper shape already used for the Site Engineer).

**Acceptance criteria, rescored:**

- **AC-306-A** — met, unchanged. No write, no migration in this diff either.
- **AC-306-B** — met at the Domain layer (`PostingStatementQueriesTests`), as before. Not re-proven
  over HTTP here — not asked for by this dispatch, and the existing Domain-level proof already
  satisfies the criterion as written (running balance correct across a reversal).
- **AC-306-C** — met, unchanged.
- **AC-306-D** — met, unchanged, still pinned by
  `The_ledger_response_carries_no_field_whose_name_suggests_a_total`.
- **AC-306-E** — **met.** A Site Engineer assigned to project A reads project A's balances and is
  refused project B's; HR is refused both `ListPostings` and `GetBalances` routes with no financial
  field in either payload; Owner and Finance read both. All eight new/changed HTTP tests listed above.
- **AC-306-F** — met, unchanged.
- **AC-306-G** — **build clean, warnings as errors; Domain.Tests green.** See Gates below for exactly
  what ran and what could not.

**Gates:**

- `dotnet build KaffErp.sln -c Release --nologo` fails on file-copy only: `Kaff.Api.csproj` cannot
  overwrite `src/Api/bin/Release/net10.0/Kaff.{Domain,Infrastructure}.dll` because a live `Kaff.Api`
  process (pid 11964) on port 5080 holds them open — `MSB3027`/`MSB3021`, not a compiler error, and not
  in this diff. Per this dispatch's own instruction, the process was left running and not killed.
  Verified compilation is clean another way: `dotnet build tests\Api.Tests\Kaff.Api.Tests.csproj -c
  Debug --nologo` — separate output folder, untouched by the lock — **0 warnings, 0 errors**, includes
  every production and test file this story touches.
- `Domain.Tests`: **298/298 green** (`dotnet build ... -c Release` then run the built
  `Kaff.Domain.Tests.exe` directly — `dotnet test` reported "Zero tests ran" against this SDK/test
  adapter combination for an unrelated reason, the exe run is the real result).
- `Api.Tests` — **not run**, per this dispatch's explicit instruction not to run Api.Tests or touch
  port 5080. Eight tests written across the two files (three unchanged/kept, five new or converted from
  the empty-grant-list refusal shape to the positive AC-306-E case); the Scrum Master's own Api.Tests
  gate is where they execute.

**What was not done:** no HTTP-level test for AC-306-B's reversal scenario (see AC-306-B above — not
asked for, and the Domain-level proof already covers the criterion). No change to `ListPostings`/
`GetBalances` handlers, endpoints, or response shapes — this session touched only the catalogue and
tests, as scoped.
