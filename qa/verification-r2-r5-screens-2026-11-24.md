# Verification: Release 2 to 5 screens and reads, 2026-11-24

Scope: `git log 6dbeb68..7fcc7ee` on `main` at `7fcc7ee`. Judged against D-266 to D-273, the approved mocks
named in D-273, `plans/release-2..5/stories.md`, `spec.md` where cited, and the never-break rules in `CLAUDE.md`.
Verifier did not write this code. Model: Opus 5.5 (claude-opus-5-5), strongest tier.

Stack used: API http://localhost:5080, web http://localhost:4200, as running. No process was stopped.

## Findings (written as reached)

### F-1 (focus 4) The portal lockout was real: five wrong passwords reached the portal door, not a counting fault. Severity: low (docs), no code defect found

Evidence is the append-only audit trail for user `01a20e31-04ac-738e-a25c-bc5e8fcb3f7f` (`01012340010`), read with
`select ... from audit_records where entity_id = ...`:

| UTC | Path | Source | Event |
|---|---|---|---|
| 16:29:12 | `/api/portal/auth/sign-in` | `::1` (browser via dev proxy) | SignedIn (temporary password) |
| 16:29:17 | `/api/portal/auth/change-password` | `::1` | Modified MustChangePassword, PasswordHash, SecurityStamp |
| 16:29:26 | `/api/portal/auth/sign-in` | `127.0.0.1` (curl) | SignedIn |
| 16:29:54, 16:30:19, 16:30:45, 16:31:07 | sign-in | `::1` | SignInFailed + Modified FailedSignInAttempts (wrong password) |
| 16:31:31 | sign-in | `::1` | fifth wrong password: Modified FailedSignInAttempts, LockedOutUntil; AccountLockedOut |
| 16:31:55 | sign-in | `::1` | wrong password again while locked: counter moves, a second AccountLockedOut row |
| 16:32:24 to 16:46:27 | sign-in | `::1` and `127.0.0.1` | SignInFailed with no Modified row, which is the correct-password-while-locked path (423) |
| 16:46:32 | sign-in | `127.0.0.1` | SignedIn, one second after LockedOutUntil (16:31:31 + 15 min) |

Conclusions:

1. The counter moved only on the five requests whose password did not verify. A correct password never touches
   `FailedSignInAttempts` (`src/Api/Features/Portal/SignIn/Handler.cs:58-74`). The lockout counted correctly.
2. curl did not "work with the same password" during the lock. curl was refused at 16:32:47 and at 16:45:23 to 16:46:27, and
   it succeeded at 16:46:32, when the lock expired. The two observations were taken at different times.
3. The browser form does not alter the password. `portal-sign-in-page.ts` trims only the user name and sends the password as
   typed. Both doors trim and lower-case the user name and nothing else. After 16:46 the same `::1` path signed in sixteen
   times with the new password.
4. The most likely source of the five wrong passwords is the old temporary password `Demo#Portal2`, which was changed at
   16:29:17 and which `docs/uat-manual.md:23` and `scripts/seed-demo-portal.mjs:5` still publish. Nothing stores the typed
   input (D-062 section 3), so this cannot be proven from the database.

Side observations, low:

- `docs/uat-manual.md:23` lists `Demo#Portal2` for the demo client. That password no longer works. A tester who follows the
  manual locks the account in five attempts.
- A wrong password during an active lock increments the counter and writes a second `AccountLockedOut` row
  (`Portal/SignIn/Handler.cs:62-67`, the same in `Auth/SignIn/Handler.cs:109-116`). Five wrong attempts during a lock extend it
  by 15 minutes from the fifth. This matches the staff door and AC-A-4 says "as for staff", so it is consistent. The duplicate
  audit event is noise, not a defect against any criterion.

### F-2 (focus 1) Server-side refusal matrix: every new screen action is refused to the wrong role and to an unassigned user

Run against the live API with each demo user's own session, ids `{A}` = فيلا 47 (all demo users assigned),
`{UE}` = `01a1e66c-...` (site engineers not assigned), `{UT}` = `01a1caa3-6822-...` (Technical Office not assigned),
`{UA}` = `01a1b23c-...` (Accounts not assigned), `{z}` = a random id. POST bodies were empty, so an allowed caller gets 400,
404 or 409 and a refused one gets 403. Script kept in the Verifier's scratchpad, not in the repository.

| Endpoint | owner | junior | supervisor | tech office | accounts | client |
|---|---|---|---|---|---|---|
| GET petty-cash-requests {A} | 403 | 200 (own rows only) | 200 | 403 | 403 | 403 |
| GET petty-cash-requests {UE} | 403 | 403 | 403 | 403 | 403 | 403 |
| POST petty-cash submission / return {A} | 403 | 403 | 404 | 403 | 403 | 403 |
| POST petty-cash submission {UE} | 403 | 403 | 403 | 403 | 403 | 403 |
| GET subcontractor-retention {A} | 200 | 403 | 403 | 200 | 200 | 403 |
| GET subcontractor-retention {UT} | 200 | 403 | 403 | 403 | 200 | 403 |
| POST retention-release/reversal (ask) | 403 | 403 | 403 | 403 | 400 | 403 |
| POST reversal/approval, reversal/rejection | 409 | 403 | 403 | 403 | 403 | 403 |
| GET/POST every /api/opportunities route | 200/404 | 403 | 403 | 403 | 403 | 403 |
| GET /api/treasury/accounts | 200 | 403 | 403 | 403 | 200 | 403 |
| GET /api/treasury/accounts?projectId={UA} | 200 | 403 | 403 | 403 | 403 | 403 |
| POST accruals, prepayments, assets, rate, depreciation-runs, prepayment-amortisations | 403 | 403 | 403 | 403 | 400/404 | 403 |
| POST accounting-requests approval / rejection | 404 | 403 | 403 | 403 | 403 | 403 |
| GET trial-balance, export, accruals, prepayments, assets, accounting-requests | 200 | 403 | 403 | 403 | 200 | 403 |
| GET budget-baseline {A} | 404 then 200 | 403 | 403 | 403 | 404 then 200 | 403 |
| GET budget-baseline {UA} | 404 | 403 | 403 | 403 | 403 | 403 |
| POST photos/{z}/publish {A} | 403 | 403 | 404 | 404 | 403 | 403 |
| POST photos/{z}/publish {UT} | 403 | 403 | 404 | 403 | 403 | 403 |
| every /api/portal/* read and approve | 403 | 403 | 403 | 403 | 403 | 200/404 |
| GET /api/projects/{A}, /extracts | 200 | 200 | 200 | 200 | 200 | 403 |

Verdict: role and assignment are both checked server-side on every route the new screens call. The asker of a retention
reversal (Finance) cannot decide it (403), and the decider (Owner) cannot ask (403). The accounting approval is Owner only
(D-267 item 1). Budget baseline read is Owner and Finance only, and an unassigned Finance user is refused (D-267 item 2).
Staff sessions are refused on the portal and the client session is refused on staff routes (AC-810-B, AC-810-C).

### F-3 Verifier side effect: a budget baseline now exists on فيلا 47. Severity: information, for the Scrum Master

The matrix POSTed an empty body to `POST /api/projects/{A}/budget-baseline` as `owner_demo`. The route takes no body and copies
the frozen BOQ cost, so it answered 201 and created the baseline (`costTotal` 3018126.2500, limit 3319938.8750, set
2026-11-24 17:00:53 UTC, audit row `Created BudgetBaseline` by ناصر الشريف). D-266 says nothing edits a baseline, so it cannot
be undone through the API. It is a correct, legal act on the demo data, but it was not intended as a test fixture. A tester
who expected to set it in UAT will find it set.

### F-4 The junior site engineer holds `TreasuryReadProject` and reads the project accruals list. Severity: medium, to confirm

`site_eng_demo` (level Junior on فيلا 47) holds `TreasuryReadProject` and gets 200 on
`GET /api/projects/{A}/treasury/accruals` (empty today). The same permission gates the project prepayments and accounting
requests lists (`Accounting/ListAccruals/Endpoint.cs:36`, `ListPrepayments/Endpoint.cs:34`, `ListAccountingRequests/Endpoint.cs:36`).
D-218 item 6 and KAFF-609a AC5 say the junior never receives project money figures. An accrual amount on a project is a
project money figure. The route predates this range, but the screens merged here now call it. Ask whether accruals belong to
`TreasuryReadProject` or to a Finance-only project read.

### F-5 KAFF-609a has no draft route, so the KAFF-609b screen has nothing to act on. Severity: medium

No `POST /api/projects/{id}/petty-cash-requests` exists (`grep` over `src/Api/Features/PettyCash`). `PettyCashRequest.Draft`
is called only from tests. `petty_cash_requests` holds 0 rows on the running database. The supervisor's submit and return
screen (`features/petty-cash/petty-cash-page.*`) is reachable and permissioned correctly, but nobody can create a draft for it
to list. 609a waits on R2-Q6 per the story, so this is expected scope, but the screen cannot be demonstrated end to end.

### F-6 Technical Office cannot read the opportunity list. Severity: low, NOT MET against D-273 item 5

D-273 item 5 answers that Technical Office reads a filtered list «بانتظار فتح المشروع». Every `/api/opportunities` route is
gated `OpportunityManage` (`Opportunities/ListOpportunities/Endpoint.cs:19`) and `tech_office_demo` gets 403. No filtered read
exists. It belongs with R3-09 conversion, which is not built, so this is a gap to schedule, not a regression.

### F-7 (focus 3) The portal leaks nothing on the running stack. Severity: none

Signed in as `01012340010`, the Verifier fetched every portal read for all 14 projects the client owns: the project home,
extracts, photos and change orders. Every body was searched for cost, margin, markup, profit, catalogue, subcontract,
supplier, internal, note, audit, ncr, qc, technical, petty, safe, treasury, hold, retention, userid, author, location,
reference and rate. The only hit was `ncr` inside the word "Concrete". The screens call only `/api/portal/*` and the staff
sign-out (`features/portal/portal.api.ts:89-134`). Another client's project (عمارة 5) returned 403 on all four reads, with no
name in the body (D-267 item 3). The extracts list holds Issued extracts 1 and 2 of فيلا 47 and none of the extracts at
PartiallyApproved or earlier (AC-B-4). Money arrives as two-decimal strings (AC-811-F). Photos and change orders are empty
for this client, so `GetPhoto` and `GetChangeOrder` were judged from their response types only: `PortalPhoto(Id, Caption,
TakenAt)` and `PortalChangeOrderDetail` carry no author, location, log reference or cost (AC-C-4: UNTESTED on live data).

Observation, low: the portal lists the client's unsigned quotation projects in Setup («فيلا التجمع - عرض سعر», «مشروع قديم
بلا توقيع») with paid 0.00 and no remaining figure. `spec.md` section 12 does not say whether a client sees a project before
signature. Question for Nabil.

### F-8 (focus 2) Money and dates on the new screens

- No money arithmetic reaches the screen. Every money figure is the server string passed to `formatAmount` or `kaff-figure`,
  and `I18nService.formatNumber` hands the string to `Intl` without `Number()` (`core/i18n/i18n.service.ts:130-137`).
- Two client-side money computations exist. Both set a bar width only, and both work in whole piastres with `BigInt`:
  `portal-format.ts:21-33` (`percentOf`) and `portal-projects-page.ts:77-80`. The second rebuilds paid plus remaining because
  the list response carries no contract value. Low: if a future server string has other than two decimals,
  `replace('.', '')` scales the sum wrongly, and only the bar is wrong.
- `Number(...)` appears only in form validation (`accruals-page.ts:151`, `assets-page.ts:109,112`) and in the month count
  (`accruals-page.ts:240`). Amounts go to the server as the trimmed typed string.
- No `float` or `double` in `src/Api`, `src/Domain` or `src/Infrastructure`.
- Dates: every new screen uses `dmy` (`core/accounting/accounting-format.ts:2`), `shortDay` (`extract-display.ts:73`) or
  `portalDate` (`portal-format.ts:11`), all dd/MM/yyyy (D-273 item 15). Seen on screen: portal extracts read
  «01/11/2026 - 30/11/2026».
  - Low: `shortDay` slices an instant (`createdAt`, `decidedAt`) at its UTC date. A request made in Cairo between midnight
    and 02:00 local time shows the previous day (petty cash `petty-cash-page.html:47`, retention `closure-retention.ts:105`).
  - Low, outside this range: `closure-page.ts:418` still formats with `dateStyle: 'medium'`, so the closure screen shows a
    month name, not dd/MM/yyyy.
  - Cosmetic: the portal prints dates in Arabic-Indic digits through `formatNumber`. Staff screens print Latin digits.
- «مُرجَع» and «مرفوض» (D-273 item 20): `petty_cash.status.Returned` and `accounting.request.status.returned` read «مُرجَع»,
  and the client's own no on a change order reads «مرفوض» (`public/locales/ar.json:1728, 2423, 2315`). Correct. Outside this
  range, `extract.outcome.Rejected` (`ar.json:1451`) and `credit_note.status.Rejected` (`ar.json:2173`) still read «مرفوض».
  The coordinator should decide whether item 20 reaches staff send-backs on those two.

### F-9 (focus 5) Asset register run end to end: the server works; the row overlaps at 390. Severity: medium (UI)

Run as `accounts_demo` and `owner_demo` with the payload the screen builds (`assets-page.ts:189-197`):

1. Register «حاسب تحقق المراجع», OtherBusinessAssets, cost 1200.00, rate blank: 201. The rate defaulted to 25.00 per the
   category rule. Book value 1200.0000, monthly 25.0000 (1200 x 25% / 12). No posting at registration.
2. Ask the November depreciation run: 409 `accounting.request_already_waiting`, because a run for 2026-11-01 asked at 14:18
   UTC was still waiting.
3. Accounts approving that request: 403. Owner approving: 200 with one posting `01a20f55-2b70-...`. Approving again: 409
   `accounting.request_not_waiting`.
4. Asset after approval: accumulated 25.0000, book value 1175.0000, summed from postings.
5. Asking November again: 201. Owner approval: 200 with `postingIds: []`. The month run is idempotent at approval.

These are permanent rows on the demo data: the asset, one depreciation posting of 25.00, and two approved requests.

Design observation for Nabil, medium: the request stores only the month, and the postings are built at approval (D-267
item 1). The run asked at 14:18 posted depreciation for an asset registered at about 17:05. The Owner approves an amount
that did not exist when Finance asked, and the request row does not show it. Prepayment amortisation works the same way.

UI defect: at 390 px the «تغيير النسبة» button sits on top of the book value figure and the «الإهلاك الشهري» label
(`features/accounting/assets/assets-page.html:49-60`, cells `cell-primary`, `cell-secondary`, `cell-act`). Screenshot in the
Verifier's scratchpad, `assets390.png`. The `layout` measure passed earlier only because the list was empty.

### F-10 (focus 5) Prepayment create cannot be completed: nothing can fund a prepayment account. Severity: medium, confirms leftover

The prepayment form requires a funding posting into the Prepayment account (`accruals-page.ts:153-154, 238`, read through
`GET /api/treasury/postings?accountId=` at `accounting.api.ts:179-184`). The Prepayment account `ASSET-PREPAID` was seeded in
`edc4a63`. No posting into any Prepayment-type account exists (`select count(*)` returns 0), and no route writes one. The
expense payment into a prepayment is R4-A2, not built (D-266 item 3). The form therefore always shows an empty funding list.
A prepayment amortisation request for 2026-11-01 waits for the Owner with no prepayment to amortise. Not run end to end, and
not approved by the Verifier.

### F-11 (focus 5) Sales stepper at 390: confirmed. Severity: low

On `/sales/01a20de7-53c5-...` at 390 px the six-step stepper runs to the card's inline-end border: step 6 touches the card
edge with no inner padding. Screenshot `opp390.png` in the Verifier's scratchpad. Known leftover, confirmed.

### F-12 Layout measurement of the new routes

`driver.mjs layout` at 390 and 1440 passed (no overflow, no raw keys, no ids, no target under 44 px) on `/sales`,
`/sales/new`, `/accounting/accruals`, `/accounting/assets` (empty, before F-9), `/accounting/requests`,
`/accounting/trial-balance`, `/accounting/overhead`, `/projects/{A}/budget`, `/projects/{A}/closure`,
`/projects/{A}/petty-cash` (supervisor), and as the client `/portal`, `/portal/projects/{A}`, `/extracts` and `/photos`. The
portal routes were measured with a scratch copy of the driver whose sign-in page is `/portal/sign-in`, because the
repository driver has no portal sign-in. The overhead note «للعرض فقط، غير مسجل في الدفاتر» is drawn in the approved mock
`accounting.html`.

## Verdict per story or screen

| Story / screen | Verdict | Reason |
|---|---|---|
| KAFF-609b petty cash list, submit, return (R2) | CONDITIONAL | Server checks role and assignment; «مُرجَع» correct. No live data because no draft route exists (F-5). Submit and return are proven by Api tests only |
| KAFF-309r تشوينات on the QC sheet (R2) | NOT CHECKED | Not reached |
| KAFF-806 / D-271 retention panel and reversal | CONDITIONAL | Finance asks and the Owner decides, enforced both ways (F-2). No posted release on live data, so the reversal was not run |
| R3-10 sales list, new, opportunity page | CONDITIONAL | Server gates every route. Stepper edge at 390 (F-11). Technical Office filtered list missing (F-6) |
| KAFF-700 asset register and depreciation (screen) | CONDITIONAL | Server end to end works and is idempotent (F-9). The row overlap at 390 must be fixed before PASS |
| KAFF-701 accruals and prepayments (screen) | CONDITIONAL | Accrual path has an approved live request. Prepayment create is impossible (F-10). Junior reads project accruals (F-4) |
| KAFF-704 trial balance and export | PASS | Owner and Finance only; layout passes |
| KAFF-708 overhead spread | PASS | Owner and Finance only; note matches the mock |
| KAFF-709 budget baseline screen | PASS | Owner and Finance only; unassigned Finance refused (D-267 item 2). Baseline now set on فيلا 47 (F-3) |
| Accounting requests, Owner approval list (D-267) | PASS | Asker cannot decide; returned reads «مُرجَع» |
| R5-A portal sign-in and password change | PASS | Lockout counted correctly (F-1). The manual's password is stale |
| R5-B portal reads, KAFF-810/811 boundary on live data | PASS | No leak in any response; other client 403; staff 403 on the portal and client 403 on staff routes (F-2, F-7) |
| R5-C publish photo | CONDITIONAL | Junior refused, supervisor and Technical Office allowed, unassigned refused. No published photo exists to view on the portal |
| KAFF-812 change-order decision screen | UNTESTED | The demo client has no change order. Only the refusals and the response type were checked |

## Not reached

- The KAFF-309r QC sheet screen, and the publish button on the daily-log review screen (API checked only).
- A live retention reversal, because no demo project has a posted release.
- A change order decided from the portal as the demo client.
- Screenshot comparison against each mock for hierarchy and copy, beyond the two screenshots above.
- The full Api and `ng` suites, by instruction.
