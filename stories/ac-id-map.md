# ac-id-map.md — old positional AC label → stable AC ID

**Produced 2026-08-22, refinement action SM-23. Written by the BA, consumed by QA.**

Every acceptance criterion in `slice-1-foundation/` now carries a permanent identifier,
`AC-<story>-<LETTER>`. The rules are in `README.md` § *The acceptance-criterion ID scheme*. **This
file is the one-time translation between the labels QA's test cases cite today and the IDs they must
cite from now on.**

It exists because guessing is what caused the defect. Thirty-one cases had already drifted — they
cited a position, a criterion was inserted above it, and the position silently came to mean a
different rule. **Relock every citation from this table, not by counting down the story file.**

**232 criteria across 26 live stories.** KAFF-122 is `Superseded` and has none.

*(Corrected 2026-08-22: this read **228**, and the per-story table gave KAFF-108 **6** while its own
criterion table listed **7**. The figure was then 229. The `AC-108-B2` → `AC-108-G` retirement made on
the same day does **not** change it — a retired ID is not a criterion.)*

*(**229 → 230, later on 2026-08-22.** `AC-106-K` was appended under **SM-21** (finding **V-05**), and
`AC-105a-F` was retired in favour of `AC-105a-H` (finding **V-04**) — a one-for-one replacement, so
KAFF-105a's count is unchanged. See the note under *Count, per story*.)*

*(**230 → 231, 2026-08-23.** `AC-101a-O` appended — the audit record for a failed sign-in against an
unknown username, from **D-062 §3** and **D-063 §2/§3**. **KAFF-101a goes 14 → 15.** Nothing was
retired: `AC-101a-G` was amended, not replaced, and an amended criterion keeps its ID and does not
move the count.)*

*(**231 → 232, 2026-08-24.** `AC-101a-P` appended — the locked account answering on the truth of the
password, from **D-072 §1**, which closes Q47's fifth and last case. **KAFF-101a goes 15 → 16.**
**Three criteria were amended on the same day and none of them moves the count:** `AC-101a-B` regained
the locked-account case in its wrong-password half, `AC-105a-C` changed sides from refusal to field
(**D-072 §2**), and `AC-103-B` lost `GET /api/auth/me` from the endpoints it refuses. **`AC-101a-F` and
`AC-100-F` were amended too** — same rule, and `AC-101a-F` was the artefact V-03's own table missed.
**Nothing was retired and no letter was recycled.**)*

---

## How to read it

- **Old reference** is what a test case cites today: the story ID and the positional label.
- **Stable ID** is what it must cite from now on. It is complete on its own — `AC-118-F`, never
  `KAFF-118 AC-118-F`, and never a bare `F`.
- **Criterion** is the criterion's own title, verbatim from the story, so a case can be checked
  against the rule it actually asserts rather than against a label. **If the title does not describe
  what your case tests, the case was already drifted — fix the case, not the map.**

## Three traps in this table

1. **Suffixed labels are not sub-criteria and never were.** `AC1b`, `AC4b`, `AC6b`, `AC6c`, `AC1a`,
   `AC1c` were the ad-hoc workaround for exactly this problem — an insertion that did not want to
   renumber its neighbours. They are ordinary criteria and they get ordinary IDs. `KAFF-115 AC4b`
   is **`AC-115-E`**, not a child of `AC-115-D`.
2. **The letters run in reading order *today only*.** `AC-100-C` is the third criterion in the file
   this morning; it will not be after the first insertion, and that is the entire point. **Never
   re-derive an ID by counting.**
3. **A case citing bare `KAFF-101` or `KAFF-105` is citing a story that no longer resolves.** Those
   were split (`README.md` § *The ID scheme*): map to `101a` / `105a` unless the case concerns the
   sign-in screen or the project list, which are `101b` / `105b`. **Resolve the story ID first, then
   the criterion** — a case that cites `KAFF-105 AC3` has two things wrong with it, not one.

## What this file does not cover

**Historical labels are deliberately absent.** Four stories narrate what a *previous version* of
themselves asserted — KAFF-101a's old `AC1` (`:25`), KAFF-103's old `AC3` (`:13`), KAFF-110's earlier
`AC4` (`:112`), KAFF-121's old `AC2` (`:22`). Those criteria do not exist. They have no stable ID and
they are not in this table. **A test case that cites one of them is asserting a withdrawn rule** —
that is a case to retire, not to relabel, and it is a finding worth raising rather than quietly
fixing.

**This file does not grow.** After today a criterion is born with its ID and there is nothing to
translate. If a future sprint finds itself extending this table, the scheme has been broken
somewhere upstream.

---

### KAFF-100 · Bootstrap the first Owner through a one-time setup screen

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-100 AC1` | **`AC-100-A`** | an empty system offers the screen, and one Owner comes out of it |
| `KAFF-100 AC2` | **`AC-100-B`** | it cannot happen twice |
| `KAFF-100 AC3` | **`AC-100-C`** | two simultaneous requests produce one Owner |
| `KAFF-100 AC4` | **`AC-100-D`** | the lock is the emptiness test, and nothing else |
| `KAFF-100 AC5` | **`AC-100-E`** | deactivating the Owner does not re-open it |
| `KAFF-100 AC6` | **`AC-100-F`** | the Owner types their own password and is not forced to change it |
| `KAFF-100 AC7` | **`AC-100-G`** | no shared login survives review |
| `KAFF-100 AC8` | **`AC-100-H`** | the password never leaves the database |
| `KAFF-100 AC9` | **`AC-100-I`** | Arabic, RTL, at mobile width |

### KAFF-101a · Sign in, and the server sets an `HttpOnly` session cookie

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-101a AC1` | **`AC-101a-A`** | a valid credential opens a session and hands JavaScript nothing |
| `KAFF-101a AC2` | **`AC-101a-B`** | ~~wrong password, unknown user and locked account are indistinguishable~~ → ~~a wrong password, an unknown username, a client and a subcontractor are indistinguishable~~ → **a wrong password, an unknown username, a client, a subcontractor and a locked account given the wrong password are indistinguishable.** Retitled twice: 2026-08-23 under **D-065** (locked-account case struck, `Role.Client` and `Role.Subcontractor` added), and **2026-08-24 under D-072 §1** (the locked account returns, narrowed to its wrong-password half). **Same ID, same letter, never retired** |
| `KAFF-101a AC3` | **`AC-101a-C`** | five failures lock the account for fifteen minutes |
| `KAFF-101a AC4` | **`AC-101a-D`** | a success resets the counter |
| `KAFF-101a AC5` | **`AC-101a-E`** | eight characters is enough, and nothing more is demanded |
| `KAFF-101a AC6` | **`AC-101a-F`** | a temporary password has exactly one destination |
| `KAFF-101a AC7` | **`AC-101a-G`** | a subcontractor cannot sign in |
| `KAFF-101a AC8` | **`AC-101a-H`** | a deactivated user cannot sign in, and their open session dies |
| `KAFF-101a AC9` | **`AC-101a-I`** | a password change kills every other session |
| `KAFF-101a AC10` | **`AC-101a-J`** | thirty idle minutes ends the session |
| `KAFF-101a AC11` | **`AC-101a-K`** | the session grants nothing by itself |
| `KAFF-101a AC12` | **`AC-101a-L`** | the password never leaves the database |
| `KAFF-101a AC13` | **`AC-101a-M`** | the browser store stays empty |
| `KAFF-101a AC14` | **`AC-101a-N`** | a stale security stamp is refused |
| — *(new 2026-08-23)* | **`AC-101a-O`** | a failed sign-in against an unknown username is recorded, and what was typed is not — no old reference; a case citing this is new work, not a relabel. Appended under **D-062 §3** (Karim's Q53 ruling) and **D-063 §2/§3**. 🟡 **Its mechanism is decided and not yet built** — the IP column and the nullable subject — and the criterion says so in its own text. **Do not write a case that expects it to pass today** |
| — *(new 2026-08-24)* | **`AC-101a-P`** | the locked account answers on the truth of the password, and the hash runs either way — no old reference; a case citing this is new work, not a relabel. Appended under **D-072 §1**, which closes Q47's last case. **The criterion has a timing clause and it is load-bearing**: a case that asserts only the two status codes passes on the implementation the ruling forbids. **Write the case so that checking the lockout before verifying the password turns it red** |

> **`AC-101a-G` is not retired and its letter is not free.** It always asserted the refusal, which was
> certain; what was struck on 2026-08-23 is the status and the `messageKey` it used to name, which
> **D-063 A-02** found to be a username-existence oracle contradicting `AC-101a-B`. The shape was
> folded into **Q47** as its fifth case, and **~~is unanswered~~ was answered the same day: D-065,
> case 5 — the generic `401` with `errors.auth.invalid_credentials`, identical to what an unknown
> username gets.** ~~A QA case citing `AC-101a-G` should assert the refusal and the audit record, **and
> nothing about the response body**.~~ **A QA case citing `AC-101a-G` asserts the refusal, the audit
> record, and that the body is byte-for-byte the one `AC-101a-B` returns for an unknown username.**
> The struck clause stays struck: D-065 supplied a shape, it did not restore the one that was removed.

> **`AC-101a-B` lost a case and gained three, and it is not a new ID.** ~~*"Its locked-account third is
> Q47 case 3 ... That clause is struck from the criterion and replaced by nothing ... A QA case citing
> `AC-101a-B` asserts those four cases and says nothing about a locked account's status, body or
> `messageKey`, in either shape."*~~ — **superseded 2026-08-24, D-072 §1.**
>
> **The set is five and the fifth is half a case.** D-065 added `Role.Client` and `Role.Subcontractor`
> (cases 4 and 5); **D-072 §1 returns the locked account, split on the truth of the password.**
> Locked **plus wrong password** joins this criterion's identical set. Locked **plus correct password**
> answers **`423`** and is **`AC-101a-P`**, a new ID.
>
> **A QA case citing `AC-101a-B` asserts those five cases byte-for-byte — and asserts the timing.** A
> suite that checks only status codes reports green on the one implementation D-072 §1's ordering
> constraint forbids (lockout checked before the hash runs). **`AC-101a-P` is where that has to fail.**
>
> **`AC-101a-B` was amended, not retired** — the count moves only for `AC-101a-P`: **231 → 232**, and
> KAFF-101a **15 → 16**, A–P with no gaps.

### KAFF-101b · The staff sign-in screen, and where each role lands after it

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-101b AC1` | **`AC-101b-A`** | a staff sign-in arrives at the staff shell |
| `KAFF-101b AC2` | **`AC-101b-B`** | a client never sees the staff shell |
| `KAFF-101b AC3` | **`AC-101b-C`** | the portal is not discoverable from here |
| `KAFF-101b AC4` | **`AC-101b-D`** | HR lands on the team surface |
| `KAFF-101b AC5` | **`AC-101b-E`** | the screen imposes only what was ruled |
| `KAFF-101b AC6` | **`AC-101b-F`** | a forced change cannot be walked around |
| `KAFF-101b AC7` | **`AC-101b-G`** | one refusal for three causes |
| `KAFF-101b AC8` | **`AC-101b-H`** | Arabic, RTL, at mobile width |

### KAFF-102 · Sign out

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-102 AC1` | **`AC-102-A`** | the browser stops being signed in |
| `KAFF-102 AC1b` | **`AC-102-B`** | and the limit is asserted, not assumed |
| `KAFF-102 AC2` | **`AC-102-C`** | my other device is untouched |
| `KAFF-102 AC3` | **`AC-102-D`** | the cookie is actually cleared |
| `KAFF-102 AC4` | **`AC-102-E`** | sign-out does not disable the account |
| `KAFF-102 AC5` | **`AC-102-F`** | a portal user can sign out |

### KAFF-103 · Change the temporary password on first sign-in

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-103 AC1` | **`AC-103-A`** | a new user changes the temporary password and is then free |
| `KAFF-103 AC2` | **`AC-103-B`** | until then, nothing else is reachable |
| `KAFF-103 AC3` | **`AC-103-C`** | the Owner's credential stops working the moment I change it |
| `KAFF-103 AC4` | **`AC-103-D`** | the current password is required |
| `KAFF-103 AC5` | **`AC-103-E`** | eight characters, and nothing more, is the whole rule |
| `KAFF-103 AC6` | **`AC-103-F`** | the change ends every other session |
| `KAFF-103 AC7` | **`AC-103-G`** | the creator never learns the chosen password |
| `KAFF-103 AC8` | **`AC-103-H`** | a subcontractor record has nothing to change |
| `KAFF-103 AC9` | **`AC-103-I`** | Arabic, RTL, at mobile width |

### KAFF-104 · Reset a forgotten password with an Owner-generated link

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-104 AC1` | **`AC-104-A`** | the Owner never holds the credential |
| `KAFF-104 AC2` | **`AC-104-B`** | the link works once |
| `KAFF-104 AC3` | **`AC-104-C`** | the link expires |
| `KAFF-104 AC4` | **`AC-104-D`** | a second link kills the first |
| `KAFF-104 AC5` | **`AC-104-E`** | every session dies |
| `KAFF-104 AC6` | **`AC-104-F`** | the user is not asked to change it again |
| `KAFF-104 AC7` | **`AC-104-G`** | a deactivated user cannot be reset |
| `KAFF-104 AC8` | **`AC-104-H`** | no phone, no reset |
| `KAFF-104 AC9` | **`AC-104-I`** | a reset does not shortcut a lockout |
| `KAFF-104 AC10` | **`AC-104-J`** | a reset changes nothing but the credential |
| `KAFF-104 AC11` | **`AC-104-K`** | a subcontractor has nothing to reset |
| `KAFF-104 AC12` | **`AC-104-L`** | only the Owner may generate |
| `KAFF-104 AC13` | **`AC-104-M`** | the token never appears anywhere it can be read later |
| `KAFF-104 AC14` | **`AC-104-N`** | both ends are audited |

### KAFF-105a · `GET /api/auth/me` returns who I am and what I may do

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-105a AC1` | **`AC-105a-A`** | the caller learns who they are |
| `KAFF-105a AC2` | **`AC-105a-B`** | no token, anywhere |
| `KAFF-105a AC3` | **`AC-105a-C`** | **a forced password change is announced, as a field on a `200`.** Retitled 2026-08-24 under **D-072 §2** (finding **V-03**): the criterion said the call is *refused*, the ruling says it **succeeds and carries `mustChangePassword: true`**. **The assertion inverts; the ID does not move.** A QA case citing this must now assert a `200` and the flag, **and must not assert any refusal shape** — the old case asserts the withdrawn rule and is a case to retire, not to relabel |
| `KAFF-105a AC4` | **`AC-105a-D`** | signed out is not "signed in as nobody" |
| `KAFF-105a AC5` | **`AC-105a-E`** | the endpoint and the catalogue cannot drift |
| `KAFF-105a AC6` | **~~`AC-105a-F`~~ — RETIRED 2026-08-22** | *a portal client gets two permissions and no more — **superseded by `AC-105a-H`**. It asserted that `GET /api/auth/me` returns a portal client `PortalRead` and `PortalApprove`; both rows are `ProjectScoped` and the payload carries `CompanyWide` rows only, so it contradicted its own story's rule 4 — finding **V-04**. **Retired, not recycled:** never issued to a different criterion. The old reference is kept so a case citing `KAFF-105a AC6` still resolves — and learns the criterion is dead. `qa/slice-1/test-cases.md` -> `TC-1-042` cites it and asserts the withdrawn rule: **relock it to `AC-105a-H`, whose assertion is the inverse — it cannot be carried across unrewritten.*** |
| — *(new 2026-08-22)* | **`AC-105a-H`** | a portal client's company-wide set is empty, and nothing project-scoped leaks into it — the replacement for `AC-105a-F`. **Not a relabel: the assertion is inverted**, so a case may not be carried across without being rewritten |
| `KAFF-105a AC7` | **`AC-105a-G`** | nothing secret leaks |

### KAFF-105b · `GET /api/auth/me` returns the projects I reach, and how I reach them

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-105b AC1` | **`AC-105b-A`** | an engineer sees his own seniority, per project |
| `KAFF-105b AC2` | **`AC-105b-B`** | the Owner's reach needs no assignment row |
| `KAFF-105b AC3` | **`AC-105b-C`** | HR gets names, and only names |
| `KAFF-105b AC4` | **`AC-105b-D`** | HR sees a project nobody is on yet |
| `KAFF-105b AC5` | **`AC-105b-E`** | HR is routed to the team surface, not the dashboard |
| `KAFF-105b AC6` | **`AC-105b-F`** | the surfaces are separate types, not one type filtered |
| `KAFF-105b AC7` | **`AC-105b-G`** | a portal client is bounded |
| `KAFF-105b AC8` | **`AC-105b-H`** | a revoked assignment disappears |
| `KAFF-105b AC9` | **`AC-105b-I`** | a role change empties the list on the next call |
| `KAFF-105b AC10` | **`AC-105b-J`** | the catalogue drives the per-project permissions |

### KAFF-106 · The Owner creates a user with a role and a department

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-106 AC1` | **`AC-106-A`** | the Owner creates a Finance user |
| `KAFF-106 AC2` | **`AC-106-B`** | nobody else can, whatever their role |
| `KAFF-106 AC3` | **`AC-106-C`** | HR cannot mint a login |
| `KAFF-106 AC4` | **`AC-106-D`** | an Operations user must carry a sub-department |
| `KAFF-106 AC5` | **`AC-106-E`** | a portal client cannot be given a department |
| `KAFF-106 AC6` | **`AC-106-F`** | a client user must name a client |
| `KAFF-106 AC7` | **`AC-106-G`** | usernames do not collide |
| `KAFF-106 AC8` | **`AC-106-H`** | the temporary password is not a permanent one |
| `KAFF-106 AC9` | **`AC-106-I`** | eight characters is enough for the temporary one too |
| `KAFF-106 AC10` | **`AC-106-J`** | Arabic, RTL, at mobile width |
| — *(new 2026-08-22)* | **`AC-106-K`** | an HR user cannot be created outside the HR department — no old reference; a case citing this is new work, not a relabel. Appended under **SM-21** / finding **V-05**: the create-path half of KAFF-107's `AC-107-B`, which the fold left uncovered |

### KAFF-107 · An HR user cannot be created or moved outside the HR department

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-107 AC1` | **`AC-107-A`** | HR in HR is fine |
| `KAFF-107 AC2` | **`AC-107-B`** | HR anywhere else is refused |
| `KAFF-107 AC3` | **`AC-107-C`** | an existing HR user cannot be moved out |
| `KAFF-107 AC4` | **`AC-107-D`** | HR reaches nothing financial |
| `KAFF-107 AC5` | **`AC-107-E`** | a Marketing user moved to HR gains nothing |

### KAFF-108 · Move a user between departments

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-108 AC1` | **`AC-108-A`** | a move takes effect on the next request |
| `KAFF-108 AC2` | **`AC-108-B`** | and the reverse takes effect just as fast |
| — | **~~`AC-108-B2`~~ — RETIRED 2026-08-22** | *reissued as `AC-108-G`. A suffixed insertion between `B` and `C`, contrary to the scheme it was created under — trap 1 below abolishes exactly this pattern. **Retired, not recycled:** never issued to a different criterion.* |
| — *(new 2026-08-22)* | **`AC-108-G`** | the department alone is never enough on money — no old reference; a case citing this is new work, not a relabel. **Was `AC-108-B2` for one day; nothing cited it.** Its reading position between `B` and `C` is deliberate and correct — the ID is an identity, not a position (rule 3) |
| `KAFF-108 AC3` | **`AC-108-C`** | the department rules are re-applied on a move |
| `KAFF-108 AC4` | **`AC-108-D`** | HR stays in HR |
| `KAFF-108 AC5` | **`AC-108-E`** | nobody but the Owner can move anyone |
| `KAFF-108 AC6` | **`AC-108-F`** | assignments survive the move |

### KAFF-109 · Change a user's role

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-109 AC1` | **`AC-109-A`** | a supervisor comes off site, and is not refused |
| `KAFF-109 AC2` | **`AC-109-B`** | junior assignments go too |
| `KAFF-109 AC3` | **`AC-109-C`** | the mirror case |
| `KAFF-109 AC4` | **`AC-109-D`** | history survives |
| `KAFF-109 AC5` | **`AC-109-E`** | nothing is restored |
| `KAFF-109 AC6` | **`AC-109-F`** | a role change takes effect immediately |
| `KAFF-109 AC7` | **`AC-109-G`** | the department rules are re-applied |
| `KAFF-109 AC8` | **`AC-109-H`** | a change to the same role does nothing |
| `KAFF-109 AC9` | **`AC-109-I`** | only the Owner may |
| `KAFF-109 AC10` | **`AC-109-J`** | the before-state and every revocation are in the trail |
| `KAFF-109 AC11` | **`AC-109-K`** | it is one transaction |

### KAFF-110 · Deactivate a user, and their access ends on the next request

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-110 AC1` | **`AC-110-A`** | access ends on the next request |
| `KAFF-110 AC2` | **`AC-110-B`** | including on the requests that name no project |
| `KAFF-110 AC3` | **`AC-110-C`** | every device, not just this one |
| `KAFF-110 AC4` | **`AC-110-D`** | they cannot sign in again |
| `KAFF-110 AC4b` | **`AC-110-E`** | and cannot recover their way back in |
| `KAFF-110 AC5` | **`AC-110-F`** | their assignments are revoked, and stay on file |
| `KAFF-110 AC6` | **`AC-110-G`** | the reason is stored when it is given |
| `KAFF-110 AC7` | **`AC-110-H`** | the record survives |
| `KAFF-110 AC8` | **`AC-110-I`** | only the Owner may |
| `KAFF-110 AC9` | **`AC-110-J`** | twice is refused |

### KAFF-111 · Deactivating a user revokes their project assignments

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-111 AC1` | **`AC-111-A`** | the assignments are revoked |
| `KAFF-111 AC2` | **`AC-111-B`** | and the rows survive |
| `KAFF-111 AC3` | **`AC-111-C`** | the active team loses them, the trail keeps them |
| `KAFF-111 AC4` | **`AC-111-D`** | one record each, one story |
| `KAFF-111 AC5` | **`AC-111-E`** | a leaver with no assignments deactivates cleanly |
| `KAFF-111 AC6` | **`AC-111-F`** | nothing comes back on its own |
| `KAFF-111 AC7` | **`AC-111-G`** | the whole act is one transaction |

### KAFF-112 · Reactivate a user, who comes back with nothing

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-112 AC1` | **`AC-112-A`** | a returning user is the same user |
| `KAFF-112 AC2` | **`AC-112-B`** | they come back with no access to any project |
| `KAFF-112 AC3` | **`AC-112-C`** | the revoked rows are not resurrected |
| `KAFF-112 AC4` | **`AC-112-D`** | the old password is dead |
| `KAFF-112 AC4b` | **`AC-112-E`** | an old token does not come back to life with them |
| `KAFF-112 AC5` | **`AC-112-F`** | the new one is temporary, like a new starter's |
| `KAFF-112 AC6` | **`AC-112-G`** | reactivating an active user is refused |
| `KAFF-112 AC7` | **`AC-112-H`** | only the Owner may |
| `KAFF-112 AC8` | **`AC-112-I`** | putting them back on a project is a deliberate act with a named author |

### KAFF-113 · Assign a user to a project, with seniority for site engineers

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-113 AC1` | **`AC-113-A`** | HR staffs a project it was never assigned to |
| `KAFF-113 AC2` | **`AC-113-B`** | and still cannot open that project |
| `KAFF-113 AC3` | **`AC-113-C`** | HR's reach stops at a project that does not exist |
| `KAFF-113 AC4` | **`AC-113-D`** | the same engineer, two seniorities |
| `KAFF-113 AC5` | **`AC-113-E`** | seniority is refused where §9 does not put it |
| `KAFF-113 AC6` | **`AC-113-F`** | clients and subcontractors are not assignable |
| `KAFF-113 AC7` | **`AC-113-G`** | nobody else can staff a project |
| `KAFF-113 AC8` | **`AC-113-H`** | an inactive user is not assignable |
| `KAFF-113 AC9` | **`AC-113-I`** | no duplicate active assignment |

### KAFF-114 · Revoke a project assignment without losing who could act when

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-114 AC1` | **`AC-114-A`** | access ends on the next request |
| `KAFF-114 AC2` | **`AC-114-B`** | the row survives |
| `KAFF-114 AC3` | **`AC-114-C`** | re-assignment is legal |
| `KAFF-114 AC4` | **`AC-114-D`** | twice is refused |
| `KAFF-114 AC5` | **`AC-114-E`** | nobody else can |
| `KAFF-114 AC6` | **`AC-114-F`** | revocation is not deletion |

### KAFF-115 · The project team panel is built from assignment rows, not from the access check

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-115 AC1` | **`AC-115-A`** | the Owner is not on every team |
| `KAFF-115 AC2` | **`AC-115-B`** | nor is HR |
| `KAFF-115 AC3` | **`AC-115-C`** | seniority shows, per project |
| `KAFF-115 AC4` | **`AC-115-D`** | revoked members are gone |
| `KAFF-115 AC4b` | **`AC-115-E`** | and a leaver is gone the same way |
| `KAFF-115 AC5` | **`AC-115-F`** | an empty team has an explicit empty state |
| `KAFF-115 AC6` | **`AC-115-G`** | a client cannot read it |
| `KAFF-115 AC6b` | **`AC-115-H`** | HR reads the team, and reaches nothing else |
| `KAFF-115 AC6c` | **`AC-115-I`** | the two surfaces are different types |
| `KAFF-115 AC7` | **`AC-115-J`** | Arabic, RTL, at mobile width |

### KAFF-116 · Every audit record says how the actor reached the project

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-116 AC1` | **`AC-116-A`** | an assigned actor |
| `KAFF-116 AC2` | **`AC-116-B`** | the Owner leaves a trace after all |
| `KAFF-116 AC3` | **`AC-116-C`** | HR's staffing is distinguishable from an assigned actor's |
| `KAFF-116 AC4` | **`AC-116-D`** | a portal action |
| `KAFF-116 AC5` | **`AC-116-E`** | company-level changes carry none |
| `KAFF-116 AC6` | **`AC-116-F`** | the field cannot be added later |

### KAFF-117 · The Owner reads the audit trail, and nobody else does

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-117 AC1` | **`AC-117-A`** | the Owner reads it, company-wide |
| `KAFF-117 AC2` | **`AC-117-B`** | an assigned user cannot read their own project's trail |
| `KAFF-117 AC3` | **`AC-117-C`** | no role but the Owner reaches it |
| `KAFF-117 AC4` | **`AC-117-D`** | a subcontractor has no login to try with |
| `KAFF-117 AC5` | **`AC-117-E`** | redacted fields stay redacted |
| `KAFF-117 AC6` | **`AC-117-F`** | the grant path is shown |
| `KAFF-117 AC7` | **`AC-117-G`** | a rejection shows its reason |
| `KAFF-117 AC8` | **`AC-117-H`** | the trail cannot be edited from the API |
| `KAFF-117 AC9` | **`AC-117-I`** | Arabic, RTL, at mobile width |

### KAFF-118 · Every state change in slice 1 writes an audit record

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-118 AC1` | **`AC-118-A`** | the committed slice-1 entities are covered |
| `KAFF-118 AC1a` | **`AC-118-B`** | the same holds for a client, when clients arrive |
| `KAFF-118 AC1b` | **`AC-118-C`** | deactivation writes more than one record |
| `KAFF-118 AC1c` | **`AC-118-D`** | a role change writes more than one record too |
| `KAFF-118 AC2` | **`AC-118-E`** | one request is one story |
| `KAFF-118 AC3` | **`AC-118-F`** | redaction is visible, not silent |
| `KAFF-118 AC4` | **`AC-118-G`** | the reason lands where the flow requires it |
| `KAFF-118 AC5` | **`AC-118-H`** | a read writes nothing |
| `KAFF-118 AC6` | **`AC-118-I`** | a failed write writes nothing |
| `KAFF-118 AC7` | **`AC-118-J`** | the trail outlives the actor |

### KAFF-119 · Register a client, with a generated code and a duplicate-phone warning

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-119 AC1` | **`AC-119-A`** | a client is registered, and the system names them |
| `KAFF-119 AC2` | **`AC-119-B`** | the codes run in sequence and are never typed |
| `KAFF-119 AC3` | **`AC-119-C`** | the same phone in three formats warns once, about the same client |
| `KAFF-119 AC4` | **`AC-119-D`** | the warning does not block the save |
| `KAFF-119 AC5` | **`AC-119-E`** | the decision is in the trail |
| `KAFF-119 AC6` | **`AC-119-F`** | an archived match still warns, and says it is archived |
| `KAFF-119 AC7` | **`AC-119-G`** | a portal client cannot reach the client master |
| `KAFF-119 AC8` | **`AC-119-H`** | nobody outside Marketing and the Owner may register one |
| `KAFF-119 AC9` | **`AC-119-I`** | no money on the record |
| `KAFF-119 AC10` | **`AC-119-J`** | no withholding category on the record |
| `KAFF-119 AC11` | **`AC-119-K`** | an individual cannot be given a tax registration number |
| `KAFF-119 AC12` | **`AC-119-L`** | Arabic, RTL, at mobile width |

### KAFF-120 · An individual's contract cannot carry a withholding rate, and nor can the individual

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-120 AC1` | **`AC-120-A`** | a tax registration number on an individual is refused, through the API |
| `KAFF-120 AC2` | **`AC-120-B`** | and the refusal reads as Arabic, not as a key |
| `KAFF-120 AC3` | **`AC-120-C`** | a rate on an individual's contract is refused |
| `KAFF-120 AC4` | **`AC-120-D`** | `None` is always legal |
| `KAFF-120 AC5` | **`AC-120-E`** | a corporate contract is unaffected |
| `KAFF-120 AC6` | **`AC-120-F`** | the client record has no category to set |
| `KAFF-120 AC7` | **`AC-120-G`** | the rules live in the domain |
| `KAFF-120 AC8` | **`AC-120-H`** | one key, not two |

### KAFF-121 · Edit a client's name and contact details

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-121 AC1` | **`AC-121-A`** | a name can be corrected at all |
| `KAFF-121 AC2` | **`AC-121-B`** | a correction is recorded with its before-state |
| `KAFF-121 AC3` | **`AC-121-C`** | changing the phone re-runs the duplicate check, and warns |
| `KAFF-121 AC4` | **`AC-121-D`** | the check runs on the normalised number |
| `KAFF-121 AC5` | **`AC-121-E`** | the code cannot be edited |
| `KAFF-121 AC6` | **`AC-121-F`** | kind changes cannot smuggle a tax registration past §6.7 |
| `KAFF-121 AC7` | **`AC-121-G`** | nobody outside Marketing and the Owner may edit |
| `KAFF-121 AC8` | **`AC-121-H`** | internal notes stay internal |
| `KAFF-121 AC9` | **`AC-121-I`** | Arabic, RTL, at mobile width |

### KAFF-122 · Set a corporate client's withholding category and tax registration number

**No acceptance criteria.** `Superseded` — nothing to relabel and nothing to relock.

### KAFF-123 · Archive a client

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-123 AC1` | **`AC-123-A`** | an archived client leaves the working list but not the database |
| `KAFF-123 AC2` | **`AC-123-B`** | the archived client still surfaces in the duplicate check |
| `KAFF-123 AC3` | **`AC-123-C`** | archiving twice is refused |
| `KAFF-123 AC4` | **`AC-123-D`** | no delete exists |
| `KAFF-123 AC5` | **`AC-123-E`** | nobody outside Marketing and the Owner may archive |

### KAFF-124 · Find a client by name or phone

| Old reference | Stable ID | Criterion |
|---|---|---|
| `KAFF-124 AC1` | **`AC-124-A`** | a phone in any format finds the client |
| `KAFF-124 AC1b` | **`AC-124-B`** | two clients with one number both come back |
| `KAFF-124 AC1c` | **`AC-124-C`** | the generated code finds the client |
| `KAFF-124 AC2` | **`AC-124-D`** | partial name search works in Arabic |
| `KAFF-124 AC3` | **`AC-124-E`** | archived clients are hidden by default and findable on request |
| `KAFF-124 AC4` | **`AC-124-F`** | a portal client cannot list clients |
| `KAFF-124 AC5` | **`AC-124-G`** | no money in the payload |
| `KAFF-124 AC6` | **`AC-124-H`** | an empty search says so |
| `KAFF-124 AC7` | **`AC-124-I`** | Arabic, RTL, at mobile width |

---

## Count, per story

| Story | Criteria |
|---|---:|
| KAFF-100 | 9 |
| KAFF-101a | **16** |
| KAFF-101b | 8 |
| KAFF-102 | 6 |
| KAFF-103 | 9 |
| KAFF-104 | 14 |
| KAFF-105a | 7 |
| KAFF-105b | 10 |
| KAFF-106 | 11 |
| KAFF-107 | 5 |
| KAFF-108 | 7 |
| KAFF-109 | 11 |
| KAFF-110 | 10 |
| KAFF-111 | 7 |
| KAFF-112 | 9 |
| KAFF-113 | 9 |
| KAFF-114 | 6 |
| KAFF-115 | 10 |
| KAFF-116 | 6 |
| KAFF-117 | 9 |
| KAFF-118 | 10 |
| KAFF-119 | 12 |
| KAFF-120 | 8 |
| KAFF-121 | 9 |
| KAFF-122 | 0 |
| KAFF-123 | 5 |
| KAFF-124 | 9 |
| **Total** | **232** |

*(Moved from 229 to 230 on 2026-08-22, and the arithmetic is worth stating because two of the three
changes cancel. **KAFF-106 goes 10 → 11**: `AC-106-K` appended under **SM-21** (finding **V-05**), the
create-path HR-department refusal the KAFF-107 fold left uncovered. **KAFF-105a stays at 7**:
`AC-105a-F` was retired and `AC-105a-H` issued in its place (finding **V-04**) — a retired ID is not a
criterion, so a one-for-one replacement moves nothing. Letters for KAFF-105a now run **A–E, G, H**, and
that gap is correct: `README.md` rule 4, retired not recycled.)*

*(And on this file "not growing": the paragraph above says a new criterion is born with its ID and
there is nothing to translate. That is still true — the two new rows here carry **no old reference**,
exactly as `AC-108-G` already did on the day this file was written. What the table now doubles as is
the **register** the ID-integrity check reads: every ID in `slice-1-foundation/` appears here and
nothing here is absent from a story. Adding a born-with-an-ID criterion keeps that property; omitting
it would break it.)*
