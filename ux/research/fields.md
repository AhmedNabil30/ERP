# UX research — which business fields actually belong on each screen

**Author:** UX researcher 2 of 3 (read-only pass). **Date:** 2026-09-26. **Demo:** 2026-09-27.
**Angle:** per screen, per field — is it required, invented, or in the wrong place?

Nabil, verbatim: *"the busniess fileds you are puting everywhere is not valid in real project."*

This file is evidence, not a decision. Every field named carries a `file:line`. Every proposed
removal is a **finding with a confidence rank**, never a schema change. Where `spec.md` does not
answer a business question, the question is listed in §7 for Nabil or Karim and nothing is decided
here — `CLAUDE.md`: *"If `spec.md` doesn't answer a business question, stop and ask. Do not decide."*

**Scope read:** `CLAUDE.md`, `STATUS.md` (including its map section), `spec.md`, `ux/screen-inventory.md`,
`ux/components.md` §8, `decisions.md` by section. Screens: `src/Web/src/app/features/{babs, catalogue,
clients, employees, users, suppliers, subcontractors, departments, audit, day-labour}`. APIs:
`src/Api/Features/*/{Request,Response}.cs`.

---

## 1. The finding underneath all the others

Nabil's sentence is accurate, and it is not mainly about the *number* of fields. Three of the four
master forms are already short. What is wrong is **which** fields occupy the positions a working
person reads first, and **when** in the flow they are demanded.

Four repeating shapes, in descending order of how much damage they do at the demo:

1. **The identity slot is spent on a generated code.** Every shipped list row except the user list
   puts a machine-generated code in the first (inline-start, i.e. right-hand) column. Nobody at Kaff
   knows a worker as `E-10007` or a client as `C-10001`. `ux/components.md:259` already rules
   *"Identity first — the name / primary identifier is the first column"*, and six screens break it.
2. **The field a person searches by is missing while a field nobody searches by is present.** The
   worker pool ships `frequency` and `rating` on the row and **never renders the trade/باب**, which is
   the one thing a site engineer selects on ("give me a نجار"). The API already sends it.
3. **Salaried-only fields render on day-labour records.** `job title`, `department` and `hired on` are
   rendered unconditionally on the employee form, so a يومية worker — who in Egypt has no written
   contract and no Form 1 file — is asked for all three.
4. **Financial parameters are demanded at registration.** The subcontractor retention rate is a
   required-looking input at *creation*, and is then repeated as a column on every list row, although
   it has no effect until a subcontractor extract exists (slice 5). This is the same shape as the
   withholding rate that was already moved from project creation to contract signature (S-057).

---

## 2. Field-by-field classification

Legend: **S** = required by `spec.md` (section cited) · **L** = required by Egyptian law or trade
practice (source cited) · **I** = invented, nothing in `spec.md` asks for it · **W** = real but in the
wrong place (belongs on a later screen, or a different role's screen).

### 2.1 Catalogue (S-017 list, S-018 form)

| Field | `file:line` | Class | Evidence |
|---|---|---|---|
| `code` | `src/Api/Features/Catalogue/ListCatalogueItems/Response.cs:32` · `catalogue-list-page.html:105` | **S** | `spec.md` §4.1 names `code` in the item. §4.5: "Add item … searches the catalogue **by code** or description" — the code is a real working handle here, unlike on the people masters |
| `descriptionAr` | `Response.cs:33` · `catalogue-list-page.html:106` | **S** | §4.1 `description` |
| `descriptionEn` | `Response.cs:34` · `catalogue-form-page.html:90` | **S**, by amendment | `spec.md` §4.1 names **one** description. Two columns were ruled by D-144 (Karim, via the import template). Decision-backed, not invented |
| `unit` | `Response.cs:35` · `catalogue-list-page.html:107` | **S** | §4.1 |
| `babId` | `Response.cs:36` · rendered as the group heading, `catalogue-list-page.html:78-83` | **S** | §4.1, §4.5 "auto-creates its باب section" |
| `costPrice` | `Response.cs:37` · row at `catalogue-list-page.html:116-119`, detail at `:220-228` | **S** on the record, **W** on the row | §4.1 names it and §4.2 says it "rides alongside for margin and variance". §4.2 also forbids a single blended figure and requires cost, sell and profit % **separately** — which is the margin panel (S-056), not a list row. On the row it doubles the numeric load of every line for a comparison nobody makes item-by-item |
| `baseSellRate` | `Response.cs:38` · `catalogue-list-page.html:120-123` | **S** | §4.1 |
| `status` | `Response.cs:39` · `catalogue-list-page.html:131-137` | **S** | §4.1 `status` |

No invented field on the catalogue. This screen is the closest to right.

### 2.2 باب tree (S-021, S-022)

| Field | `file:line` | Class | Evidence |
|---|---|---|---|
| `code` | `src/Api/Features/Babs/ListBabs/Response.cs:15` · `bab-tree-page.html:44` | **S**, by amendment | `spec.md` §2 and §4.1 do not name a باب code. D-144 rules the import names a باب **by code**, so the code has to exist and be visible |
| `nameAr` | `Response.cs:16` · `bab-tree-page.html:59` | **S** | §2 "Bab (باب) … ~40 trades, tree" and §14 |
| `nameEn` | `Babs/CreateBab/Request.cs:21` | **I**, low harm | Nothing in `spec.md` asks for an English trade name. It exists because the app carries a locale switch (KAFF-927). Not worth removing; worth not making **required** |
| `defaultMarkup` | `Response.cs` (`DefaultMarkup`) · `bab-tree-page.html:62-71` | **S** | §2 "carries default markup %", §4.2 |
| `parentBabId` | `Babs/CreateBab/Request.cs:22` | **S** | §2 "tree" |
| `sortOrder` | `Babs/CreateBab/Response.cs:14` | **S**, correctly not rendered | On the wire only; the tree order is the render. Correct — an operator never types a sort order |

### 2.3 Client (S-011 list, S-012/S-014 form)

| Field | `file:line` | Class | Evidence |
|---|---|---|---|
| `phone` | `Clients/CreateClient/Request.cs:40` · `client-form-page.html:34-50` | **S** | §2 "deduplicated by phone"; first field is right |
| `name` | `Request.cs:39` · `client-form-page.html:67-76` | **S** | §2 |
| `kind` | `Request.cs:41` · `client-form-page.html:78-100` | **S** | §6.7 — an individual does not withhold, so the distinction is load-bearing |
| `taxRegistrationNumber` | `Request.cs:45` · `client-form-page.html:107-118` | **S** | §6.7 amendment (D-049): "The client still carries the registration number, because that identifies the legal entity". Correctly corporate-only |
| `alternatePhone` | `Request.cs:42` · `client-form-page.html:120-130` | **I** | `spec.md` names no second phone. Its only source is the story's i18n list, `stories/slice-1-foundation/KAFF-119-register-a-client.md:74`, which cites no spec section for it |
| `email` | `Request.cs:43` · `client-form-page.html:132-141` | **I** | Same source, same absence. And `spec.md` §9 D-049 ruling 4 records the opposite premise about this population — *"Site engineers often have no company email, so a reset link cannot be the primary path"*. An Egyptian finishing client is contacted on WhatsApp, not by email |
| `address` | `Request.cs:44` · `client-form-page.html:143-152` | **I / ambiguous** | Not in `spec.md`. It is also **two different facts** in this trade — the client's billing address and the project site address — and the form does not say which. See Q-F-2 |
| `notes` | `Clients/EditClient/Request.cs:59` · `client-form-page.html:155-168` | **S** | §12 "The client MUST NEVER see … internal notes" presupposes internal notes exist. The hint at `:158` says so |
| `code` | `Clients/ListClients/Response.cs:35` · `client-list-page.html:76` | **S** on the record, **W** in column 1 | §2 amendment D-049: codes are generated, "a stable reference for extracts and ledgers". A ledger reference is not the identity a Marketing operator scans |

### 2.4 Employee / worker register (S-023 list, S-024 form)

The API carries one `Employee` for both populations (`EmployeeKind.Salaried` / `DayLabour`), which is
correct per `spec.md` §10 ("exactly one record", "nobody appears in both"). The **form does not
dispatch on kind**, and that is where the invalid fields are.

| Field | `file:line` | Class | Evidence |
|---|---|---|---|
| `fullName` | `Employees/CreateEmployee/Request.cs:31` · `employee-form-page.html:21-30` | **S** | §10 |
| `phone` | `Request.cs:32` · `employee-form-page.html:32-44` | **S** | §10 "Deduplicated by phone" |
| `kind` | `Request.cs:33` · `employee-form-page.html:68-79` | **S** | §10 two populations |
| `babId` | `Request.cs:34` · `employee-form-page.html:82-111` | **S** | §10 "trade/باب". Correctly wrapped in `@if (isDayLabour())` — this is the one field that does dispatch |
| `specialty` | `Request.cs:35` · `employee-form-page.html:113-122` | **S** for day labour, **W** for salaried | §10 puts specialty on the **worker registry** sentence only. Rendered unconditionally at `:113`, so an accountant is asked for a specialty |
| `nationalId` | `Request.cs:36` · `employee-form-page.html:124-134` | **L** for salaried, **W** for day labour | Egypt Labour Law 12/2003: the contract must carry the "employee's name, ID, address … and social insurance number", filed as Form 1 with the Social Insurance Office within three months. That duty attaches to a contracted employee, not to a يومية gang hired for a week |
| `jobTitle` | `Request.cs:38` · `employee-form-page.html:136-145` | **L** for salaried, **W** for day labour | Same source (contract particulars). A يومية worker's "job title" is his باب — the field duplicates `babId` for that population |
| `department` | `Request.cs:37` · `employee-form-page.html:147-156` | **I / broken as built** | Free-text `string?`. Meanwhile `KAFF-321` shipped a `Department` **master entity** and the user list now carries `DepartmentId` (`Users/ListUsers/Response.cs`, `STATUS.md` 2026-09-15 items 2–3). Two incompatible notions of "department" in one system. D-153 §2 refused the member list as **Q85**, so this field was built against silence — exactly the failure `CLAUDE.md` names |
| `hiredOn` | `Request.cs:39` · `employee-form-page.html:158-166` | **L** for salaried, **W** for day labour | Contract/Form 1 particular. Kept by Karim's ruling in D-144. For a day labourer the equivalent fact is the **engagement** `openedOn`, which already exists (`DayLabour/ListEngagements/Response.cs:9`) |

### 2.5 Worker pool and engagement (S-025, S-026, S-027)

| Field | `file:line` | Class | Evidence |
|---|---|---|---|
| `fullName`, `phone` | `DayLabour/ListPool/Response.cs:25-26` · `worker-pool-page.html:18-19` | **S** | §10 |
| `specialty` | `Response.cs:28` · `worker-pool-page.html:20-22` | **S** | §10 |
| **`babId` — sent and never rendered** | `Response.cs:27`; no line in `worker-pool-page.html` renders it | **S, missing from the screen** | §10 makes the pool "searchable" by "trade/باب". The muster-roll tradition is the same: a casual-labour roll carries name, **job role**, hours and wage. The row shows a free-text specialty and omits the structured trade |
| `frequency` | `Response.cs:30` · `worker-pool-page.html:24-27` | **S** | §10 "average day rate, frequency and rating" |
| `averageRating` | `Response.cs:31` · `worker-pool-page.html:28-31` | **S** | §10 |
| `code` | `Response.cs:24` · `worker-pool-page.html:17` | **W** | A site engineer at 390px, one-handed, does not read `E-10007`. `ux/screen-inventory.md` S-026 is **M1** |
| rating `<input type="number">` **in every row** | `worker-pool-page.html:77-96` | **W** | Rating belongs to one engagement, on S-027, which already renders it (`worker-history-page.html:45-48`). An editable numeric control on every row of a mobile list is the densest possible thing to put on a thumb-reachable screen |
| `averageDayRate` | `DayLabour/ListEngagements/Response.cs:27` · `worker-history-page.html:13-15` | **S**, correctly permission-gated | §10. Gated by `DayLabourRateManage` (D-153 §1) and absent from the pool per `Q76`/D-152 — correct as built |
| engagement dates | `Response.cs:9-10` · `worker-history-page.html:36-43` | **S** | Read-only today; `V-39-C` already records that they cannot be **entered**, and a day count multiplies money in slice 6 |

### 2.6 Subcontractor (S-028, S-029)

| Field | `file:line` | Class | Evidence |
|---|---|---|---|
| `phone` | `Subcontractors/CreateSubcontractor/Request.cs:37` · `subcontractor-form-page.html:22-35` | **S** | §2 dedup-by-phone pattern, D-139 §1 |
| `name` | `Request.cs:36` · `subcontractor-form-page.html:48-51` | **S** | §2 |
| `tradeBabId` | `Request.cs:38` · `subcontractor-form-page.html:53-74` | **S** | §2 "Subcontractor — Technical Office — rates and BOQ"; §5.1 one weekly extract each, per trade |
| `retentionRate` | `Request.cs:39` · form `subcontractor-form-page.html:77-91`, **row** `subcontractor-list-page.html:37` | **S** on the record, **W** on the create form and on the row | §5.1: "Kaff retains **5%** from every sub extract … zeroable per subcontractor 🟡" and §16 assumption 19. So the *default* legitimately lives on the master. Asking for it **at registration** is the withholding-at-project-creation shape again: it has no effect until a sub extract exists (S-067, slice 5), and in practice it takes exactly two values, 5 and 0 |
| `taxRegistrationNumber` | `Subcontractors/GetSubcontractor/Response.cs:29` · read-only at `subcontractor-form-page.html:97-105` | **L** | Egypt withholding: Form 41 reports payments to resident suppliers and service providers quarterly to the ETA, keyed on the payee's tax registration. `spec.md` §6.7 amendment explicitly leaves subcontractor/supplier withholding **unruled** 🟡, and D-147 answered only *who may edit it*. Correctly read-only for the Technical Office |

### 2.7 Supplier (S-030)

| Field | `file:line` | Class | Evidence |
|---|---|---|---|
| `phone`, `name` | `Suppliers/CreateSupplier/Request.cs:32-33` · `supplier-form-page.html:21-49` | **S** | §2 "Supplier — Finance — one account, serves many projects" |
| `address` | `Request.cs:34` · form `:51-54`, **row** `supplier-list-page.html:35-37` | **L** on the record, **W** on the row | A withholding return and a supplier invoice both carry the payee's address. On a **list row** it is a long free string that pushes the phone off a 390px line and answers no question a storekeeper asks |
| `taxRegistrationNumber` | `Request.cs:35` · `supplier-form-page.html:57-66` | **L** | Form 41, as above. D-139 §5 gives it to Finance |
| **what Kaff buys from them** | absent everywhere | **missing** | The one thing an accountant or storekeeper needs to find a supplier is the material category (حديد، أسمنت، سيراميك…). `spec.md` says nothing about it, so **it is not proposed here** — see Q-F-4 |

### 2.8 User (S-006, S-007, S-008)

| Field | `file:line` | Class | Evidence |
|---|---|---|---|
| `fullName`, `userName`, `phone`, `role`, `department`, sub-department, temporary password | `user-form-page.html:25,29,42,74,103/113,138,182` | **S** | `ux/screen-inventory.md` S-007 lists exactly these; `spec.md` §9 amendments D-044/D-049 rule role, department and the temporary password |
| `email` | `user-form-page.html:55-66` | **I**, and contradicted | S-007's field list does not contain an email. `spec.md` §9 D-049 ruling 4 is built on the opposite fact: *"Site engineers often have no company email, so a reset link cannot be the primary path."* The field invites an operator to record a channel the recovery flow is forbidden to rely on |
| `client` (portal linkage) | `user-form-page.html:163` | **S** | §12 · `KAFF-119` rule 11: a portal login is a `User` with `Role.Client` carrying `ClientId` |
| `reason` on deactivate/reactivate | `user-form-page.html:360,416` | **S** | `CLAUDE.md`: "Rejections return to origin with a reason", audit carries *why* |

### 2.9 Audit trail (S-015)

| Field | `file:line` | Class | Evidence |
|---|---|---|---|
| when · who · what · changed-field count | `audit-trail-page.html:94,96-103,105-113,122-126` | **S** | `CLAUDE.md`: who, when, what changed (before and after), and why where the flow requires it |
| why (reason) | `:211-214` | **S** | Same. Correctly omitted when null |
| where (request path) | `:216-219` | **I**, low harm | Not in `spec.md`; a technical aid on an Owner-only screen |
| grant path | `:225-228` | **S** | `KAFF-116` — "every audit record says how the actor reached the project" |
| IP address | `:230-233` | **not invented** — decision-backed | D-063 §2 and D-079 rule it, read from `Connection.RemoteIpAddress`. ⚠️ `Q54` (retention of personal data in a table with no delete path) is **still open** |
| correlation id | `:235` | **I**, low harm | Diagnostic, Owner-only |

### 2.10 Department (S-, `KAFF-321`)

| Field | `file:line` | Class | Evidence |
|---|---|---|---|
| `nameAr` | `department-settings-page.html:156` | **S** | §9 names Finance, HR, Marketing, Operations |
| `nameEn` **required** | `department-settings-page.html:166` (`[required]="true"`) | **I** | `CLAUDE.md`: "The UI is Arabic." Nothing asks for an English department name, and making it **required** forces every operator to invent one |

---

## 3. Minimum list-row columns, per screen

A list row exists to let a person **find the one record they want** and see whether it is usable.
Everything else is the detail panel — which `KAFF-930` already shipped (`kaff-detail-panel`,
used at `catalogue-list-page.html:203`). The proposals below reuse it rather than asking for new UI.

Order is inline-start (right, under RTL) first, per `ux/components.md` §8.

| Screen | Proposed row | Moves to the detail panel | Why |
|---|---|---|---|
| **S-017 Catalogue** | description · unit · باب (already the group heading) · **sell rate** · status badge | `costPrice`, `code` demoted to a second line under the description | §4.2 wants cost and sell compared **in the margin panel**, not scanned per row. The code stays visible because §4.5 searches by it — but as secondary text, not as the identity column |
| **S-021 باب tree** | name · default markup · archived badge | code | Already close to minimal. Only the code/name order is wrong |
| **S-011 Clients** | **name** · phone · kind badge · archived badge | code, email, address, alternate phone, notes | Marketing recognises a client by name and finds them by phone. The code is a ledger reference (D-049) |
| **S-023 Employees (salaried)** | **name** · phone · job title · archived badge | code, national ID, department, hired on, specialty | HR scans a staff list by name and role-in-the-company |
| **S-023/S-025 Day labour** | **name** · **باب (trade)** · specialty · phone · frequency · rating | code; the rating **input**; engagement controls behind the row menu | The muster-roll set: who, what trade, how often, how good. The trade is currently the missing one |
| **S-028 Subcontractors** | **name** · trade · phone · archived badge | code, retention rate, tax registration | Retention is a slice-5 billing parameter with two possible values |
| **S-030 Suppliers** | **name** · phone · archived badge | code, address, tax registration | An address on a row is unreadable at 390px |
| **S-006 Users** | name · role · department · username · active badge | phone, email | Already correct — the only screen that puts the name first |
| **S-015 Audit** | when · who · what · fields-changed count | path, IP, correlation id, before/after | Already correct |

**One rule that would fix six screens at once:** the generated code is never the first column. It is
secondary text under the name, or it is in the detail panel. `ux/components.md:259` already says so.

---

## 4. Fields asked too early in the flow

Same shape as the withholding rate already moved from project creation to contract signature (S-057).

| Field asked | Where it is asked now | Where the fact is actually created | Confidence |
|---|---|---|---|
| Subcontractor `retentionRate` | create form, `subcontractor-form-page.html:77-91` | The first subcontractor extract (S-067, slice 5). §5.1 sets the rate at **5%**; per-subcontractor zeroing is 🟡 assumption 19. Default it silently at 5% and expose it where it bites | **High** |
| Employee `jobTitle`, `hiredOn`, `nationalId`, `department` | employee create form, `employee-form-page.html:124-166`, for **both** kinds | Salaried: the employment contract / Form 1 file. Day labour: never — the engagement carries the dates | **High** for day labour, **Medium** for salaried-at-create (they are contract particulars, so they exist at hire; the question is whether HR has the ID card in hand at the moment of creating the record) |
| Employee `specialty` | unconditional, `employee-form-page.html:113` | Day-labour registration only (§10) | **High** |
| Worker rating | every pool row, `worker-pool-page.html:77-96` | The end of an engagement, S-027 | **High** |
| باب `nameEn`, Department `nameEn` | `CreateBab/Request.cs:21`, `department-settings-page.html:166` (required) | Whenever anyone actually needs an English label | **Medium** — harmless as optional, wrong as required |
| Catalogue `costPrice` **required** at create | `catalogue-form-page.html:136-156`, `[required]="true"` | §4.1 names it as part of the item, so it is not too early — but a catalogue loaded from Excel (§4.1, `KAFF-200`) is where the real prices arrive | **Low** — flagged only so it is not confused with the others |

---

## 5. How real construction contracting software presents these objects

Not offered as authority over `spec.md`. Offered as evidence about what practitioners put on a row.

- **Subcontractor / vendor.** Procore's directory keys a company on **name**, and its import template
  is name, email and **cost codes** — the trade classification is the field that makes a vendor
  findable, exactly the field the Kaff worker pool is missing. Insurance, licensing and addresses live
  in the company record, not in the picking list.
  <https://support.procore.com/products/online/user-guide/company-level/directory/tutorials/add-a-company-to-the-company-directory>
  · <https://support.procore.com/products/procore-imports/company-directory/tutorials/prepare-users-and-vendors-for-import-to-the-procore-imports-app>
- **BOQ / بنود الأعمال / أبواب.** Egyptian contracting practice records work items (الحصر) split into
  main and sub items (بنود رئيسية وفرعية), each with quantity and price — a tree with a rate, which is
  what §4.1 plus the باب tree already describe.
  <https://www.babelsoftco.com/preparation-extract/>
- **مستخلص (extract).** Described in the Egyptian market as a payment certificate organising three
  things: percentage complete, net due, and **retentions and guarantees** — i.e. the retention shows up
  on the *extract*, which is where §5.1 puts the hold and where the subcontractor retention becomes
  visible. Not on a master-data row.
  <https://www.babelsoftco.com/preparation-extract/> · <https://www.daftra.com/templates/نموذج-مستخلص-مقاولات>
- **Day labour / يومية.** The muster roll is the trade's own instrument: attendance, wages paid, unpaid
  wages and quantity of work, per worker, with **name, job role, hours, wages** — no record number.
  <https://www.qandle.com/glossary-muster-roll> · <https://civilatwork.blogspot.com/2015/12/muster-rolls.html>
- **عهدة (petty cash).** Egyptian practice: an amount advanced against a named project, cleared with
  invoices and supporting documents, cost centres assigned **at settlement**, not at request. Matches
  §6.4.4's junior-drafts → supervisor-submits → accounts-pays chain, and supports keeping cost
  classification off the request screen (S-080) and on the clearance screen (S-081).
  <https://www.babelsoftco.com/العهدة-فى-شركات-المقاولات/> · <https://www.daftra.com/templates/نموذج-طلب-صرف-عهدة-نقدية>
- **Employee record (Egypt).** Labour Law 12/2003: the written contract, in Arabic and in triplicate,
  carries the employee's name, **ID**, address, education, experience and social insurance number; the
  file (Form 1) goes to the Social Insurance Office. This is the source for `nationalId`, `hiredOn` and
  `jobTitle` on **salaried** records — and the reason they do not belong on a يومية record.
  <https://sadanykhalifa.com/en/single-blog/The-Main-Provisions-of-the-Labor-Law-In-Egypt>
  · <https://asanify.com/global-employer-of-record/egypt/employment-laws/>
- **Withholding (suppliers and subcontractors).** Form 41 is filed quarterly with the ETA and reports
  payments to resident suppliers and service providers — which is what makes the payee's tax
  registration number and address real fields on those masters.
  <https://taxsummaries.pwc.com/egypt/corporate/withholding-taxes>
  · <https://help.sap.com/docs/SAP_S4HANA_CLOUD/0c676015e0414931bdc9ebacd6eb2dff/e6ed92ff7b1340fbb70bb415461e2a12.html>

---

## 6. Findings ranked by confidence

Each is a **finding**, not an instruction. None proposes a migration.

| # | Finding | Evidence | Confidence |
|---|---|---|---|
| F-1 | Salaried-only fields render on day-labour records: `jobTitle`, `department`, `hiredOn`, and `specialty` for salaried | `employee-form-page.html:113-166` all unconditional, while `babId` at `:82` correctly guards on `isDayLabour()` | **High** |
| F-2 | The worker pool never renders the trade/باب although the API sends it | `DayLabour/ListPool/Response.cs:27` vs no render in `worker-pool-page.html` | **High** |
| F-3 | The generated code occupies the identity column on six lists, against `ux/components.md:259` | `catalogue-list-page.html:105`, `client-list-page.html:76`, `employee-list-page.html:48`, `bab-tree-page.html:44`, `subcontractor-list-page.html:33`, `supplier-list-page.html:32`, `worker-pool-page.html:17` | **High** |
| F-4 | Employee `department` is free text while a `Department` master exists, and it was built while `Q85` was open | `Employees/CreateEmployee/Request.cs:37` vs `KAFF-321` / `Users/ListUsers/Response.cs` `DepartmentId`; D-153 §2 | **High** |
| F-5 | Subcontractor retention rate is demanded at registration and repeated on every row | `subcontractor-form-page.html:77-91`, `subcontractor-list-page.html:37` | **High** |
| F-6 | An editable rating input sits in every row of an M1 mobile list | `worker-pool-page.html:77-96` | **High** |
| F-7 | `User.email` is invented and cuts against D-049 ruling 4's own premise | `user-form-page.html:55-66`; S-007's field list | **Medium-high** |
| F-8 | Client `email`, `alternatePhone`, `address` have no `spec.md` source; only a story's i18n list | `client-form-page.html:120-152`; `KAFF-119-register-a-client.md:74` | **Medium-high** (the *fields* may be wanted; the point is nobody asked for them) |
| F-9 | `costPrice` on the catalogue row duplicates what §4.2 assigns to the margin panel | `catalogue-list-page.html:116-119` vs §4.2, S-056 | **Medium** |
| F-10 | Supplier `address` on a list row is unreadable at 390px and answers no lookup | `supplier-list-page.html:35-37` | **Medium** |
| F-11 | Department `nameEn` is **required** while the UI is Arabic | `department-settings-page.html:166` | **Medium** |
| F-12 | The worker pool has no search input, though §10 calls it a "searchable pool" | no search control in `worker-pool-page.html` | **Medium** (a gap, not a field) |
| F-13 | باب `nameEn`, audit `requestPath`, audit `correlationId` are unasked-for but harmless | `CreateBab/Request.cs:21`; `audit-trail-page.html:216,235` | **Low** |

---

## 7. Questions for Nabil or Karim — not decided here

| # | Question | Why it cannot be answered from `spec.md` |
|---|---|---|
| **Q-F-1** | Does a client record need an **email** at all, and does a **user** account need one? | `spec.md` never names either. D-049 ruling 4 states the opposite premise for staff ("no company email"). Removing a field that Kaff's Marketing actually uses would be as wrong as keeping one nobody fills |
| **Q-F-2** | Client `address` — is it the client's billing address, the project site address, or both? | `spec.md` names neither. In this trade they are different facts and only one belongs on a project-independent master (§2) |
| **Q-F-3** | Which of `nationalId`, `jobTitle`, `hiredOn` must HR have **before** a salaried record can be saved, and which may follow? | Law 12/2003 says the **contract** carries them; it does not say the ERP must refuse a record without them. `spec.md` §10 says nothing |
| **Q-F-4** | Does a supplier carry a **material category** (حديد / أسمنت / سيراميك …), and is it the same باب tree or its own list? | `spec.md` §2 gives the supplier one line. Inventing a second classification tree is exactly the kind of guess `CLAUDE.md` forbids |
| **Q-F-5** | Does a **salaried** employee record need a specialty, or is that day-labour only? | §10 mentions specialty only in the worker-registry sentence |
| **Q-F-6** | `Q85` is still open — what are the **department** values for an employee, and is `Employee.Department` the same thing as the `Department` master `KAFF-321` shipped? | D-153 §2 refused to invent the member list; the field shipped anyway as free text |
| **Q-F-7** | Is a subcontractor's **retention rate** ever anything but 5% or 0%? If not, should the master carry a toggle rather than a percentage input? | §5.1 says 5%, "zeroable per subcontractor 🟡"; §16 assumption 19 is unconfirmed |
| **Q-F-8** | `spec.md` §6.7 states withholding as **1% contracting and supplies · 3% services · 5% professional fees**. Public 2025-26 guidance states **0.5% supplies and contracting · 2% services · 5% commissions and brokerage** (<https://taxsummaries.pwc.com/egypt/corporate/withholding-taxes>). Which figures does Kaff file on? | `spec.md` wins by `CLAUDE.md`'s rule, so nothing was changed. But these numbers move real cash on every collection and every supplier payment, and the divergence should be confirmed rather than inherited |
| **Q-F-9** | `Q54` — how long may an audit `ipAddress` be retained? | Open since 2026-08-25 across D-063, D-079. Personal data in an append-only table with no delete path |

---

## 8. What this pass did **not** do

- No source file was edited. This is the only file written.
- No schema change, migration or deletion is proposed as fact. §6 is findings; §7 is questions.
- The five treasury and project screens (S-031…S-058) were not examined — they are not built as
  Angular screens in `src/Web/src/app/features/` today.
- `KAFF-215` (the sidebar) and the portal screens were out of scope.
- Field **validation** rules, i18n key coverage and RTL correctness were not audited — that is
  researcher 1's and 3's ground, and `ux/rtl-and-i18n.md` already rules part of it.
- No claim is made about whether a proposed column set passes the demo script; nothing was run.
