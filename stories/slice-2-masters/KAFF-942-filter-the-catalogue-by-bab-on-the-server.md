# KAFF-942 · Filter the catalogue by باب on the server

<!-- kaff id=KAFF-942 slice=2 points=2 state=BUILT -->

**Slice:** 2 (Masters) · **Epic:** Catalogue · **Points:** 2.
**Owner:** Backend, then Frontend.
**Spec:** `spec.md` §4.1.
**Raised by:** the catalogue list build, 2026-10-06, and ruled by Nabil the same day.

## Story

`GET /api/catalogue-items` takes a status and a search term but no باب. The catalogue screen offers a
باب filter anyway, so the built screen narrows the list **in memory** after fetching every row
[Verified: 2026-10-06 — the build note on the catalogue list screen names this as a place the design
assumed an API that does not exist].

At 612 items that works. It is still the wrong place for the filter: the server already filters on
status and search in the same query, and a client-side narrowing of a list that grows every year is a
cost that only goes up. Nabil ruled it moves to the server.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | A caller may ask for one باب, and gets only that باب's items | Nabil, 2026-10-06 |
| 2 | The باب filter composes with the existing status and search filters rather than replacing them | `spec.md` §4.1 |
| 3 | Omitting the باب returns every باب, exactly as today | backward compatibility |
| 4 | An unknown باب id is a refusal, not an empty list — an empty list reads as "this trade has no items" and that is a different fact | `CLAUDE.md`, on refusals |

## Acceptance criteria

**AC-942-A — one باب's items**
Given a catalogue holding items across several أبواب
When the list is requested for one باب
Then only that باب's items are returned, and the count reflects that
*Rule: 1.*

**AC-942-B — it composes with the other filters**
Given items in one باب, some archived and some active, with descriptions that differ
When the list is requested for that باب with `status=archived` and a search term
Then all three narrow the same result together
*Rule: 2.*

**AC-942-C — omitting it changes nothing**
Given any catalogue
When the list is requested with no باب
Then the response is identical to today's
*Rule: 3.*

**AC-942-D — an unknown باب is refused**
Given a باب id that does not exist
When the list is requested for it
Then the call is refused with a translated message, not answered with an empty list
*Rule: 4.*

**AC-942-E — the screen stops narrowing in memory**
Given the catalogue screen with a باب chosen
When the list loads
Then the request carries the باب and the client does no filtering of its own; the باب also round-trips through the URL as the other filters already do
*Rule: 1.*

## Not in this story

Pagination. Nabil ruled the client-side load-more stays as built. Any change to what the list returns
per row. The باب tree screen itself.

## Questions for Karim

None — a query filter, no business rule beyond the four above.

## Build note (2026-10-06)

**Parameter:** `GET /api/catalogue-items?bab=<guid>`, a plain `string?` on the handler like `status` and `search`. Empty or omitted means every باب. A value that is not a GUID, or a GUID naming no باب, is refused `400` with `errors.master.catalogue_item_list_bab_unknown` (new `MasterDataErrors.CatalogueItemListBabUnknown`, translated in both catalogues). Permissions are untouched: same `CatalogueManage` gate, no new check.

**Evidence.**
- AC-942-A, B, D (two cases): API tests in `ListCatalogueItemsTests`. Run against the old handler, 4 of 18 failed; after the change 18 of 18 pass.
- AC-942-C: passes before and after by design, because it guards today's behaviour; it is a regression test, not a new behaviour.
- AC-942-E: `catalogue-list-page.spec.ts`. The fake API answers with both babs' rows regardless of the filter. Test 1 asserts the request carries the باب and that both rows still render (no in-memory narrowing). Against the old page it failed; with the change it passes. The باب already round-trips through `?bab=` as before, and `catalogue-bab-filter` is unchanged.
- Client code removed: the `visibleItems` computed (6 lines). `countText` and `groups` read `items()` directly.
- Gates: build 0 warnings 0 errors; Api.Tests total 667, failed 0; `ng build --configuration production` exit 0 with no warning; vitest 30 files, 182 tests passed.
- Domain.Tests total 299, failed 1: `TranslationCatalogueTests.Every_screen_key_in_the_catalogues_is_read_by_a_template_or_a_component` names `action.clear_filters`, a key no template reads. It is present in HEAD before this story and is not caused by it. Left unfixed.

**Not done:** pagination (ruled out), per-row response shape, the باب tree screen.
