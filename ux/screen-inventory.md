# Screen inventory — slices 1 to 9

**Breadth, not depth.** This is the map: every screen the system needs, who sees it, and whether it
is project-scoped. Detailed flows exist only for the slice being built (`slice-1-flows.md`);
`process/agile.md` refines one sprint ahead and no further, and writing slice 7's screens in detail
now would mean designing against business rules Karim has not been asked about.

---

## How to read the table

**Roles** — `O` Owner · `F` Finance · `TO` TechnicalOffice · `SE` SiteEngineer · `HD` HeadOfDesign ·
`MS` MarketingSales · `HR` Hr · `C` Client (portal only).
**Subcontractor never appears in any row: `spec.md` §9 gives the role no login at all.**
Role letters here say *who the screen is designed for*. They are **not** the permission list — the
server decides, every request, on role × assignment. See `navigation.md` §"What hiding is and is not".

**Scoped** — `P` the screen is about one project and the API will require an assignment (or global
reach, for Owner and HR); `C` company-wide; `—` neither (session/self).

**Mobile** — `M1` mobile-first, designed at 390px before anything else; `M2` must be comfortable at
390px; `M3` desktop-primary, must still be correct and scrollable at 390px (Definition of Done).

---

## Slice 1 — Foundation: auth, roles, assignment, audit, Client master

**Twenty-one screens.** Five are new on 2026-08-21 (D-049, D-050, D-051): S-003a, S-008a, S-009a,
S-009b, S-016a. **S-002 is no longer blocked.**

| # | Screen | Roles | Scoped | Mobile | Purpose |
|---|---|---|---|---|---|
| S-001 | Login | all with a login | — | M2 | Username and password; the server sets an `HttpOnly` cookie (D-050). **The screen stores nothing.** Staff host only — clients sign in elsewhere. |
| S-002 | One-time Owner setup | — | — | M2 | Shown **only while the users table is empty**; creates the Owner and locks permanently on the emptiness test (D-051 Q31). Karim's reason is auditability: the first record in the trail must name a human. |
| S-003 | Change password | all with a login | — | M2 | Two modes: **forced** (a temporary password the Owner set, unskippable) and voluntary. At least 8 characters, no complexity (D-049 rulings 3, 4). |
| S-003a | Set a new password from a reset link | anyone holding a valid link | — | **M1** | **New.** The recipient half of recovery: opened from an SMS or WhatsApp link on a phone, no session, no shell (D-051 Q38). |
| S-004 | Session resolution and landing dispatcher | all with a login | — | M2 | Not a screen users see. Calls `GET /api/auth/me` — **the only way the UI learns anyone is signed in** — and defines the `resolving` state every screen inherits (D-050). |
| S-005 | My profile | all with a login | — | M2 | Own name, phone, role, department, and the projects I am assigned to with my level. Read-only except password. |
| S-006 | User list | O | C | M3 | Every user, their role, department, active state. Owner only — `UserManage` is `CompanyWide`, Owner alone (D-044 §1). |
| S-007 | User create | O | C | M3 | Mint a login: name, phone, role, department, sub-department, **and a temporary password the user must change on first sign-in** (D-049 ruling 4). The most privileged screen in the system. |
| S-008 | User detail / edit | O | C | M3 | Change department, **change role** — which revokes every project assignment (D-051 Q27) — deactivate, reactivate. |
| S-008a | Send a password reset link | O | C | M2 | **New.** The Owner generates a temporary link sent to the user's registered phone. **The Owner never sees the link and never sets a password** (D-051 Q38). |
| S-009 | Project team (assignments) | O | P | M2 | Who is on this project and at what level. Requires `ProjectRead`. Built from `ProjectAssignment` rows, **never** from the access check. **Not HR's screen** — see S-009b. |
| S-009a | HR project list | HR | C | M2 | **New.** Project **names** and team sizes, on HR's own routes against its own API. Zero financial detail (D-051 Q32). |
| S-009b | HR project team | HR | P | M2 | **New.** The team of one project, and the way in to S-010. A **separate surface**, not a filtered S-009 — a filtered view leaks the first time somebody adds a field. |
| S-010 | Assign user to project | O, HR | P | M2 | Pick a user, pick a level (Standard / Junior / Supervisor), assign. HR reaches any project without being assigned (D-044 §3). **Its user picker is still blocked — Q-UX-16.** |
| S-011 | Client list | MS, O | C | M2 | All clients, searchable by name and phone. Marketing's home. |
| S-012 | Client create | MS, O | C | M2 | New client. Phone is the matching key; **the code is generated (`C-10001`), never typed**; **no withholding field** — it moved to the contract (D-049 rulings 7, 9, 10). |
| S-013 | Duplicate-phone **warning** | MS, O | C | M2 | **The save proceeds.** Names the client already holding the number and asks whether to continue (D-049 ruling 8). Was a refusal until 2026-08-21. |
| S-014 | Client detail / edit | MS, O | C | M2 | Contact details, kind, tax registration number (corporate only), notes, history. **Code read-only. No withholding.** |
| S-015 | Audit trail | O | C | M3 | Who changed what, when, before and after. **Owner-only, company-wide, settled** — hidden from every other role even on their own projects (D-049 ruling 1). |
| S-016 | Access denied / not found / failed | all | — | M2 | Three terminal states. A 403 is expected and must read as a refusal, not a crash. |
| S-016a | Session expired | all with a login | — | M2 | **New.** 30 minutes of inactivity, sliding, and **the page cannot see it coming** (D-049 ruling 2 + D-050). A re-authentication dialog over the current screen, so unsaved work survives. |
## Slice 2 — Masters: catalogue, أبواب, employees, workers, subcontractors, suppliers

| # | Screen | Roles | Scoped | Mobile | Purpose |
|---|---|---|---|---|---|
| S-017 | Catalogue list | TO, O | C | M3 | Code, description, unit, باب, cost price, base sell rate, status. |
| S-018 | Catalogue item create / edit | TO, O | C | M3 | One item. `costPrice` rides alongside and **must never reach a client-facing surface** (`spec.md` §4.2). |
| S-019 | Catalogue Excel import | TO, O | C | M3 | Setup load only. `spec.md` §4.1: not an ongoing sync — the screen must say so. |
| S-020 | Custom-item review queue | TO | C | M3 | BOQ lines flagged `pendingCatalogueReview`. They MUST NOT write to the catalogue automatically (§4.5). |
| S-021 | باب tree | TO, O | C | M3 | ~40 trades, hierarchical, each carrying its default markup %. |
| S-022 | باب create / edit | TO, O | C | M3 | Name, parent, default markup. |
| S-023 | Employee list | HR, O | C | M3 | Every costed person. HR is the single source (`spec.md` §10). |
| S-024 | Employee create / edit | HR, O | C | M3 | Salaried staff. Deduplicated by phone. Costing type is immutable after creation. |
| S-025 | Worker (يومية) registry | HR, O, TO | C | M2 | Searchable pool: trade, specialty, average day rate, frequency, rating. |
| S-026 | Register worker from site | SE | P | **M1** | An engineer registering a worker on site: name, phone, trade/باب, specialty. Phone camera keyboard, 390px, one hand. |
| S-027 | Worker engagement history & rating | HR, O, SE | C | M2 | Per-engagement history and ratings feeding the pool. |
| S-028 | Subcontractor list | TO, O | C | M3 | Owned by the Technical Office; Finance only disburses (`spec.md` §2). |
| S-029 | Subcontractor create / edit | TO, O | C | M3 | Rates, trade, retention % (5%, zeroable per subcontractor 🟡 §5.1). |
| S-030 | Supplier list + create / edit | F, O | C | M3 | One account per supplier, serving many projects (`spec.md` §2). |

## Slice 3 — Treasury: postings, accounts, the five ledgers, non-cash types

| # | Screen | Roles | Scoped | Mobile | Purpose |
|---|---|---|---|---|---|
| S-031 | Account tree | F, O | C | M3 | The `spec.md` §6.3 tree. Two dimensions only: project × party. Not an open chart of accounts. |
| S-032 | Open an account | F, O | C | M3 | `AccountManage`. Creation freezes configuration (guard 3c) — the screen must say the choice is permanent. |
| S-033 | Account statement | F, O | C | M3 | Postings on one account, with a **derived** balance. There is no balance field to show. |
| S-034 | Posting detail | F, O | C | M3 | One posting, its source document, and its reversal if any. **No edit control and no delete control exist.** |
| S-035 | Create reversing posting | F | C/P | M3 | The only correction path. Mirrors the original exactly; requires a reason. |
| S-036 | Safe and bank balances | F, O | C | M2 | Live derived balances. The safe can never go negative — the refusal is a database `KAFF_NEGATIVE_BALANCE`. |
| S-037 | Owner dashboard | O | C | M2 | Includes **aggregate firm-advance exposure across all projects**, which `spec.md` §6.4.3 requires be visible here. |
| S-038 | Owner current account (جاري المالك) | O, F | C | M2 | Injections as a liability; withdrawals as returnable advance or final drawing. |
| S-039 | Record owner injection | O, F | C | M2 | Prompted directly by a refused payment (§6.1). Reachable from the refusal. |
| S-040 | Project five-ledger view | F, O | P | M2 | Client advance · hold · firm advance · عهدة · owner current account, **side by side and never netted**. |
| S-041 | Collections and cheques | F | P | M2 | Method, date, reference; cheque states `received → deposited → cleared → bounced`. |
| S-042 | Withholding on collection | F | P | M2 | A corporate client transfers less than the net; the withheld amount posts to a recoverable asset (§6.7). |

## Slice 4 — Spine: opportunity, pipeline, quotation, conversion, BOQ freeze

| # | Screen | Roles | Scoped | Mobile | Purpose |
|---|---|---|---|---|---|
| S-043 | Pipeline board | MS, O | C | M2 | `Lead → Meeting → SiteVisit → Quotation → Negotiation → Contract` 🟡. Inactivity alerts at day 2/4/7. |
| S-044 | Opportunity detail | MS, O | C | M2 | Client, activity log, stage, stalled state, reopen. |
| S-045 | Close as lost | MS, O | C | M2 | **Reason is mandatory** (`spec.md` §3). |
| S-046 | معاينة (site visit) record | MS, TO, O | C | M2 | The visit report. **The fee is billable and is a deposit on the Opportunity, not revenue.** |
| S-047 | Quotation / estimate builder | MS, O | C | M3 | Estimated quantities. Becomes a *reference* at Closed Won, never the binding BOQ. |
| S-048 | Convert to project | MS, O | C | M3 | Closed Won → Project{type}, carrying client, visit report, estimate, and the معاينة deposit as a credit. |
| S-049 | Open offers on old pricing | TO, MS, O | C | M3 | "You have X open offers on old pricing" — a human decides per estimate (§4.4). |
| S-050 | Project list | O, F, TO, SE, MS, HD | P | M2 | Only projects the user may reach. The status chip and health tag live here — **slice 4, not before**. |
| S-051 | Create / edit project | O, TO — **and F on the tax fields only** | P | M3 | **Two acts, two permissions, and the screen must not blur them (D-055 §1, §3, 2026-08-22).** *Create* needs `ProjectCreate` (company-wide, Owner + Technical Office). *Edit* needs `ProjectManage` (project-scoped — the editor must be **assigned**, Owner excepted by global reach). The contract's withholding category is neither: it needs `ProjectFinancialsEdit`, held by **Finance and the Owner**, and Finance holds no `ProjectManage` at all. This cell read *"unresolved · `ProjectManage` is granted to nobody"* until 2026-08-22 and was wrong twice over. |
| S-052 | Project overview | O, F, TO, SE, MS, HD | P | M2 | Header, contract type, value, progress metric, team, ledgers summary. Type dispatches the metric; it does not fork the screen. |
| S-053 | حصر (quantity survey) | TO | P | M2 | Surveyed quantities — the state that produces the binding BOQ. |
| S-054 | BOQ builder | TO, O | P | M3 | "Add item" always visible, searches by code or description; selecting an item auto-creates its باب section. |
| S-055 | BOQ line editor | TO, O | P | M3 | Conditions (additive, free-entry per project, **no master list**) and line markup defaulting from the باب. |
| S-056 | Margin panel | O, TO | P | M3 | **Total cost, total sell, profit % shown separately.** A single blended figure is forbidden (§4.2). Internal only. |
| S-057 | Contract signature / BOQ freeze | O | P | M3 | The moment the BOQ **copies** catalogue values. The screen must state that prices are frozen by copy. **This is also where the withholding category lands** — set by **Finance**, on the contract, never on the client (D-049 rulings 9, 10 · KAFF-416). |
| S-058 | Three-quantity variance | TO, O | P | M3 | Estimated ↔ surveyed and surveyed ↔ executed, both computable and both shown. |

## Slice 5 — Billing: extract chain, three calculators, change orders

| # | Screen | Roles | Scoped | Mobile | Purpose |
|---|---|---|---|---|---|
| S-059 | Extract list | O, F, TO, SE | P | M2 | مستخلصات with state and value. A stopped project MUST NOT issue extracts — the screen must refuse, not hide. |
| S-060 | Extract builder (Lump Sum) | SE, TO | P | M2 | Period quantities against the BOQ. Certified value is separate from what is paid. |
| S-061 | Extract certificate | O, F, TO, SE | P | M3 | **MUST show: work value · hold this period · hold to date · advance recovered · تشوينات · net payable.** |
| S-062 | Change-order section on the extract | O, F, TO | P | M3 | **Its own section, at its own prices, never merged into original BOQ lines.** |
| S-063 | Extract approval queue | TO, F, O | P | M2 | The chain: QC → Technical Office gate → Accounts → Owner. Nobody creates and approves the same movement. |
| S-064 | Partial approval / NCR hold | TO | P | M2 | Approve a percentage, hold the remainder until an NCR closes → `PartiallyApproved`. |
| S-065 | Reject with reason | TO, F, O | P | M2 | Any rejection returns the extract to `Draft` with a written reason. **Never a silent step-back.** |
| S-066 | Extract documents | TO, SE | P | M2 | حصر report, site photos, subcontractor claim, attached to the extract. |
| S-067 | Subcontractor extract | TO, F, O | P | M2 | Weekly, same chain; 5% retained, released at warranty end. |
| S-068 | Cost Plus statement | F, O, TO | P | M3 | `Σ(supervised × (1+supervision%)) + Σ(exempt at cost)`. **No percentage progress bar.** Summary, not line items 🟡. |
| S-069 | Cost Plus line classification | TO | P | M3 | Every line classified once at contract creation: supervised / exempt / non-billable. |
| S-070 | Design stage board | HD, O, F | P | M2 | Five stages with fixed weights 30·20·20·20·10. A stage bills when the client approves in the portal. |
| S-071 | Change order editor | TO, O | P | M3 | Lump Sum priced chain; Cost Plus and Design are `Draft → Logged`, documentary and unpriced. |
| S-072 | Adjustment (credit / debit note) | F, O | P | M2 | The one object for every case where money flows back (§6.9). |

## Slice 6 — Execution: daily log, عهدة, site expenses

| # | Screen | Roles | Scoped | Mobile | Purpose |
|---|---|---|---|---|---|
| S-073 | **Daily log — capture** | SE | P | **M1** | The reference mobile screen for the whole product. One log per engineer per project per day. |
| S-074 | Daily log — executed quantities | SE | P | **M1** | **Period deltas, never cumulative totals.** The field labels must make that unmistakable. |
| S-075 | Daily log — day labour (يومية) | SE | P | **M1** | Workers present today, hours, rate. Feeds HR costing. |
| S-076 | Daily log — materials and تشوينات | SE | P | **M1** | Delivered to site, not yet installed. |
| S-077 | Site photo capture | SE | P | **M1** | Attach to the log. Client-visible by default but **published deliberately**, not mirrored automatically. |
| S-078 | Photo publish queue | Operations/Administrative | P | M2 | The deliberate publication step (§8). |
| S-079 | Check-in / check-out | SE | P | **M1** | Times on the log; feeds the attendance half of the KPI set. |
| S-080 | عهدة request | SE | P | **M1** | Junior drafts → supervisor submits → accounts pays. Per-request cap 10,000 EGP 🟡. |
| S-081 | عهدة clearance with receipts | SE, F | P | **M1** | **No new عهدة before the previous one is cleared.** The screen must state the block, not hide the button. |
| S-082 | عهدة approval / disbursement | F, O | P | M2 | Online only. Money never moves offline. |
| S-083 | Site expense draft | SE | P | **M1** | An engineer's expense entry is a **draft**. The word draft must be on the screen. |
| S-084 | Site expense confirmation | F, Operations/Administrative | P | M2 | Accounts confirms and posts. Entered by Finance or Admin, not the engineer (§8). |

## Slice 7 — Accounting: depreciation, accruals, close, statements

| # | Screen | Roles | Scoped | Mobile | Purpose |
|---|---|---|---|---|---|
| S-085 | Asset register | F, O | C | M3 | Straight-line / declining defaults from Law 91/2005 Art. 25, editable per asset. |
| S-086 | Asset create / depreciate | F, O | C | M3 | 50% computers and software · 25% other · 5% buildings · 10% purchased intangibles. |
| S-087 | Accruals and prepayments | F, O | C | M3 | Posting types, not a separate ledger. |
| S-088 | Month-end close | F (assumed) | C | M3 | **A closed period is immutable.** Who performs it is unresolved (`decisions.md` D-012 Q10). |
| S-089 | Trial balance + export | F, O | C | M3 | §6.6 requires the export. |
| S-090 | Balance sheet and P&L | O, F | C | M3 | Equity accounts, current-year profit rolling into retained earnings at year close. |
| S-091 | Revenue recognition / WIP | F, O | C | M3 | Contract asset (executed > billed) or contract liability (billed > executed) at close. |
| S-092 | Withholding register | F, O | C | M3 | Asset side (clients withheld from us) and liability side (we withheld from subs and suppliers), **separately**. |
| S-093 | Budget vs committed | O, F, TO | P | M3 | Alerts fire on **committed** money — approved invoices and orders — not only cash paid (§6.8). |
| S-094 | Overhead spread report | O, F | C | M3 | 🟡 A report, **not postings**. Project actual cost stays clean. |

## Slice 8 — Closure, warranty, and the client portal

| # | Screen | Roles | Scoped | Mobile | Purpose |
|---|---|---|---|---|---|
| S-095 | Snag list | TO, SE, O | P | M2 | Description · location · owner (Kaff or subcontractor) · priority · `open → fixed → verified`. |
| S-096 | Snag resolution | TO, O, F | P | M2 | Dual toggle: deduct from the subcontractor (debit note) or absorb as internal cost. |
| S-097 | Handover | O, TO | P | M2 | **Major snags block handover; minor snags do not, and the hold still releases in full.** |
| S-098 | Hold release | O, F | P | M2 | Once, in full, at handover. The only screen in the system that debits the hold. |
| S-099 | Warranty and callbacks | O, TO, F | P | M2 | 4 months from handover, started automatically. Fault decides who pays. |
| S-100 | Project closure checklist | O, F | P | M2 | Closes only when extracts issued, advance recovered, hold released, sub accounts closed. |
| S-101 | **Portal — login** | C | — | M2 | **On a separate URL and host** (D-051 Q33) — its own origin, its own cookie, and no link from the staff application in either direction. See `navigation.md` → Client. |
| S-102 | **Portal — project overview** | C | P | M2 | Section-level progress and the time plan. Nothing below section level. |
| S-103 | **Portal — my مستخلصات** | C | P | M2 | His extracts, paid versus remaining. Never a cost, never a margin, never a subcontractor. |
| S-104 | **Portal — approve change order** | C | P | M2 | Priced change orders only. Approve or reject with a reason. |
| S-105 | **Portal — design stages** | C | P | M2 | Approve a stage deliverable, which is what bills it. Deliverables watermarked until paid. |
| S-106 | **Portal — published photos** | C | P | M2 | Only what was deliberately published. |
| S-107 | **Portal — released deliverables** | C | P | M2 | The final documentation releases on delivery; the last 10% is collected after. |

## Slice 9 — Mobile and offline

| # | Screen | Roles | Scoped | Mobile | Purpose |
|---|---|---|---|---|---|
| S-108 | Mobile shell | SE | — | **M1** | The engineer's whole application. Bottom navigation, thumb-reachable, one hand. |
| S-109 | Sync status | SE | — | **M1** | What is queued, what has synced, what failed. Honest about being offline. |
| S-110 | Pending drafts | SE | P | **M1** | Everything created offline is a draft. **Money never moves offline** — the screen states it. |
| S-111 | Conflict review | SE | P | **M1** | Same engineer, same day, same field: latest timestamp wins. Two engineers' entries sum. |

## Not in any slice — raised, not designed

`agents.md`'s slice sequence has no home for these. They are in `spec.md`, so somebody will build them
eventually, and pretending otherwise means they arrive as an unplanned sprint.

| # | Screen | Source | Note |
|---|---|---|---|
| S-112 | Weekly performance scoring | `spec.md` §10 🟡 | Eleven weighted KPIs; supervisor scores the judgement half **weekly, not daily**. |
| S-113 | Engineer monthly KPI report | `spec.md` §10 🟡 | Score, rank, trend. |
| S-114 | Technical Office task board | `spec.md` §10 | Planned vs actual hours, 1–5 quality score, feeding department KPIs. |
| S-115 | Payroll run | `spec.md` §10 🟡 | A treasury event: day labour weekly, salaries monthly, owner-approved, tagged project or company. Touches money — it is not a small screen. |
| S-116 | Company expenses | `spec.md` §6.10 | Six categories, and **every expense tagged project or company at the moment of spending — never both, never neither**. |
| S-117 | Marketing assets on the client file | `spec.md` §11 | Closure produces them; a referral opportunity is created manually. |

**Total: 122 screens.** Slice 1 is 21 of them — five were added on 2026-08-21 when Karim's
rulings turned four unanswered questions into screens that have to exist.

---

## Two boundaries that cut across the whole inventory

### 1. The portal is a different application on a different host, not a filtered view

S-101 to S-107 are the only screens a `Role.Client` user ever reaches. They must be built against a
separate `/api/portal/*` surface with **its own response types and no shared DTOs** — the failure mode
being guarded against is a shared DTO with an `if (isClient) omit` branch that somebody forgets to
update (`decisions.md` D-035, action A9). This came within one careless endpoint of leaking already.

**And since D-051 (Q33) it is a separate URL and host**: *"their portal must be a completely isolated
interface."* Clients never see the staff sign-in screen, the staff application carries no portal route
of any kind, and the session cookie cannot travel between the two — it is `__Host-` prefixed with no
`Domain` (D-050), so the isolation is enforced by the browser rather than by every future endpoint
remembering.

Nothing on any other row of this inventory may be reached by a client, including by URL.

### 1b. HR's project surface is the same pattern, for the same reason

S-009a and S-009b are to the internal project screens what the portal is to everything else: **a
separate surface with its own routes, its own API and its own narrow permission** (D-051 Q32). If you
find yourself adding a role check to S-009 so that it can serve HR too, that is the mistake this row
exists to name.

### 2. Cost never crosses to a client-facing surface

`costPrice`, line markup, the margin panel (S-056), the catalogue (S-017), subcontractors (S-028,
S-067) and internal notes exist on internal screens only. `spec.md` §4.2: `costPrice` "MUST NOT appear
in any client-facing output". That includes a print view, a PDF, a photo caption and an email.
