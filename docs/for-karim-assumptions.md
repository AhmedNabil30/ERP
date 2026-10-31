# Kaff ERP: what we assumed, and what it costs if we are wrong

For Karim. Use this sheet while you watch the demo. Each topic says what we assumed, why, where you can see it, and what changes if you rule differently. Topics are ordered by what costs most to get wrong.

**How to read "cost".** Cheap = a wording or a setting. Moderate = days of rework on screens already built. Expensive = rework of money rules, or of postings already made (money postings cannot be edited, only reversed, so a wrong rule that has already posted is very hard to undo).

**Honest note on the demo.** The catalogue, the أبواب tree, the import, the audit trail and the people lists are built. The money rules (topics 1 to 5) are not built as screens yet, so "what you can see" for them is the worked example in the specification (section 15) rather than a live screen. We need your answers before we build them.

---

## 1. When does an extract (مستخلص) post to the ledgers?  (nobody has asked this yet)

| | |
|---|---|
| **What we assumed** | Nothing yet. This is the most important open question in the system. |
| **Why** | It was never in the question register. It decides when revenue counts, when the 20% hold starts building, when the advance starts coming back, and when withholding applies. |
| **The options** | (a) When the Technical Office approves. (b) When the Owner approves. (c) When the extract is issued to the client. (d) When the client pays. |
| **In the demo** | Nothing to point at yet. The worked example (spec section 15) shows the results of each extract but not the moment they are booked. |
| **If you rule differently** | Expensive if we guess and you differ. A rejection after booking is a reversal, not an undo. Asking now is free. |

## 2. How much of the تشوينات (material advance) comes back on each extract? (Q58)

| | |
|---|---|
| **What we assumed** | Nothing is built. The only candidate: 75% of the material actually installed in the period. |
| **Why** | The specification's example recovers 45,000 on extract 2 and 30,000 on extract 3 with no rule behind it. 75% of installed material (60,000 then 40,000) gives exactly those numbers. That could be coincidence, so we will not adopt it without you. |
| **In the demo** | Specification section 15, the تشوينات column: 75,000 in, then -45,000, then -30,000, ending at zero. |
| **If you rule differently** | Moderate. It is one calculation, but every future extract total depends on it. |

**Ask Karim:** "The client pays 75,000 up front for material on site. How do you decide how much comes off each extract?"

## 3. Withholding tax: the base, and the hold release

| | |
|---|---|
| **What we assumed** | Withholding is two fields and two accounts only. The category is set on the project (not the client). Individual clients never withhold. |
| **Why we had to assume it** | Open question: is the base the net payable, the period work value, or certified value less hold? And does the final hold release (200,000 in the example) also carry withholding? Also Q30: may the tax rate change after the first extract is issued? |
| **In the demo** | Not visible. Not built as a screen. |
| **If you rule differently** | Expensive once extracts have posted. Cheap before. Needs your accountant. |

## 4. Rounding (Q82, Q21)

| | |
|---|---|
| **What we assumed** | Each extract line rounds to 2 decimals and the total is the sum of the rounded lines (already ruled). Stored values keep 4 decimals. |
| **Why** | Still open: are hold, advance recovery and withholding worked out on the rounded total, or line by line then added? And is the printed contractual figure 2 or 4 decimals? |
| **In the demo** | Not visible. Visible in the catalogue only as price display. |
| **If you rule differently** | Moderate. A few piasters per extract, but the hold must end at exactly 20% of contract value, so the choice matters for the final reconciliation. |

## 5. What does a Terminated project settle?

| | |
|---|---|
| **What we assumed** | Nothing. The state exists; there is no rule. |
| **Why** | The specification lists "termination settlement" but says nothing about what happens to the hold, the unrecovered advance, or the unrecovered تشوينات. |
| **In the demo** | Not visible. Project list only. |
| **If you rule differently** | Expensive to guess, cheap to ask. Today nothing can post against it. |

Related and also unruled: may Finance post money to a Stopped, Closed or Terminated project? Today nothing checks.

## 6. Who turns a won opportunity into a project?

| | |
|---|---|
| **What we assumed** | Recommended, not ruled: Marketing marks it won and stops. The Technical Office opens the project from a "won, awaiting survey" list. |
| **Why** | Marketing can win a deal but is not allowed to create a project. The survey must come before the contract value. |
| **In the demo** | Project list as Owner, then sign in as Sales (`karim_sales_demo`) and note there is no create-project action. |
| **If you rule differently** | Moderate. A permission and a hand-off screen. |

## 7. Units: fixed list or free text?

| | |
|---|---|
| **What we assumed** | A fixed list. We invented seven for the demo: م³ · م² · م.ط · عدد · طن · كجم · مقطوعية. The system itself accepts any text. |
| **Why** | Free text turns "م2", "م²" and "متر مربع" into three units. Quantities are totalled by unit in the BOQ. |
| **In the demo** | Catalogue, create an item, open the unit dropdown. Those seven are our guess, not Kaff's list. |
| **If you rule differently** | Cheap if you give us the list now. Moderate later, after items exist with odd units (they show as "not on the list"). |

**Ask Karim:** the real list of units.

## 8. Day-labour specialisation (Q95)

| | |
|---|---|
| **What we assumed** | Recommended: a fixed list. The mobile worker screen draws four pills (بنّاء / مساعد / نقاش / حداد) which are a design guess. Today the field is typed text. |
| **Why** | Free text makes "نقاش" and "نجار نقاشة" two different workers. |
| **In the demo** | Phone-width worker registration screen, if shown. The specialisation is a text box. |
| **If you rule differently** | Cheap. |

## 9. Archiving a parent باب

| | |
|---|---|
| **What we assumed** | Recommended: nothing new may be filed under an archived باب. Its active children stay where they are, shown with their archived parent's path. Today the tree promotes such children to the top level without saying why (nobody decided that). |
| **Why** | Open decision 1. |
| **In the demo** | أبواب tree: archive a parent that has children, restore it. A باب with active items is refused, with the reason in Arabic. |
| **If you rule differently** | Cheap to moderate (tree display and filing rules). |

## 10. Whose is a باب's default markup?

| | |
|---|---|
| **What we assumed** | Owner only, like cost and sell rate. Today the Technical Office can change it too. |
| **Why** | It starts every future BOQ line. |
| **In the demo** | أبواب: the markup editor. Try it as the Owner, then as the Technical Office. |
| **If you rule differently** | Cheap (half a day). |

## 11. Duplicate Arabic names and sort order for أبواب

| | |
|---|---|
| **What we assumed** | Duplicates allowed (the code is the identity, e.g. "تشطيبات" under two parents). Sort order is a number typed once at creation, defaults to 0, cannot be changed later, and ties fall back to code. |
| **Why** | Open decisions 3 and 4. |
| **In the demo** | أبواب tree: the order of trades. A wrong number today is permanent. |
| **If you rule differently** | Cheap if alphabetical is fine. Moderate if order matters, because reordering must be built. |

## Other assumptions you can see in the demo

| Assumed | Seen at | Cost if changed |
|---|---|---|
| Advance 25%, hold 20% as defaults, owner-adjustable (register #1) | Specification section 15 | Cheap |
| Advance recovery is 25% of period work value (#2); no hold on تشوينات (#3) | Section 15 | Moderate |
| Revenue counts at the certified extract (#14) | Not visible | Expensive |
| Subcontractor retention 5%, released at end of warranty (#19) | Not visible | Moderate |
| Re-import updates prices only, never description, unit or باب | Import preview | Cheap |
| An unknown code on import creates a new item | Import | Cheap |
| A bad row skips and the rest applies | Import with a wrong row | Already ruled |
| Technical Office sets the first price on a new item; only the Owner changes it later | Create item panel | Moderate |
| A باب code is permanent. A mistyped code blocks that code for good, and a باب cannot be deleted, only archived | أبواب create | Moderate |
| Every باب must have an English name | أبواب create | Cheap |
| A markup or hold over 100% warns, it does not block (ruled) | Markup editor | Already ruled |
| Nothing is translated to English except sign-in. The language switch only flips direction | Language switch | Business task: someone writes the English |

---

## What we have not built at all

Say this plainly. Do not read the demo as more than it is.

- **Projects:** a list only. No hub, no timeline, no contract screen.
- **The BOQ** (bill of quantities): does not exist. Today a contract value can be typed in, and signing freezes nothing. This must be fixed before any money screen.
- **Extracts (مستخلصات):** no screens, no approval chain, no calculators. The specification's worked example passes only as a hand-built test.
- **Treasury and payments:** no safe, no payment, no عهدة screens.
- **The daily log and site execution:** not built. Only a day-worker registration form exists.
- **Client portal:** not built.
- **Alerts or notifications:** there is no place to show one yet.
- **Change orders, collections, closure, hold release, warranty:** not built.
- **Catalogue export** to Excel: not built, so updating through import means typing codes by hand.
- **Failure screens** ("could not load, retry"), "discard unsaved changes", and the phone back button on panels: not built.

Do not promise dates.
