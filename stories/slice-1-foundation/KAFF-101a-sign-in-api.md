# KAFF-101a · Sign in, and the server sets an `HttpOnly` session cookie

**Slice:** 1 · **Epic:** Foundation · **Points:** 5 · **Status:** `Ready` — **with one clause held and two builds sequenced first.** See **Status, precisely** below
**Spec:** §9 · **Decisions:** D-011, D-035, D-044 (ruling 2), **D-049 (rulings 2, 3, 4)**, **D-050**, **D-051 (N5, Q33)**, **D-062 §1/§2/§3**, **D-063 §1/§2/§3**, **D-065**
**Depends on:** KAFF-100 *(now `Ready` — the soft-dependency argument is retired, see below)*

> **Split from KAFF-101** on 2026-08-21. The API half is answerable and is this story. The login
> screen, and where each role lands after it, is **KAFF-101b**, which was BLOCKED on Q33 and is now
> `Ready` too (D-051 Q33 — the portal is a separate host). QA's
> finding F-22 showed the old KAFF-101 rule 6 treating an open UX question as settled.
> `stories/README.md`: a split story keeps its number and gains `a` / `b`. **KAFF-101 as a single ID
> is retired; test cases written against it map to 101a unless they concern the screen.**

> **V-02 / N9 ANSWERED — Karim via Nabil, 2026-08-22, `decisions.md` D-062 §2. HARD NO.**
> *"It is strictly forbidden from a security standpoint for any user holding the `Role.Client` to sign
> in or authenticate through the staff portal (Staff Origin) ... A sign-in request from a Client
> against this endpoint must explicitly fail."*
>
> **All four things this note listed on 2026-08-22 are now closed, and so is the blocker that
> replaced them.** Updated by the BA, 2026-08-23, second pass, under **D-065**.
>
> - **✅ Rule 16 is rewritten** to the ruled mechanism — an explicit refusal at the door, and a guard
>   in the staff-session minter rather than in this handler. **D-062 §2, D-063 §1.**
> - **✅ 401 vs 403 is settled: `401`**, in the same body, `messageKey` and time envelope as the other
>   three refusals. **D-063 §1** — a 403 fires only when the credential is real, which is an oracle.
> - **✅ The audit criterion is written** — `AC-101a-O`, from D-062 §3 and D-063 §2/§3. **⚠️ It asserts
>   a mechanism that does not exist yet** and says so in its own text; see the criterion.
> - **✅ `AC-101a-G`'s refusal shape is RULED — the generic `401`.** *"The door must treat a
>   subcontractor exactly the same way it treats a non-existent user."* **D-065, case 5**, which closes
>   **D-063 A-02**. The criterion now asserts a shape again; see it.

## Status, precisely

**`Ready` for everything except one clause, and shippable only after two builds land.** Written out
because *"most of it is buildable"* is not a status somebody can act on.

| # | What | State | Who |
|---|---|---|---|
| 1 | **Cases 1, 2, 4 and 5 of Q47** — wrong password · unknown username · `Role.Client` at the staff door · `Role.Subcontractor` | **✅ RULED — one generic `401`, identical in status, body and `messageKey`.** **D-065**, 2026-08-23. Build them | — |
| 2 | **The locked account** — struck from `AC-101a-B` on 2026-08-23 and covered by no criterion; **rule 14**, which is the same case, is left unchanged and unruled | **🟡 OPEN — Q47 case 3.** Nabil ruled a distinct **423** and the ruling is **flagged back to him**, because a 423 only exists when the username does, which is the enumeration Cases 2 and 5 were ruled to close, and five failed attempts manufacture it on demand. **D-065, the first 🟡 block.** **Write no criterion for it in either shape.** Sequenced last: it is a prerequisite for nothing | **Nabil** |
| 3 | **The IP column** on `AuditRecord` — `AC-101a-O` cannot pass without it | 🟡 **Decided, not built. D-063 §2.** **N-19 applies in full: it must land before this story ships**, because a column never written cannot be backfilled into an append-only table. **A build dependency, not a question** | Backend |
| 4 | **The nullable subject** — `AuditRecord.EntityId` and `AuditEvent.SubjectId` become `Guid?`, `EntityType` stays required, plus `ck_audit_records_entity_change_has_subject` | 🟡 **Decided, not built. D-063 §3.** May land in this story's own migration. **A build dependency, not a question** | Backend |
| 5 | **The `AuditEventKind` member** `SignInFailedUnknownUser` | 🟡 **Delegated to this story by D-063 §3** and not yet drafted. **Decoupled from `AC-101a-G` by D-065**: the second member D-063 §3 *"arguably implies"* was coupled to `AC-101a-G`'s open shape, and that shape is now ruled — so whether the client and subcontractor refusals need their own kinds is a vocabulary question this story answers, not a Karim one | BA / Architect |
| 6 | **Q28** — lockout per account, or per account **and** address | Open, and **does not block**. The ruling as given is buildable | Karim |

**No longer blocked on the Architect** — D-063 decided all three things D-062 routed to him — **and no
longer blocked on Karim** — D-065 ruled Q47's four unambiguous cases. What is left is one clause with
Nabil and two migrations with Backend.

**Why `Ready` and not `BLOCKED`.** `stories/README.md` — *"block the smallest thing that is actually
unanswerable. A question about one field blocks that field, not the story"* — and it names KAFF-101
itself as one of the two places that was got wrong. Q47 case 3 is one clause of one criterion; D-065
says in its own words *"Cases 1, 2, 4 and 5 are built; Case 3 is built neither way. Sequenced last so
it is not a prerequisite."* **Row 2 is the thing nobody may build, and it is the only one.** Rows 3
and 4 are this story's own migration work and gate the **ship**, not the **start** — `AC-101a-O` is
the only criterion behind them.

## Story
As a member of Kaff staff, I sign in with my username and password, and the server puts my session
in a cookie the browser will not let a script read — so that every later request carries a named
actor, and nothing on the page can steal the credential that names them.

`spec.md` does not describe authentication. It describes **authorization** in detail (§9) and takes
for granted that a user is a person who can be identified. That gap is now filled by ruling rather
than by assumption: D-049 rulings 2–4, and D-050.

## What D-050 changed about this story's shape
**The response body no longer carries a token.** The previous version of this story was titled
*"…and receive an access token"*, and its AC1 asserted *"I receive a token the API accepts on a
subsequent request"*. That is now wrong in a way that matters:

> The access token is carried in an `HttpOnly; Secure; SameSite=Strict` cookie. `localStorage` and
> `sessionStorage` are prohibited for it. UI state comes from a separate `GET /api/auth/me` returning
> profile claims and **no token**. — D-050

So the sign-in response is a status and, at most, a `messageKey`. Anything that hands the token to
JavaScript defeats the decision, whatever the storage.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | On success the server sets the access token in a cookie named by `JwtOptions.CookieName` (`__Host-kaff-auth`) — `HttpOnly`, `Secure`, `SameSite=Strict`, path `/`, **no `Domain`** | D-050 |
| 2 | **The response body carries no token, in any field, under any name.** `localStorage` and `sessionStorage` are prohibited for it | D-050 |
| 3 | `SameSite=Strict` is the CSRF control and it is the whole of it. If it is ever relaxed to `Lax` or `None`, an anti-forgery token is required the same day | D-050 |
| 4 | The `Authorization: Bearer` header still authenticates, deliberately — service-to-service callers and the integration suite use it, and neither is reachable by an XSS payload in the SPA. The cookie is read only when the header is absent | D-050 |
| 5 | The session expires after **30 minutes of inactivity**, sliding on activity. The number is `JwtOptions.InactivityMinutes`, not a literal in a handler | D-049 ruling 2 |
| 6 | A password of at least **8 characters** is accepted, and **no complexity is demanded**. Karim's reason is itself a requirement: *"so site workers don't struggle to log in"* | D-049 ruling 3 |
| 7 | **5 consecutive failed attempts lock the account for 15 minutes.** A success resets the count. The state and the transitions are on the entity — `RecordFailedSignIn` takes the two numbers as arguments rather than writing them, and restarts the counter when the lock is applied [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `RecordFailedSignIn`, `FailedSignInAttempts`, `LockedOutUntil`] | D-049 ruling 3 |
| 8 | A user whose password was set for them by the Owner MUST change it before the session may do anything else. The flag is `User.MustChangePassword`, set by `SetTemporaryPassword` and cleared by `SetOwnPassword` — there is no boolean parameter, deliberately [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `MustChangePassword`, `SetTemporaryPassword`, `SetOwnPassword`] | D-049 ruling 4 · KAFF-103 |
| 9 | `Role.Subcontractor` cannot log in at all — *"Subcontractor (record only, no login)"*. The refusal is in the entity's private `StorePasswordHash`, so **both** public setters inherit it and neither can be given a credential [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `StorePasswordHash`]. The evaluator refuses the role before the catalogue is consulted as well [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `RoleCannotLogIn`]. **What the *door* tells the caller is not this path**: it is the generic `401` / `errors.auth.invalid_credentials`, **D-065 case 5** — see `AC-101a-G` | §9 · `User.StorePasswordHash` · **D-065 (case 5)** |
| 10 | An inactive user cannot sign in, and cannot use a cookie issued before deactivation. D-048 already makes this instant: every authorized request re-reads the user row | §9 · D-048 |
| 11 | A password change or a deactivation invalidates **every** active session, everywhere, immediately. `User.SecurityStamp` is the hook. **`SetPasswordHash` no longer exists** — it was split into `SetOwnPassword` and `SetTemporaryPassword`, both of which rotate through the private `StorePasswordHash`; `ClearPassword` and `Deactivate` rotate too [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `SetOwnPassword`, `SetTemporaryPassword`, `StorePasswordHash`, `ClearPassword`, `Deactivate`]. **`Reactivate` does not rotate** [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `Reactivate`] — that gap is KAFF-112 rule 9a, not this story | D-049 ruling 2 · D-048 |
| 11a | **This story is where the security stamp is actually validated.** The token carries `KaffClaimTypes.SecurityStamp`; **every authorized request compares that claim to the stored `User.SecurityStamp` and refuses on mismatch, and on absence.** **BUILT — do not build it again; verify it.** The comparison is in the `WHERE` clause of the subject read, and the empty-stamp refusal is before the query, so there is no null-vs-null match and no bypass [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`]. D-051 (N5) recorded that the claim type and the rotation both existed and nothing compared the two; **that gap is closed** (D-053 §1). Covered by `Rotating_the_security_stamp_kills_every_existing_session` and `A_request_with_no_security_stamp_is_refused` [Verified: 2026-08-22 @ `tests/Api.Tests/PermissionMechanismTests.cs` -> `Rotating_the_security_stamp_kills_every_existing_session`, `A_request_with_no_security_stamp_is_refused`]. Without it, rules 10 and 11 and AC-101a-H, AC-101a-I and AC-101a-N are assertions with no mechanism | **D-051 (N5)** · D-053 §1 · D-049 ruling 2 |
| 11b | **There is no session table.** Per-device sign-out is clearing the cookie (KAFF-102); the global kill is the stamp. **The accepted limit, stated rather than hidden:** with no per-session identity there is no way to revoke *one other* device — losing a phone means signing out everywhere. Do not add a session table to close it; D-051 records this as the right trade for a first-party SPA on one origin, and adding one later is additive | **D-051 (N5)** |
| 12 | The token carries the user id, display name and role. It carries **no permission list and no assignment list** — those are re-evaluated server-side per request against `PermissionCatalogue` and `ProjectAssignment` | §9 (*"Enforcement is server-side"*) · D-012 |
| 13 | A wrong password and an unknown username produce the same refusal — **`401`, `errors.auth.invalid_credentials`, the same body** — in the same time envelope. ✅ **CITED. No longer a waiver: Q47 cases 1 and 2 are ruled.** *"Never tell an attacker the account does not exist."* | **D-065 (cases 1, 2)** · §9 |
| 14 | A **locked** account produces that same refusal. Saying "locked" tells an attacker the username is real and that their lockout worked. 🟡 **NOT RULED. This is Q47 case 3 and it is OPEN with Nabil** — he ruled a distinct `423` and the ruling is flagged back to him in D-065's own first 🟡 block, on this rule's reasoning. **The rule text is unchanged and is not the answer; build no criterion for this case in either shape until he answers.** *(The Q47 waiver is spent: it covered rules 13 and 14 together, and rule 13 is now ruled.)* | 🟡 **D-065 — OPEN, case 3** · §9, same reasoning as rule 13 |
| 15 | Passwords are stored only as a hash; `User.PasswordHash` is `[AuditRedacted]` and must never reach a response, a log or an audit record [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `PasswordHash`, `SecurityStamp` — both carry the attribute] | CLAUDE.md audit · slice 0 `User` |
| 16 | **A `Role.Client` credential is refused at the staff sign-in endpoint. It never authenticates and no staff session is minted for it** — the refusal is a property of the door, not of what the catalogue happens to contain. The refusal is **`401`, with the same body, the same `messageKey` (`errors.auth.invalid_credentials`) and the same time envelope as rule 13**: it is the fourth member of that one indistinguishable set, not a fourth answer. **Confirmed as Q47 case 4 by D-065**, which also makes `Role.Subcontractor` the fifth member (case 5, rule 9 and `AC-101a-G`). *(The cross-reference used to read "rules 13 and 14"; rule 14 — the locked account — is Q47 case 3 and is not ruled, so it is no longer cited as the yardstick.)* **No `Set-Cookie`, no distinguishing field anywhere in the response.** A 403 was rejected — *"your credential was valid and you may not come in"* fires only when the credential is real, which is the single most informative answer an anonymous door can give | **D-062 §2** · **D-063 §1** · **D-065 (case 4)** · §12 · D-035 · D-051 (Q33) · KAFF-101b rule 3 |
| 16a | **The role is checked *after* the password verification has run**, never before. A handler that short-circuits on `Role.Client` before hashing returns in a fraction of the time and re-creates the oracle rule 16 just closed — as a clock instead of a status code. Rule 13's *"same time envelope"* already says this; it is repeated because the natural way to write the guard breaks it | **D-063 §1** |
| 16b | **The guarantee lives in the function that mints a staff session, not in this handler.** The staff session is one thing — a token for `JwtOptions.Audience` in `JwtOptions.CookieName` [Verified: 2026-08-23 @ `JwtOptions.cs` -> `CookieName`] — and every present and future staff door goes through it: this endpoint, KAFF-103's forced change, KAFF-104's reset link, anything slice 8 adds. **One guard there refuses `Role.Client` for all of them.** It is a **programmer-error guard and it throws**; it is not the user-facing path. **Two places, two jobs:** the minter guarantees no `Role.Client` staff session can exist, the handler decides what the caller is told (rule 16). Deliberately not the same rule written twice | **D-063 §1** |
| 16c | This is **not** the existing `RoleCannotLogIn` path. That error is `Error.Forbidden` [Verified: 2026-08-23 @ `SeparationOfDuties.cs` -> `RoleCannotLogIn`] — authorization on an **already-authenticated** request — and it never runs on an anonymous endpoint. **Not decided, and named so nobody assumes it was:** how the *portal* host authenticates a client is slice 8 (**N7**). Rule 16 says only that the staff minter refuses `Role.Client`; it says nothing about the portal's own minter, except that it is a different one | **D-063 §1** · N7 |
| 17 | **Unresolved:** whether the lockout counts failures per account, or per account **and** address | **Q28** — does not block; see below |

> **Rule 16, as it read until 2026-08-23:** ~~*"A `Role.Client` credential authenticates, and reaches
> only `PortalRead` / `PortalApprove` — but the portal is a separate host, so a client's session is
> refused at the staff origin."*~~ **Rewritten, not deleted, and the difference is the point.** The
> old rule's safety was a property of what `PermissionCatalogue` happens to contain today: one
> company-wide row a client happens to hold re-opens it. The new rule is a property of the door and
> survives any catalogue change. Karim ruled for the second (D-062 §2); the Architect chose the status
> code and the location (D-063 §1). Rules 16a–16c are the halves of that ruling that a handler author
> would otherwise have to re-derive — they are **not** new rules and carry no new authority.

## Permissions, money, audit, i18n
- **Permissions:** the endpoint is anonymous — it is the only one in the system that is. Every other
  endpoint requires an identity; §9 makes role × assignment mandatory everywhere else.
- **Money:** moves no money.
- **Audit:** a successful sign-in writes a record naming the user, the time and the request path. A
  **failed** sign-in writes one too, and **a lockout writes its own record**, because *"the account
  was locked at 14:02"* is the fact somebody will ask about.
  **~~"with the attempted username"~~ — struck 2026-08-23, and it was the dangerous half.** Q53 is
  answered: *"Log the attempt as a security event, but strictly FORBID storing the typed input. Users
  frequently type their password into the username/email field by mistake"* (**D-062 §3**). The record
  says **"Failed sign-in — Unknown user"** and keeps **only metadata: the IP address and the
  timestamp**, omitting the entered string entirely. **The reason is the rule, not just its outcome:**
  `audit_records` is append-only by database trigger, so a plaintext password written into it can
  never be deleted — not by an admin, not by a migration. The one table that can never be corrected is
  the worst place to put an unvalidated string a human typed. **See `AC-101a-O`, which carries the
  ⚠️ on the mechanism.**
- **i18n:** `errors.auth.invalid_credentials`, `errors.auth.account_inactive`,
  `errors.auth.password_change_required`. The API returns a `messageKey`, **never prose**
  (`problem-details.ts`, slice 0). The screen's own keys belong to KAFF-101b.

  **`errors.auth.invalid_credentials` is the one refusal key for cases 1, 2, 4 and 5.** D-065 named
  `errors.identity.invalid_credentials`; **the Scrum Master's consistency call, recorded in D-065, is
  `errors.auth.*`** and it is what this story specifies. The namespaces are already divided along a
  line the ruled name crossed: **`errors.auth.*` is door and authorization refusals**
  (`not_authenticated`, `forbidden`, `not_assigned_to_project`, `role_cannot_log_in`)
  [Verified: 2026-08-23 @ `en.json` -> `errors.auth.role_cannot_log_in`], while **`errors.identity.*`
  is `User` entity validation** (`hr_role_requires_hr_department`, `password_hash_required`,
  `full_name_required`) [Verified: 2026-08-23 @ `en.json` -> `errors.identity.hr_role_requires_hr_department`].
  A sign-in refusal is a door refusal. **Reversible by Nabil at no cost — no code depends on the name
  yet.**

  **⚠️ Three of the four keys above do not exist in either catalogue today.** Only
  `errors.auth.role_cannot_log_in` does [Verified: 2026-08-23 @ `en.json` -> `errors.auth.role_cannot_log_in`,
  `ar.json` -> `errors.auth.role_cannot_log_in`]; `errors.auth.invalid_credentials`,
  `errors.auth.account_inactive` and `errors.auth.password_change_required` are absent from both
  [Verified: 2026-08-23 — absent from `en.json` and `ar.json`]. **Each must be added to `en.json` and
  `ar.json` together.** `TranslationCatalogueTests` fails the build the moment a `MessageKey` reaches
  the domain without both locales behind it
  [Verified: 2026-08-23 @ `TranslationCatalogueTests.cs` -> `Every_domain_error_key_has_an_arabic_and_an_english_translation`].
  **That is the guard working, not a mystery break.**

  **`errors.auth.role_cannot_log_in` stops being reachable from this door — and it is NOT dead.**
  D-065 case 5 replaces it here with the generic 401, but `SeparationOfDuties` still declares it
  [Verified: 2026-08-23 @ `SeparationOfDuties.cs` -> `RoleCannotLogIn`] and `PermissionEvaluator`
  still returns it on already-authenticated requests
  [Verified: 2026-08-23 @ `PermissionEvaluator.cs` -> `RoleCannotLogIn`]. **Nobody deletes that key on
  the strength of this ruling**, in either catalogue.

  **No key for a locked account is specified.** That is Q47 case 3 and it is open — see rule 14 and
  **Status, precisely** row 2. D-065 floats `account_locked`; it is named here only so that its
  absence is visibly deliberate.

## Acceptance criteria
**AC-101a-A — a valid credential opens a session and hands JavaScript nothing** *(fails if the rule is broken)*
Given an active user with a password set
When I post that username and password
Then the response body contains no token in any field
And a `Set-Cookie` header sets `__Host-kaff-auth` with `HttpOnly`, `Secure`, `SameSite=Strict`, path `/` and no `Domain`
And the next request carrying that cookie is authenticated

**AC-101a-B — a wrong password, an unknown username, a client and a subcontractor are indistinguishable** *(fails if the rule is broken)*
~~*"— wrong password, unknown user and locked account are indistinguishable"*~~ — the title's third case is struck 2026-08-23; see the note below.
Given an active user with a password set
When I post the correct username with a wrong password; then a username that does not exist; then a `Role.Client` credential; then a `Role.Subcontractor` username
Then all four responses are identical in status, body and `messageKey`, and none reveals which case it was
And the shared answer is **`401`** with `messageKey` **`errors.auth.invalid_credentials`** and no other distinguishing field
~~*"then the username of a locked account ... all three responses are identical"*~~ — **the locked-account case is struck from this criterion on 2026-08-23, and replaced by nothing.**

> 🟡 **Why struck, and why nothing replaces it. The locked account is Q47 case 3 and it is OPEN.**
> **The ID is not retired and the criterion is not weakened** — it lost one case and gained two,
> because D-065 ruled the `Role.Client` and `Role.Subcontractor` cases into the same set (cases 4
> and 5) alongside the wrong password and the unknown username (cases 1 and 2).
>
> **The locked case is the one Nabil ruled the other way**, to a distinct `423`, and that ruling is
> **flagged back to him** in D-065's own text, on this story's rule 14. **Leaving the clause standing
> would have this criterion command the 401** — one of the two shapes nobody may build yet — and a
> story that commands a shape the decision owner has questioned is the failure `process/agile.md`
> names: *"A story can command a defect."* **Striking it commands neither**, which is the required
> state.
>
> **What QA and Backend do until Nabil answers:** assert the four cases above; **write nothing that
> asserts a locked account's status, body or `messageKey`, in either shape.** `AC-101a-C` is
> unaffected — it asserts that the sixth attempt *fails*, not what it says. See **Status, precisely**
> row 2, and rule 14, whose text is deliberately unchanged.

**AC-101a-C — five failures lock the account for fifteen minutes** *(fails if the rule is broken)*
Given an active user
When five consecutive wrong passwords are posted
Then the sixth attempt fails **even with the correct password**
And after fifteen minutes the correct password succeeds
And an audit record records the lockout

**AC-101a-D — a success resets the counter**
Given four consecutive failures
When the correct password is posted
Then sign-in succeeds, and five further failures are required before the account locks

**AC-101a-E — eight characters is enough, and nothing more is demanded** *(fails if the rule is broken)*
Given a password of exactly 8 lower-case letters, with no digit and no symbol
When it is set and then used to sign in
Then both succeed — no complexity rule refuses it

**AC-101a-F — a temporary password has exactly one destination**
Given a user whose password was set by the Owner and never changed
When they sign in and then call any endpoint other than the change-password endpoint
Then the request is refused with `errors.auth.password_change_required`

**AC-101a-G — a subcontractor cannot sign in, and the door says nothing that reveals it** *(fails if the rule is broken)*
Given a `User` with `Role.Subcontractor`
When a sign-in is attempted with that username, with any password or none
Then no session is minted and no `Set-Cookie` header is returned
And the response is **`401`** with `messageKey` **`errors.auth.invalid_credentials`**, byte-for-byte the body `AC-101a-B` returns for an unknown username, in the same time envelope
And **no field anywhere in the response distinguishes it** — not the status, not the `messageKey`, not a detail, not a header
And the attempt is audited

> ✅ **RULED 2026-08-23 — `decisions.md` D-065, case 5**, which closes **D-063 A-02**. Nabil, as Owner
> and Architect: *"If we return a specific `errors.auth.role_cannot_log_in`, we are explicitly telling
> the attacker: 'This account exists and belongs to a subcontractor.' That is a security breach. The
> door must treat a subcontractor exactly the same way it treats a non-existent user."*
>
> **The struck clause stays struck; the ruling replaces it, it does not restore it.**
> ~~*"Then it is refused with `errors.auth.role_cannot_log_in`"*~~ — **struck 2026-08-23. The ID is
> not retired and its letter is not free**; the criterion asserted the refusal throughout, and what it
> lost on 2026-08-23 was the status and the `messageKey`. Those are what D-065 has now supplied, and
> they are the opposite of what was struck.
>
> **`errors.auth.role_cannot_log_in` is not deleted.** It stops being reachable from this door;
> `SeparationOfDuties` still uses it [Verified: 2026-08-23 @ `SeparationOfDuties.cs` -> `RoleCannotLogIn`].
> See the i18n bullet.
>
> **The reasoning is kept below rather than deleted, because the ruling agrees with it** and the next
> session to read this criterion should see why a distinct message was refused, not merely that it
> was.
>
> **Why the struck clause was a leak and not merely an inconsistency.** A subcontractor **can hold no
> credential at all** — the entity refuses it in the private `StorePasswordHash`
> [Verified: 2026-08-23 @ `User.cs` -> `StorePasswordHash`] and the database refuses it in
> `role <> 'Subcontractor' OR password_hash IS NULL`
> [Verified: 2026-08-23 @ `IdentityConfigurations.cs` -> `ck_users_subcontractor_cannot_log_in`].
> **There is no password to check**, so a distinct refusal can only be produced **from the username
> alone** — it announces *"this username exists and belongs to a subcontractor"* to anybody who types
> it. That is exactly what `AC-101a-B` exists to prevent, in the same story. The key is real
> [Verified: 2026-08-23 @ `en.json` -> `errors.auth.role_cannot_log_in`], which is why nothing caught
> it; and its existing mechanism cannot serve here anyway — `RoleCannotLogIn` is `Error.Forbidden`
> [Verified: 2026-08-23 @ `SeparationOfDuties.cs` -> `RoleCannotLogIn`], authorization on an
> already-authenticated request, so it would return a **403** where `AC-101a-B` demands an identical
> status.
>
> ~~**This is not resolved by D-063 §1, and it must not be resolved by whoever writes the handler.**~~
> **Resolved 2026-08-23, and not by the handler's author — by D-065.** D-063 §1 ruled only on the
> **`Role.Client`** credential and made it the **fourth** member of the indistinguishable set; the
> Architect saw this case and **routed it rather than ruling it** — *"the architectural position is
> the same as §1 ... but `AC-101a-G` is the BA's text and Q47 already owns the underlying question"*
> (**D-063 A-02**, under *Routed, not settled*). It became **Q47's fifth case**, and **D-065 rules it:
> the same generic 401, making the subcontractor the fifth member of that one set.** The route worked
> — the question went to the decision owner and came back answered, which is why it was handed back
> rather than settled here.
>
> **The register row records the closure** [Verified: 2026-08-23 @ `questions-for-karim.md` -> `Q47`].
> **`AC-101a-G` is certifiable.**

**AC-101a-H — a deactivated user cannot sign in, and their open session dies** *(fails if the rule is broken)*
Given a signed-in Finance user holding a live session cookie
When the Owner deactivates them
Then their very next request is refused, and a fresh sign-in with the old password is refused too

**AC-101a-I — a password change kills every other session** *(fails if the rule is broken)*
Given the same user signed in on two devices
When they change their password on one
Then the session on the other device is refused on its next request

**AC-101a-J — thirty idle minutes ends the session** *(fails if the rule is broken)*
Given a session with no requests for 30 minutes
When a request is made
Then it is refused
And given a session used every 20 minutes for two hours, it is still valid

**AC-101a-K — the session grants nothing by itself** *(fails if the rule is broken)*
Given a valid session for a Site Engineer assigned to no project
When they call a `ProjectScoped` endpoint for any project
Then the request is refused with `errors.auth.not_assigned_to_project`

**AC-101a-L — the password never leaves the database**
Given any successful or failed sign-in
When the response body, the application log and the audit record are inspected
Then none contains the password, the hash, or the security stamp

**AC-101a-M — the browser store stays empty** *(fails if the rule is broken)*
Given a completed sign-in in the browser
When `localStorage` and `sessionStorage` are inspected
Then neither contains a token, and `AuthService` has no token field to put one in

**AC-101a-N — a stale security stamp is refused** *(fails if the rule is broken — this is the mechanism, not a restatement of AC-101a-H/AC-101a-I)*
Given a valid, unexpired token whose `SecurityStamp` claim no longer matches the stored `User.SecurityStamp`
When any authorized endpoint is called with it
Then the request is refused
And given a token carrying **no** `SecurityStamp` claim at all, it is refused too — a revocation check with a bypass is worse than an absent one (D-051 N5)

**AC-101a-O — a failed sign-in against an unknown username is recorded, and what was typed is not** *(fails if the rule is broken)*
Given a sign-in posted against a username that does not exist
When the audit record written for that attempt is read
Then it records **"Failed sign-in — Unknown user"**, the **IP address** the request connected from, and the **timestamp**
And it carries **`EntityType = "User"` and no subject** — a sign-in was attempted against a `User` that does not exist
And **nowhere in the record — in any column, in any JSON payload, in any form — does the string the caller typed appear**
And the response to the caller is unchanged: the same 401, body and `messageKey` as `AC-101a-B`

> 🟡 **This criterion is written against a mechanism that is DECIDED and NOT YET BUILT. Do not read it
> as describing today's code.** Appended 2026-08-23 under D-062 §3 (Karim's ruling) and D-063 §2/§3
> (the Architect's consequences). Marked explicitly because a criterion that assumes an unbuilt
> mechanism is finding **V-01** repeating, which is what this whole chain started from.
>
> **Three things must land before `AC-101a-O` can pass, and none of them exists today:**
>
> 1. **The IP column.** `AuditRecord` has no IP field — it carries `OccurredAt`, `Action`,
>    `EntityType`, `EntityId`, `ActorUserId`, `ActorDisplayName`, `ActorRole`, `BeforeJson`,
>    `AfterJson`, `ChangedProperties`, `Reason`, `CorrelationId`, `ProjectId`, `RequestPath`,
>    `EventType` and, since KAFF-116, `GrantPath`
>    [Verified: 2026-08-23 @ `AuditRecord.cs` -> `EntityId`]. **D-063 §2** decides it: one **nullable**
>    column on **every** record, PostgreSQL `inet` from `System.Net.IPAddress`, written by
>    `AuditCorrelationMiddleware` beside `RequestPath` and never by a handler, sourced from
>    `HttpContext.Connection.RemoteIpAddress` and **never `X-Forwarded-For`** (a caller-supplied string
>    in a table nobody can correct). **N-19 applies in full: it must land before this story ships**,
>    because a column never written cannot be backfilled into an append-only table.
> 2. **A subject that can be absent.** `AuditRecord.EntityId` is a non-nullable `Guid`
>    [Verified: 2026-08-23 @ `AuditRecord.cs` -> `EntityId`] and `AuditContext.Record` **throws** on
>    `Guid.Empty` — *"An audited event must name its subject"*
>    [Verified: 2026-08-23 @ `AuditContext.cs` -> `Record`]. **D-063 §3** decides it: `EntityId` and
>    `AuditEvent.SubjectId` become `Guid?`, **`EntityType` stays required**, and a **new** check
>    constraint `ck_audit_records_entity_change_has_subject` keeps the database saying what it said
>    before for every row that is not an event. **A sentinel was rejected** — `Guid.Empty` would
>    require deleting the guard that catches a handler which forgot an id. This may land in this
>    story's own migration.
> 3. **The `AuditEventKind` member.** D-063 §3 leaves the vocabulary **to this story** deliberately —
>    *"the nullability is the part that cannot be added casually; the enum is the part that can"* — and
>    the member this case needs is the one D-063 §3 names in its own worked constraint,
>    **`SignInFailedUnknownUser`**. **Not yet drafted:** whether the rule-16 client refusal needs a
>    second member. D-063 §3 says it *"arguably implies another"* and stops there; it is left undrafted
>    rather than guessed, and it is coupled to `AC-101a-G`'s open shape.
>
> **The retention of the IP is Karim's and is open — `Q54`** [Verified: 2026-08-23 @
> `questions-for-karim.md` -> `Q54`]. It does **not** block this criterion: Karim has already ruled the
> address is captured; Q54 is only what happens to it afterwards.

## Not in this story
The login screen, and where each role lands after signing in — **KAFF-101b**, now `Ready`.
Setting a password (KAFF-103). Resetting one (KAFF-104). Sign-out (KAFF-102), which under D-051 (N5)
is clearing the cookie and nothing more. `/api/auth/me` (KAFF-105a, KAFF-105b). A session table:
**rejected in D-051 (N5)** — see rule 11b. Refresh tokens: **rejected in D-050** for a first-party
SPA on one origin, and the header path is already open if a mobile app or third-party client ever
needs bearer tokens.

## The dependency on KAFF-100 is no longer soft — it is satisfied
KAFF-100 was BLOCKED on Q31 and the backlog carried a soft-dependency argument for building this
story anyway. **Q31 is answered (D-051) and KAFF-100 is `Ready`**, so the argument is retired rather
than relied on. Slice-1 fixtures still create `User` rows directly and the Api harness still issues
identities directly (`TestAuthHandler`, slice 0), so the build order between the two is a
scheduling choice — but **the demo's first step now exists**, which it did not before.

## Readiness waiver — signed, and it does not answer the question
`process/agile.md`'s Definition of Ready says an uncited rule is a question, not a story. **Rules 13
and 14 are uncited and are built anyway, under a named waiver** (`decisions.md` D-055 §4, **superseded by D-062 §1 — see below**):

> *"I accept the six stories containing uncited rules to pass them through the Definition of Ready so
> the sprint does not stall. I take this on my own responsibility as the Architect."*

> **✅ COUNTERSIGNED FOR SEVEN — Nabil, 2026-08-22. `decisions.md` D-062 §1.**
>
> *"Signed and approved. I officially approve adding KAFF-106 as the seventh story under the waiver.
> The numerical discrepancy flagged by the Scrum Master is accurate, and the story is essential to
> complete the creation flow for the first user (Owner). This closes the discrepancy and allows the
> build to proceed."*
>
> **Both halves changed.** The count is **seven**, not six — KAFF-100, 101a, 103, 106, 110, 112, 114.
> The signatory is **Nabil**, not the Architect. An Architect's waiver is one agent accepting risk on
> rules Karim has not answered; Nabil is the decision owner's proxy, which is the right weight for
> seven committed stories to be built on.
>
> **It still answers nothing.** Q45–Q51 stay open. The waiver permits the build; the questions remain
> Karim's.
> — **the Architect, signed, 2026-08-22**

| Waived rule | Open question |
|---|---|
| ~~**Rules 13 and 14** — one refusal for a wrong password, an unknown username and a locked account. The source column reads *"§9 — derived"*. It is a security convention, correctly applied, and **it is not something Karim said**~~ | ~~**Q47**, open, for Karim~~ |
| **✅ RULE 13 IS OFF THE WAIVER, 2026-08-23.** Q47 cases 1 and 2 are ruled — **D-065**. Rule 13 now cites a D-number and the waiver no longer carries it | **Closed. D-065** |
| **🟡 RULE 14 IS NOT WAIVED EITHER, AND IT IS NOT RULED.** Q47 case 3 — the locked account — is **open with Nabil** (D-065's first 🟡 block). A waiver permits a build; there is nothing here to permit, because **the case is built neither way** | **Q47 case 3**, open, **with Nabil** — not Karim |

**What the waiver did and what replaced it.** The waiver let this story be built while rules 13 and 14
were uncited. **Rule 13 no longer needs it** — D-065 cites it. **Rule 14 does not use it** — the
instruction on case 3 is not *"build it under a waiver"* but *"do not build it at all yet"*, which is a
stronger constraint than the waiver ever was. **The waiver is spent for this story.** The rest of the
seven stories it covers are untouched by D-065 — checked, not assumed.

## Questions for Karim
- **Q28** — when somebody gets a password wrong five times, should the lock be on the account, or on
  the account **and** the device they are trying from? *(Per account alone, anyone who knows a site
  engineer's username can lock him out for fifteen minutes at a time, indefinitely, from anywhere.)*
  **This does not block the story.** The ruling as given is buildable; the answer would tighten it
  rather than change its shape. It is raised because the ruling has a live consequence Karim was not
  shown.
- **Q47** — **CLOSED for four of its five cases, 2026-08-23, `decisions.md` D-065.** Cases ① wrong
  password, ② unknown username, ④ `Role.Client` at the staff door and ⑤ `Role.Subcontractor` all
  produce **one generic `401`**, identical in status, body and `messageKey`. Rules 13 and 16 and
  criteria `AC-101a-B` and `AC-101a-G` carry the citation.
  **🟡 Case ③ — the locked account — is OPEN, and it is with Nabil, not Karim.** He ruled a distinct
  `423`; a 423 only exists when the username does, so it announces the thing cases ② and ⑤ were ruled
  to hide, and five failed attempts manufacture it for any username on demand. **A narrowing has been
  put to him — return the 423 only when the submitted credentials were otherwise correct**, so only
  the password-holder ever sees it. **Until he answers, rule 14 stands unruled and no criterion is
  written for the case in either shape.** See **Status, precisely** row 2.
- **N9** *(Architect, not Karim)* — rule 16 still says a `Role.Client` credential *"authenticates"*
  here, which was written before D-051 (Q33) made the portal a separate host. Accepting it mints a
  valid session cookie on the staff origin for somebody with no business holding one; it reaches
  nothing today only because of what the catalogue happens to contain. **Should be answered before
  this story is built**, since it decides one of its rules. *(From `ux/questions.md` Q-UX-20.)*
