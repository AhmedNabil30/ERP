# Questions the design needs answered

**These are not decisions and they are not a backlog.** Each one is a place where the design needs a
business rule that `spec.md` does not contain. `process/agile.md` puts them in bucket three: *answered
by nobody*, which means the story is `BLOCKED` and does not enter the sprint.

> An agent that invents a business rule to fill a gap is the single most expensive failure mode in
> this project — the invention is always plausible, which is why it survives review.
> — `agents.md`

**Revised 2026-08-21.** Karim answered two rounds. **Ten of the original fifteen are closed**,
including all four that stopped slice 1. Seven new ones are raised, of which one blocks a slice-1
screen and the rest do not.

**Numbering note:** these were `Q1 … Q15`; every other document cites them as `Q-UX-n`
(`stories/KAFF-105`, `KAFF-101b`, `qa/questions.md`). **The file now uses `Q-UX-n` and the numbers are
unchanged** — old `Q3` is `Q-UX-3`. New ones start at 16.

---

## Where things stand

| | Count | Which |
|---|---|---|
| **Closed by a ruling** | 11 | Q-UX-1, 2, 3, 4, 5, 6, 8, 9, 10, 11, **16** |
| **Still open, blocks a slice-1 screen** | **0** | ~~Q-UX-16~~ — **closed 2026-08-22, decisions.md D-055 §2.** No open question blocks a slice-1 screen |
| **Still open, shapes a slice-1 screen but does not stop it** | 4 | Q-UX-17, 18, 19, 20 |
| **Still open, coordination rather than business** | 2 | Q-UX-21, 22 |
| **Still open, later slices** | 5 | Q-UX-7, 12, 13, 14, 15 |

---

# Part 1 — Closed

Each of these is closed **with the ruling that closed it**. They are kept rather than deleted, because
a question that was asked and answered looks identical to one that was never asked, six months later,
to whoever is reading the file next. Two of them were answered in the direction opposite to what this
design had assumed, and those say so.

## Q-UX-1 · How does the first Owner come to exist? — **CLOSED · D-051 (Q31)**

**Shape B: a one-time setup screen**, shown only while the users table is empty, which creates the
Owner and locks permanently.

> *"I do not want hidden database scripts. My name and account creation date must appear naturally in
> the Audit Trail from day one."* — Karim

**The deciding argument was auditability, not convenience** — a seeded account has no actor, so the
first row of the trail would name nobody.

**Designed at** `slice-1-flows.md` S-002. Two follow-ups from the original question were answered
inside the ruling: the emptiness check must be atomic against a concurrent second request (the screen
handles it with a terminal `errors.setup.already_completed` panel), and *"locks permanently"* means
the **emptiness test**, not a flag anyone can clear — so the screen never returns, including after the
last user is deactivated.

**What it left behind:** Q-UX-17, below.

## Q-UX-2 · What are the password rules? — **CLOSED · D-049 (rulings 3, 4)**

**At least 8 characters. No forced complexity. Lockout for 15 minutes after 5 consecutive failures.
Onboarding is a temporary password set by the Owner, which the user must change on first sign-in.**

**The absent complexity rule is a requirement, not a gap.** Karim removed it *"so site workers don't
struggle to log in"* — a rule that makes a site engineer write the password inside his helmet is worse
than a simple one he remembers. **So there is still no strength meter**, and there is no complexity
hint: a meter would put the policy back as a picture.

**The lockout message was the design question underneath this one, and it has an unusual answer:
there is no lockout message.** KAFF-101a rules 13 and 14 return **one** `messageKey` for a wrong
password, an unknown username *and* a locked account, in one time envelope — saying "locked" tells an
attacker the username is real and that their lockout worked. **The absence of the message is the
design.** `slice-1-flows.md` S-001 covers the usability hole that leaves, with a static policy line
that is shown to everybody and derived from nothing.

**Designed at** S-001, S-002, S-003, S-003a, S-007.

## Q-UX-3 · What may HR see of a project? — **CLOSED · D-051 (Q32)**

**The project name and the list of assigned engineers, on a separate screen with zero financial
detail.**

> *"If the main project dashboard contains financial data, HR must be routed to a separate 'Project
> Team' tab/screen that contains zero financial details."*

**The shape of the answer matters as much as the answer: a separate surface, not a filtered view** —
the same pattern `spec.md` §12 uses for the portal, for the same reason. A filtered view leaks the
first time somebody adds a field, and slice 4 will put a contract value on the project screen without
thinking about HR at all.

It implies a **new narrow permission** rather than `ProjectRead`, and D-051 says naming it belongs to
the story, so this folder does not name it.

**Designed at** `slice-1-flows.md` S-009a / S-009b, `navigation.md` § Hr. *Asked three times in three
registers before it reached Karim — recorded because that is the interesting part.*

## Q-UX-4 · What happens when an entered client phone already exists? — **CLOSED · D-049 (ruling 8)**

**Warn, name the client that already holds the number, and let the save proceed.**

> *"A corporate client and its CEO might be registered as two separate entities sharing the same
> contact number."*

**This reverses what this folder said**, and it reverses a database constraint with it —
`ux_clients_phone` was a unique index and is now the non-unique `ix_clients_phone`. `spec.md` §2's
"deduplicated by phone" and §3's "never create a duplicate client" are amended in place. **The interim
behaviour this file recommended — refuse and offer to open the existing client — was the wrong
guess**, and it is worth noting that it was the *conservative* guess: conservative is not the same as
correct.

All three sub-questions are answered:

1. **Archived match** — the warning fires, says the match is archived, and still proceeds
   (KAFF-119 rule 6). No reactivation offer inside the dialog.
2. **Editing a phone into a collision** — the same interaction, not a special case (KAFF-121 rule 4).
3. **A legitimate shared phone** — yes, and that is the whole ruling.

**What was given up, stated plainly** (D-049 records it): nothing now prevents two client records for
one person. The control moved from the database to a human reading a warning, and a human dismissing a
warning is a well-understood failure mode. **So the match matters more than it did, not less** — a
missed match used to mean a wrongly-accepted save; it now means a warning nobody sees.

**Designed at** `slice-1-flows.md` S-013, `components.md` §13.

## Q-UX-5 · Where does the session live, and how long does it last? — **CLOSED · D-049 (ruling 2) + D-050**

**An `HttpOnly; Secure; SameSite=Strict` cookie named `__Host-kaff-auth`. `localStorage` and
`sessionStorage` are prohibited for it. 30 minutes of inactivity, sliding.** Signing out on one device
does not sign out the others; **a password change or a deactivation kills every session everywhere.**

**The consequence for this design is larger than the storage choice.** The page cannot read the
cookie, so `GET /api/auth/me` is the **only** way the UI learns anyone is signed in:

- every screen inherits a **pre-resolution state**, and a component that treats "no session yet" as
  "signed out" flashes the sign-in form on every reload;
- there is **no token to show, store or clear** anywhere in this folder;
- **expiry cannot be predicted**, only discovered — so there is no countdown and no warning banner,
  because either would be a second implementation of the server's sliding clock.

**Designed at** `slice-1-flows.md` S-004 (the three session states) and S-016a (what expiry looks
like, and whether unsaved work survives — it does).

## Q-UX-6 · Does deactivating a user revoke their project assignments? — **CLOSED · D-049 (ruling 5)**

**Yes.** Leavers are never deleted, they stay on historical project teams, and **a returning employee
comes back with zero project assignments.**

The revoked rows stay as history (`ProjectAssignment.Revoke`), which is what lets S-015 still answer
who was on a project last March.

**Designed at** S-008's confirmation text, which can now state the consequence instead of hedging, and
S-009 / S-009b, where a deactivated member is absent **because their assignment was revoked** rather
than because the panel filters on `IsActive` — one mechanism, in one place (KAFF-111, KAFF-115 rule 7).

## Q-UX-8 · Can a user's role be changed after creation? — **CLOSED · D-051 (Q27), which reverses D-049 (ruling 6)**

**Yes, and the change automatically revokes every project assignment they hold — Supervisor and Junior
alike.** HR re-assigns them in the new role if they are still needed.

> ⚠️ **Read this one carefully. It is the opposite of the answer given the day before.** D-049 ruling 6
> said the change is *refused* while the user is an active Supervisor, because auto-removal *"leaves a
> construction site headless"*. D-051 says the link to the site *"must be severed automatically to
> prevent lingering responsibilities."* **The second is the answer.** Both weigh the same two risks and
> land on opposite sides, and `spec.md` §9 deliberately leaves the reversal visible rather than editing
> it away — because a rule that changed direction is exactly the kind a future session will "correct"
> back if it only sees the current state.

**Designed at** S-008, where the confirmation is now the screen's main job: it names the projects the
person is about to come off, counted, from the server.

## Q-UX-9 · Do portal clients sign in on the same screen and host as staff? — **CLOSED · D-051 (Q33)**

**No. A separate URL and host.** *"Their portal must be a completely isolated interface."*

This strengthens D-035, which found the portal one careless endpoint from leaking: a separate host
makes the boundary infrastructural instead of something every future endpoint must remember. **And
D-050 makes it self-enforcing** — the cookie is `__Host-` prefixed with no `Domain`, so it cannot
travel between the two hosts in either direction.

**The previously planned portal "not available yet" state inside the staff application is withdrawn.**
The staff app now carries **no portal route at all**.

**Designed at** `navigation.md` § Client, `screen-inventory.md` boundary 1, `slice-1-flows.md` S-004.

🟡 **Still not asked** (D-051 flags it): whether the portal is a separate *deployment* or the same API
behind a second origin. It changes the cookie and CORS story, not this navigation.

## Q-UX-10 · Is `Client.Code` typed by Marketing or generated? — **CLOSED · D-049 (ruling 7)**

**Generated. Sequential, of the form `C-10001`. Manual entry and later editing are both forbidden**, so
that a code is a stable reference for extracts and ledgers.

**This closes the first half of D-040**, which had flagged `Client.Code` as a required field `spec.md`
never asked for. It was right to flag it and the answer is that it should exist.

**Designed at** S-012 (a line of text, **not a disabled input** — a disabled input is a field somebody
later enables) and S-014 (read-only, `clients.field.code.not_editable`).

## Q-UX-11 · Who sets a client's withholding category? — **CLOSED · D-049 (rulings 9, 10)**

**Neither. The question dissolved: the category is not a property of a client.** It moved to the
contract, and **Finance sets it, not Marketing.**

> *"The same client (e.g. a government body) might sign a design contract (one rate) and an execution
> contract (another rate). Storing it on the client profile breaks this reality."* … the rate
> *"directly dictates ledger entries and money reconciliation. It is a strict accounting parameter,
> not a marketing detail."*

**The old model could not have been right**, and §6.7 contradicted itself: the section sets the rate by
*what is supplied* (1% contracting, 3% services, 5% professional fees) while one sentence in it gave
the flag to the Client. One value per client cannot express 5% on a design and 1% on its execution.

**Designed as a deletion.** No withholding field on any Marketing screen — not hidden, not disabled,
absent. **The tax registration number stays on the client** (it identifies the legal entity and does
not vary by contract), Corporate-only. The rate reappears in slice 4 on the contract, KAFF-416.

---

# Part 2 — Open

## Q-UX-16 · What may HR see of a **user**? — ✅ **CLOSED, 2026-08-22 · decisions.md D-055 §2**

> **✅ ANSWERED — Nabil, 2026-08-22. `decisions.md` D-055 §2, merged as Q42.**
> **`UserRead`** — company-wide, held by **HR and the Owner**, returning **names and roles only**. No
> editing, and no visibility into pay if it is ever added.
>
> **The answer is narrower than the question's worst case, and the narrowness is the ruling.** HR does
> **not** get the Owner's user-administration surface — usernames, departments, active state for every
> account — which would have repeated one screen over the mistake Q-UX-3 was answered to avoid.
> **So the permission is not the whole control: S-010's picker projects name and role, and stops.** A
> picker that renders the full user row satisfies `UserRead` and breaks the ruling.
>
> `EmployeeManage` is still **not** the answer — `User` and `Employee` are different entities and the
> Employee register is slice 2. The question below is kept as written because its reasoning is what
> the answer rests on.

Q-UX-3 answered what HR may see of a *project*. **Nobody has answered what HR may see of a user, and
HR cannot assign somebody it cannot name.**

`Role.Hr` holds `EmployeeManage` and `ProjectAssignmentManage`. It does **not** hold `UserManage` —
deliberately, because folding the two together would let HR grant itself the financial visibility
D-044 ruling 2 denies it. But S-010's user picker needs a list of users, and there is no catalogued way
for HR to obtain one. **It is Q-UX-3's exact shape, one entity across.**

**Ask Nabil / Karim:** *"HR puts people onto projects. To do that HR has to pick the person from a
list. What is HR allowed to see about the people in that list — just their name and job, or more? And
should the list show everybody in Kaff, or only the people who could actually work on a site?"*

**Do not close it by giving HR the Owner's user list (S-006).** That list carries usernames, roles,
departments and active state for every account in Kaff. Handing it to HR repeats precisely the mistake
Q-UX-3 was answered to avoid, one screen over.

**Blocks:** HR's half of S-010, which is the only thing HR exists to do. **Does not block** S-009a or
S-009b, or the Owner's use of S-010.

## Q-UX-17 · Must the first Owner change the password he just chose? — **NEW**

KAFF-100 rule 4: the first Owner *"MUST change it before the account can do anything else"*. That rule
was written for a credential somebody **else** chose. Under Shape B (Q-UX-1) the Owner chooses his own
password on the setup screen, **so nobody else has ever known it** and the non-repudiation reason
behind the forced change — D-049 ruling 4's *"after it, the Owner does not know the credential that
acts as that user"* — is already satisfied.

**Ask Nabil:** *"Karim sets his own password on the setup screen. Should the system still make him
change it immediately afterwards?"*

**It does not block anything**, and that is by design: S-002 takes no position. It signs in and hands
control to S-004, which routes to S-003 if the server says a change is required and to the user list
if it does not. **Both are correct from the UI's side.** The question is recorded so that whoever
implements the endpoint knows it is a choice rather than an oversight.

## Q-UX-18 · Is `password_change_required` a refusal or a field? — **NEW · payload shape, for the BA**

**KAFF-105 contradicts itself.** Rule 3 says `GET /api/auth/me` *"reports whether the signed-in user
must still change a temporary password"* — a field on the response. AC5 says the call is **refused**
with `errors.auth.password_change_required`. Both cannot be the shape, and the dispatcher branches on
one of them.

**This is not a business rule** — it is a payload question for the BA and the Architect, raised here
because S-004 cannot be written against both.

**The design is written against the refusal**, which is the stricter of the two and the one an
acceptance criterion asserts. If the field also exists it is redundant, and redundant state that can
disagree with itself is worth removing rather than reading.

## Q-UX-19 · The reset link: lifetime, reuse, and whether a channel exists at all — **NEW**

D-051 Q38 rules that the Owner generates a temporary reset link sent by **SMS or WhatsApp** to the
registered phone, and flags three unanswered details itself: *"link lifetime, single-use, and what
happens to active sessions on reset."* Three more sit underneath:

1. How long does a link live — and should S-008a tell the Owner before he sends it?
2. Does generating a second link invalidate the first?
3. **Does Kaff have an SMS or WhatsApp gateway at all?** The ruling assumes a channel the system does
   not yet have. **If there is none, S-008a cannot work as drawn** — and the obvious workaround, showing
   the Owner a link to forward himself, is precisely what the ruling was written to prevent: a link the
   Owner can read is a credential the Owner holds.
4. Who monitors delivery failures, and what does the Owner see when a message is rejected?

**Ask Nabil / the Architect** (1, 2, 3, 4) — with 3 first, because it decides whether the others
matter.

**Designed for the ruled shape and not hedged**: the system sends, the Owner never sees the link
(S-008a). Hedging toward a copy-the-link variant would build the thing the ruling avoided.

**Blocks:** demo step 14 only. S-008a and S-003a are drawable and buildable; they are not *provable*
without a channel.

## Q-UX-20 · Does the staff sign-in endpoint refuse a `Role.Client` credential? — **NEW**

Q-UX-9 ruled that clients sign in at a different URL and never see the staff sign-in **screen**.
**Nothing has been ruled about the staff sign-in *endpoint*.** KAFF-101a rule 16 still says a
`Role.Client` credential *"is accepted here"*, which predates the separate-host ruling.

**Ask the Architect:** *"Now that the portal is a separate host — should the staff sign-in endpoint
refuse a client's credentials outright, or accept them and let the client reach nothing?"*

**Why it is not cosmetic:** accepting them mints a valid session cookie on the staff origin for a user
who has no business holding one. It reaches nothing today, because `PortalRead` opens no internal
endpoint — but "reaches nothing" is a property of the current permission catalogue, and refusing at the
door is a property of the door.

**The design does not wait for it.** If `GET /api/auth/me` ever answers `role = Client` on the staff
host, the shell renders S-016 forbidden and mounts no staff chrome — safe under either answer.

## Q-UX-21 · Two documents, two key names for the same three labels — **NEW · coordination, for the BA**

`slice-1-flows.md` has used `auth.field.username`, `auth.field.password`, `auth.action.sign_in` since
it was written. **KAFF-101b's i18n list says `auth.login.username`, `auth.login.password`,
`auth.login.submit`.** One of them has to go, and neither document should pick unilaterally — this is
the same shape as finding F-08, where two documents carried two keys for one refusal and the one in the
code was neither.

**This folder proposes `auth.field.*`**, because `<feature>.field.*` is the convention in
`rtl-and-i18n.md` §6 and the same three labels are reused by S-002, S-003, S-003a and S-016a — they are
not the login screen's. **Not applied.** It needs one owner and one edit in both catalogues.

**A second, smaller conflict in the same story:** KAFF-101b rule 2 puts an 8-character minimum on the
**sign-in** password field. S-001 validates only "not empty", because enforcing a length on sign-in
refuses a correctly-typed password before the server sees it and tells the typist something about the
stored credential. The 8-character rule is a policy about *setting* a password.

## Q-UX-22 · May HR see a project's code? — **NEW · small, and easy to get wrong in either direction**

D-051 says HR sees *"the project name and the list of assigned engineers"*. Taken literally, that is
name only — which is what S-009a renders. **But two projects can share a name**, and HR would then have
no way to tell them apart before assigning somebody to the wrong site.

**Ask Karim, as a yes/no:** *"When HR picks a project to put someone on, is it enough to show the name
— or should the reference code be there too, in case two projects are called the same thing?"*

The same question covers the one other number on S-009a: **the team size**, which tells HR which sites
are unstaffed and is the reason the screen is useful. It is not financial. If either is judged to be
more than Karim's sentence allows, it comes off — but it should come off because he said so.

## Q-UX-7 · Are users deduplicated by phone, and what happens on a collision? — **STILL OPEN**

`User` carries `PhoneEntered` / `PhoneNormalised`. `PhoneNumber`'s doc comment says `spec.md`
deduplicates Client, Worker and — "by the same 'exactly one record' rule" — Employee. **`User` is not in
that list.**

**D-049 ruling 8 did not answer this.** Karim ruled on *client* phones; nobody has ruled on user
phones, and the reasoning does not obviously transfer — the client case is about two legal entities
sharing a switchboard, and two people who sign in are two people.

**Ask Nabil:** *"Can two people who use the system share a phone number?"*

**Until it is answered, S-007 renders whatever the server returns and offers no "proceed anyway"
affordance.** Copying S-013's warning here would be assuming the client answer applies to users, which
is the failure mode this file exists to prevent — arriving, this time, dressed as consistency.

**Blocks:** S-007's duplicate handling only.

## Q-UX-12 · تم تأجيلها or متأجلة? — **STILL OPEN**

`CLAUDE.md` writes **تم تأجيلها**. `agents.md` writes **متأجلة**. Two spellings of a word the rules
require "verbatim" is a defect in the continuity files, tracked as action **A1b** and awaiting Karim's
own word.

The catalogues carry تم تأجيلها, matching `CLAUDE.md`, which is authoritative for conventions.
**Nobody changes it on their own initiative.**

**Ask Karim:** *"Write the five words down exactly as you say them."* And `spec.md` should become their
single home, with `CLAUDE.md` and `agents.md` pointing at it rather than repeating them.

## Q-UX-13 · Which way does a time axis run in an RTL chart? — **STILL OPEN, slice 7**

Reading order says right to left in Arabic. Near-universal chart convention says time runs left to
right, and Kaff's staff have read left-to-right charts in Excel their whole careers.

**Ask Karim by showing him two pictures**, not by asking a question in words.

No chart exists before slice 7. It is recorded so whoever builds the first one knows it is a decision
and not a default. The same question does **not** apply to a progress bar, which follows reading order
and fills from the right — a progress bar is a reading metaphor, not a chart.

## Q-UX-14 · Does "all master data" mean all of it? — **STILL OPEN, slice 2**

`decisions.md` D-045, raised by the Architect. Karim's ruling gives the Owner global reach for "all
master data"; its example line names three (Clients, Suppliers, Banks). The rule line was applied, so
the Owner now holds `CatalogueManage`, `BabManage`, `EmployeeManage`, `SubcontractorManage` and
`OpportunityManage` as well.

**Why UX cares:** it decides whether the Owner's navigation carries a "Master data" section at all from
slice 2, or only Clients and Suppliers. Also unresolved in D-045: what `BankManage` is, given that
`spec.md` has no Bank master record — a bank is an account of `AccountType.Bank` in the §6.3 tree.

## Q-UX-15 · Does money cross the wire as a string or a number? — **STILL OPEN, slice 3**

Money crossing as a JSON number becomes a JavaScript `double`, and `decimal(18,4)` does not survive
that round trip.

**The minimum position was agreed at the kickoff and this design holds to it: the frontend performs no
money arithmetic, ever.** Every total comes from the server. Whether money crosses as a string is a
slice-3 decision for the Architect and Nabil, not Karim.

**Blocks:** the money input's submit shape (`components.md` §2) and every figure rendered from slice 3
onward. Not slice 1, which moves no money.

---

## Already open elsewhere — recorded here, not re-raised

Live in `decisions.md` and the stories. They affect the design and none of them is ours to answer.

| Where | Question | Effect on the design |
|---|---|---|
| `decisions.md` D-049 §2 · KAFF-101a **Q28** | **Is the lockout per account, or per account *and* address?** Per account alone means anyone who knows a site engineer's username can lock him out for fifteen minutes at a time, indefinitely, from anywhere. | Nothing on S-001 changes either way — the screen is told nothing about lockouts (Q-UX-2). It changes how often a real user hits the wall this design cannot explain to them. |
| `decisions.md` D-049 §9–10 🟡 | **Subcontractor and supplier withholding.** §6.7's next paragraph has the same shape as the client rate Karim moved, and those rates are still held on the party record. | Slice 2's supplier and subcontractor forms. Extending the ruling without being asked would be inventing it. |
| `decisions.md` D-051 (Q33) 🟡 | **Is the portal a separate deployment, or the same API behind a second origin?** | Cookie `Domain` and CORS, not navigation. See also Q-UX-9. |
| `stories/KAFF-113` **Q17** · `decisions.md` D-012 Q3 | **Who creates a project at all?** `spec.md` §2 names "Projects", which is not a §9 role, and `ProjectManage` is granted to nobody. | S-051 has no role, and slice 1 assigns against seed-data projects — **a test fixture, not a business rule.** |
| Kickoff §5 Q7 / `decisions.md` D-014 | **What do the five Arabic labels describe?** D-044 ruling 7 settled that متعثرة and تم تأجيلها are health tags, not states. It did not settle what **انتهت** means — site-finished, or file-closed-and-money-collected. | Blocks slice 4. Slice 1 renders no project status anywhere, by agreement. |
| `decisions.md` D-051 (N5) | **Nothing yet compares `User.SecurityStamp` against the token's claim** — the global kill is declared, not implemented, and belongs to KAFF-101a. Note `Reactivate` does not rotate the stamp. | S-016a's "you were signed out on your other device" case depends on it working. |
| `spec.md` §16 | **Every 🟡 assumption.** | Not repeated here. Several land on screens in slices 3–6. |

### Closed since the last revision, and no longer listed above

- **Who may read the audit trail** — the Owner alone, company-wide, and Karim explicitly rejected a
  project-scoped read *"even for their own projects"* (D-049 ruling 1). The row is no longer marked
  `Unresolved`. The awkwardness the kickoff named is now explicit and accepted: the only person who
  reaches every project is the only person who can read the record of what he did there.
- **`/api/me`** — it exists as a story, `GET /api/auth/me` (KAFF-105a), and D-050 made it structural
  rather than convenient. **KAFF-105b**, the project list, is still deferred behind Q-UX-3's new
  permission.
