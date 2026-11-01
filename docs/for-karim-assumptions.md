# Kaff ERP: what we assumed, and what it costs if we are wrong

For Karim. Use this sheet while you watch the demo. Each topic says what we assumed, why, where you can see it, and what changes if you rule differently. Topics are ordered by what costs most to get wrong.

**How to read "cost".** Cheap = a wording or a setting. Moderate = days of rework on screens already built. Expensive = rework of money rules, or of postings already made (money postings cannot be edited, only reversed, so a wrong rule that has already posted is very hard to undo).

**Honest note on the demo, updated 2026-11-01.** The catalogue, the أبواب tree, the import, the audit trail and the people lists are built. Since this sheet was first written, three things changed and they change what you can see:

1. **The contract value is now derived from a signed bill of quantities, not typed.** Signing freezes the priced lines, and the database itself refuses to alter a frozen line afterwards.
2. **The three billing calculators are built**, and the specification's worked example (section 15) is now produced by the real calculation rather than by figures typed into a test. Every number in that example matches.
3. **There is one money screen: إثبات الأرقام** (proof of numbers). It shows a project's actual ledger postings beside what the contract rules expect, and says line by line whether they agree.

So topics 1 to 5 are no longer invisible — but they are **running on our assumptions, not your rulings**. Every assumption below is isolated in one place in the code so that your answer is a one-line change rather than a rebuild. That is deliberate: we built so you could correct us cheaply after seeing it, instead of waiting.

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

## 12. Do the hold and the advance recovery apply to change-order value?

| | |
|---|---|
| **What we assumed** | Nothing. The calculator **refuses** any extract carrying change-order value rather than guessing. |
| **Why** | Section 5.1 says the hold is taken on "certified work value only". A change order is certified work, but it is priced separately and approved separately, so we cannot tell whether it joins the base for the 20% hold and the 25% advance recovery, or sits outside both. |
| **In the demo** | Nothing to point at — an extract with a change order on it is refused with a clear message rather than producing a number we invented. |
| **If you rule differently** | Moderate. It is the same calculation either way; the cost is that no extract with a change order can be issued until you answer. |

**Ask Karim:** "When you add work to a signed contract and bill it, does the 20% hold come off that added work too? And does the advance come back out of it?"

## 13. What does a billable design revision cost?

| | |
|---|---|
| **What we assumed** | Nothing. The design stage fee ignores revision rounds entirely. |
| **Why** | Section 5.3 gives the five design stages and their weights, and says revisions beyond the agreed rounds are billable. It never says at what price. |
| **In the demo** | The design calculator bills a stage at its weight (concept is 30%). Revisions are simply not counted. |
| **If you rule differently** | Cheap, if it is a rate or a fixed fee. Moderate if it varies per contract, because then it has to be a contract term captured at signing. |

**Ask Karim:** "The client asks for a sixth revision when the contract allowed five. What do you charge?"

## 14. How is the Cost Plus supervision markup rounded?

| | |
|---|---|
| **What we assumed** | Not rounded at all — it is kept at four decimal places. |
| **Why** | Our rounding rule (topic 4) covers deductions. The supervision markup is not a deduction, so nothing told us what to do, and we chose not to round rather than to round invisibly. |
| **In the demo** | A Cost Plus project: 100,000 of supervised cost at 10%, plus 5,000 exempt, bills 115,000. |
| **If you rule differently** | Cheap. One setting, alongside topic 4. |

## 15. What two accounts does withholding tax move between? (Q91)

| | |
|---|---|
| **What we assumed** | Nothing is settled. The calculation produces the withheld amount correctly, but which two accounts it is booked against is still open, and the posting rules may refuse the pair we picked. |
| **Why** | Q91 is still pending in the question register. Section 6.7 says withholding is two fields and two accounts, and does not name the accounts. |
| **In the demo** | We calculate 1% of a 240,000 net payable, which is 2,400. We have **not** tested booking it end to end. |
| **If you rule differently** | Cheap to change the pair. **Expensive if it posts wrongly first**, because a posting can only be reversed, never edited. |

## 16. What is the rule for a delay penalty?

| | |
|---|---|
| **What we assumed** | That somebody types the amount in. The calculator takes it as given and refuses it if the project's terms do not allow one. |
| **Why** | No formula exists anywhere in the specification — not a rate per day, not a cap. |
| **In the demo** | Nothing to point at. |
| **If you rule differently** | Cheap now. It becomes a calculation rather than a typed figure. |

**Ask Karim:** "If the contractor finishes late, how is the penalty worked out — per day, as a percentage, and is there a maximum?"

## 17. On site, does a second quantity entry on the same line add, or replace?

| | |
|---|---|
| **What we assumed** | It **adds**. |
| **Why** | The specification contradicts itself on exactly this case. Section 8 says the daily log records period deltas, which means a second entry adds. The same section says that for the same engineer, same day, same field, the latest entry wins, which means it replaces. Both cannot be true for a quantity. Our reading: a quantity is a delta and adds; "latest wins" governs single-valued things such as the check-in time, the weather, or a note. |
| **In the demo** | Not visible yet — the daily log is being built now. |
| **If you rule differently** | Moderate, and it matters more than it sounds: executed quantities are what extracts are billed from. If a correction is meant to replace and we add it, the client is billed twice for the same work. |

**Ask Karim:** "An engineer logs 40 metres in the morning, then logs 25 metres on the same line that afternoon. Did he do 65 metres, or was he correcting himself to 25?"

## 18. Can hours be charged to a worker whose account is switched off? (Q118)

| | |
|---|---|
| **What we assumed** | Nothing. The system currently lets it happen, and nobody decided that. |
| **Why** | The specification says engineers register workers and record day labour on the daily log. It does not say what happens to a worker who has been marked inactive. Today the daily log's worker list still offers him, and the system accepts the hours. |
| **In the demo** | On a project, open the daily log and add day labour. Workers marked inactive still appear in the list and can be given hours. |
| **If you rule differently** | Cheap if inactive workers should simply disappear from the list and be refused. Moderate if hours already recorded against an inactive worker need a rule of their own. |

**Ask Karim:** "A worker has left, or you have stopped using him, and he is marked inactive. Should an engineer still be able to give him hours on a project — or should he stop appearing?"

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

- **Projects:** a list only. No hub, no timeline, no contract screen. A hub is being designed now and is not approved yet.
- **The BOQ** (bill of quantities): **built** — priced lines, frozen at signature, and the contract value is now read from it instead of typed. There is no screen for it yet, so it is reachable only through the interface, not by clicking.
- **Extracts (مستخلصات):** the **three calculators are built** and the worked example in section 15 is now produced by them rather than typed into a test. There is still **no extract screen and no approval chain** — no quantity gate, no deductions step, no Owner approval, no issuing.
- **Treasury and payments:** no safe, no payment, no عهدة screens.
- **The daily log and site execution:** being built now. Only a day-worker registration form exists today.
- **Client portal:** not built.
- **Alerts or notifications:** there is no place to show one yet.
- **Change orders, collections, closure, hold release, warranty:** not built.
- **Catalogue export** to Excel: not built, so updating through import means typing codes by hand.
- **Failure screens** ("could not load, retry"), "discard unsaved changes", and the phone back button on panels: not built.
- **إثبات الأرقام is reachable by typing its address only** — nothing links to it from the project list yet, and the column that compares against what the rules expect is still switched off, because it was built before the calculators were.

Do not promise dates.
