# KAFF-117 · The Owner reads the audit trail, and nobody else does

**Slice:** 1 · **Epic:** Foundation · **Points:** 5 · **Status:** ~~Ready~~ → **COMMITTED to sprint 4, Lane A, 2026-09-05.** Both dependencies cleared this week — KAFF-116 was accepted on 2026-08-24 (and the board said `Ready` until 2026-09-05, which is its own finding) and KAFF-118 was built 2026-09-05, D-116. **`AC-117-I` moved to `KAFF-128` before the pull**, under `process/agile.md` §2a rule 6
**Spec:** §7, §9 · **Decisions:** D-012, D-043, **D-049 (ruling 1)**, **D-117**
**Depends on:** KAFF-116, KAFF-118 — **both satisfied 2026-09-05**
**Discharges its UI criterion in:** **KAFF-128** (§2a rule 6 — named before the pull, not after)

## Story
As the Owner, I read the history of who changed what, so that a disagreement about an extract, an
assignment or a client record is settled by the record rather than by memory — and no other role in
Kaff can read it, on any project, including their own.

## What Karim ruled, and what he rejected
`AuditRead` had been granted to the Owner as an **assumption** since slice 0, marked
`Unresolved: true` in `PermissionCatalogue` with a test pinning it so it could not grow quietly
(D-012). The assumption was right. That is not the same as it having been answered, and it is now
answered:

> **The audit trail is the Owner's alone.** Company-wide, and *"completely hidden from all other
> roles, **even for their own projects**"*. — D-049 ruling 1

**What was rejected is the part worth keeping in front of whoever builds this:** a project-scoped
audit read for the people working on that project. From slice 3 the trail records every movement of
money, so scoping it by project would have reopened the zero-financial-visibility rule from a
direction nobody was watching. `AuditRead` is no longer `Unresolved`.

**The governance point stands and Karim has accepted it explicitly:** the only person who reaches
every project is the only person who can read the record of what he did there. It was put to him in
those words. **Nobody re-opens it by adding a reader.**

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `AuditRead` is `CompanyWide` and granted to `Role.Owner` **alone**, and is no longer marked `Unresolved` [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.AuditRead`] | D-049 ruling 1 |
| 2 | Every other role is refused, **including on projects they are assigned to and including their own actions**. There is no project-scoped audit view and no "my changes" view | D-049 ruling 1 |
| 3 | Every state change writes a record: who, when, what changed before and after, and why where the flow requires it | CLAUDE.md audit |
| 4 | Records are append-only and immutable, enforced by a database trigger | slice 0 `AuditRecord` · D-043 |
| 5 | Records carry `ProjectId` where the entity belongs to a project, so the Owner can filter by project — filtering is not the same as scoping, and only the Owner does either [Verified: 2026-08-22 @ `src/Domain/Auditing/AuditRecord.cs` -> `ProjectId`, nullable] | slice 0 `AuditRecord` · D-049 ruling 1 |
| 6 | Each record names **how** the actor reached the project, and the reading shows it. **The four values are the domain's own: `Assignment`, `OwnerGlobal`, `HrGlobal`, `PortalClient`** [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `enum ProjectAccessPath`]. *(This row said "client-of-project"; the enum member is `PortalClient` — corrected with KAFF-116's AC-116-D on 2026-08-22.)* The field itself is KAFF-116's work and does not exist on `AuditRecord` yet [Verified: 2026-08-22 @ `src/Domain/Auditing/AuditRecord.cs` -> `class AuditRecord`] | KAFF-116 · D-055 §7 · D-049 ruling 1 |
| 7 | `PasswordHash` and `SecurityStamp` are `[AuditRedacted]` and must not surface in any reading of the trail [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `PasswordHash`, `SecurityStamp`] | slice 0 `User` |
| 8 | A rejection's stored reason is part of the record and is displayed with it — never a silent step-back | §7 |
| 9 | A portal client never reads it. §12 lists what the client sees, and the trail is not on the list | §12 · D-049 ruling 1 |
| 10 | Reading writes nothing. An audit record per audit read would bury the records that matter | CLAUDE.md (*state change*) |

## Permissions, money, audit, i18n
- **Permissions:** `AuditRead`, `CompanyWide`, **Owner only**, no longer `Unresolved`. Every other
  role, in every scope, is refused — and the test that used to pin the assumption now pins a ruling.
- **Money:** shows no money in slice 1. **From slice 3 the trail carries every posting**, which is
  why this ruling is also a money-visibility ruling. It was said to Karim in that form and he ruled
  anyway.
- **Audit:** reading writes nothing.
- **i18n:** `audit.title`, `audit.filter.project`, `audit.filter.actor`, `audit.filter.date`,
  `audit.action.created`, `audit.action.modified`, `audit.action.deleted`, `audit.reason`,
  `audit.changed_properties`, plus the `audit.grant.*` keys from KAFF-116.

## Acceptance criteria
**AC-117-A — the Owner reads it, company-wide**
Given I am the Owner
When I request the audit trail with no project filter
Then I receive records from every project and every company-level change

**AC-117-B — an assigned user cannot read their own project's trail** *(fails if the rule is broken)*
Given I am a Technical Office user with an active assignment on project A, and I made changes on it this morning
When I request the audit trail for project A
Then I am refused with 403 — *"even for their own projects"* is the ruling, and this is the criterion that proves it

**AC-117-C — no role but the Owner reaches it** *(fails if the rule is broken)*
Given I am, in turn, Finance, Technical Office, Site Engineer, Head of Design, Marketing, HR and a portal Client
When each requests the audit trail, with and without a project id
Then all fourteen requests are refused with 403

**AC-117-D — a subcontractor has no login to try with**
Given a `Role.Subcontractor` record
When authentication is attempted
Then it is refused before the trail is reachable at all

**AC-117-E — redacted fields stay redacted** *(fails if the rule is broken)*
Given a user whose password was set and later changed
When the Owner reads the audit records for that user
Then no reading contains the password hash or the security stamp, in any field, in either the before or the after state

**AC-117-F — the grant path is shown** *(fails if the rule is broken)*
Given the Owner changed something on a project he holds no assignment row for
When he reads that record
Then it shows the change was reached by Owner-global — his own reach is legible to him in his own trail

**AC-117-G — a rejection shows its reason**
Given a state change recorded with a reason
When the record is read
Then the reason is displayed with it

**AC-117-H — the trail cannot be edited from the API** *(fails if the rule is broken)*
Given any audit record
When an update or delete is attempted through the API
Then no such endpoint exists, and a direct database attempt is refused by the trigger

~~**AC-117-I — Arabic, RTL, at mobile width**~~
~~Given the trail at 390px in Arabic~~
~~When it renders~~
~~Then direction is RTL, Latin identifiers and timestamps inside Arabic rows are bidi-isolated, and there is no horizontal overflow~~

> **MOVED to `KAFF-128` as `AC-128-A` on 2026-09-05, before this story was pulled** — Scrum Master,
> `meetings/2026-09-05-sprint-4-locked.md`. **Moved, not copied.**
>
> **This is `process/agile.md` §2a rule 6's first application, and it is the point of the rule.**
> Every previous UI criterion on a backend story moved *after* the story was delivered — `AC-106-J`
> nineteen days after, `AC-119-L` one day after. This one moved before a line of the story was
> written, because the rule now says a backend story carrying a UI criterion is not pullable until
> the Frontend story that will discharge it **exists on the board**. `KAFF-128` exists. So this is
> pullable.
>
> **The backend half of this story is unaffected** — `AC-117-A` … `AC-117-H` are all API and
> permission criteria and all stay here.

## Not in this story
Writing the records — that is the interceptor, already built (D-041), and KAFF-118 proves it for
slice 1's entities. The grant-path field itself (KAFF-116). Exporting the trail.

**A global Finance/Audit role.** D-049 ruling 1 anticipates one *"if added later"* and **does not
create one**. Nobody at Kaff has asked for it, and a role that exists before anybody needs it is a
member of the permission model that means nothing. If it is ever wanted it is a new question and a
new story.

**Retention or archiving:** `spec.md` says nothing about how long records are kept, and an agent
inventing a retention period would be inventing a rule with legal consequences.

## Questions for Karim
None. D-049 ruling 1 answers the question this story was blocked on.
