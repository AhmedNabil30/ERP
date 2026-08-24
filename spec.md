# Kaff ERP — Business Specification

**Status:** authoritative. This file is the single source of business truth for every agent session. If code and this file disagree, this file wins.
**Client:** Kaff — Egyptian construction and finishing contractor.
**Owner of decisions:** Karim (business owner), via Nabil (BA lead).

---

## 0. How to read this file

- **MUST / MUST NOT** are hard rules. Violating one is a defect regardless of tests passing.
- **🟡** marks an assumption awaiting confirmation. Implement it, keep it configurable, don't design around it as if permanent.
- Arabic domain terms are used deliberately. They map to code identifiers in §14.
- Money figures in examples are acceptance criteria, not illustrations.
- **📌 AMENDMENT** blocks record rulings Karim has given since this file was first written. They sit
  beside the original text rather than replacing it, so what changed stays visible. **An amendment
  has the same force as the paragraph above it, and where the two disagree the amendment wins.**
  Every amendment names its date and its `decisions.md` entry.

---

## 1. Scope

Kaff runs one business cycle end to end:

`Lead → معاينة (site visit) → quotation → contract → execution or design → مستخلصات (progress billing) → handover → 4-month warranty → furnishing → project photos feed marketing`

**Explicitly out of scope. Do not implement, do not "helpfully" add:**
- Any tax module. VAT handling is limited to §6.7.
- E-invoicing / ETA integration.
- Multi-company, multi-branch, multi-currency (currency field exists, conversion logic does not).
- A general ledger with free-form manual journal entries.
- The consultant role.
- Supplier bidding, RFQ, quote comparison (phase 2).
- Bank guarantee letters (خطابات ضمان).
- Depreciation schedules beyond straight-line/declining defaults in §6.6.

---

## 2. Core entities and ownership

Exactly one module owns each entity. Others reference it. A second copy of any master record is a defect.

| Entity | Owner | Notes |
|---|---|---|
| Client | Marketing | project-independent, full history, deduplicated by phone |
| CatalogueItem | Technical Office | unified for sales and execution |
| Bab (باب) | Technical Office | ~40 trades, tree, carries default markup % |
| Employee / Worker | HR | every costed person, exactly one record |
| Subcontractor | Technical Office | rates and BOQ; Finance only disburses |
| Supplier | Finance | one account, serves many projects |
| Opportunity | Sales | becomes a Project at Closed Won |
| Project | Projects | one of three contract types |
| Account / Posting | Treasury | the ledger |

> ### 📌 AMENDMENT — Karim, 2026-08-21 · `decisions.md` D-049
>
> **"Deduplicated by phone" is a warning, not a refusal.** A repeated number shows the operator which
> client already holds it and asks whether to proceed. **It does not block the save.** Karim: *"a
> corporate client and its CEO might be registered as two separate entities sharing the same contact
> number."*
>
> This softens §3's *"never create a duplicate client"* in the same direction: the system's job is to
> make a duplicate obvious at the moment it would be created, not impossible. The index on the
> normalised phone remains and is no longer unique — and matching *harder* matters more now, because a
> missed match used to mean a wrongly-accepted save and now means a warning nobody sees.
>
> **Client codes are generated, never typed.** Sequential, of the form `C-10001`, with manual entry
> and later editing both forbidden — so a code is a stable reference for extracts and ledgers rather
> than something a person can mistype or change. This closes D-040, which had flagged `Client.Code` as
> a required field nobody had asked for.

> ### 📌 AMENDMENT — Karim, 2026-08-21 · `decisions.md` D-052
>
> **The table above names "Projects" as the owner of `Project`, which is not one of §9's roles.**
> It is now answered: **only the Owner and the Technical Office may open a project.**
>
> Karim: Marketing brings in the client and registers their master file, but opening a project
> *"triggers engineering items, accounting ledgers, and cost tracking. It is strictly a technical and
> administrative responsibility. Site Engineers and Marketing have no business creating projects."*
>
> This was the oldest open question in the permission catalogue: the capability was granted to nobody,
> so **no project could be created at all.**
>
> 🟡 One half is still open, and it is structural rather than commercial: the permission is
> project-scoped, and a *create* request cannot name a project that does not exist yet. Whether it
> becomes company-wide or splits into create and edit is an architecture decision with a §9
> consequence. See D-052.

---

## 3. Sales pipeline

Stages 🟡: `Lead → Meeting → SiteVisit → Quotation → Negotiation → Contract`

- Inactivity: day 2 first alert, day 4 second alert, day 7 status becomes `Stalled`. Configurable. Activity revives it.
- Closed Lost MUST record a reason.
- Reopening attaches to the same Client. Never create a duplicate client.
- **معاينة is billable.** The fee is recorded as a deposit on the Opportunity, not as revenue. On Closed Won it transfers as a credit against the new contract.
- Pre-contract expenses are tracked against the Opportunity and are not recovered from the client.

**Conversion at Closed Won:** Opportunity → Project{type}, carrying the client, site visit report, and the estimate. The estimate becomes a *reference* for the Technical Office, never the binding BOQ directly.

---

## 4. Catalogue, BOQ and pricing

### 4.1 Catalogue
One catalogue serves both sales and execution. Item: `code · description · unit · bab · costPrice · baseSellRate · status`.
Loaded from Excel at setup. Edited manually after. Excel import is not an ongoing sync.

### 4.2 Line pricing
```
lineRate = baseSellRate × (1 + Σ conditions) × (1 + lineMarkup)
```
- **Conditions** are additive, not compounding. They are free-entry **per project** — the engineer writes what the site demands. There is no master list of conditions.
- **lineMarkup** defaults from the item's باب (e.g. concrete 15%, finishes 30%) and is overridable per line.
- `costPrice` rides alongside for margin and variance. It MUST NOT appear in any client-facing output.
- Margin display MUST show total cost, total sell, and profit % separately. A single blended margin figure is forbidden — the client owner found it unreadable.

### 4.3 Three quantity states, same item, all retained
1. **Estimated** — entered by Sales in the quotation
2. **Surveyed (حصر)** — set by the Technical Office at Closed Won; this produces the binding BOQ
3. **Executed** — captured from the site daily log

Two variance signals must be computable: estimated↔surveyed and surveyed↔executed.

### 4.4 Freeze rule — MUST
At contract signature the BOQ **copies** catalogue values. It MUST NOT hold a reference. A later catalogue price change cannot reach a signed contract because no link exists to follow.

Open (unsigned) estimates re-price only through an explicit review: the system alerts "you have X open offers on old pricing" and a human decides per estimate.

### 4.5 BOQ builder
- "Add item" is always visible and searches the catalogue by code or description.
- Selecting an item auto-creates its باب section if absent.
- New lines inherit the باب default markup.
- Custom items are allowed on the BOQ only, flagged `pendingCatalogueReview`, and land in a Technical Office queue. They MUST NOT write to the catalogue automatically.
- Empty BOQ shows an explicit empty state. Never phantom pre-filled rows.

---

## 5. Contract types

Three types. They share one treasury, one approval engine, one project entity. They differ only in **billing calculator** and **progress metric**. Forking the project module three ways is forbidden.

### 5.1 Lump Sum

**Billing:** each مستخلص certifies work done at full value. Deductions reduce the *payment*, never the certified value.

```
period value      = cumulative certified work − previously certified
+ تشوينات          = material value × 75%
− hold            = 20% of period work value          ← accumulates, released at handover
− advance recovery = 25% of period work value 🟡      ← until the advance ledger reaches zero
− تشوينات recovery = as advanced material is installed
− delay penalty   = optional, off by default 🟡
= net payable
```

**Hold rules — MUST:**
- The hold posts into its own ledger that **only grows** during the project.
- Nothing may be taken out of it mid-project — not a snag, not a debit note, not an adjustment.
- It releases **once, in full, at handover**, even with minor snags open.
- Hold is calculated on certified work value only. **تشوينات carries no hold** 🟡.
- There is **no 80% billing ceiling**. Billing runs to 100% of certified work; the hold accumulates on its own to 20% of contract value.

**Extract display MUST show:** work value · hold this period · hold to date · advance recovered · تشوينات · net payable.
**Change orders MUST appear in their own section** at their own prices, never merged into original BOQ lines.
**Documents attach to the extract:** حصر report, site photos, subcontractor claim.

**Subcontractors:** one weekly extract each. Kaff retains **5%** from every sub extract, released when the project's warranty ends (4 months after handover), zeroable per subcontractor 🟡.

### 5.2 Cost Plus

Every contract line is classified by the Technical Office at contract creation into exactly one of:
1. **Supervised** — billed at `cost × (1 + supervision%)`
2. **Exempt** — billed at cost, no supervision markup (e.g. مشال, mobilization)
3. **Non-billable** — Kaff absorbs it

`invoice = Σ(supervised × (1 + supervision%)) + Σ(exempt at cost)`

No hold. No تشوينات. No billing ceiling. Progress metric is cost-to-date plus supervision — **no percentage progress bar**. Kaff engineers' hours are internal cost and MUST NOT be billed. The client statement is a summary, not line items 🟡.

### 5.3 Design

`fee = area × rate per m²` (currently 450 EGP/m²). No lump-sum option.

Five stages with **fixed** payment weights: `Concept 30 · Schematic 20 · 3D 20 · Design Development 20 · Final Documentation 10`. The 30% is the deposit.

- A stage bills when the client approves its deliverable in the portal.
- Deliverables are watermarked until paid (IP retention).
- The final deliverable releases on delivery; the last 10% is collected after.
- Revisions within the agreed rounds per stage are free rework. Beyond that, a billable mini change order referencing stage and round number.
- No BOQ, no extract, no hold, no تشوينات, no subcontractors, no site.
- Daily log for design records phase progress %, not materials.

### 5.4 Linked projects
Two distinct link semantics, both sharing a client and portal view but keeping separate accounts and billing:
- `design_to_execution` — on execution signature, **30% of the design total** posts as a credit adjustment on the execution contract, and design quantities seed the execution BOQ.
- `parent_child` — furnishing is a small execution project linked to its parent, with its own BOQ and subcontractors.

A project MUST NOT mutate from one type into another.

---

## 6. Money

### 6.1 The posting model — MUST
Every financial event is a `Posting`. Balances are **always derived by summing postings** and MUST NOT be stored as an editable value.

```
Posting: id · date · fromAccount · toAccount · amount · type ·
         sourceDocument · projectId? · createdBy · createdAt · reversesId?
```

- Postings are **append-only**. Never updated, never deleted. Corrections are new reversing postings referencing the original.
- Amounts are exact decimals — `decimal(18,4)`. Floating point is forbidden anywhere money is involved.
- **The safe balance MUST NOT go negative.** A payment that would breach this fails and prompts an owner injection instead. Enforce in the database, not only in application code.

> ### 📌 AMENDMENT — Karim, 2026-08-20 · `decisions.md` D-044 §6
>
> **Four decimals in storage and in every calculation; two decimals in anything a client sees.**
> "Calculations and database storage must maintain 4 decimal places for precision, but client-facing
> extracts and UI displays must be rounded to 2 decimal places."
>
> The rounding happens at the **last** step, on display. Rounding earlier lets display precision back
> into the arithmetic, which is the failure the split exists to prevent.
>
> 🟡 Rounding **direction**, and whether the contractual figure printed on an extract is 2 or 4
> decimals, was not asked and is not answered — see `stories/questions-for-karim.md` Q21.

### 6.2 Posting types — cash and non-cash
The engine MUST support non-cash postings from day one: revenue recognition, expense accrual, prepayment, depreciation, WIP adjustment, tax withheld. Building it cash-only means rewriting the core later.

### 6.3 Account tree
```
Safe (cash)  +  Bank accounts (QNB, CIB, الأهلي, …)
  └ Project account
       ├ Client sub-ledger
       └ Subcontractor sub-ledger
  └ Company / overhead account
  └ Owner current account (جاري المالك)
```
Two dimensions only: project × party. This is not an open-ended chart of accounts.

### 6.4 The five ledgers — never netted against each other
1. **Client advance** — in, recovered through extracts, reaches zero
2. **Hold** — accumulates only, releases once at handover
3. **Firm advance** — Kaff spending on a client's behalf: owner approval, hard cap the system enforces, aggregate exposure across all projects visible on the owner dashboard
4. **عهدة (petty cash)** — junior drafts → supervisor submits → accounts pays. Ceiling per project 🟡: `collected − spent − owner safety margin`; plus a per-request cap of 10,000 EGP 🟡. No new عهدة before the previous one is cleared with receipts.
5. **Owner current account** — injections are a liability repaid later; withdrawals are either a returnable advance or a final drawing.

> ### 📌 AMENDMENT — Karim, 2026-08-20 · `decisions.md` D-044 §8
>
> **Exactly three accounts carry a hard, non-negative floor enforced by the database: the safe (§6.1),
> the client advance (§15), and عهدة.** No others.
>
> The hold, the firm advance and تشوينات are **not** floored. Two consequences must be carried by the
> tests rather than by the database, because the database no longer refuses them:
>
> - nothing stops a **firm advance** being recovered past zero — §6.4.3's *hard cap* above is the
>   control, and it is not built yet;
> - nothing stops **تشوينات** being recovered past what was issued — §15's "تشوينات in equals تشوينات
>   recovered" is still required, but is now caught at reconciliation rather than at the posting.
>
> The hold loses nothing in practice: it may not be posted out of before handover, and its release
> must leave it at exactly zero.

### 6.5 Collections and cheques
Every collection records method (cash / cheque / transfer), date and reference. Cheque states: `received → deposited → cleared → bounced`. Client collections default to bank; عهدة and day labour are cash 🟡.

### 6.6 Accounting layer
Required so the balance sheet is real rather than approximate:
- **Asset register with depreciation.** Defaults from Egyptian Income Tax Law 91/2005 Art. 25: computers, software and data storage 50%; all other business assets 25%; buildings 5%; purchased intangibles 10%. Editable per asset.
- **Accruals and prepayments** as posting types.
- **Month-end close** — a closed period is immutable.
- **Trial balance export.**
- **Equity accounts** — paid-in capital, retained earnings, current-year profit. Profit rolls into retained earnings at year close.
- **Bank loans / equipment financing** 🟡 — principal, instalments, interest to expense, remaining principal on the balance sheet. Confirm whether Kaff has any.

**Revenue recognition:** operate day to day on certified extracts. At month close, compute percentage-of-completion revenue from executed quantities and present the difference as a contract asset (executed > billed) or contract liability (billed > executed).

### 6.7 Withholding tax — MUST, and this is not a tax module
Egyptian withholding at source (Law 91/2005, Decree 308/2018): **1% contracting and supplies · 3% services · 5% professional fees**, computed on the amount before VAT.

- When a **corporate client** pays, they withhold and transfer less than the extract's net. The collection MUST record the withheld amount, posting it to a "tax withheld at source" **asset** (recoverable against income tax), so cash reconciles.
- When Kaff pays subcontractors and suppliers, Kaff withholds and carries a **liability** to remit.
- Individual clients do not withhold. Each Client carries a flag 🟡 for whether they are a withholding entity.

> ### 📌 AMENDMENT — Karim, 2026-08-21 · `decisions.md` D-049
>
> **The rate belongs to the contract, not to the client.** The line above put a flag on the Client;
> that cannot be right, and the reason is in this same section: the rate follows **what is supplied**
> — 1% contracting and supplies, 3% services, 5% professional fees — while §5.4 lets one client hold a
> design contract and an execution contract at the same time. Karim: *"The same client (e.g. a
> government body) might sign a design contract (one rate) and an execution contract (another rate).
> Storing it on the client profile breaks this reality."*
>
> **Finance sets it, during contract creation or approval. Marketing cannot.** The rate dictates ledger
> entries and how much cash a collection is expected to carry, which makes it an accounting parameter
> rather than a detail of the client's file.
>
> **"Individual clients do not withhold" is unchanged and now enforced in two places** — a rate on a
> contract whose client is an individual is refused, and so is a tax registration number on an
> individual, which is the same claim by another field. The client still carries the registration
> number, because that identifies the legal entity and does not vary by contract.
>
> 🟡 **Not ruled on: subcontractors and suppliers.** The paragraph above — "when Kaff pays
> subcontractors and suppliers, Kaff withholds" — has the same shape, and those rates are still held
> on the party record. Karim's ruling named the client only, so nothing was changed there. See D-049.

Without this, collections will never match issued extracts and staff will invent adjustments to close the gap.

**VAT** 🟡: pending confirmation of Kaff's registration status. If registered, add an output VAT line and a VAT payable account. If not registered, remove the optional VAT print line entirely so nobody uses it by accident.

### 6.8 Budgets
Baseline per project = signed BOQ cost plus an owner-set tolerance 🟡. Alerts fire on **committed** money — approved invoices and orders — not only on cash paid. An approved change order raises the baseline. Nothing else may edit it.

### 6.9 Adjustments
One `Adjustment` object covers every case where money flows back: client credit note, subcontractor debit note, the 30% design credit, termination settlement. Type, reason, source document, target account. It posts like any other movement.

### 6.10 Company expenses
Categories: rent and utilities · admin and office payroll · assets and vehicles · general materials · bank charges · other. **Every expense is tagged project or company at the moment of spending — never both, never neither.** This is what makes gross and net margin correct.

A monthly analytical report 🟡 spreads company overhead across projects in proportion to project size. It is a report, not postings — project actual cost stays clean.

---

## 7. The مستخلص approval chain

```
Site engineer prepares quantities
  → weekly QC report
  → Technical Office quantity gate   [BLOCKING]
  → Accounts (deductions, hold, advance recovery)
  → Owner approval                   [EVERY extract, no threshold]
  → Issued → Collected
```

- The Technical Office gate is **splittable**: approve a percentage for payment and hold the remainder until an NCR closes. The extract enters `PartiallyApproved`; the held portion releases when the NCR closes.
- The Technical Office assembles the client extract document. The supervising engineer confirms field quantities.
- **Any rejection at any gate returns the extract to Draft** with a written reason and full audit trail. Never a silent step-back.
- A disputed issued extract resolves as either a revised extract or a credit note.
- A stopped project MUST NOT issue extracts.

Subcontractor extracts follow the same shape: certify → Technical Office gate → Accounts → Owner → post to the subcontractor's sub-ledger.

---

## 8. Site execution

**One daily log per engineer per project**, only on projects he is assigned to. It captures executed quantities, day labour, materials and تشوينات, photos, and check-in/check-out times.

- **The daily log records period deltas, never cumulative totals.** This makes offline sync additive and conflict-free: two engineers' entries sum. Same engineer, same day, same field: latest timestamp wins.
- **Site financial expenses are entered by Finance or Admin, not the engineer.** An engineer's expense entry is a draft that Accounts confirms and posts.
- A stopped project still accepts daily entries recording the stoppage and its reason.
- Photos are client-visible by default and are **published deliberately**, not mirrored automatically.

**Mobile app** (offline-first): daily report, عهدة request, invoice photos, site photos, check-in/out. **Money never moves offline** — offline actions produce drafts; approval and disbursement happen online against a live balance.

---

## 9. Roles and permissions

**Permission = role × assignment.** A user MUST be assigned to a project to open it or act on it. Role alone is insufficient. Enforcement is server-side; hiding UI elements is presentation, not security.

**Roles:** Owner · Finance/Accounts · Technical Office · Site Engineer (Supervisor and Junior) · Head of Design (phase 2) · Marketing/Sales · Client (portal) · Subcontractor (record only, no login).

**Departmental segregation:** Finance, HR, Marketing, Operations. Operations subdivides into **Technical** (quantities, BOQ, extract gate), **Financial** (site expenses, عهدة), and **Administrative** (reports, photos, tasks).

**Separation of duties — MUST:**
- Owner approves all financial movements.
- Finance prepares and disburses but does not approve change orders.
- Technical Office gates quantities, never money.
- Site engineers approve nothing financial.
- **Nobody creates and approves the same movement.**

**Junior vs Supervisor:** a junior engineer raises requests as drafts; the supervisor submits them.

> ### 📌 AMENDMENT — Karim, 2026-08-17 and 2026-08-20 · `decisions.md` D-010, D-044
>
> **1. The Owner is globally scoped.** "Owner role is like the admin so yes global." The Owner reaches
> every project without an assignment row — the single exception to the rule above that role alone is
> insufficient. This is **reach, not capability**: the Owner still holds only what the permission
> catalogue grants, and still cannot both create and approve the same movement.
>
> **2. HR is a ninth role, not only a department.** The roles list above names eight. `Hr` is added,
> "to ensure strict segregation of duties, rather than dangerously piggybacking on the Finance role."
>
> **3. HR is strictly administrative and has zero financial visibility** — it "cannot see project
> costs, margins, or the safe". HR holds exactly two capabilities: managing employee records (§2, §10)
> and assigning users to projects. It holds no read on a project, nothing in the treasury, and no gate.
>
> > **⚠️ THE COUNT IS SUPERSEDED — Nabil, 2026-08-22, `decisions.md` D-055 §2.** HR holds **three**
> > capabilities: `UserRead` is added, names and roles only, so HR can name the people it is required
> > to staff projects with. Until then HR could reach every project and staff none of them.
> > **The rule this point states is unchanged** — strictly administrative, zero financial visibility.
> > `UserRead` touches no money, holds nothing on a project and reaches no gate. See the 2026-08-22
> > amendment at the end of this section.
>
> **4. HR also has global reach for assignments.** "HR does not need to be assigned to a project first
> in order to staff it." Requiring an assignment in order to create assignments is circular — on a new
> project nobody is assigned, so nobody could make the first one.
>
> **5. Only the Owner creates users**, company-wide. Nothing in this file previously said who could,
> which meant no user could be created at all.
>
> **6. The Owner may create and edit all master data**, across the company, without the departmental
> restriction of §2. §2's ownership column still says who owns a record day to day.
> 🟡 Karim's ruling stated "all master data" and then listed clients, suppliers and banks; the general
> statement is what has been applied. See `decisions.md` D-045.
>
> **7. Junior/Supervisor is a property of the assignment, not the person** — confirming the paragraph
> above. "An engineer can be a Supervisor on one project and a Junior on another."

> ### 📌 AMENDMENT — Karim, 2026-08-21 · `decisions.md` D-049
>
> **1. The audit trail is the Owner's alone.** Company-wide, and "completely hidden from all other
> roles, **even for their own projects**". The rejected option is the one worth recording: a
> project-scoped audit read for the people working on that project. From §6 the trail carries every
> movement of money, so scoping it by project would reopen the visibility rule from a direction nobody
> was watching. 🟡 The ruling anticipates a global finance/audit role later; it does not create one.
>
> **2. Sessions expire after 30 minutes of inactivity.** Signing out on one device does not sign the
> user out elsewhere — but **a password change or a deactivation must invalidate every active
> session**, everywhere, immediately.
>
> **3. Passwords are at least 8 characters with no forced complexity**, and an account locks for 15
> minutes after 5 consecutive failed attempts. Karim's reason for the absent complexity rule is a
> requirement in itself: site workers must be able to sign in.
>
> **4. Onboarding is a temporary password set by the Owner, which the user MUST change on first
> sign-in.** Site engineers often have no company email, so a reset link cannot be the primary path.
> Forcing the change is what keeps the audit trail meaningful: after it, the Owner does not know the
> credential that acts as that user.
>
> > **Clarified — Nabil, 2026-08-21, `decisions.md` D-052.** This does **not** apply to the first
> > Owner created through the setup screen. He types that password himself, so nobody else has ever
> > known it and the non-repudiation the rule protects is not at risk. The rule exists for an account
> > created *for somebody else* with a credential its creator knows.
>
> **5. Leavers are deactivated, never deleted, and stay on historical project teams.** A returning
> employee gets a new password and **zero project assignments** — nothing is restored automatically.
>
> **6. A role change is refused while the user is an active Supervisor on any project.** Not
> auto-removed: Karim — auto-removal "leaves a construction site headless", so HR must take them off
> each project deliberately, which is what a handover looks like in the data.
>
> > **⚠️ SUPERSEDED the next day — Karim, 2026-08-21 (second ruling), `decisions.md` D-051.**
> > Point 6 above is **reversed**: a role change **automatically revokes every project assignment**,
> > Supervisor and Junior alike. Karim: *"their direct link to the site must be severed automatically
> > to prevent lingering responsibilities. If they are needed on the project in their new capacity, HR
> > must re-assign them."*
> >
> > Kept visible rather than rewritten because the two rulings weigh the same risk in opposite
> > directions — a headless site against a lingering liability — and the second answer is the one that
> > holds. The re-assignment step is what replaces the deliberate handover the first ruling wanted.

> ### 📌 AMENDMENT — Karim via Nabil, and Nabil with the Architect, 2026-08-22 · `decisions.md` D-055
>
> **Three permissions are added.** Each exists because a capability above was held by the wrong row —
> either by a row too wide for the act, or by no row at all.
>
> **1. Opening a project and editing one are different permissions.** `ProjectCreate` is
> **company-wide**, held by the Owner and the Technical Office — the holders Karim named on 2026-08-21.
> `ProjectManage` keeps its **project-scoped** form, so the assignment requirement at the head of this
> section keeps applying to every edit of a project that already exists.
>
> > *Why the split, since it will look like duplication:* a create request cannot name the project it
> > is about to create, and a project-scoped permission requires one. Making the single row
> > company-wide would have fixed creation **by removing the assignment requirement from editing** —
> > which is a change to this section, not a drafting convenience. Two narrow rows keep each grant the
> > size of its ruling. A later session must not merge them back.
>
> **2. Finance never holds `ProjectManage`, and gets its own narrow row instead.** Nabil: *"An
> accountant must not alter the engineering scope of a project."* The contract's tax and financial
> settings — the withholding category of §6.7, which the 2026-08-21 amendment moved from the client
> onto the contract — sit behind **`ProjectFinancialsEdit`**, held by **Finance and the Owner** alone.
>
> > *This resolves a collision between two earlier rulings, not a gap.* The 2026-08-21 ruling gave
> > `ProjectManage` to the Owner and the Technical Office, from a ruling about *opening* a project.
> > The ruling of the day before gave **Finance** the withholding category — *"a strict accounting
> > parameter, not a marketing detail"*. Finance held no `ProjectManage` grant, so an edit endpoint
> > gated on it would have refused Finance the one field Karim assigned to them. Both rulings stand;
> > the permission splits.
>
> **3. HR may read the user list — names and roles, nothing else.** **`UserRead`** is company-wide and
> held by **HR and the Owner**. HR staffs projects and, until now, could not name a single person to
> put on one.
>
> > *The limit is part of the ruling, not a refinement of it:* names and roles only, no editing, and
> > no visibility into pay if it is ever added. This does **not** hand HR the Owner's user
> > administration surface — usernames, departments and active state for every account — which would
> > repeat one screen over the mistake amendment 3 of 2026-08-20 exists to prevent. HR's zero
> > financial visibility is unchanged.
>
> **HR therefore holds three capabilities, not two.** Amendment 3 of the 2026-08-20 block above says
> *"exactly two"*; that count is superseded by this amendment. The **rule** it states —
> "strictly administrative, zero financial visibility" — is unchanged and `UserRead` does not touch
> money, hold anything on a project, or reach a gate.
>
> **🟡 Raised and not answered by this amendment:** Finance has no global reach, so on a
> newly-opened project Finance cannot set the withholding category until somebody assigns Finance to
> that project. Karim said Finance sets it *"during contract creation or approval"*, which reads as
> immediate. Whether opening a project implies staffing, or staffing simply precedes the tax setting,
> is a workflow question nobody has asked. See `decisions.md` D-055 §1.


---

## 10. HR and performance

- HR is the single source for every costed person. Two populations, one source each: **day labour (يومية)** costed from the daily log, **salaried staff** from timesheets. Nobody appears in both.
- **Worker registry**: engineers register workers from site — name, phone, trade/باب, specialty. Deduplicated by phone. Carries engagement history and per-engagement ratings, producing a searchable pool with average day rate, frequency and rating.
- **Payroll** is a treasury event: day labour weekly, salaries monthly 🟡, owner-approved, tagged project cost or company overhead.
- **Performance review** 🟡: eleven weighted KPIs from Kaff's own assessment sheet. The system derives roughly half automatically — report submitted and on time, photos attached, delays logged, material request timing, attendance, quality findings. A supervisor scores the judgement half **weekly, not daily**. Weighted total computes automatically; monthly report per engineer shows score, rank and trend.
- Technical Office tasks carry planned vs actual hours and a 1–5 quality score, feeding department KPIs.

---

## 11. Closure, warranty, furnishing

`Practical completion → snag list → handover → UnderWarranty (4 months) → Closed`

- Snag item: description · location · owner (Kaff or subcontractor) · priority · status (open → fixed → verified).
- **Major snags block handover. Minor snags do not**, and the hold still releases in full.
- Snag resolution is a dual toggle: deduct from the subcontractor (debit note) or absorb as internal cost.
- Warranty starts automatically on the handover date. Callbacks attach to it; cost falls on the subcontractor when at fault, otherwise on Kaff.
- A project closes only when all accounts are settled: extracts issued, advance recovered, hold released, subcontractor accounts closed.
- **Design closure differs**: final documents delivered, last 10% collected, IP transfers. No snag list, no handover, no hold.
- Furnishing is an optional linked mini-execution project.
- Closure produces marketing assets on the client's file, and a referral opportunity can be created manually.

---

## 12. Client portal

**Read and approve only.**

The client sees: section-level progress, the project time plan, his مستخلصات, paid versus remaining, published photos, released deliverables.
The client does: approve priced change orders, approve design stages.
**The client MUST NEVER see:** costs, margins, catalogue, subcontractors, internal notes, or any other client's data.

---

## 13. State machines

**Opportunity:** `Lead → Meeting → SiteVisit → Quotation → Negotiation → ClosedWon` · `Stalled` (auto, revives) · `OnHold` · `ClosedLost(reason)` · `Reopened`

**Project:** `Setup → Active → HandoverPending → Handover → UnderWarranty → Closed` · `Stopped` (logs, no billing) · `Terminated` (settlement)

> ### 📌 AMENDMENT — Karim, 2026-08-20 · `decisions.md` D-044 §7
>
> **متعثرة and تم تأجيلها are health tags, not states.** They do not appear in the machine above and
> must not be mapped onto it. "A struggling project should remain structurally Active in the backend
> so that corrective financial postings (like material purchases or sub-contractor payments) can still
> be executed."
>
> The mapping to avoid is onto `Stopped`: §7 forbids a stopped project from issuing extracts, so
> flagging a project as متعثرة would freeze the very payments meant to unstick it.
>
> 🟡 What the two words mean, and whether they attach to a whole project or a single unit, is still
> open — see `stories/questions-for-karim.md` Q18.

**Extract:** `Draft → QC → TechnicalOffice → Accounts → OwnerApproval → Issued → Paid` · `PartiallyApproved` (held remainder) · `Disputed` · `Void` · any rejection → `Draft` with reason

**Design phase:** `NotStarted → InProgress → DeliverableSubmitted → InternalReview → Approved → Invoiced` · rejection → `InProgress`

**Change order:** Lump Sum priced: `Draft → TechnicalReview → ClientApproval → OwnerApproval → Approved` (raises contract value) · rejection → `Rejected` · `Withdrawn`. Cost Plus and Design: `Draft → Logged` (documentary, unpriced).

---

## 14. Glossary — Arabic term to code identifier

| Arabic | Meaning | Identifier |
|---|---|---|
| مستخلص | progress billing certificate | `Extract` |
| مستخلص سابق / حالي / إجمالي | previous / current / cumulative | `previousValue` / `periodValue` / `cumulativeValue` |
| تشوينات | on-site material advance (75%) | `MaterialAdvance` |
| محجوز / حجز | retention hold (20%) | `Hold` |
| عهدة | petty cash advance to staff | `PettyCashAdvance` |
| جاري المالك | owner current account | `OwnerCurrentAccount` |
| يومية | day labour | `DayLabour` |
| باب | trade section | `Bab` |
| حصر | quantity survey | `QuantitySurvey` |
| معاينة | paid site visit | `SiteVisit` |
| خزنة | cash safe | `Safe` |
| مقاول باطن | subcontractor | `Subcontractor` |
| أمر تغيير | change order | `ChangeOrder` |
| ضمان | warranty | `WarrantyPeriod` |

---

## 15. Acceptance criteria — the worked example

**These numbers are a test, not an illustration.** Any change that breaks them fails the build.

Contract 1,000,000 · advance 25% = 250,000 · hold 20% · تشوينات 75% of 100,000 material = 75,000 · advance recovery 25% of period work value.

| Event | Work | Hold | Advance | تشوينات | Client pays |
|---|---:|---:|---:|---:|---:|
| Advance at signing | — | — | — | — | 250,000 |
| Extract 1 | 300,000 | −60,000 | −75,000 | +75,000 | **240,000** |
| Extract 2 | 300,000 | −60,000 | −75,000 | −45,000 | **120,000** |
| Extract 3 | 400,000 | −80,000 | −100,000 | −30,000 | **190,000** |
| Handover — hold release | — | +200,000 | — | — | **200,000** |
| **Total** | **1,000,000** | **200,000** | **250,000** | **0** | **1,000,000** |

**Invariants that MUST hold:**
- Accumulated hold = exactly 20% of contract value
- Advance ledger reaches exactly zero, never negative
- تشوينات in equals تشوينات recovered
- Total client cash equals contract value exactly
- No posting sequence can produce a negative safe balance

---

## 16. Open assumptions register

| # | Assumption | Owner |
|---|---|---|
| 1 | Advance 25%, hold 20% as defaults, owner-adjustable per project | Karim |
| 2 | Advance recovery at 25% of period work value | accountant |
| 3 | No hold on تشوينات | accountant |
| 4 | عهدة ceiling formula and the 10,000 per-request cap | Karim |
| 5 | Delay penalty line exists but defaults off | Karim |
| 6 | Pipeline stage names | Karim |
| 7 | Store: one warehouse, cost charged on issue | Karim |
| 8 | Monthly overhead spread by project size, as a report | Karim + accountant |
| 9 | Budget baseline = BOQ cost + tolerance | accountant |
| 10 | Payroll: day labour weekly, salaries monthly | Karim |
| 11 | Weekly performance scoring, not daily | Karim |
| 12 | EGP only, currency field present for later | Karim |
| 13 | Accounting layer built now or in a later update | Karim |
| 14 | Revenue at certified extract with WIP line displayed | accountant |
| 15 | Is Kaff VAT-registered | Karim |
| 16 | Any bank loan or financed equipment | Karim |
| 17 | Opening capital and retained earnings figures | Karim's records |
| 18 | Which clients are corporate withholding entities | Karim |
| 19 | Sub retention 5%, released at warranty end | Karim |
| 20 | Cost Plus client statement is summary, not line items | Karim |
