# Slice 1 — high-level scenarios

**Slice 1 gate (`agents.md`): permission tests pass.**

These are business journeys, not unit checks. They are written so Nabil can walk them at acceptance
and so Karim could read one and say "no, that is not how we work". Each names the roles involved, the
narrative, and the stories it exercises. The detailed assertions are in `test-cases.md`; the
scenarios below are what those assertions are *for*.

**Eleven scenarios. Four are blocked**, and they are blocked on business questions nobody at Kaff has
answered — not on work anybody could do. That is stated plainly rather than worked around, because a
scenario built on a guess passes and means nothing.

**Question numbering:** `Q-BA-n` is `stories/questions-for-karim.md`; `Q-UX-n` is `ux/questions.md`.
The two registers use the same numbers for different questions — see `qa/questions.md` F-01.

---

## Coverage map

| # | Scenario | Status | Stories exercised |
|---|---|---|---|
| HLS-1-01 | The first account, and the trail that watches it | **BLOCKED** Q-UX-1, Q-BA-2, Q-BA-5 | KAFF-100, 103 |
| HLS-1-02 | Karim opens a new system and staffs his first project | Ready (steps 1 and 8 blocked) | 100, 105, 106, 107, 113, 116, 118 |
| HLS-1-03 | HR staffs a project it is not allowed to look at | Ready | 105, 107, 113, 115, 116 |
| HLS-1-04 | An engineer is Supervisor on one site and Junior on another | Ready | 105, 113, 115 |
| HLS-1-05 | Somebody leaves Kaff | Partly blocked Q-BA-6 | 110, 111, 112, 114, 118 |
| HLS-1-06 | Marketing opens a client file, and cannot open it twice | **BLOCKED** Q-BA-8, Q-BA-9 | 119, 121, 123, 124 |
| HLS-1-07 | An individual client is refused a tax the law does not ask of them | Ready — **defect fix** | 120, 121 |
| HLS-1-08 | A client signs in and sees nothing that is not his | Ready | 105, 115, 117, 121, 124 |
| HLS-1-09 | The trail answers "by what authority" | Ready | 116, 117, 118 |
| HLS-1-10 | Everybody is refused something | Ready — **this is the gate** | 105–124, permission-matrix.md |
| HLS-1-11 | Arabic, right to left, on a phone on site | Ready | 101, 103, 106, 115, 121, 124 |

---

## HLS-1-01 · The first account, and the trail that watches it — **BLOCKED**

**Roles:** nobody yet. That is the problem.
**Stories:** KAFF-100, KAFF-103.
**Blocked on:** Q-UX-1 (how the first Owner comes to exist), Q-BA-2 (password rules), Q-BA-5 (how the
first credential reaches Karim).

### The narrative

The system is installed. The database is empty. `UserManage` is held by the Owner and by nobody else,
so the application cannot create its own first user. Somebody has to be first, and `spec.md` does not
say who puts them there.

Two shapes exist and neither has been chosen (`ux/slice-1-flows.md` S-002):

- **A seeded Owner.** Nobody can start the system without database access, and the first record in
  the audit trail — the creation of the most privileged account that will ever exist — names no
  human.
- **A first-run screen.** An endpoint that creates an Owner with no authentication, whose entire
  correctness rests on an emptiness check.

Whichever is chosen, the account must arrive with **no password**, and Karim must set his own, so
that nobody else ever holds a credential that can act as him — which matters because the Owner
approves every financial movement in Kaff (§7, §9).

### Why it is not "just pick the seed"

`stories/KAFF-100` rule 3 already describes an idempotent seed, which reads as a decision. It is not
one — see `qa/questions.md` **F-02**. Choosing here would be `process/agile.md`'s bucket-three item
resolved in the room, which is the thing the process exists to stop.

### What can be checked today

`TC-1-001` … `TC-1-006`: whichever shape lands, exactly one Owner exists, a second start creates no
second Owner, the account cannot authenticate before a password is set, the seeded username is a
named person and not `admin`, and an audit record exists for the creation even though its actor is
null.

---

## HLS-1-02 · Karim opens a new system and staffs his first project

**Roles:** Owner, HR, Site Engineer, Marketing.
**Stories:** KAFF-100, 105, 106, 107, 113, 116, 118.
**Status:** Ready. Step 1 depends on HLS-1-01; step 8 needs Q-BA-1 answered.

### The narrative

1. Karim signs in as the Owner. *(blocked — HLS-1-01)*
2. He creates an **HR user**. The department field is fixed to HR and he cannot change it: an HR
   person placed in Operations/Administrative would inherit the ability to confirm site expenses,
   which is exactly the financial visibility his own ruling denies HR (D-044 ruling 2).
3. He creates a **Site Engineer** in Operations/Technical and a **Marketing** user in Marketing.
4. Each new account is created **without a password** and cannot yet sign in. The person sets their
   own.
5. Karim tries to create a user in Operations without choosing a sub-department, and is refused.
6. Karim tries to give a portal Client user a department, and is refused.
7. He asks the HR user to staff the project. HR — who Karim never assigned to anything — puts the
   Site Engineer on the project as **Supervisor**.
8. Karim opens the audit trail and sees four `Created` records naming himself, one `Created` record
   naming HR, and for each one **how the actor reached the project**. *(reading the trail is
   KAFF-117, blocked on Q-BA-1)*

### What it proves

That the system can be brought from empty to a staffed project by exactly the people Karim named, and
that every step left a record naming who did it. `TC-1-047` … `TC-1-066`, `TC-1-099` … `TC-1-112`, `TC-1-129` … `TC-1-135`.

---

## HLS-1-03 · HR staffs a project it is not allowed to look at

**Roles:** HR, Owner, Technical Office.
**Stories:** KAFF-105, 107, 113, 115, 116.
**Status:** Ready — and it is the sharpest scenario in the slice.

### The narrative

Karim's ruling of 2026-08-20 gives HR two things that sound contradictory and are not:

- HR may staff **any project that exists**, without being assigned to it. Requiring an assignment in
  order to create assignments is circular — on a brand-new project nobody is assigned, so nobody
  could ever make the first one.
- HR is **strictly administrative, with zero financial visibility**: no project costs, no margins, no
  safe.

So: HR assigns a Technical Office user to project A. It works. One line later HR opens project A, and
is refused. HR assigns somebody to project B. It works. HR opens project B, refused. HR tries to
approve a movement on project A, refused. HR tries to create a user, refused — creating logins and
handing out roles is Karim's alone, because whoever sets a user's department can hand out
project-assignment power.

Then HR looks at project A's team panel and **HR is not on it**, because HR reached the project
without an assignment row and the panel is built from rows, not from who the access check would let
in. Nor is Karim, for the same reason.

### What it proves

That global **reach** and global **capability** are different things, and that the only thing standing
between HR and every project's financial data is the absence of a grant. `TC-1-040`, `TC-1-041`,
`TC-1-060` … `TC-1-066`, `TC-1-099` … `TC-1-101`, `TC-1-121` … `TC-1-127`.

### The gap this scenario exposes

HR must pick a project from a list, and holds no permission that lets HR see a project's name. There
is no answer — see `qa/questions.md` **F-03** and `ux/questions.md` Q-UX-3. **The scenario is written
so HR is handed a project id, not a picker.** Do not close the gap by granting HR `ProjectRead`.

---

## HLS-1-04 · An engineer is Supervisor on one site and Junior on another

**Roles:** HR, Site Engineer.
**Stories:** KAFF-105, 113, 115.
**Status:** Ready.

### The narrative

Kaff's engineers move between jobs and their seniority moves with the job, not with the person —
Karim, 2026-08-20: *"An engineer can be a Supervisor on one project and a Junior on another."*

HR assigns Ahmed to project A as **Supervisor** and to project B as **Junior**. Ahmed signs in and his
profile lists both projects, each with its own level. On project A he can submit what a junior has
drafted; on project B he can draft but the submission is refused. Project A's team panel shows him as
Supervisor; project B's shows him as Junior. Nowhere does the system show one seniority for the
person.

HR then tries to assign a **Finance** user with the level Supervisor, and is refused — seniority
belongs to the Site Engineer role and to no other. HR tries to assign the Site Engineer at `Standard`,
and that is refused too.

### What it proves

That seniority lives on the assignment, that the same person is genuinely two different things on two
projects, and that the level cannot leak onto a role §9 does not give it to. `TC-1-037`, `TC-1-038`, `TC-1-102` … `TC-1-105`, `TC-1-123`.

---

## HLS-1-05 · Somebody leaves Kaff

**Roles:** Owner, the departing user, HR.
**Stories:** KAFF-110, 111, 112, 114, 118.
**Status:** Partly blocked — Q-BA-6.

### The narrative

A Finance user resigns. Karim deactivates the account **with a reason**, and the account stops working
**on the very next request** — not when the token expires. The same holds for an Owner: Karim has two
Owner accounts, deactivates the second, and the second is refused with a token that was valid a second
earlier. There is no account the rule exempts.

The departing user cannot sign in again, and cannot use a password reset as a way back in. Everything
they ever did stands: twelve audit records still name them, and the user row is still there, because
a deleted user makes every record they wrote unreadable.

**Then the scenario stops.** Do they come off the project team lists, or stay on them so Kaff can see
who was on the job? If they come back six months later, do they land back on the same projects, and
does their old password still work? Both readings are defensible, which is precisely why an agent must
not choose (Q-BA-6, KAFF-111 and KAFF-112).

### What it proves

That access ends when Karim says it ends, that it ends for everybody including Karim, and that ending
it destroys no evidence. `TC-1-081` … `TC-1-098`, `TC-1-113` … `TC-1-117`.

---

## HLS-1-06 · Marketing opens a client file, and cannot open it twice — **BLOCKED**

**Roles:** Marketing, Owner.
**Stories:** KAFF-119, 121, 123, 124.
**Blocked on:** Q-BA-8 / Q-UX-10 (`Client.Code` — typed, generated, or gone) and Q-BA-9 / Q-UX-4
(may two clients share a phone, and what does the screen do on a match).

### The narrative

A referral comes in. Marketing searches the client list by phone first, because the cheapest place to
prevent a duplicate is the moment before one is created. Nothing matches, so they register the client:
name, phone, individual or corporate.

Six months later the same person comes back for a second flat. Marketing types the phone in a
different format — `+20 100 123 4567` instead of `01001234567` — and the system recognises it as the
same client. §3: *"Reopening attaches to the same Client. Never create a duplicate client."* This
holds even if the client was archived: archiving takes a client off the working list without releasing
their phone number, so an archived client still collides.

### Why it is blocked

Two things stop it being buildable, and neither is technical.

- **`Client.Code` is a required, uniquely indexed field that `spec.md` never asked for** (D-040, still
  open, confirmed by D-045). Marketing has a mandatory field on the very first form they touch and
  nobody has decided whether they type it, the system generates it, or it should not exist. Three
  answers, three different screens.
- **What the person sees on a duplicate is undecided,** and underneath the interaction is a business
  question: can a husband and wife, or a company and its manager, legitimately share a number? If yes,
  the dialog needs an override — and an override is the mechanism by which "never create a duplicate
  client" stops being true.

### What can be checked today

The deduplication rule itself, which the spec does settle: `TC-1-151` … `TC-1-159`,
`TC-1-180` … `TC-1-194`.

---

## HLS-1-07 · An individual client is refused a tax the law does not ask of them

**Roles:** Marketing.
**Stories:** KAFF-120, 121.
**Status:** Ready. **This is a defect fix, not a feature** — the spec answers it and the code does not
enforce it (D-040, D-045).

### The narrative

Marketing registers a private client — a person building a flat. The withholding fields are not on the
form, because §6.7 says plainly: *"Individual clients do not withhold."* If a request arrives anyway
with a withholding category on an individual — from a script, from a stale form, from the next
developer — **the server refuses it**, and it refuses it in the entity, not in a validator that guards
one endpoint.

The same refusal holds when somebody tries to arrive by a side door: setting the category on an
existing individual, supplying a tax registration number for one, or taking a corporate client who
already has a category and changing their kind to Individual without clearing it.

### Why it matters more than a validation rule

§6.7's own justification: *"Without this, collections will never match issued extracts and staff will
invent adjustments to close the gap."* An individual marked as withholding produces an extract whose
expected collection is short by 1–5%, permanently. The gap is small, recurring, and gets closed by
somebody inventing an adjustment — which is the behaviour the whole rule exists to prevent.

### What it proves

`TC-1-160` … `TC-1-169`.

---

## HLS-1-08 · A client signs in and sees nothing that is not his

**Roles:** Client (portal), Marketing.
**Stories:** KAFF-105, 115, 117, 121, 124.
**Status:** Ready.

### The narrative

There are two clients, X and Y, each with a project. X's portal user signs in. He reaches his own
project. He does **not** reach Y's — and the check is made against the database, never against
anything his request carried.

Then everything he must never see is attempted, one at a time, and each is refused:

- the client master list, which would show him every client Kaff has;
- another client's record, and his own client record's **internal notes**;
- the project team panel — §12 lists what the client sees and the team is not on the list;
- any internal project endpoint, even for his own project — a portal user holding the same read
  permission as internal staff would reach any endpoint requiring only `ProjectRead` (D-035);
- the audit trail, with and without a project id;
- the user list, and user creation.

And `/api/me` for X names X's project and does not contain Y's project **anywhere in the payload** —
not in a name, not in an id, not in a count.

### What it proves

§12, which is the most absolute sentence in `spec.md`: *"The client MUST NEVER see costs, margins,
catalogue, subcontractors, internal notes, or any other client's data."* Slice 8's gate is *the portal
leaks nothing*; slice 1 is where the boundary is first drawn, and D-035 records that it has already
been drawn wrong once. `TC-1-042`, `TC-1-043`, `TC-1-126`, `TC-1-136`, `TC-1-154`,
`TC-1-171`, `TC-1-189`.

---

## HLS-1-09 · The trail answers "by what authority"

**Roles:** Owner, HR, Technical Office, Client.
**Stories:** KAFF-116, 117, 118.
**Status:** Ready. Reading the trail is blocked (Q-BA-1); writing it correctly is not.

### The narrative

Four people change something on the same project, and each reached that project a different way:

| Who | How they reached it | What the record must say |
|---|---|---|
| A Technical Office user | an assignment row | `Assignment` |
| Karim | global reach, no row anywhere | `OwnerGlobal` |
| HR | global reach, no row anywhere | `HrGlobal` |
| The portal client | his client owns the project | `ClientOfProject` |

Without this field, three of the four are indistinguishable from each other and from nothing. The
Owner in particular is now **the one actor whose authority leaves no row** — the trail records that he
acted and cannot say what let him.

A company-level change — creating a user — carries **no** project and **no** grant path. Not
`OwnerGlobal` by default: null, because there was no project to reach.

Every one of these records is then permanent. An update against an audit record is refused by the
database whether it comes from the API or from a psql prompt.

### Why it must land in slice 1 and not later

`AuditRecord` is append-only and enforced as such by a trigger. **A column added after slice 3 cannot
be backfilled — the rows cannot be updated, by design.** Cheap now; impossible later.

### What it proves

`TC-1-129` … `TC-1-150`.

---

## HLS-1-10 · Everybody is refused something — **this is the gate**

**Roles:** all nine.
**Stories:** every slice-1 story; the expectations live in `permission-matrix.md`.
**Status:** Ready.

### The narrative

`agents.md` sets slice 1's gate at *permission tests pass*, and `CLAUDE.md` says what that means: *one
test per role asserting what it cannot reach, hitting endpoints directly rather than through the UI.*

Nine roles are walked against thirty-one permissions. For each role the interesting list is the one of
refusals:

- **Owner** — cannot gate quantities, cannot prepare or disburse a movement (nobody creates and
  approves the same movement), cannot reach a project that does not exist.
- **Finance** — cannot approve a change order, cannot approve any financial movement, cannot mint a
  user, cannot staff a project, cannot open a project it is not assigned to.
- **Technical Office** — gates quantities and **never money**.
- **Site Engineer** — approves nothing financial; a junior drafts and does not submit; a supervisor on
  project A is nobody on project B.
- **Head of Design** — phase 2, and holds almost nothing. (What it does hold is a finding —
  `permission-matrix.md` F-05.)
- **Marketing** — owns the client file and reaches no treasury.
- **Client** — `PortalRead` and `PortalApprove` on his own project, and nothing else anywhere.
- **Subcontractor** — refused before anything else is considered. *"Record only, no login."*
- **HR** — two permissions, global reach, zero financial visibility.

And across all of them, the two rules that are not about any single role:

- **The right role without an assignment is refused.** Role alone is never sufficient.
- **A deactivated account, or a token claiming a role the user no longer holds, is refused on the next
  request** — for company-wide endpoints as much as for project-scoped ones.

### What it proves

The gate. If this scenario is not green, slice 1 does not ship regardless of what else is.
`permission-matrix.md` in full, plus `TC-1-202` … `TC-1-215`.

---

## HLS-1-11 · Arabic, right to left, on a phone on site

**Roles:** any.
**Stories:** KAFF-101, 103, 106, 115, 121, 124.
**Status:** Ready.

### The narrative

An engineer opens the system on his phone, standing on a site, in Arabic. Every screen slice 1 has —
sign-in, set password, the user form, the team panel, the client form, the client list — renders at
390px with the direction right to left, and the page does not scroll sideways.

Latin things inside Arabic rows behave: a phone number, a username, a client code and a timestamp each
stay in their own reading order instead of being torn apart by the surrounding Arabic. Every visible
word comes from the catalogue: there is no English literal, no Arabic literal, and no raw key showing
through.

Kaff's own status words — لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها — appear **nowhere in
slice 1**, by agreement. No project status chip goes on a screen "because it's useful": what those
five words mean is still open (Q-BA-18) and one of them has two spellings in the continuity files
(Q-BA-19).

### What it proves

`CLAUDE.md`: RTL is the primary direction, not a mirror; the daily log is designed mobile-first; no
hardcoded user-facing strings, from the first commit. `TC-1-195` … `TC-1-201`.

---

## The demo script, as Nabil will run it

Reproduced from `ux/slice-1-flows.md` so acceptance and QA are reading the same twelve steps. The
mapping to scenarios is what makes it checkable.

| # | Step | Scenario | Blocked? |
|---|---|---|---|
| 1 | The first Owner exists; sign in as the Owner | HLS-1-01 | **yes** — Q-UX-1 |
| 2 | The Owner creates an HR user; department fixed to HR | HLS-1-02 | no |
| 3 | The Owner creates a Site Engineer and a Marketing user | HLS-1-02 | no |
| 4 | The Owner opens the audit trail, sees three `Created · User` records naming himself | HLS-1-09 | **yes** — Q-BA-1 |
| 5 | HR signs in; no treasury, no project overview, no user list | HLS-1-03 | partly — Q-UX-3 |
| 6 | HR assigns the Site Engineer to a project as Supervisor, on a project HR was never assigned to | HLS-1-03 | no |
| 7 | The Site Engineer signs in, sees `Supervisor` on that project, is refused on any project he is not on | HLS-1-04 | no |
| 8 | Marketing signs in, lands on the client list, creates a client | HLS-1-06 | **yes** — Q-BA-8 |
| 9 | Marketing enters the same phone again and is stopped, with the existing client offered | HLS-1-06 | **yes** — Q-BA-9 |
| 10 | Marketing creates an individual client and the withholding fields are not present | HLS-1-07 | no |
| 11 | The Site Engineer types the user list URL and is refused with 403 — the route resolves, the API refuses, the refusal is legible | HLS-1-10 | no |
| 12 | Every screen renders at 390px in Arabic with no horizontal overflow, every figure in Latin digits | HLS-1-11 | no |

**Four of twelve steps are blocked on business questions.** The gate is not: steps 5, 6, 7, 10, 11 and
12 are the permission model end to end, and `stories/backlog.md` records why — the Api harness issues
tokens directly, so KAFF-105 through KAFF-124 are testable without a login endpoint. **The gate does
not wait on the auth questions.**
