# KAFF-940 · Cost and the sell-price override belong to the Owner alone

<!-- kaff id=KAFF-940 slice=2 points=5 state=BUILT -->

**Slice:** 2 (Masters) · **Epic:** Catalogue · **Points:** 5.
**Owner:** Backend, then Frontend.
**Spec:** `spec.md` §4.1 (the catalogue item carries `costPrice` and `baseSellRate`), §2 (who owns
each master record).
**Decision:** `D-177` §3, Nabil, 2026-10-06.

## Story

`Permission.CatalogueManage` is `CompanyWide` and granted to the Owner **and** the Technical Office
[Verified: 2026-10-06 @ `src/Domain/Authorization/PermissionCatalogue.cs` ->
`new(Permission.CatalogueManage, PermissionScope.CompanyWide, [owner, technicalOffice], "§2, §4.1")`].
That single grant covers the whole catalogue item, so today the Technical Office can change what an
item costs Kaff and what it sells for.

Nabil ruled that it must not. The Technical Office keeps the catalogue — creating items, descriptions,
units, archiving — but **the cost price and the sell-price override are the Owner's alone.** Everyone
else reads them.

Split the permission so the server enforces it. Hiding a field is presentation; the rule has to hold
against a direct call.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Only the Owner may change an item's `costPrice` | `D-177` §3 |
| 2 | Only the Owner may set or clear an item's sell-price override | `D-177` §3 |
| 3 | The Technical Office keeps every other catalogue capability it has today | `D-177` §3 · `spec.md` §2 |
| 4 | A non-Owner may still **read** cost and sell price on the catalogue screen — it is internal data, not secret from internal staff | `D-177` §3 |
| 5 | The client never sees cost or margin at all, unchanged | `spec.md` §542 |

## Acceptance criteria

**AC-940-A — the Technical Office cannot change a cost**
Given a signed-in Technical Office user with `CatalogueManage`
When they call the edit endpoint with a changed `costPrice`
Then the call is refused, the stored cost is unchanged, and the refusal is a translated message rather than a 500
*Rule: 1.*

**AC-940-B — the Technical Office cannot set or clear an override**
Given the same user
When they call the edit endpoint with a changed sell-price override, in either direction
Then the call is refused and the stored value is unchanged
*Rule: 2.*

**AC-940-C — the Owner can do both**
Given a signed-in Owner
When they change a cost and when they set and then clear an override
Then each succeeds and each writes an audit record carrying who, when, before and after
*Rule: 1, 2.*

**AC-940-D — every other catalogue capability survives for the Technical Office**
Given a signed-in Technical Office user
When they create an item, edit its descriptions or unit, move it between أبواب, or archive and unarchive it
Then each still succeeds exactly as before this story
*Rule: 3.*

**AC-940-E — a non-Owner still reads the figures**
Given a signed-in Technical Office user
When they list the catalogue and open an item
Then cost and sell price are both returned and rendered, read-only
*Rule: 4.*

**AC-940-F — the UI reflects the rule but does not enforce it**
Given a non-Owner on the catalogue screen
When the detail panel is open
Then the cost field and the override control are visibly read-only, and the server refuses the call regardless of what the client sends
*`CLAUDE.md`: never enforce permissions in the frontend alone.*

## Not in this story

- **The request-and-approve workflow.** Nabil's full ruling was that a non-Owner submits a request for
  his approval. No request entity, queue, approval screen, audit shape or notification exists for that,
  and improvising one inside a screen is how a system grows a second approval engine. It is scoped
  separately as `KAFF-941` and Nabil approved shipping this story's end state first (`D-177`).
- Any change to what re-prices when a cost changes — `D-177` §1 settled that nothing does, silently.
- A باب's markup, which is displayed here and edited on the project (`D-177` §2).

## Questions for Karim

None — Nabil ruled this himself as the Owner of the business process (`D-177`).


## Build note — Backend, 2026-10-06

**State: BUILT for the backend criteria AC-940-A to AC-940-E. AC-940-F is the frontend's and is not built here.**

- **Permission.** New `Permission.CataloguePriceEdit = 72`, `CompanyWide`, granted to the Owner only, not
  `TouchesMoney`. It follows `SubcontractorTaxRegistrationEdit` (`D-147`): one record, two owners.
  `CatalogueManage` is unchanged and still granted to the Owner and the Technical Office.
- **Enforcement point.** `EditCatalogueItem` keeps its single `PUT` route, because the screen sends the whole
  record. The handler refuses with `master.catalogue_price_owner_only` (403, translated in `ar.json` and
  `en.json`) when `costPrice` or `baseSellRate` differs from the stored value and the caller does not hold
  `CataloguePriceEdit`. An unchanged price passes, so the Technical Office can still edit descriptions and
  unit with the whole record. `ConfirmCatalogueReimport` had the same hole (it re-prices existing codes), so
  it refuses the whole confirm, creating nothing, when any existing item's price would move and the caller
  lacks the permission. Preview stays open, since it changes nothing.
- **Audit.** No new path. The Owner's edits are written by `AuditSaveChangesInterceptor` as before.
- **Existing tests.** Tests that re-priced as the Technical Office now act as the Owner.
  `A_negative_cost_price_is_refused_on_edit` also moved to the Owner, because a Technical Office price change
  is now refused before price validation runs.
- **Not done / open question.** `spec.md` §4.1 and the entity have no separate "sell-price override" field,
  only `baseSellRate`. This story guards `baseSellRate` as the sell price. "Clear an override" (AC-940-B,
  "set or clear") has no meaning until an override field exists, so only changes in both directions are
  tested. Whether Nabil meant a new nullable override field is a question for him. Creating an item still
  lets the Technical Office type the initial prices (AC-940-D keeps create). Frontend permission mirror and
  the `KAFF-941` request workflow are not touched.
