# Questions for Karim — the one register

**This is the master register.** `ux/questions.md` and `qa/questions.md` merge into it, with their
origin recorded on every row (`meetings/2026-08-20-sprint-1-refinement.md`, action **SM-4**).

Ordered by what they block. **Nabil asks; Karim answers; the answer becomes a D-number in
`decisions.md`; the BA rewrites the rule with the citation.** Nobody on the agent team answers any of
these, and no story is unblocked by a plausible reading.

> *"An agent that invents a business rule to fill a gap is the single most expensive failure mode in
> this project — the invention is always plausible, which is why it survives review."* — `agents.md`

**Why one register, and why it is not tidying.** Two registers used the same numbers for different
questions: BA `Q1` was the audit trail, UX `Q1` was the first Owner. A test case marked `PENDING Q3`
was unexecutable because it did not say whose. Worse, **`Q-UX-3` and `Q-UX-9` never reached this
register at all** — and KAFF-101 was written treating Q-UX-9 as settled, which is exactly how an
unanswered question gets answered silently by whoever writes the story (finding **F-01**, **F-22**).

**Numbers Q1–Q26 keep their meaning.** Test cases, stories and `qa/` cite them, and renumbering would
detach the references. Merged questions take new numbers from **Q27** and carry their origin.

**Second sweep, 2026-08-21 (refinement action SM-8).** SM-4 made this the master register and then
nothing swept it again. **Eight questions raised in `ux/questions.md` and `qa/questions.md` minutes
after the merge — Q-UX-16 … Q-UX-22 and QA-4 — never reached it**, and a search of this file for any
of the eight returned nothing. They are merged below from **Q42** (Q41 was the highest in use; **Q44**
was taken outside this file by D-052 §3). One register that is not swept is two registers with extra
steps, which is the failure SM-4 was meant to end — so the sweep is now an action, not a habit.

---

## Answered — do not re-ask

**Karim answered ten on 2026-08-21 (D-049), five more the same day in a second round (D-051), and
Q17 and Q34 in a third (D-052)**; Nabil with the Architect answered the token question (D-050), three
mechanism questions (D-051) and Q44 (D-052). Eight earlier ones are in D-044. Where a ruling closed only part of a question, the residual appears
in the open list below and says so.

**Swept 2026-08-22 (refinement action SM-31).** **Q42**, **N10** and the residual half of **Q17** are
closed by **D-055** and moved into the table below. Their rows are gone from the open list, and the
prose that named Q42 as the thing blocking sprint 1 is rewritten rather than left standing — a
question answered in one file and left open in three is how a closed finding gets re-reported as
live, which happened four times on 2026-08-21 and again in `decisions.md` on 2026-08-22 (D-056).
**One new question replaces them: Q-N10-2b, raised by D-055 §1 and open for Karim.**

> **⚠️ One of these reversed.** **Q7 / Q27** — a role change was *refused* while the user was an
> active Supervisor (D-049 ruling 6); it now **automatically revokes every assignment** (D-051 Q27).
> `spec.md` §9 carries both, the first marked `⚠️ SUPERSEDED`, deliberately: a rule that changed
> direction is exactly the kind a future session will "correct" back if it only sees the current
> state. **The second ruling is the answer.**

| Was | Question | The answer | Where |
|---|---|---|---|
| **Q27** | Role change vs. `Junior` assignments — the half D-049 left open | **REVERSED and closed. A role change automatically revokes every project assignment — Supervisor *and* Junior — and the mirror case (`Standard` rows on somebody becoming a Site Engineer) goes the same way.** *"Their direct link to the site must be severed automatically to prevent lingering responsibilities. If they are needed on the project in their new capacity, HR must re-assign them."* Nothing is refused any more | **D-051 (Q27)** · §9 ⚠️ SUPERSEDED block |
| **Q31** | Who creates the first Owner | **Shape B — a one-time setup screen, shown only while the users table is empty, that creates the Owner and locks permanently.** Karim's reason is an audit one: *"I do not want hidden database scripts. My name and account creation date must appear naturally in the Audit Trail from day one."* A seed was rejected because its first trail record would name nobody. **Two properties the story must carry**: the emptiness check is atomic against a concurrent second request, and "locks permanently" means the emptiness test itself, not a flag anyone can clear | **D-051 (Q31)** |
| **Q32** | What HR may see of a project | **The project's name and its assigned engineers, and nothing else.** *"If the main project dashboard contains financial data, HR must be routed to a separate 'Project Team' tab/screen that contains zero financial details."* Note the shape — **a separate surface, not a filtered view**, the pattern §12 uses for the portal and for the same reason. Implies a **new narrow permission**, named `ProjectTeamRead` in KAFF-105b; **HR is not granted `ProjectRead`**, which would undo D-044 ruling 2 | **D-051 (Q32)** |
| **Q33** | Does a client sign in at the same address as staff | **No — the client portal is a separate host.** *"Their portal must be a completely isolated interface."* Strengthens D-035: the boundary becomes infrastructural instead of a matter of every future endpoint remembering | **D-051 (Q33)** |
| **Q38** | Password recovery | **An Owner-generated temporary reset link, sent by SMS or WhatsApp to the registered phone.** The Owner must **not** type a new password: *"that would compromise the non-repudiation of the Audit Trail"* — the same reasoning as ruling 4, applied consistently. The user follows the link and sets their own password | **D-051 (Q38)** |
| **N5** | Per-device sign-out vs. the global kill | **(Architect.) No session table.** Per-device sign-out clears the cookie in that browser; the global kill rotates `User.SecurityStamp` and the API rejects any token carrying the old one. **Accepted limit, stated not hidden:** there is no way to revoke *one other* device — a lost phone means signing out everywhere. **BUILT 2026-08-22 (D-053 §1):** the comparison now runs on every authorized request and refuses a mismatch or an absence. KAFF-101a rule 11a is verification, not construction. Also: `Reactivate` does not rotate the stamp and should — **KAFF-112 rule 9a** | **D-051 (N5)** |
| **Q17** | Who opens a new project | **The Owner and the Technical Office.** *"Opening a project triggers engineering items, accounting ledgers, and cost tracking. It is strictly a technical and administrative responsibility. **Site Engineers and Marketing have no business creating projects.**"* The oldest open question in the catalogue (D-012, slice 0) — `ProjectManage` was granted to nobody and no project could be created at all. **FULLY CLOSED 2026-08-22.** The holder was closed by D-052 §2; the scope residual (N10) is closed by **D-055 §3** — opening a project now requires **`ProjectCreate`**, CompanyWide, Owner and Technical Office, and `ProjectManage` stays `ProjectScoped` for editing so §9's assignment requirement keeps applying to every edit. Nothing of Q17 remains open [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectManage`, `Permission.ProjectCreate`, `Permission.ProjectFinancialsEdit`, `Permission.UserRead`] | **D-052 §2** + **D-055 §3** |
| **Q42** | What HR may see of a user, so HR can pick somebody to staff a project | **CLOSED 2026-08-22. A new `UserRead` permission — CompanyWide, granted to `Role.Hr` and `Role.Owner`. Names and roles only**, no editing and no visibility into salary if one is ever added. **Nabil answered it himself**, as he did Q44; legitimate, and noted because it saved a Karim round trip on the one open question blocking committed work. **The register's two traps were respected, not overridden.** The warning this register carried against Q42 is preserved verbatim, because two committed stories quote it and it is the control on the endpoint: ***"Do not close it by handing HR the Owner's user list"*** — that list carries usernames, roles, departments and active state for every account in Kaff, which would repeat one screen over the mistake Q32 was answered to avoid. Concretely: this is *not* the Owner's user list (which carries usernames, departments and active state), and `EmployeeManage` was not the answer. **The permission is not the whole control — the endpoint's projection is.** A `UserRead` endpoint returning the full user row satisfies the permission and breaks the ruling | **D-055 §2** |
| **Q53** | Is a failed sign-in against an unknown username recorded, and may it keep what was typed? | **CLOSED 2026-08-22. Log the event; FORBID the input.** *"Log the attempt as a security event, but strictly FORBID storing the typed input. Users frequently type their password into the username/email field by mistake ... which is a critical security vulnerability."* The record says *"Failed sign-in — Unknown user"* and keeps **only metadata — IP address and timestamp — completely omitting the entered string.** **Karim's reasoning is the rule's justification, not just its outcome:** the audit table is append-only by database trigger, so a plaintext password written into it **can never be deleted** — not by an admin, not by a migration, not by Support. 🟡 **Not free:** `AuditRecord` has no IP field, and `EntityId` is a non-nullable `Guid` while an unknown username has no subject. Both with the Architect — D-062 §3a, §3b | **D-062 §3** |
| **N10** | `ProjectManage` is `ProjectScoped` and cannot authorise a create | **CLOSED 2026-08-22 — the proposal is approved as written, design A.** `ProjectCreate` splits from `ProjectManage`. Company-wide is not a weakening: a create request cannot name the project it is about to create, and the evaluator returns `ProjectNotSpecified` for a `ProjectScoped` row with no project [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `ProjectNotSpecified`]. The rejected alternative — widening `ProjectManage` — fixes creation by removing the assignment requirement from editing. **Architect's decision, not Karim's** | **D-055 §3** · `proposals/N10-project-creation.md` |
| **Q-N10-2** | Does D-052 §2 settle who may *edit* a project, or only who may open one? Two of Karim's own rulings pointed opposite ways | **CLOSED 2026-08-22 — a third permission, `ProjectFinancialsEdit`.** The Finance department will never hold `ProjectManage`; an accountant must not alter the engineering scope of a project. The contract's tax and financial settings move behind `ProjectFinancialsEdit`, `ProjectScoped`, `TouchesMoney`, granted to `Role.Finance` and `Role.Owner` alone. Adding Finance to `ProjectManage` was the one-line fix and was rejected: a grant written to reach one field hands over the whole record. **It raised Q-N10-2b rather than resolving it — see the open list** | **D-055 §1** |
| **Q34** | Who signs off a site expense | **CLOSED, and fixed in code — not merely answered.** `SiteExpenseConfirm` is granted to `Role.Finance`, and to `Role.TechnicalOffice` **conditional on** Operations / Administrative, so the one role §8 excludes by name holds nothing [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`]. The Architect's ruling pins the class, not the row: *"financial permissions must never be granted to a bare department without specifying a role"*, held by a test over **twelve** money-touching permissions — eleven until `ProjectFinancialsEdit` joined on 2026-08-22 (D-055 §1), and the list is written out by name at `tests/Domain.Tests/PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department` rather than read from the flag, so the change had to be a conversation. **F-04 is closed** | **D-052 §1** |
| **Q44** | Must the first Owner change the password he typed himself | **No.** The forced change of D-049 ruling 4 covers an account created *for somebody else* with a credential its creator knows; the Owner types his own at the setup screen, so **nobody else ever knew it** and the non-repudiation the rule protects is not at risk. Recorded as **the scope of an existing rule, not an exception to it**. Story-level, no code — KAFF-100 rule 8 and AC-100-F | **D-052 §3** |
| **Q1** | Who may read the audit trail? | **The Owner, alone.** Company-wide, and hidden from every other role **even on their own projects**. A project-scoped audit read was considered and rejected. `AuditRead` is no longer `Unresolved` | **D-049 ruling 1** · §9 amendment |
| **Q2** | Password rules, and lockout | **Minimum 8 characters, no forced complexity.** Lock for **15 minutes** after **5** consecutive failures. Karim's reason for the absent complexity rule is itself a requirement: site workers must be able to sign in | **D-049 ruling 3** |
| **Q3** | How long a session lasts | **30 minutes of inactivity** | **D-049 ruling 2** |
| **Q4** | Does signing out on one device sign out the others | **No — per device.** But **a password change or a deactivation kills every session, everywhere, immediately** | **D-049 ruling 2** |
| **Q5** | How a new user gets a first password | **The Owner sets a temporary password; the user MUST change it on first sign-in.** Site engineers often have no company email, so a link cannot be the primary path. The forced change is what protects non-repudiation | **D-049 ruling 4** |
| **Q6** | Leavers, and returners | **Deactivated, never deleted; they stay on historical project teams.** A returning employee gets **a new password and zero project assignments** | **D-049 ruling 5** |
| **Q7** | Role change vs. existing assignments | ~~**Partial.** A role change is **refused** while the user is an active **Supervisor**; nothing is auto-removed~~ — **superseded the next day. See Q27 above: every assignment is revoked automatically** | ~~D-049 ruling 6~~ → **D-051 (Q27)** |
| **Q8** | Do clients have a reference number | **Generated, sequential, `C-10001`.** Manual entry and later editing both forbidden. Closes D-040's first half | **D-049 ruling 7** · §2 amendment |
| **Q9** | Two clients, one phone number | **Allowed.** Warn, name the client that already holds it, **and do not block the save**. The unique index is gone | **D-049 ruling 8** · §2 amendment |
| **Q10** | Client or contract for the withholding rate | **The contract.** One value per client cannot express a design contract at one rate and an execution contract at another | **D-049 ruling 9** · §6.7 amendment |
| **Q11** | Who sets it | **Finance**, at contract creation or approval. Marketing cannot — it is *"a strict accounting parameter, not a marketing detail"* | **D-049 ruling 10** |
| **N1** | Where the access token lives in the browser | **An `HttpOnly; Secure; SameSite=Strict` cookie**, `__Host-kaff-auth`. `localStorage` / `sessionStorage` **prohibited**. UI state comes from `GET /api/auth/me`, which returns claims and **no token** | **D-050** |
| **N3** | `spec.md` §9 contradicts the code | **Done.** §2, §6.1, §6.4, §6.7, §9 and §13 now carry marked **📌 AMENDMENT** blocks with the same force as the text above them | **D-047**, **D-049** |
| Q-UX-8 | Can a user's role be changed at all? | **Yes** — by necessary implication of ruling 6, which describes when a change is *refused*. Recorded rather than assumed, so it can be challenged | **D-049 ruling 6** |
| Q-UX-4 §2 | May a client's phone be edited into a collision? | **Yes, with the same warning.** The amendment is written as a property of the record, not of the create path, and the constraint is gone. *Stated explicitly because Karim was asked about registering, and this applies his answer to editing* | **D-049 ruling 8** · §2 amendment |
| — | Eight rulings of 2026-08-20 | `UserManage`; the dedicated `Role.Hr`; HR's global reach; the Owner over master data; seniority per assignment; 4 decimals stored / 2 displayed; متعثرة and تم تأجيلها as health tags; the three hard ledger floors | **D-044** |

---

## Where the merged questions came from

| Origin | Now | Note |
|---|---|---|
| `ux/questions.md` Q-UX-1 | **Q31** | also QA **F-02**. *Answered* — D-051 |
| `ux/questions.md` Q-UX-2 | *answered* | D-049 rulings 3, 4 |
| `ux/questions.md` Q-UX-3 = `qa/questions.md` QA-2 | **Q32** | also QA **F-03**, **F-13**. *Answered* — D-051 |
| `ux/questions.md` Q-UX-4 | *answered* (rulings 8) — **sub-question 1 → Q39** | |
| `ux/questions.md` Q-UX-5 | *answered* | D-049 ruling 2, D-050 |
| `ux/questions.md` Q-UX-6 | *answered* | D-049 ruling 5 |
| `ux/questions.md` Q-UX-7 | **Q36** | |
| `ux/questions.md` Q-UX-8 | *answered* | by implication of ruling 6 |
| `ux/questions.md` Q-UX-9 | **Q33** | also QA **F-22**. *Answered* — D-051 |
| `ux/questions.md` Q-UX-10 | *answered* | D-049 ruling 7 |
| `ux/questions.md` Q-UX-11 | *answered* | D-049 ruling 10 |
| `ux/questions.md` Q-UX-12 | **Q19** | already in this register |
| `ux/questions.md` Q-UX-13 | **Q40** | |
| `ux/questions.md` Q-UX-14 | **Q12**, **Q13** | already in this register |
| `ux/questions.md` Q-UX-15 | **N2** | not Karim's |
| `qa/questions.md` QA-1 | **Q34** | also QA **F-04**. *Answered* — D-052 §1, and **fixed in code** |
| `qa/questions.md` QA-3 | **Q35** | |
| `ux/questions.md` Q-UX-16 (`ux/questions.md:221`) | **Q42** | Merged by **SM-8**, 2026-08-21. ***Answered* — D-055 §2, by Nabil rather than Karim** |
| `ux/questions.md` Q-UX-17 (`:242`) = `qa/questions.md` QA-4 (`qa/questions.md:630`) | *answered* | **D-052 §3.** Both folders raised it and disagreed about whose it was; the Scrum Master routed it to Nabil rather than resolving it |
| `ux/questions.md` Q-UX-18 (`:258`) | **not a register row** | `GET /api/auth/me` payload shape — refusal or field. **BA + Architect**, refinement action **SM-16**. Not a business question and not Karim's |
| `ux/questions.md` Q-UX-19 (`:272`) | **folded into N7 and N8** | Sub-questions 1–3 were already carried: N8 holds the link lifetime, N7 holds *"does a gateway exist at all"*. **Only sub-question 4 was new** — delivery failures — and it is now on **N7**. Deliberately not re-filed as a new number |
| `ux/questions.md` Q-UX-20 (`:295`) | **N9** | Architect, not Karim |
| `ux/questions.md` Q-UX-21 (`:312`) | **not a register row** | `auth.field.*` vs `auth.login.*` for three labels, plus KAFF-101b's 8-character rule on the *sign-in* field. **BA**, refinement action **SM-15** |
| `ux/questions.md` Q-UX-22 (`:329`) | **Q43** | Karim. Merged by **SM-8**, 2026-08-21 |
| `qa/questions.md` QA-4 | *answered* | same question as Q-UX-17 above — **D-052 §3** |

`qa/questions.md`'s findings `F-nn` are **not** questions and do not merge here. They are defects and
document contradictions, owned by the BA, the Architect, Backend or Nabil, and they are tracked in
`qa/questions.md` where they belong.

---

## The open list

**Ordered by what they block.** The preamble here said *"the top of this list blocks nothing"* and
named **Q12** as the first row that stops work. **That was written before the SM-8 sweep and it was
wrong** — eight questions raised on 2026-08-21 had never been merged, and one of them stops a
committed sprint-1 story.

**~~The first row that actually stops work is Q42.~~ Closed 2026-08-22 — D-055 §2.** HR now holds
`UserRead`, CompanyWide, names and roles only [Verified: 2026-08-22 @
`src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserRead`]. **Nothing on this list blocks a committed
sprint-1 story any more.** The sentence above is left struck through rather than deleted because it
was the one row this register existed to surface, and a reader who remembers it needs to see where it
went.

**The first row now is Q43** (the same screen, one field down — it blocks a field, not a story), then
**Q45 … Q51**, which are cheap and slice-1-adjacent: each is a rule somebody has already invented
once, in a story, from the code. **Q47 is the exception in that batch as of 2026-08-23: four of its
five cases are ruled (D-065) and the residual — the locked account — is with Nabil rather than Karim.
Its row stays here because a half-closed question recorded as closed is how a live question gets
lost.** Then **Q12** at slice 2. **Q17 and N10 are both gone from this list —
answered in full (D-052 §2, D-055 §3)**, and so is **Q-N10-2** (D-055 §1). What that last one left
behind is **Q-N10-2b**, which *is* Karim's and is the first new row below.

| # | Question, as Nabil should ask it | Blocks | Origin |
|---|---|---|---|
| **Q-N10-2b** | **"When a new project is opened, Finance has to set the tax category on the contract. But Finance can only see projects somebody has put them on. So does opening a project automatically give Finance access to it — or does HR have to staff Finance onto every project before the tax can be set?"** **New, 2026-08-22, raised by D-055 §1 rather than resolved by it.** It is **Q-N10-1's exact shape, one entity across**: Finance has no global reach — the access policy gives global reach only to `Role.Owner` and `Role.Hr`, and everyone else falls through to the assignment lookup [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `EvaluateAsync`]. Karim said Finance sets the rate *"during contract creation or approval"* (D-049 ruling 11), which reads as immediate. **It is a workflow question, not a permission question** — the permission is correct as split; what is undecided is whether staffing precedes the tax setting, or whether opening a project implies something | **KAFF-416, slice 4.** Blocks nothing before then — no endpoint in the committed fifteen touches it | New, raised by **D-055 §1**, which flags it 🟡 itself and declines to resolve it. Ask it in the same breath as **Q-N10-1** and **Q-N10-3** |
| **Q-N10-1** | **"When somebody in the Technical Office opens a new project, is he automatically on that project's team — or does HR have to put him on it before he can open the file he just created?"** A Technical Office user who opens a project holds no assignment row on it, so one line later he cannot read or edit it: `ProjectRead` and `ProjectManage` are both project-scoped and TO has no global reach. The Owner is unaffected (D-010). §9's *"role alone is insufficient"* leans against opening implying an assignment, **and leaning is not a ruling** | **KAFF-407, slice 4** | `proposals/N10-project-creation.md:382`, carried by **D-055 §8**. Never merged here until the 2026-08-22 sweep (**SM-31**) |
| **Q-N10-3** | **"When the Technical Office opens a project, does that need your approval first, or can they just do it?"** Your phrasing put the Technical Office and yourself side by side, which reads as either acting alone. Opening a project *"triggers … accounting ledgers"*, and §9 puts the Owner on every financial movement. **If it needs approval it is a state machine, not a permission** — it belongs in KAFF-407's story, and choosing a permission now would foreclose the question | **KAFF-407, slice 4** | `proposals/N10-project-creation.md:403`, carried by **D-055 §8**. Never merged here until the 2026-08-22 sweep (**SM-31**) |
| **Q43** | **"When HR picks a project to put someone on, is it enough to show the name — or should the reference code be there too, in case two projects are called the same thing?"** D-051 (Q32) says *"the project name and the list of assigned engineers"*; read literally that is name only, and **two projects can share a name**, so HR would have no way to tell two sites apart before putting somebody on the wrong one. **The same question covers the one other number on that screen — the team size**, which is what tells HR which sites are unstaffed and is the reason the screen is useful. Neither is financial. If either is more than Karim's sentence allows it comes off, but it should come off because he said so | KAFF-115 (deferred) and the picker in KAFF-113's screen. Does not block either endpoint | UX Q-UX-22 (`ux/questions.md:329`), merged by **SM-8** |
| **Q45** | **"Are there usernames nobody may take — `admin`, `root`, `kaff`?"** | Nothing. `KAFF-100` AC-100-G | New, from the 2026-08-21 refinement. **`KAFF-100:106` names that exact blocklist and cites no source.** Rule 3 (`KAFF-100:38`) only says the Owner's account is *"a real person's account, not a shared `admin` login"*, which is an argument about naming a person, not a list of forbidden words |
| **Q46** | **"You are not in one of the four departments — Marketing, Technical Office, Finance, Operations. Is that right, or do you sit somewhere?"** | Nothing. `KAFF-100` rule 2 and AC-100-A | New, 2026-08-21 refinement. **`KAFF-100:37` states the first Owner carries no department and cites *"§9 · D-051 (Q31)"*. D-051 Q31 never mentions a department**, and §9 does not exclude the Owner from having one. Probably right; sourced to a ruling that does not say it |
| **Q47** | **"When somebody cannot get in, should the screen say the same thing every time — no matter which of these five it was?"** **Read him all five; the answer to three of them is not the answer to five.** ① they typed the wrong password · ② they typed a username that does not exist · ③ the account is one we have locked for fifteen minutes · ④ **a client typed his real, correct client password into the staff screen** — the right credential at the wrong door · ⑤ **a subcontractor's username** — a role you record but never give a login to, so there is no password for him to get right or wrong. Say why: telling them which one it was tells anybody which usernames are real; ④ additionally *confirms* to whoever typed it that the password he holds is genuine; and ⑤ needs no password at all, so a different message there tells the world *"that username exists, and it is a subcontractor's"* to anyone who types it **🟢 CASES ①②④⑤ — CLOSED 2026-08-23, `decisions.md` D-065.** Nabil, as Owner and Architect: **one generic `401`**, identical in status, body and `messageKey`, for a wrong password, an unknown username, a `Role.Client` credential at the staff door and a `Role.Subcontractor` username. *"Never tell an attacker the account does not exist."* · *"The door must treat a subcontractor exactly the same way it treats a non-existent user."* **Case ④ confirms D-063 §1** rather than changing it. **Case ⑤ closes D-063 A-02** and therefore `AC-101a-G`, which is certifiable again. **The key is `errors.auth.invalid_credentials`** — the ruling named `errors.identity.invalid_credentials`, which does not exist and crosses a namespace line the catalogue already draws; the Scrum Master's consistency call, recorded in D-065, moved it to `errors.auth.*` and is reversible by Nabil at no cost. — **🟡 CASE ③ — THE LOCKED ACCOUNT — IS OPEN, and it is with Nabil, not Karim.** He ruled a distinct **`423 Locked`**, reasoning that *"a legitimate user needs to know their account has been locked ... so they stop trying and contact administration."* **The ruling is flagged back rather than applied**, because a locked account only exists if the username exists, so a distinct 423 announces *"this username is real"* — the precise thing cases ② and ⑤ were ruled to prevent — and **five failed attempts manufacture it for any username on demand**, making it an enumeration primitive and a denial-of-service one. `KAFF-101a`'s own rule 14 is the counter-argument in the story's own words [Verified: 2026-08-23 @ `KAFF-101a-sign-in-api.md` -> rule 14]. **A narrowing has been put to him: return the 423 only when the submitted credentials were otherwise correct**, so only the password-holder ever sees it and it leaks nothing. **His reason is legitimate and is not dismissed** — the narrowing satisfies both halves. — **⚠️ Half-closed, and the halves must not be collapsed.** This row stays in the open list on case ③ alone. It is **not** a closed question, and it is **not** a blocked story either | **Cases ①②④⑤: nothing — ruled, build them** (D-065). **Case ③ blocks one clause and no more:** the locked account, **struck out of `AC-101a-B` on 2026-08-23 and covered by no criterion in either shape**, and `KAFF-101a` rule 14, which is the same case and is left unchanged and unruled. **`KAFF-101a` is `Ready` with that clause held** — D-065 sequences case ③ last so it is a prerequisite for nothing. **No criterion is written for it in either shape.** `AC-101a-G` is **no longer blocked** | New, 2026-08-21 refinement. Source column read *"§9 — derived"* [Verified: 2026-08-23 @ `KAFF-101a-sign-in-api.md` -> rule 13]. It is a good rule and a security convention, not something Karim said. **Widened 2026-08-23 from three cases to five (D-063 A-03)** — ④ added by **D-063 §1**, ⑤ by **D-063 A-02**, which found `AC-101a-G` demanding a distinct `errors.auth.role_cannot_log_in` for a role that can hold no credential [Verified: 2026-08-23 @ `User.cs` -> `StorePasswordHash`], contradicting `AC-101a-B` in the same story. **Ruled the same day by D-065** — the widening is what produced case ⑤, and case ⑤ is the one the ruling quotes at greatest length. **`errors.auth.role_cannot_log_in` is not deleted on the strength of this**: `SeparationOfDuties` still uses it [Verified: 2026-08-23 @ `SeparationOfDuties.cs` -> `RoleCannotLogIn`] |
| **Q48** | **"When somebody changes their own password, should the system make them type the old one first?"** Why it matters: if not, an unattended signed-in phone is a password reset — but rule 2 also means the session belongs to somebody who has not yet proved they own it | Nothing. `KAFF-103` is buildable either way | New, 2026-08-21 refinement. **`KAFF-103:33`, source reads *"§9 — the same reasoning"*; §9 says nothing about it** |
| **Q49** | **"Can the last engineer be taken off a project, leaving nobody on it?"** | Nothing. `KAFF-114` rule 7 | New, 2026-08-21 refinement. **`KAFF-114:21`, source *"§9 — absence noted deliberately"*.** The absence is real and the reading is the conservative one — inventing a minimum team size would be worse. It is still a rule read out of a silence |
| **Q50** | **"When somebody comes back, does he arrive with no password at all until you set him one, or do you set the new one at the moment you switch him back on?"** | Nothing. `KAFF-112` rules 3 and 4 | New, 2026-08-21 refinement. **`KAFF-112` says the stored credential is *"cleared as part of reactivation"* and a temporary one arrives afterwards. D-049 ruling 5 says only *"a new password"***. ~~And "cleared" has no method: `User.SetPasswordHash` refuses null or whitespace (`User.cs`, at lines 160-163 as it then stood).~~ **⚠️ The mechanism half is CLOSED, 2026-08-22 — that citation was doubly stale.** `SetPasswordHash` no longer exists (split into `SetOwnPassword` and `SetTemporaryPassword`), and **`ClearPassword` does** [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `ClearPassword`]. **What survives is the pure workflow question and only that** — does he arrive with no credential, or does one arrive at the moment of reactivation? The story no longer needs a mechanism invented for it; it needs Karim's sequence |
| **Q51** | **Four refusals the stories take from slice-0 code rather than from a ruling. Ask them as one:** *"Switching off an account that is already off, switching on one that is already on, taking somebody off a project he is already off, and two people picking usernames that differ only in capitals — should each of those be refused with an error, or quietly do nothing?"* | Nothing. All four are already built that way | New, 2026-08-21 refinement. `KAFF-110:25`, `KAFF-112:34`, `KAFF-114:18`, `KAFF-106:31`, each sourced to *"slice 0"*. **They are probably all right, and *probably right* is what this register is for** — a rule read off an implementation is still a rule nobody gave |
| **Q52** | **"Operations / Administrative publishes site photos. If you move a site engineer into that team, should he be able to publish them too?"** | Nothing before slice 6 | New, raised by **D-052 §1**. `Permission.PhotoPublish` [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PhotoPublish`] is **the last bare-department grant in the catalogue** and is deliberately left: the Architect's ruling is scoped to *financial* permissions and a photo moves no money, so extending it there would be applying a rule nobody gave. It is the same mechanism as D-035, D-044 ruling 2 and F-04 — **it needs its own ruling, not an inherited one** |
| **Q54** | **"When somebody fails to sign in, we will be recording the address their computer connected from. That address is personal information about a person, and the audit table is built so that nothing in it can ever be deleted — not by us, not by you. Is keeping those addresses forever what you want, or should they stop being kept after some time?"** If there is a time limit, say what it is; honouring one needs a different storage shape and that shape has to be chosen before the first row is written. | Nothing. KAFF-101a is buildable either way — Karim has already ruled the address is captured (D-062 §3); this is only what happens to it afterwards | New, raised by **D-063 §2**. **Not settled by any agent**: `audit_records` is append-only and no-truncate by database trigger and D-033 refuses to start without them [Verified: 2026-08-23 @ `DatabaseInitializer.cs` -> `FindMissingGuardsAsync`], so a retention period cannot be honoured by deleting rows. The two shapes that do not break the rule are partition-and-detach by age, or storing a keyed hash instead of the address |
| **Q41** | **"If somebody who works for you stops working for you and becomes a client, should their staff login be turned into their client login — or should that be a new account?"** Say why it is being asked: the first keeps a staff person's whole audit history under a client-facing login. | Nothing. KAFF-109 rule 11 already governs the transition through the creation-time invariants (§9, §12, D-035); no source forbids or permits it deliberately | New, raised applying **D-051 (Q27)** to KAFF-109. Was the second, unanswered half of the old Q27 |
| **Q28** | **"When somebody gets their password wrong five times, should the lock be on the account, or on the account and the device they're trying from?"** **Per account alone, anyone who knows a site engineer's username can lock him out of the system for fifteen minutes at a time, indefinitely, from anywhere.** | Does not block KAFF-101a — the ruling as given is buildable | New, raised by **D-049 ruling 3**. Karim was not shown this consequence |
| **Q37** | **"Should the temporary password you set stop working after a while if the person never signs in?"** | Does not block KAFF-103 — no expiry is what was ruled | New, raised by **D-049 ruling 4**. Consequence if the answer is no: a forgotten account keeps a credential the Owner knows, indefinitely |
| **Q35** | **"When you switch someone's account off, should the system make you type why, or is that optional?"** | KAFF-110 AC — does not block; the mandatory half has been withdrawn from the story | QA QA-3. If yes, the same shape applies to every rejection gate in slice 5 and the mechanism is built once |
| **Q36** | **"Can two people who use the system share a phone number?"** | KAFF-106's error handling — does not block | UX Q-UX-7. `Client`, `Worker` and `Employee` are deduplicated by phone (§2); `User` is not in that list |
| **Q39** | **"When the number typed belongs to a client you archived, is showing them enough, or should the system offer to bring that client back?"** | KAFF-119, KAFF-123 — does not block; there is no unarchive path in slice 1 at all | UX Q-UX-4, sub-question 1. Not covered by ruling 8 |
| **Q12** | **"When you said you can create and edit all master data — did you mean all of it, or the three you listed: clients, suppliers and bank accounts?"** If the list was literal, the Owner comes back off `CatalogueManage`, `BabManage`, `EmployeeManage`, `SubcontractorManage` and `OpportunityManage`. | slice 2 | **D-045 #2**, open. Raised by D-044 ruling 4 rather than closed by it. QA **F-15** — five matrix cells |
| **Q13** | **"When you said bank accounts — do you mean a list of your banks as records in their own right, or just the accounts themselves in the ledger?"** | slice 2, slice 3 | **D-045 #1**, open. QA **F-16** |
| **Q14** | **"At extract 1 the client pays an extra 75,000 for material delivered to site, and it comes off later extracts as that material is installed — correct?"** One sentence, and it confirms the D-034 fix. | slice 3 · **the §15 gate** | Kickoff Q8, still open |
| **Q15** | **"Which banks — QNB, CIB, الأهلي, others?"** §6.5 defaults client collections to bank, and §15 cannot be reconciled without one. | slice 3 | Kickoff Q9, still open |
| **Q16** | **"Do any of your bank accounts have an overdraft?"** The other half — which ledgers carry hard floors — was answered by D-044 ruling 8. | slice 3 | Kickoff Q10, half still open |
| **Q29** | **"You told us the tax rate belongs to the contract, not the client. Does the same hold for the subcontractors and suppliers you pay — is their rate a property of the job, or of the firm?"** §6.7's next paragraph — *"when Kaff pays subcontractors and suppliers, Kaff withholds"* — has exactly the same shape, and **those rates are still held on the party record because your ruling named the client only.** Worth asking in the same breath: who sets the tax registration number on a client's file, now that the rate has moved to Finance? | slice 2 (party masters) · slice 3 **KAFF-318** | New, raised by **D-049 rulings 9–10** and flagged 🟡 in §6.7's amendment. Extending the ruling would be inventing it |
| **Q30** | **"Once you've issued the first extract on a contract, can the tax rate on it still be changed?"** | slice 4 **KAFF-416** · slice 5 | New, raised by **D-049 ruling 9**. A rate that moves after an extract is issued makes two extracts on one contract irreconcilable, and nothing says whether that is allowed |
| **Q18** | **"When you see متعثرة next to a project, does it mean work has stopped on site, or that it's still running but late and going badly? Is تم تأجيلها a pause you agree with the client? Do you write these words on the whole project or on a single unit? And does انتهت mean the site is finished, or the file is closed and the money collected?"** | slice 4 | Kickoff Q7. D-044 ruling 7 made them health tags; it did not say what they mean |
| **Q19** | **"Which do you write — تم تأجيلها or متأجلة?"** `CLAUDE.md` says one, `agents.md` said the other, and both require the word verbatim. | slice 4, and the continuity files | Kickoff A1b; UX Q-UX-12. `agents.md` was corrected on 2026-08-20 to تم تأجيلها — **that is consistency, not an answer** |
| **Q20** | **"When work restarts on a project you'd stopped, does it carry on from where it was?"** | slice 4 | Kickoff Q11 |
| **Q21** | *(the accountant, not Karim)* Rounding direction, and whether the contractual figure printed on an extract is 2 decimals or 4. D-044 ruling 6 settled storage at 4 and display at 2; direction was not asked. | slice 5 | Kickoff Q12, half open |
| **Q22** | **"How much عهدة can a site have out at once, and is 10,000 the most anyone can ask for in one go?"** §6.4 marks both 🟡. | slice 6 | §16 assumption 4 |
| **Q23** | **"Who closes the month once the books are done?"** `PeriodClose` is `Unresolved` with Finance assumed. §6.6 requires the close and does not say who performs it, nor whether it needs owner approval like every other financial act. | slice 7 | D-012; QA **F-17**. **Since D-052 answered Q17 this is the *only* `Unresolved` row left in the catalogue** — `Permission.PeriodClose` [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PeriodClose`], the single `Unresolved: true` in the file. Anywhere that still says "the last two" is stale |
| **Q24** | **"Do you have any bank loan, or equipment you're paying for in instalments?"** | slice 7 | §16 assumption 16 |
| **Q25** | **"What did you put into the company, and what has it kept since?"** Opening capital and retained earnings — without them the balance sheet cannot balance, which is slice 7's gate. | slice 7 | §16 assumption 17 |
| **Q40** | **Which way a time axis runs in an Arabic chart.** **Ask by showing two pictures, not in words.** Reading order says right to left; near-universal chart convention says left to right, and Kaff's staff have read left-to-right charts in Excel all their working lives. | slice 7 | UX Q-UX-13. No chart exists before slice 7; it is here so the first one is a decision, not a default |
| **Q26** | **"You keep 5% from every subcontractor and release it when the warranty ends — is that right for all of them, or do some have nothing held?"** | slice 8 | §5.1 🟡, §16 assumption 19 |

`spec.md` §16 remains the master register for the assumptions not listed here.

---

## What blocks sprint 1, in one message

**Nothing, as of 2026-08-22.** Q42 was the one row that blocked committed work and **D-055 §2 closed
it**: HR holds `UserRead`, CompanyWide, names and roles only [Verified: 2026-08-22 @
`src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserRead`]. HR can now name a person to put on a
project, which is the only thing HR exists to do.

**One thing carries forward into the build, and it is not a question.** `UserRead` is a permission,
not a projection. **A `UserRead` endpoint that returns the full user row satisfies the permission and
breaks Karim's ruling** — the row carries username, department and active state, which is the Owner's
user-administration surface arriving one screen sideways. Whoever builds the endpoint projects name
and role and stops. Its endpoint is slice 4; the catalogue row exists now.

**This section said "Nothing" and that was false when it was written.** Q42 and seven others were
raised on 2026-08-21 and never merged into this register (SM-8); the claim was true of the five
questions the section was counting and of nothing else. **The lesson is the mechanical one:** a
register is only as true as its last sweep, and the sentence that summarises it is the part that goes
stale first.

**Still true:** no slice-1 story is `BLOCKED` in the backlog sense, and nothing waits on a ruling
before it can be *written*. Q42 blocks a screen, not a story's readiness.

What goes in the next message to Karim, in this order:

1. ~~**Q42** — the only one that blocks committed work.~~ **Answered 2026-08-22, D-055 §2.**
1b. **Q-N10-1, Q-N10-2b, Q-N10-3** — three workflow questions about opening a project, all slice 4,
   all cheapest answered together because each is about who can touch a project the moment it exists.
2. **Q43** — HR's picker, one field down from Q42; it now stands alone rather than riding with it.
3. **Q45, Q46, ~~Q47~~, Q48, Q49, Q50, Q51** — ~~seven~~ **six** rules the stories currently read out
   of slice-0 code or out of a silence in §9. None blocks. Each is a rule somebody has already
   invented once. **Q47 comes off Karim's list 2026-08-23 — D-065 ruled four of its five cases and
   the fifth is with Nabil, not Karim.** It stays in the table above on case ③ alone; it is struck
   here rather than deleted because the batch was quoted by count.
4. **Q28, Q35, Q36, Q37, Q39, Q41** — the previous batch, unchanged and still cheap.
5. **Q52** — `PhotoPublish`, the last bare-department grant, deliberately left unruled by D-052.

**Q17 came off this list on 2026-08-21 — answered (D-052 §2) — and what it left behind, N10, is now
closed too (D-055 §3, 2026-08-22).** `ProjectCreate` splits from `ProjectManage`; creating is
company-wide because a create request cannot name a project, and editing stays project-scoped so §9's
assignment requirement keeps applying. **Slice 4 is no longer blocked on a permission. It is blocked
on three workflow questions — Q-N10-1, Q-N10-2b, Q-N10-3 — and those are Karim's.**

**Two of the five answers created work rather than only unblocking it**, and the sprint should know:

| Ruling | What it added |
|---|---|
| **Q31** | An unauthenticated endpoint that mints the most privileged account in the system. Its correctness is one atomic emptiness check — KAFF-100 re-estimated 3 → 5 |
| **Q32** | A new permission (`ProjectTeamRead`) and a second surface — KAFF-115 re-estimated 2 → 3 |
| **N5** | The stamp comparison that makes the global session kill real. It was declared and never implemented — KAFF-101a rule 11a, AC-101a-N |

---

## Not for Karim — decisions Nabil and the Architect owe

These are open, they block work, and they are not business questions. Putting them on Karim's list
would waste the one thing that is scarce here.

| # | Decision | Blocks | Where it came from |
|---|---|---|---|
| **N7** | **Is the client portal a separate deployment, or the same API behind a second origin?** Q33 rules the portal is a separate **host**; it does not rule this. The second shape still needs D-050's cookie and CORS worked through — `__Host-` forbids a `Domain` attribute, so a second origin is a second cookie and a second session boundary, and CORS must name both origins explicitly because credentials are in play. **Also here: how the reset link of Q38 is actually delivered.** Nothing in the pinned stack sends an SMS or a WhatsApp message, and `CLAUDE.md` forbids adding a dependency without a `decisions.md` entry — either a provider integration, or the endpoint hands the link to the Owner who sends it from his own phone (which is how he already hands out temporary passwords). **And, added by SM-8 from UX Q-UX-19 sub-question 4: who monitors delivery failures, and what does the Owner see when a message is rejected?** A send that silently fails leaves the Owner believing a locked-out engineer has a way back in. Neither the ruling nor KAFF-104 says whether the endpoint reports a failed send, retries, or falls back — and under either shape of the origin question the failure path is the same one. | **KAFF-810 / slice 8** for the origin half. The delivery half does not block **KAFF-104** — every rule in it holds under either shape | New, raised by **D-051 (Q33, Q38)**. D-051 flags the origin half 🟡 itself |
| **N8** | **How long a password-reset link lives.** KAFF-104 rule 6 requires that a finite lifetime exists, is enforced server-side and is configured rather than written into a handler (the precedent is `JwtOptions.InactivityMinutes`). **The number is not Karim's** — he ruled the mechanism, not its clock. | Nothing. KAFF-104 is buildable with the value in options | New, raised by **D-051 (Q38)**, which flags lifetime, single-use and session-kill as things the story must settle. Single-use and the session kill are settled in KAFF-104 rules 5 and 7; only the number is open |
| **N6** | **The client-code generator's contract.** Sequential `C-10001` is ruled (D-049 ruling 7); how it is produced is not. Two clients created in the same instant must not collide — `ux_clients_code` is still unique, so a read-max-and-add-one loses the race as a failed insert. Sequence, locked counter row, or retry. | KAFF-119 | New, raised by D-049 ruling 7. **Not open:** the format, that it is sequential, and that nobody can type one |
| **N9** | **Does the staff sign-in endpoint refuse a `Role.Client` credential outright, or accept it and let the client reach nothing?** Q33 ruled the portal is a separate host and settled the *screen*; **nothing has been ruled about the endpoint**. `KAFF-101a` rule 16 (`KAFF-101a:55`) still says a client credential *"authenticates"* there, which predates the ruling. Not cosmetic: accepting it mints a valid session cookie on the staff origin for a user who has no business holding one. It reaches nothing today — but *"reaches nothing"* is a property of the current permission catalogue, and refusing at the door is a property of the door | Nothing in slice 1: there is no portal shell and a client holds no staff permission. It decides one line of KAFF-101a rule 16, and it should be decided before KAFF-101a is built rather than after | UX Q-UX-20 (`ux/questions.md:295`), merged by **SM-8**. Related to **N7**, which owns the deployment shape; this owns the door |
| **N2** | **Whether money crosses the wire as a string.** A JSON number becomes a JavaScript `double`, and `CLAUDE.md` forbids floating point anywhere near money. The minimum position is already agreed — *the frontend performs no money arithmetic, ever* — and the wire format should be settled **before** slice 3 opens, not during it. | slice 3 | Kickoff §3; UX Q-UX-15 |
| **N4** | Kickoff actions **A2–A8**, all due before slice 3 opens: the posting-type × account-pair legality table; the transaction seam and per-transaction lock ordering; the audit gaps (`ExecuteUpdate`/`ExecuteDelete`, disconnected updates, the reason cleared before the save succeeds); the §15 fixture; the `PostgresException` → `Error` translator; the from/to convention written down; and something that creates a project's account set. | slice 3 | Kickoff §6 |

**Closed 2026-08-22:** **N10** → **D-055 §3**, approved as proposed (`proposals/N10-project-creation.md`,
design A). `ProjectCreate` is a new CompanyWide row for Owner and Technical Office; `ProjectManage`
keeps its name, its grants and its `ProjectScoped` scope for editing. **No database change** —
permissions are code. Its row is in the Answered table above rather than deleted, because a decision
that reads as *"we made this company-wide"* out of context is exactly the one a later session
"tidies" back and reopens the §9 hole.

**Closed since the last revision:** **N5** → **D-051**, and the answer was *no session table* — see the
Answered table above, and note that the half which does not exist yet (the stamp comparison) is now
carried by **KAFF-101a rule 11a** rather than by this register. Earlier: **N1** → D-050. **N3** →
D-047 and D-049; `spec.md` now carries marked amendment blocks in §2, §6.1, §6.4, §6.7, §9 and §13,
so a Verifier reading `spec.md` in a fresh session reads Karim's rulings.

---

## How an answer lands

1. Karim answers.
2. It goes into `decisions.md` with a D-number, in the house format: **Decision · Why · What we
   rejected · What would make us revisit.** An answer with no "why" recorded gets re-litigated in four
   months by an agent with no memory.
3. **`spec.md` gets a 📌 AMENDMENT block** where the ruling contradicts or extends the text (D-047).
   `CLAUDE.md` says `spec.md` wins, so a ruling that never reaches it is a ruling the Verifier is
   right to fail.
4. The BA rewrites the affected story's rule with the D-number cited, and clears `BLOCKED`.
5. The Scrum Master re-reads it against the Definition of Ready in the next refinement.

**A question is never closed by writing a plausible answer into a story.** And — new, from D-049 —
**a ruling that answers half a question leaves the story BLOCKED on the other half.** Q7 answered the
Supervisor case and left the Junior case; KAFF-109 stayed blocked. That is the correct outcome, not
an obstruction.
