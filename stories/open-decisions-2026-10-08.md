# Open decisions — the BA / UX / Scrum Master closure pass, 2026-10-08

Nabil asked the three disciplines to close everything open. The BA and the UX designer answered the
same fourteen-item list independently, so their disagreements would surface rather than one deferring
to the other. This file records what the team closed on its own authority, and what is left for Nabil.

**It is a snapshot, not a register.** Once each item below is ruled it moves into `decisions.md` as a
`D-nnn` and this file stops being current. Do not read it for the present position after that.

---

## Part 1 — closed by the team, no business ruling needed

| # | Item | Ruling | Owner |
|---|---|---|---|
| 1 | Import: bad rows skip and the rest applies | Already ruled — `KAFF-200` rule 7, `D-129 §3`, `D-130 §1`. Not an open question. | — |
| 2 | Import: a blank code | Already ruled — it is a row failure, `code_required`. The design's "the code is generated" was wrong and is corrected. | — |
| 3 | Import: an unknown code | Already ruled — codes are typed (`spec.md` §4.1), so an unknown code **creates** a new item. The design called it an error; the design was wrong. | — |
| 4 | Import: the file layout and template | Already ruled — `KAFF-200` rule 3, `D-144 §§3-4`: seven columns, a template, باب by code, no status column. | — |
| 5 | Slice-4 identifier drift | The project-list screen never had a story file and `KAFF-401` is double-booked with a CRM story in `backlog.md`. Give the screen a new id in the 9xx range and cut a one-page file. **Do not rename `KAFF-400`** — its file and `AC-` ids exist and are cited. | Scrum Master |
| 6 | A باب must show its code beside its name everywhere it is offered | The tree does; the catalogue's باب filter, the create form's باب step and the import's باب column do not. Whatever is ruled about duplicate names, the code is the identity and must be visible. | UX, then Frontend |
| 7 | An orphaned active child under an archived parent never loses its path | The built screen promotes it to top level with no indication of where it came from, which makes it read as a real top-level trade. It carries the dim path line the flattened search already uses, with the parent marked archived. **This is presentation only — whether the state may exist at all is Nabil's, item 1 below.** | UX, then Frontend |
| 8 | A stored value the UI cannot represent is never coerced, hidden or blanked | A free-text unit outside the select shows verbatim, and in edit appears as a selected option tagged "(غير مدرجة)". Applies to every select in the system, not just units. | UX |
| 9 | The phone primary-action slot | Adopt the UX contract: a screen declares exactly one `primaryAction` and never renders a primary itself; the shell places it in the page header on desktop and the bottom bar on phone. Zero actions means the menu expands to fill. Two or more means the most frequent constructive one takes the slot and the rest go to the phone toolbar. Destructive actions are never in the bar. An open overlay hides the bar and its own footer carries the primary. | UX, then Frontend |
| 10 | Failure states, discarding unsaved edits, and the phone back button | None of the three exists: no "failed to load" with retry, no save failure that is not validation, no confirmation when a panel with typed changes is dismissed, and no history entry for an open panel so the hardware back button closes it. All three are design and build work with no business question in them. | UX, then Frontend |
| 11 | `KAFF-941` is the wrong shape and is parked | Cutting a catalogue-only request-and-approve entity would build the second approval engine `CLAUDE.md` forbids. It belongs to whichever slice first builds the general engine. **Do not estimate it.** One question for Nabil survives — item 11 below. | Scrum Master |
| 12 | The "last changed by" line is not built from the audit read | `Permission.AuditRead` is Owner-only (`D-049`), so a Technical Office panel would 403 on its own footer. If it is ever built, it reads `updatedAt` / `updatedBy` from the record itself, with the audit-log link for the Owner only. Parked until a screen needs it. | — |

### Defects found during this pass, not questions

| Defect | Why it matters |
|---|---|
| **The import's overwrite warning is false.** It says the file's data will replace the item, but `CatalogueItem.Reprice` updates **prices only** and silently ignores a changed description, unit or باب. | The most consequential screen in the product tells the user something untrue about what it is about to do. The wording must change whatever Nabil rules on item 7 below. |
| **`KAFF-205` is stale on the board.** `AC-205-D` and `AC-205-J` require moving an item between أبواب; the old form page was deleted, the endpoint is uncalled, and the trailer still reads VERIFIED. | A verified story whose criterion no user can reach is the board lie this project keeps paying for. Reopen it. |
| **The أبواب screen invented a rule.** Promoting an active child to top level when its parent is archived was decided by nobody. | Third invented rule caught in this module. Items 1 and 7 above cover the fix. |
| **The باب code's character set differs between client and server.** The client enforces letters only; the server accepts any non-blank string up to 32. A code containing a digit can arrive by import or API and the UI will not accept it back. | One of the two is wrong. The format itself is Nabil's (item 12). |

---

## Part 2 — Nabil's decisions

Each carries the team's recommendation. None is a default applied in his absence.

| # | Question | Recommendation |
|---|---|---|
| 1 | When a باب is archived, may a new child باب or a new item still be filed under it, and what happens to its existing active children — they stay where they are, they must be moved or archived first as items are, or they become unavailable for new BOQ lines like the parent? | **Nothing new may be filed under an archived باب, existing children stay and show their archived parent's path.** The closest precedent is `D-130 §3`: an archived item cannot go on a new BOQ line. A cascade is ruled out by `D-130 §5`. |
| 2 | Should only the Owner change a باب's default markup, as with cost and sell rate, or may the Technical Office change it too? | **Owner only.** It starts every future BOQ line, which is the same reasoning that moved cost and sell rate in `D-177 §3`. Half a day of design, no new component. |
| 3 | May two أبواب share an Arabic name, and if not, is that forbidden across the whole tree or only under one parent? | **Allow duplicates.** The code is the identity and will now be shown everywhere (closed item 6). A tree-wide ban would forbid a legitimate "تشطيبات" under two parents. |
| 4 | Does the on-screen order of the trades matter to you? If it does, who sets it and may it change later? If not, may the tree simply sort alphabetically? | **Needs your answer.** Today the number is typed once at creation, defaults to 0, cannot be changed afterwards, and ties break silently by code — so a wrong number is permanent. If order matters, reordering has to exist. |
| 5 | Should the Technical Office be able to enter the first cost and sell rate on a new item, or should only you set any price, including the first? | **Leave it as it is** — the Technical Office sets the first price. Closing the gap needs a new "awaiting pricing" domain state, because `V-36-H` forbids an absent price, and that is 1 to 1.5 days of design plus domain work for a narrow case. |
| 6 | Is a catalogue item's unit chosen from a fixed list, and if so what are the units, or typed freely? | **A fixed list, and you supply it.** Free text means "م2", "م²" and "متر مربع" become three different units the moment two people type them, and slice 4 aggregates BOQ quantities by unit. |
| 7 | On a re-import, should an existing code change only its prices, or also its description, unit and باب? | **Prices only**, which is what the system already does — and the warning text gets corrected to say so. |
| 8 | A catalogue export does not exist. Updating by import means building a file of typed codes by hand. Should we build "تصدير الكتالوج" in import-ready format? | **Yes.** Without it the import's update path is unusable in practice. |
| 9 | Moving items between أبواب one at a time: the archive refusal tells the user to move 17 items first, and the only tool would be 17 trips through a panel. Should bulk move exist? | **Yes.** About a day. Without it the refusal's own instruction is not followable. |
| 10 | `Q95` — is a day-labour worker's specialisation a typed line or a fixed list, and what is the list? | **A fixed list**, same reasoning as units: `spec.md` §10 wants a searchable pool, and free text makes "نقاش" and "نجار نقاشة" different workers. |
| 11 | Do you want the Technical Office to be able to submit a price change for your approval inside the system, or is it enough that they cannot change prices and tell you another way? | **Not yet.** Saying yes means building the general approval engine first — it cannot be a catalogue-only mechanism. Saying no closes `KAFF-941` and the second half of `D-177` permanently. |
| 12 | May a باب with no items and no children be deleted, or is it archived forever? And what characters may a باب or item code contain? | A باب code is globally unique and immutable, so **a mistyped code blocks that code permanently**. `D-171` archived rather than deleted for departments; the same answer here means living with it. |
| 13 | Must every باب have an English name? | The domain refuses an empty one today and the design assumed it was optional. No spec line supports either. **Keep it required**, or say so and the domain changes. |
| 14 | Nothing in this system is translated except the sign-in screen. The language switch flips direction and leaves Arabic copy in place. Who writes the English? | An English copy deck is a business task, not a build task. Until it exists, the English half of the product is a layout proof only. |

---

## What the UX designer raised that nobody had asked

Recorded because it will decide how much of the next slice is design work rather than build work.

**The kit was built from the price book — the least money-like screens in this system.** The first
ledger, extract or treasury screen will need, and none of these exist: signed and negative figures
(a minus inside a `bdi` under RTL, and how a reversing posting is shown against the original), debit
and credit columns with a running balance, an approve-and-reject-with-reason pattern honouring
"rejections return to origin with a reason" and "nobody approves their own", a history timeline,
file attachments, and date ranges. They should be designed before that screen, not during it.

**There is also no home for an alert.** `spec.md` §4.4 rules that open estimates "raise a manual
review alert", and the shell has no notification surface at all — no bell, no count, no inbox. The
first ruled alert in this system has nowhere to appear.
