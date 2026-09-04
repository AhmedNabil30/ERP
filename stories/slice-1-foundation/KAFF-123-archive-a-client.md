# KAFF-123 · Archive a client

**Slice:** 1 · **Epic:** Foundation · **Points:** 2 · **Status:** **BUILT 2026-09-04 — not accepted.** decisions.md **D-112**.
`AC-123-A` … `AC-123-E` all discharged; three mechanisms watched failing, including `AC-123-D`'s absence, which was made
to fail on purpose by adding a throwaway `MapDelete`. The archive **control** on S-014 is `KAFF-126`'s (`AC-126-*`).
**Not independently verified** — built and self-reported.
**Spec:** §2 (**amended**), §3 · **Decisions:** **D-049 (ruling 8)**
**Depends on:** KAFF-119

## Story
As Marketing, I take a client off the working list without losing them, so that the list stays usable
and the history stays intact.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | A client is **archived, never deleted.** §2 requires full history and §3 requires a reopened opportunity to attach to the same client — both are impossible if the row can disappear. `Archive` flips `IsActive`; there is no delete method on the entity [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `Archive`, `IsActive`] | §2, §3 · slice 0 `Client.Archive` |
| 2 | Archiving does not take the phone number out of the duplicate check. A "new" client with that number still **matches** the archived one, and the operator is told so and told it is archived | §2 amendment · D-049 ruling 8 · KAFF-119 rule 6 |
| 2b | **The match no longer refuses the save.** Karim ruled the deduplication is a warning, not a refusal, and the unique index was dropped. So §3's *"never create a duplicate client"* is no longer held by the database across time — it is held by an operator reading a warning. **That is a real reduction and it is Karim's, made knowingly** | §2 amendment · D-049 ruling 8 |
| 3 | Archiving twice is refused — `errors.master.already_archived` [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `Archive`] | slice 0 `Client.Archive` |
| 4 | `ClientManage`, Marketing and Owner | §2 · D-044 ruling 4 |
| 5 | Archiving changes no money and settles no account. Whether a client with an open project may be archived at all is not decidable in slice 1: projects and postings do not exist yet, and §11 makes closure an accounting condition — *"A project closes only when all accounts are settled"*. **Slice 4 must revisit this,** and the note is here so the next session does not assume it was considered and allowed | §11 — raised, not resolved |

## Permissions, money, audit, i18n
- **Permissions:** `ClientManage`, `CompanyWide`, Marketing and Owner.
- **Money:** moves no money.
- **Audit:** `Modified` on `Client`, `ChangedProperties` naming `IsActive`, actor named.
- **i18n:** `clients.archive`, `clients.archive.confirm`, `clients.status.active`,
  `clients.status.archived`, and the existing `errors.master.already_archived`,
  `errors.master.not_archived`.

## Acceptance criteria
**AC-123-A — an archived client leaves the working list but not the database**
Given an active client
When Marketing archives them
Then they no longer appear in the default list, the row still exists, and an audit record names the actor

**AC-123-B — the archived client still surfaces in the duplicate check** *(fails if the rule is broken)*
Given an archived client with phone `01001234567`
When a new client is registered with the same phone
Then a warning fires naming that client **and stating that it is archived**
And the save is not blocked (D-049 ruling 8)

*This criterion previously read "then it is refused as a duplicate". Karim reversed that on
2026-08-21 and the constraint has been dropped from the database. The value of the check is now
entirely in the operator seeing the match, which is why AC-123-B tests the wording and the archived flag
rather than a status code.*

**AC-123-C — archiving twice is refused**
Given an archived client
When they are archived again
Then it is refused with `errors.master.already_archived`

**AC-123-D — no delete exists** *(fails if the rule is broken)*
Given any client
When the API surface is inspected
Then there is no endpoint that deletes one

**AC-123-E — nobody outside Marketing and the Owner may archive**
Given I am Finance, then HR, then a portal Client
When each attempts to archive a client
Then each is refused with 403

## Not in this story
Unarchiving. `Client` still has no reactivate method [Verified: 2026-08-22 @
`src/Domain/MasterData/Client.cs` -> `SetContactDetails`, `SetTaxRegistration`, `Archive` — the three public mutators, none of them an
unarchive] and `errors.master.not_archived` exists with nothing raising it [Verified: 2026-08-22 @
`src/Domain/MasterData/MasterDataErrors.cs` -> `NotArchived`, `src/Web/public/locales/en.json` -> `errors.master.not_archived`,
`src/Web/public/locales/ar.json` -> `errors.master.not_archived` — the error and both translations exist; no call site does] — if Marketing needs to bring a client back, it is a small story, but the symmetry
should be built deliberately rather than assumed. Blocking archival on open projects or unsettled
accounts (rule 5, slice 4).

## Questions for Karim
None that block. **Q39** — when the duplicate warning names an **archived** client, should the system
offer to bring that client back? *(Merged from `ux/questions.md` Q-UX-4, sub-question 1.)* There is no
unarchive path in slice 1 at all, so nothing can be offered yet either way.
