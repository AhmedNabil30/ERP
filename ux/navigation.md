# Navigation — one section per role

**Nine roles.** `spec.md` §9 names eight; `Role.Hr` is the ninth, added by Karim's ruling of
2026-08-20 (`decisions.md` D-044 §2). Each role sees a different application. This file says what
each one sees, what it must never see, and where it lands after login.

> **Revised 2026-08-21 against D-049, D-050 and D-051.** Three things changed here: **HR's section has
> a real answer** and is no longer a role with an unusable navigation (D-051 Q32); **the client portal
> is a separate host**, not a shell on this one (D-051 Q33); and **the shell now has a session state it
> did not have** — `resolving` — because the token is in a cookie the page cannot read (D-050).

---

## What hiding is and is not

> **Never enforce permissions in the frontend alone. UI hiding is convenience; the server decides.**
> — `CLAUDE.md`

Everything in this file is presentation. Concretely:

- A user who types a URL for a screen their role does not have **must reach the route, call the API,
  and be refused by the server**. The refusal renders as S-016 (`errors.auth.forbidden` /
  `errors.auth.not_assigned_to_project`). It must not render as a crash, a blank page, or a redirect
  that hides what happened.
- A route guard exists to avoid a pointless round trip and a confusing empty screen. It is not a
  security control, and no comment in the code may describe it as one.
- **`spec.md` §9: permission = role × assignment.** A user with the right role and no assignment to
  the project is refused. Two roles hold global reach and need no assignment row — Owner
  (`AssignmentLevel.Supervisor`) and HR (`AssignmentLevel.Standard`) — and that is a *reach* rule in
  `ProjectAccessPolicy`, not a permission (D-044 §3).

## Navigation is built from the permission set, not from `switch (role)`

Do not write `switch (role)` to build a menu. Two things make role alone insufficient:

1. **Department is a second, independent axis.** `SiteExpenseConfirm` and `PhotoPublish` are granted
   to `Department.Operations` + `OperationsSubDepartment.Administrative` **with no role named** — so
   a Site Engineer sitting in Operations/Administrative holds them and another Site Engineer does not.
2. **Seniority is per assignment, not per person.** `DraftSubmit` requires
   `AssignmentLevel.Supervisor`, and D-044 §5 confirms an engineer can be a Supervisor on one project
   and a Junior on another. So "can I submit?" is a question about *this project*, not about me.

The frontend therefore needs **`GET /api/auth/me`** (KAFF-105a), returning role, department,
sub-department, assignments **with their level**, and the **evaluated permission set**. Without it the
frontend re-implements `PermissionCatalogue` in TypeScript and the two copies drift — precisely the
failure `decisions.md` D-012 designed the catalogue as *data* to prevent.

**After D-050 this endpoint is structural, not convenient.** The access token lives in an `HttpOnly`
cookie, so it is the **only** way the application learns that anyone is signed in at all. There is no
token to read, no session object in `localStorage`, and no way to answer "am I signed in?" without
asking the server.

**KAFF-105 was split** (D-051): `105a` returns identity and roles and is unblocked; `105b` returns the
project list and is deferred behind Q32's new narrow HR permission. **Slice 1's navigation is built on
105a alone.**

```ts
// The shape the navigation reads. One source, no local copy of the catalogue.
// There is no token field, and there is no field it could be put in (D-050, KAFF-105 rule 2).
interface Me {
  readonly userId: string;
  readonly displayName: string;
  readonly role: Role;                  // one of nine
  readonly department: Department | null;
  readonly operationsSubDepartment: OperationsSubDepartment | null;
  readonly permissions: readonly Permission[];        // already evaluated, company-wide ones
  readonly assignments: readonly { projectId: string; level: AssignmentLevel }[];
}
```

**A user still holding a temporary password does not get this payload at all** — the call is refused
with `errors.auth.password_change_required` and the shell routes to the forced change screen (S-003).
*(KAFF-105 rule 3 describes it instead as a field on the response. The two readings conflict;
`questions.md` Q-UX-18 raises it, and `slice-1-flows.md` S-004 is written against the refusal.)*

Project-scoped permissions are answered per project: hold the company-wide set globally, and ask the
API for the project's own capability set when a project is opened. **Never infer a project-scoped
capability from role plus an assignment row in the client** — that is re-implementing the catalogue.

## Shell shapes

There are exactly **three** shells. Do not invent a fourth.

| Shell | Who | Shape |
|---|---|---|
| **Staff shell** | Owner, Finance, TechnicalOffice, SiteEngineer, HeadOfDesign, MarketingSales, Hr | Header with app name + locale switch + account menu; side navigation on desktop (inline-start = **right**), collapsing to a drawer that slides in **from the right** at 390px. |
| **Site shell** | SiteEngineer on mobile (slice 6+) | Bottom navigation, ≤5 destinations, thumb-reachable. Mobile-first, 390px, one hand, gloves-on tap targets. |
| **Portal shell** | Client | Its own minimal shell **on its own host** (D-051 Q33). Shares the design tokens and **nothing else** — no internal navigation component, no shared DTO, no shared route tree, and now not even an origin. |

## The shell has three session states, not two — D-050

The token is in an `HttpOnly` cookie the page cannot read, so on every load the application begins by
**not knowing** whether anyone is signed in. `GET /api/auth/me` is the only thing that can tell it.

| State | What renders |
|---|---|
| `resolving` | A neutral boot surface — app name, locale switch, progress indicator. **Not the sign-in form, not the staff chrome, not an empty shell.** |
| `signed-in` | The role's landing, per the table at the end of this file. |
| `signed-out` | S-001. |

- **Route guards await resolution.** A guard that resolves against `null` sends a signed-in user to
  the sign-in screen and loses the URL they typed.
- **Nothing is stored and nothing is cleared.** Sign-out is the server clearing the cookie
  (KAFF-102); the shell drops its in-memory profile and returns to `resolving`.
- Full detail, including what expiry looks like: `slice-1-flows.md` S-004 and S-016a.

---

# Owner

**Karim.** Global reach on every project without an assignment row (`decisions.md` D-010, D-044 §3),
at `AssignmentLevel.Supervisor`. "Like the admin."

**Landing:** slice 1 → **S-006 User list**. From slice 3 → **S-037 Owner dashboard**.

### Sees

| Nav item | Key | Screens | From |
|---|---|---|---|
| Dashboard | `nav.dashboard` | S-037 | slice 3 |
| Approvals | `nav.approvals` | S-063, S-071, S-082 | slice 5 |
| Projects | `nav.projects` | S-050, S-052 | slice 4 |
| Treasury | `nav.treasury` | S-031, S-036, S-038, S-040 | slice 3 |
| Clients | `nav.clients` | S-011, S-014 | **slice 1** |
| Master data | `nav.masters` | S-017, S-021, S-023, S-028, S-030 | slice 2 |
| Users | `nav.users` | S-006, S-007, S-008 | **slice 1** |
| Assignments | `nav.assignments` | S-009, S-010 | **slice 1** |
| Audit trail | `nav.audit` | S-015 | **slice 1** — and the Owner is the **only** reader, settled (D-049 ruling 1) |
| Accounting | `nav.accounting` | S-085 … S-094 | slice 7 |

**The audit trail is the Owner's alone, and that is now a ruling rather than an assumption**
(D-049 ruling 1). `AuditRead` is no longer marked `Unresolved`. Karim explicitly rejected a
project-scoped audit read for the people working on that project — *"completely hidden from all other
roles, even for their own projects"* — because the trail carries financial movements. **No other role
gains an audit item, ever, and the "Global Finance/Audit role" the ruling mentions was deliberately
not created.**

The Owner reaches **all master data** — D-044 §4, "without departmental restrictions". `spec.md` §2's
ownership column still says which department owns a record day to day; the Owner sits beside it.
🟡 One reading is recorded rather than resolved: the ruling's rule line says "all master data" and its
example line names three. See D-045 and `questions.md` Q-UX-14.

### Must never see

- **A prepare or disburse form.** The Owner holds `FinancialMovementApprove`, `ChangeOrderApprove` and
  `FirmAdvanceApprove` — and **not** `FinancialMovementPrepare`, `FinancialMovementDisburse`,
  `TreasuryPostProject` or `TreasuryPostCompany`. "Nobody creates and approves the same movement"
  (`spec.md` §9) is structural, and the Owner's navigation must not offer him the creation half.
- A quantity gate. `QuantityGateApprove` is the Technical Office's and the Owner does not hold it —
  the Technical Office gates quantities, never money; the Owner approves money, never quantities.
- The portal. `PortalRead` is `Role.Client` only.

### One trap, from the slice-1 kickoff

The project team panel (S-009) is built from **`ProjectAssignment` rows, never from the access check**
— otherwise Karim appears on the team of every project in the system, because his reach is global and
leaves no assignment row. (Kickoff §4, BA → UX.)

---

# Finance

**Prepares and disburses. Does not approve.** `spec.md` §9 is explicit that Finance does not approve
change orders, and `FinancialMovementApprove` is the Owner's alone.

**Landing:** slice 1 → **S-005 My profile** (nothing Finance-specific exists yet — say so on the
screen rather than inventing a placeholder dashboard). From slice 3 → **S-036 Safe and bank balances**.

### Sees

| Nav item | Key | Screens | From |
|---|---|---|---|
| Treasury | `nav.treasury` | S-031 … S-042 | slice 3 |
| Projects | `nav.projects` | S-050, S-052, S-040 | slice 4 |
| Billing | `nav.billing` | S-059, S-061, S-063 (Accounts step), S-067, S-072 | slice 5 |
| Site expenses | `nav.site_expenses` | S-084 | slice 6 |
| عهدة | `nav.petty_cash` | S-082 | slice 6 |
| Suppliers | `nav.suppliers` | S-030 | slice 2 |
| Accounting | `nav.accounting` | S-085 … S-094 | slice 7 |

### Must never see

- **Any approve control.** No `FinancialMovementApprove`, no `ChangeOrderApprove`, no
  `FirmAdvanceApprove`. Finance's step in the extract chain ends by sending it to the Owner.
- **`ChangeOrderApprove` specifically.** `spec.md` §9 calls it out by name. A change-order screen shown
  to Finance is read-only.
- The user list, the audit trail, the catalogue, أبواب, subcontractors, clients, opportunities.
- Any edit or delete control on a posting. **There is no such endpoint and there must be no such
  button.** The only correction path is S-035, a reversing posting.

---

# TechnicalOffice

**Gates quantities, never money.**

**Landing:** slice 1 → **S-005 My profile**. From slice 2 → **S-017 Catalogue**. From slice 5 →
**S-063 Extract approval queue**, filtered to the quantity gate.

### Sees

| Nav item | Key | Screens | From |
|---|---|---|---|
| Catalogue | `nav.catalogue` | S-017, S-018, S-019, S-020 | slice 2 |
| أبواب | `nav.babs` | S-021, S-022 | slice 2 |
| Subcontractors | `nav.subcontractors` | S-028, S-029 | slice 2 |
| Projects | `nav.projects` | S-050, S-052 | slice 4 |
| BOQ | `nav.boq` | S-053, S-054, S-055, S-058 | slice 4 |
| Quantity gate | `nav.quantity_gate` | S-063 (its own step), S-064, S-065 | slice 5 |
| Snags | `nav.snags` | S-095, S-096 | slice 8 |

### Must never see

- Any money control. `QuantityGateApprove` is the only gate the Technical Office holds; it holds no
  treasury permission, no prepare, no disburse, no approve.
- The margin panel is a judgement call the catalogue does not settle: `spec.md` §4.2 requires cost,
  sell and profit % shown separately, and the Technical Office sets `costPrice` in the catalogue, so
  it sees cost by definition. **The margin panel (S-056) is shown to the Technical Office; the
  five-ledger view, the safe and the owner current account are not.**
- The user list, the audit trail, employees, clients, users.

### Note on the split gate

The Technical Office gate is **splittable**: approve a percentage for payment and hold the remainder
until an NCR closes (`spec.md` §7). That is S-064 and it belongs to this role alone.

---

# SiteEngineer

**Approves nothing financial.** Level is on the **assignment**, not on the person — an engineer may be
a Supervisor on one project and a Junior on another (D-044 §5). Navigation therefore differs *per
project*, not per user.

**Landing:** slice 1 → **S-005 My profile**. From slice 6 → **S-073 Daily log** for today, on the
site shell.

### Sees

| Nav item | Key | Screens | From | Level |
|---|---|---|---|---|
| Today's log | `nav.daily_log` | S-073 … S-077, S-079 | slice 6 | Junior+ |
| My projects | `nav.projects` | S-050, S-052 — **only where assigned** | slice 4 | Junior+ |
| Quantities | `nav.quantities` | S-060 | slice 5 | Junior+ |
| عهدة | `nav.petty_cash` | S-080, S-081 | slice 6 | Junior+ |
| Expenses | `nav.site_expenses` | S-083 — **drafts only** | slice 6 | Junior+ |
| Workers | `nav.workers` | S-026, S-027 | slice 2 | Junior+ |
| Submit queue | `nav.submit_queue` | drafts raised by juniors on this project | slice 6 | **Supervisor only** |

`DraftCreate`, `DailyLogWrite`, `ExtractPrepare` and `SiteExpenseDraft` require `Junior` or above;
`DraftSubmit` requires `Supervisor`. On a project where the user is Junior, the submit affordance is
absent — **and the server refuses it anyway** with `errors.auth.assignment_level_too_low`.

### Must never see

- Any approval, any disbursement, any posting, any balance, any margin, any cost price.
- A project he is not assigned to. This is the assignment half of `spec.md` §9 and it is the most
  frequently exercised refusal in the system.
- **The word "expense" without the word "draft".** `spec.md` §8: an engineer's expense entry is a
  draft that Accounts confirms and posts. If the screen implies the money moved, the screen is wrong.
- Anything that moves money while offline. Offline creates drafts; approval and disbursement happen
  online against a live balance.

### Department overlay

An engineer whose department is Operations/Administrative additionally holds `SiteExpenseConfirm` and
`PhotoPublish` through a department-only grant. That is a real, catalogued grant — build the nav from
the permission set and it appears correctly; build it from the role and it will not.

---

# HeadOfDesign

**Phase 2.** `spec.md` §9 marks the role phase 2, and it holds exactly one grant today: `ProjectRead`.

**Landing:** slice 1 → **S-005 My profile**. From slice 4 → **S-050 Project list**.

### Sees

| Nav item | Key | Screens | From |
|---|---|---|---|
| Projects | `nav.projects` | S-050, S-052 — where assigned | slice 4 |
| Design stages | `nav.design_stages` | S-070 | slice 5 |

### Must never see

Everything else. Until the Design slice is specified, **do not add navigation for this role on the
assumption that it will need it.** A design stage bills when the client approves the deliverable in
the portal (`spec.md` §5.3), so the billing trigger is not on this role's screens at all.

---

# MarketingSales

Owns Client and Opportunity (`spec.md` §2). The only non-Owner role with a real screen in slice 1.

**Landing:** **S-011 Client list**, from slice 1 onward.

### Sees

| Nav item | Key | Screens | From |
|---|---|---|---|
| Clients | `nav.clients` | S-011, S-012, S-013, S-014 | **slice 1** |
| Pipeline | `nav.pipeline` | S-043, S-044, S-045 | slice 4 |
| معاينة | `nav.site_visits` | S-046 | slice 4 |
| Quotations | `nav.quotations` | S-047, S-048, S-049 | slice 4 |
| Projects | `nav.projects` | S-050, S-052 — where assigned | slice 4 |

### Must never see

- The treasury, the safe, the five ledgers, the owner current account.
- **The معاينة fee as revenue.** `spec.md` §3: it is a deposit on the Opportunity that transfers as a
  credit against the new contract at Closed Won. A "revenue" label here is a business defect.
- User management, the audit trail, employees.

### Two rules the screens must carry

- **Closed Lost requires a reason** (`spec.md` §3). Not optional, not defaulted.
- **Reopening an opportunity attaches to the same Client** (`spec.md` §3). But §2's "never create a
  duplicate client" was **amended on 2026-08-21**: a phone already on file now **warns and lets the
  save proceed**, naming the client that holds it (D-049 ruling 8), because *"a corporate client and
  its CEO might be registered as two separate entities sharing the same contact number."* The unique
  index went with it. **S-013 is a warning, not a refusal** — and the phone match is now the whole of
  the control, so it matters more than it did rather than less.

---

# Client

**Portal only. Read and approve.** `Role.Client` was deliberately removed from `ProjectRead` — a
portal user holding the same read permission as internal staff would reach any internal endpoint
requiring only `ProjectRead`: a project header, a summary, a BOQ view. The portal reaches projects
through `PortalRead` and `PortalApprove` and **nothing else** (`decisions.md` D-035).

A client user is scoped to one `ClientId`, and `ProjectAccessPolicy` matches that to
`Project.ClientId`. Assignment does not apply to clients — `errors.identity.client_is_not_assignable`.

## The portal is a separate URL and host — D-051 (Q33)

> Clients sign in at a different URL. *"Their portal must be a completely isolated interface."*
> — D-051, Q33

**This answers `questions.md` Q-UX-9 and it answers it structurally.** D-035 already found the portal
one careless endpoint away from leaking; a separate host makes the boundary infrastructural instead
of something every future endpoint has to remember.

What it means concretely, and each line is a thing somebody would otherwise do by accident:

- **Clients never see the staff sign-in screen.** There is no client-facing link, banner, tab or
  "are you a client?" affordance anywhere on the staff host. The two front doors do not know about
  each other.
- **The staff application contains no portal route** — not a portal landing, not a "portal coming in
  slice 8" placeholder, not a redirect. The old plan for a portal "not available yet" state inside
  this application is withdrawn: it belongs to the portal host, which is the portal's to build.
- **It is a second cookie and a second session.** D-050's cookie is `__Host-` prefixed, path `/`, with
  **no `Domain`** — a browser will not let it travel to another host and a neighbouring host cannot
  set it. So a client signed in on the portal is not signed in on the staff host, in either direction,
  by construction rather than by a check.
- **It is a second origin in `Kaff:AllowedOrigins`.** CORS runs with `AllowCredentials()`, so the
  origin list must stay explicit — a browser rejects a wildcard origin outright when credentials are
  in play.
- **Build it against `/api/portal/*` with unshared response types.** Unchanged by this ruling and
  reinforced by it.

**If a `Role.Client` credential somehow resolves on the staff host**, the shell renders S-016
forbidden and mounts no staff chrome — not one frame, not empty. KAFF-101a rule 16 still accepts a
client credential at the shared sign-in endpoint and nobody has ruled on refusing it there; that is
`questions.md` **Q-UX-20**, open.

🟡 **Also not asked:** whether the portal is a separate deployment or the same API behind a second
origin. D-051 flags it; it changes the cookie and CORS story, not this navigation.

**Landing:** slice 8 → **S-102 Portal project overview**, on the portal host. If the client has more
than one project, a project picker first.

### Sees — the complete list, and nothing may be added to it without Karim

| Nav item | Key | Screens |
|---|---|---|
| Project | `portal.nav.overview` | S-102 — section-level progress, the time plan |
| Payments | `portal.nav.extracts` | S-103 — his مستخلصات, paid versus remaining |
| Approvals | `portal.nav.approvals` | S-104 priced change orders, S-105 design stages |
| Photos | `portal.nav.photos` | S-106 — published photos only |
| Documents | `portal.nav.documents` | S-107 — released deliverables, watermarked until paid |

### Must never see — `spec.md` §12, verbatim

> costs · margins · catalogue · subcontractors · internal notes · **or any other client's data**

Expanded into things a developer might actually render by accident:

- `costPrice`, line markup, supervision %, the margin panel, any variance against cost.
- Anything below section level on progress. §12 says **section-level** progress.
- A subcontractor's name, a subcontractor extract, a subcontractor's retention.
- The BOQ line detail, catalogue codes, catalogue descriptions.
- Internal notes on the client record, the audit trail, any user name of Kaff staff beyond what a
  document already carries.
- A photo that was not deliberately published. Photos are client-visible **by default** but
  **published deliberately, not mirrored automatically** (`spec.md` §8) — "visible by default" is
  about the flag on the photo, not about publication.
- A URL, an ID or a code belonging to another project of another client. Enumeration is a leak.
- **Any Cost Plus line detail.** §5.2: the client statement is a summary, not line items 🟡.

### Structural rule for whoever builds it

Build the portal against `/api/portal/*` with unshared response types. Do not reuse an internal DTO
with an `if (isClient) omit` branch — that is the exact failure mode `decisions.md` D-035 and action
A9 exist to prevent, and it fails silently the first time somebody adds a field.

---

# Subcontractor

**No login at all.** `spec.md` §9: "Subcontractor (record only, no login)."

**Landing:** none. There is no navigation, no shell, no route, and no screen in this entire inventory
that a subcontractor reaches, because a subcontractor cannot authenticate.

- `PermissionCatalogue` contains **no grant referencing `Role.Subcontractor`**, and the authorization
  handler refuses the role outright even if one were added by mistake.
- `User.SetPasswordHash` refuses a subcontractor: `errors.identity.subcontractor_cannot_log_in`.
- If a login attempt is somehow made against such a record, the login screen renders
  `errors.auth.role_cannot_log_in` — a plain refusal, no retry affordance, no password reset link.

A subcontractor is a **record**, maintained by the Technical Office (S-028, S-029) and paid by Finance
(S-067). Everything about them is somebody else's screen.

**Do not build a subcontractor portal.** Nobody has asked for one and `spec.md` §1 puts supplier
bidding, RFQ and quote comparison out of scope.

---

# Hr — the ninth role

**Strictly administrative. Zero financial visibility.** Karim, 2026-08-20: HR "cannot see project
costs, margins, or the safe" (`decisions.md` D-044 §2).

HR was created as a role rather than left as a department because a department-only grant matches
**any** role carrying that department — a Marketing user moved to HR held `EmployeeManage`. Two
mechanisms now hold the line: HR holds no financial grant, **and** `User.Create` refuses an HR user in
any department but HR, so an HR user cannot be parked in Operations/Administrative and inherit
`SiteExpenseConfirm`.

**Landing:** slice 1 → **S-009a, HR's project list**. From slice 2 → **S-023 Employee list**.

HR is the role this revision changes most. It previously had a navigation that could not be used —
one item pointing at a screen HR had no way to reach. **D-051 Q32 fixed that**, and the shape of the
answer matters as much as the answer.

### Holds exactly three permissions

*Two until 2026-08-22, when `UserRead` was added — `decisions.md` D-055 §2.*

| Permission | Scope | Note |
|---|---|---|
| `EmployeeManage` | CompanyWide | The Employee and Worker registers (`spec.md` §2, §10) |
| `ProjectAssignmentManage` | ProjectScoped, **global reach** | HR does not need to be assigned to a project in order to staff it (D-044 §3). Requiring an assignment to create assignments is circular — on a new project nobody is assigned, so nobody could make the first one. |
| `UserRead` | CompanyWide | **Names and roles only.** Added 2026-08-22, D-055 §2, answering Q42. HR could reach every project and could not name a single person to put on one. **The permission is not the whole control — the endpoint's projection is:** a screen that returns usernames, departments and active state satisfies the permission and breaks the ruling. Not the Owner's user-administration surface. |

**The zero-financial-visibility rule is unchanged.** `UserRead` touches no money, holds nothing on a
project and reaches no gate. The count moved; the rule did not.

### Sees

| Nav item | Key | Screens | From |
|---|---|---|---|
| Projects | `nav.hr_projects` | **S-009a, S-009b** + S-010 | **slice 1** |
| Employees | `nav.employees` | S-023, S-024 | slice 2 |
| Workers | `nav.workers` | S-025, S-027 | slice 2 |

That is the entire HR application.

**`nav.hr_projects` is not `nav.projects`.** It is a different destination with a different key
pointing at a different route tree, and the distinction is load-bearing rather than cosmetic — see
below.

### Must never see — and this is the precise list of what "zero financial visibility" removes

HR does **not** hold `ProjectRead`. That removal is deliberate and it is larger than it looks:

- **No project overview, no project list built on `ProjectRead`, no project header, no BOQ, no
  extract, no progress metric.** HR cannot open a project. HR can only *staff* one.
- No treasury: no safe, no bank balances, no five-ledger view, no owner current account, no posting,
  no account tree.
- No margin panel, no cost price, no catalogue, no supplier, no subcontractor rate.
- No gate and no approval of any kind. HR appears in no approval chain.
- **No payroll amounts.** `spec.md` §10 makes payroll a treasury event, owner-approved. HR owns the
  *people*; HR does not see what they are paid. If a payroll screen (S-115) is later given to HR, that
  is a change to this ruling and needs Karim, not a design decision.
- **No user creation.** `UserManage` is the Owner's alone. HR staffs projects with users that already
  exist; HR does not mint logins or hand out roles. Folding the two together would let HR grant itself
  the financial visibility this ruling denies it (D-044 §1).
- No audit trail.

### How HR reaches a project at all — D-051 (Q32), the answer to Q-UX-3

The problem was real and this navigation could not be built without it: **HR must choose a project in
order to staff it, and holds no permission that returns a project.** Karim answered:

> *"HR may only see the project name and the list of assigned engineers … If the main project
> dashboard contains financial data, HR must be routed to a separate 'Project Team' tab/screen that
> contains zero financial details."* — D-051, Q32

**Note the shape: a separate surface, not a filtered view.** That is the same pattern `spec.md` §12
uses for the client portal and it is chosen for the same reason D-035 records — **a filtered view
leaks the first time somebody adds a field**, and slice 4 will add a contract value to the project
screen without thinking about HR at all.

So HR's Projects item goes to **S-009a / S-009b** (`slice-1-flows.md`), and:

| | |
|---|---|
| **Routes** | `/hr/projects`, `/hr/projects/:id/team` — **not** `/projects/:id/team` with a role check inside |
| **API** | `/api/hr/...`, with **unshared response types**. No `if (isHr) omit` branch on an internal DTO |
| **Payload** | the project name and the team. No status, no dates, no client, no value, no progress, no health tag |
| **Permission** | a **new narrow permission**, not `ProjectRead`. D-051 says naming it belongs to the story, so this file does not name it — the guard reads whatever `GET /api/auth/me` returns |
| **S-009 vs S-009b** | two screens that look alike and are not one component with a flag |

**HR still cannot open a project**, and that is unchanged and deliberate. HR can see that a project
exists, who is on it, and put somebody on or take somebody off. Nothing else.

### The gap that is still open, and it is not the one just closed

**S-010's user picker needs a list of users, and HR holds no `UserManage`.** Q32 answered what HR may
see of a *project*; nobody has answered what HR may see of a *user*, and HR cannot assign somebody it
cannot name. **`questions.md` Q-UX-16**, new and open.

**Do not close it by giving HR the Owner's user list (S-006).** That list carries usernames, roles,
departments and active state for every account in Kaff, and handing it over repeats exactly the
mistake Q32 was answered to avoid.

---

## Landing summary

| Role | Slice 1 landing | Eventual landing |
|---|---|---|
| Owner | S-006 User list | S-037 Owner dashboard |
| Finance | S-005 My profile | S-036 Safe and bank balances |
| TechnicalOffice | S-005 My profile | S-017 Catalogue, then the quantity gate queue |
| SiteEngineer | S-005 My profile | S-073 Today's daily log, site shell |
| HeadOfDesign | S-005 My profile | S-050 Project list |
| MarketingSales | **S-011 Client list** | S-043 Pipeline board |
| Client | **Not on this host at all** — the portal is a separate URL (D-051 Q33) | S-102 Portal project overview, on the portal host |
| Subcontractor | **none — no login** | none, ever |
| Hr | **S-009a HR project list** | S-023 Employee list |

A landing that is `My profile` is an honest statement that the role has nothing to do yet in this
slice. **Do not build a placeholder dashboard with invented tiles to fill it** — an empty dashboard
with plausible-looking widgets is how invented requirements enter a product.

**Every landing above is reached only after `GET /api/auth/me` resolves** (D-050). Before that the
shell shows its boot state, and a user holding a temporary password is routed to S-003 and reaches
none of these until they have changed it (D-049 ruling 4).
