# KAFF-126 · The client screens

**Slice:** 1 · **Epic:** Foundation · **Points:** 8 (**proposed**) · **Status:** **BUILT 2026-09-04 — not accepted.** decisions.md **D-113**.
`AC-126-A` … `AC-126-K` discharged, verified by driving Chromium at 390px in Arabic against a seeded stack — `dir=rtl`,
**0px horizontal overflow** on all three screens, the three filter chips server-side, and the duplicate warning firing on
blur naming both clients that share the number. **`AC-126-L` half-held** — the guard and the server's 403 are both in
place and were checked; the *rendered* S-016 Forbidden surface for a role reaching `/clients` by URL was not driven.
**⚠️ No E2E test was added** — everything above is evidence from this session, not a check that runs tomorrow. **Owed.**
**Not independently verified.**
**Spec:** §2 (**amended**), §3, §12 · **Decisions:** D-049 (rulings 7, 8, 9), **D-107**, **D-109**, **D-110**, **D-111**
**UX:** `ux/slice-1-flows.md` -> `S-011 · Client list`, `S-012 · Create client, and S-013 · the duplicate-phone warning`, `S-014 · Client detail and edit`
**Depends on:** KAFF-119, KAFF-121, KAFF-123, KAFF-124 (~~three exist today~~ **all four merged 2026-09-04**), KAFF-125 (the staff shell)

## Why this story exists

**Cut 2026-09-04 by the Scrum Master, on the precedent Nabil set on 2026-09-02:**

> *"The 'Staff Shell' dependency must be split into two distinct deliverables … **You cannot discharge
> a UI rendering dependency with a JSON response.**"*

That ruling produced `KAFF-125` out of `KAFF-105b`. **The same thing has now happened three times over
in the Client master.** KAFF-119, KAFF-121 and KAFF-124 are all built and pushed, and all three carry
an undischarged criterion that says *"Arabic, RTL, at mobile width"* — because there is no client
screen for any of them. Three delivered stories cannot be accepted, and the criteria were sitting on
stories nobody was going to open again.

**This story is where they live now.** They are **moved, not copied** — each origin story's criterion
is struck and points here, so there is exactly one place each is discharged.

## The criteria this story inherits

| From | Criterion | Was |
|---|---|---|
| **KAFF-119** | `AC-119-L` — the create form: Arabic, RTL, 390px | HELD since 2026-09-04 |
| **KAFF-121** | `AC-121-I` — the edit form and the duplicate warning: Arabic, RTL, 390px | HELD since 2026-09-04 |
| **KAFF-124** | `AC-124-I` — the list: Arabic, RTL, 390px | HELD since 2026-09-04 |
| **KAFF-124** | `AC-124-H`, **render half only** — an empty search displays `clients.empty.*`. The API half (a `200` carrying an empty array, never a `404`, never a null) is discharged and stays there | HALF-HELD since 2026-09-04 |

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | **The server decides, always.** Hiding the Clients nav item from a role that lacks `ClientManage` is convenience; every one of these screens is already refused server-side and must behave correctly when it is | `CLAUDE.md` · spec.md §9 |
| 2 | **Standalone, signals, signal forms, zoneless, `@if`/`@for`, `inject()`.** No NgModule, no `BehaviorSubject` for component state, no Zone.js, no `*ngIf` | `CLAUDE.md` |
| 3 | **RTL is the primary direction, not a mirror.** Logical properties (`margin-inline-start`), never `margin-left` | `CLAUDE.md` · `ux/rtl-and-i18n.md` |
| 4 | **No hardcoded user-facing strings.** Everything through i18n, in both catalogues, from the first commit | `CLAUDE.md` |
| 5 | **The phone is the matching key and it is the first field on the create form**, with `clients.hint.phone_is_the_key` beneath it | `ux/slice-1-flows.md` -> `S-012` |
| 6 | **The duplicate check fires on blur of the phone field**, against `POST /api/clients/phone-check`, and its result is a **warning that names the client** — never a refusal, never a `Problem` | D-107 §2 · `ux/components.md` §13 · S-013 |
| 7 | **The code is rendered read-only, never as a disabled input**, with `clients.field.code.not_editable` | D-049 ruling 7 · KAFF-121 rule 5 · S-014 |
| 8 | **Phone numbers, codes and emails inside Arabic text are `<bdi>`-isolated.** A Latin run inside an Arabic line reorders without it | `ux/rtl-and-i18n.md` · S-011 |
| 9 | **The search query is sent raw and normalised by the server.** Do not reimplement `PhoneNumber.Normalise` in TypeScript — a second implementation is a matcher that will disagree with the first | S-011, in as many words |
| 10 | **Notes are labelled internal** (`clients.hint.notes_internal`) and appear on no client-facing surface. The portal is slice 8, built by somebody who will not read this form | spec.md §12 · KAFF-121 rule 8 |
| 11 | **History is an empty state, not an invented list.** The projects and opportunities that make it up do not exist until slice 4 | S-014 · spec.md §2 |
| 12 | **No withholding field anywhere on these screens.** It left the client record on 2026-08-21 and is the contract's, from slice 4 | D-049 rulings 9, 10 · KAFF-416 |

## The API this story consumes — all of it already built

| Screen | Endpoint | Shipped |
|---|---|---|
| S-011 list and search | `GET /api/clients?search=&status=active\|archived\|all` | KAFF-124, `5e8f1ad` |
| S-012 create | `POST /api/clients` | KAFF-119, `01c7b3a` |
| S-013 warning | `POST /api/clients/phone-check` | KAFF-119, `01c7b3a` |
| S-014 edit | `PUT /api/clients/{clientId}` | KAFF-121, `1684cb9` |
| S-014 archive | `POST /api/clients/{clientId}/archive` | KAFF-123, `5a9d6d9` |
| S-014 load | `GET /api/clients/{clientId}` | **Added by this story, D-113 §1** |

> ⚠️ **Both lines above were true when this story was cut and both changed the same day.** KAFF-123
> landed, so the archive control had an endpoint by the time it was wired. And **the table was one
> endpoint short**: `PUT /api/clients/{id}` takes nine members, the list row carries six, and S-014 is
> reachable by URL — so nothing could load the record the edit saves. `GET /api/clients/{clientId}`
> was added while building this story (decisions.md D-113 §1). **The pipeline is what surfaced it:
> the gap was invisible until somebody wrote the screen.**

## Acceptance criteria

**AC-126-A — the list renders Arabic RTL at 390px** *(fails if the rule is broken)* — **inherits `AC-124-I`**
Given the client list at 390px in Arabic, with clients on file
When it renders
Then direction is RTL, codes and phone numbers are `<bdi>`-isolated inside Arabic rows, and there is no horizontal overflow

**AC-126-B — the three filter chips are the three the server knows** *(fails if the rule is broken)*
Given the list
When All, Active and Archived are each selected
Then each sends `status=all`, `status=active` and `status=archived` respectively
And the filtering is the server's — no archived client is hidden client-side from a list the server already filtered

**AC-126-C — an empty search says so, and says which kind of empty** — **inherits `AC-124-H`'s render half**
Given no clients at all, then a search matching none
When each renders
Then the first shows `clients.empty.title` / `.body` and the second shows `clients.empty.filtered.title` / `.body`
And neither shows a blank area and neither shows a phantom row

**AC-126-D — the create form renders Arabic RTL at 390px** *(fails if the rule is broken)* — **inherits `AC-119-L`**
Given the create form at 390px in Arabic
When it renders
Then direction is RTL, the phone field is first and `dir="ltr"` with `inputmode="tel"`, no string is hardcoded, and there is no horizontal overflow

**AC-126-E — the duplicate warning names the client and does not block** *(fails if the rule is broken)*
Given a phone already on file
When the operator leaves the phone field
Then `POST /api/clients/phone-check` runs and the warning names the matched client, with its code, and says so if it is archived
And the save is still available
And proceeding sends `acknowledgedDuplicatePhone: true` and succeeds

**AC-126-F — a 409 from the server reopens the warning rather than reading as a failure** *(fails if the rule is broken)*
Given a client is created on the same number between the check and the save
When `POST /api/clients` answers `409 errors.master.duplicate_phone_not_acknowledged`
Then the warning is shown again with the current matches, and the operator can proceed
And it is **not** rendered as S-016's "Failed" mode — a 409 here is a question, not an error

**AC-126-G — the edit form renders Arabic RTL at 390px, and the code is not an input** *(fails if the rule is broken)* — **inherits `AC-121-I`**
Given the edit form at 390px in Arabic
When it renders
Then direction is RTL, no horizontal overflow, and the code is rendered as `<bdi>`-isolated read-only text with `clients.field.code.not_editable` — **not as a disabled input**

**AC-126-H — kind and tax registration are changed as a pair, not submitted to be rejected**
Given a corporate client carrying a tax registration number
When the operator changes the kind to Individual
Then the form clears the registration number and confirms, rather than submitting a combination the server refuses (D-109 §1)
And if the server refuses anyway, `errors.master.individual_does_not_withhold` is shown against the field

**AC-126-I — internal notes are labelled and never leave this surface** *(fails if the rule is broken)*
Given a client with notes
When the edit form renders
Then the notes carry `clients.hint.notes_internal`
And no client-facing surface in the application renders them

**AC-126-J — history is an empty state**
Given any client in slice 1
When the detail screen renders
Then history shows `clients.history.empty` and no invented tiles

**AC-126-K — every string is in both catalogues** *(fails if the rule is broken)*
Given the screens
When the i18n catalogues are compared
Then every key these screens use exists in `ar.json` and `en.json`, and no key is added that no template uses

**AC-126-L — a role without `ClientManage` reaches nothing** *(fails if the rule is broken)*
Given Finance, then a portal `Role.Client` user
When each navigates directly to the client routes by URL
Then each sees S-016's Forbidden mode, in their language, with the app chrome intact
And the nav item is absent — **which is convenience, and the route guard and the server's 403 are the control**

## Not in this story
The archive control's behaviour beyond wiring — that is **KAFF-123**, and its endpoint does not exist
yet. Client history's content (slice 4). Merging duplicates — no merge exists. The portal's view of a
client (slice 8).

## Questions for Karim
None that block. **Q39** — what is offered when the matched client is archived — touches rule 6 and is
the same open edge KAFF-119 and KAFF-121 both carry. The warning **says** the match is archived
today; whether it should also offer to unarchive is Q39, and there is no unarchive path in slice 1 at
all.
