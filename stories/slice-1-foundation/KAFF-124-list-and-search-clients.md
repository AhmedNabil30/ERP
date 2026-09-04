# KAFF-124 · Find a client by name or phone

**Slice:** 1 · **Epic:** Foundation · **Points:** 2 · **Status:** **BUILT 2026-09-04, two criteria held — not accepted.**
`AC-124-A` … `AC-124-G` discharged, and `AC-124-A`, the wildcard escaping, `AC-124-E` and the permission gate were each
watched failing under a mutation of their own mechanism (decisions.md **D-110**). **`AC-124-H` HALF-HELD** — the `200`
with an empty array is pinned; *displaying* `clients.list.empty` needs a screen. **`AC-124-I` HELD** — Arabic/RTL at
mobile width, and **there is no client list screen**: Frontend's, the same hole as `AC-119-L` and `AC-121-I`.
**Not independently verified** — built and self-reported.
**Spec:** §2 (**amended**), §3, §12 · **Decisions:** D-044 (ruling 4), D-035, **D-049 (rulings 7, 8)**
**Depends on:** KAFF-119

## Story
As Marketing, I search the client list by name or phone before I add anybody, because the cheapest
place to prevent a duplicate is the moment before one is created.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | Search matches on name, on the generated **code**, and on the **normalised** phone, so a number typed in any format finds the client. `Client` stores the phone twice — `PhoneEntered` for display, `PhoneNormalised` for lookup — and this rule searches the second [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `PhoneNormalised`, `PhoneEntered`] | §2 · D-049 ruling 7 · slice 0 `PhoneNumber` |
| 1b | **A phone search can now legitimately return more than one client**, because duplicates are permitted. The result is a list, never "the client with this number" | D-049 ruling 8 · §2 amendment |
| 2 | The default list excludes archived clients; they remain findable through an explicit filter, because §3 requires a reopened opportunity to reach the original client | §2, §3 |
| 3 | `ClientManage`, `CompanyWide`, Marketing and Owner | §2 · D-044 ruling 4 |
| 4 | A `Role.Client` user cannot reach this endpoint under any circumstances. It returns every client in Kaff, and §12 is absolute: the client must never see *"any other client's data"* | §12 · D-035 |
| 5 | The list carries no money — no balance, no contract value, no total billed. There is none on the entity to project [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `class Client`], so this rule is about what the projection must not *join* in | §6.1 · CLAUDE.md |
| 6 | An empty result shows an explicit empty state, never a blank area and never a phantom row | §4.5 (*"Empty BOQ shows an explicit empty state. Never phantom pre-filled rows"*) — the same principle, applied consistently |

## Permissions, money, audit, i18n
- **Permissions:** `ClientManage`, `CompanyWide`, Marketing and Owner.
- **Money:** shows no money.
- **Audit:** none. It is a read.
- **i18n:** `clients.list.title`, `clients.search.placeholder`, `clients.list.empty`,
  `clients.filter.include_archived`, reusing `clients.kind.*` and `clients.status.*`.

## Acceptance criteria
**AC-124-A — a phone in any format finds the client** *(fails if the rule is broken)*
Given a client stored as `01001234567`
When I search `+20 100 123 4567`, then `0020 100 1234567`, then `01001234567`
Then all three return that client

**AC-124-B — two clients with one number both come back** *(fails if the rule is broken)*
Given two clients sharing `01001234567`, saved past a duplicate warning (D-049 ruling 8)
When that number is searched
Then both are returned, and neither is silently preferred over the other

**AC-124-C — the generated code finds the client**
Given a client with code `C-10001`
When `C-10001` is searched, and again as `c-10001`
Then both return that client

**AC-124-D — partial name search works in Arabic**
Given a client whose name is in Arabic
When I search a substring of it
Then the client is returned

**AC-124-E — archived clients are hidden by default and findable on request**
Given one active and one archived client
When I search with the default filter, then with archived included
Then the first returns one result and the second returns two

**AC-124-F — a portal client cannot list clients** *(fails if the rule is broken)*
Given I am a `Role.Client` user
When I call the client list, with any filter and any search term
Then I am refused with 403, and no client name appears in the response body

**AC-124-G — no money in the payload** *(fails if the rule is broken)*
Given the list response contract
When it is inspected
Then it carries no balance, contract value, total billed or any other money-shaped field

**AC-124-H — an empty search says so**
Given a search matching nothing
When the results render
Then `clients.list.empty` is displayed

**AC-124-I — Arabic, RTL, at mobile width**
Given the list at 390px in Arabic
When it renders
Then direction is RTL, Latin phone numbers inside Arabic rows are bidi-isolated, and there is no horizontal overflow

## Not in this story
The duplicate-check interaction on the create form — **answered by D-049 ruling 8** and specified in
KAFF-119. Client history: projects,
extracts and collections per client, none of which exist before slices 3–5.

## Questions for Karim
None that block this story.
