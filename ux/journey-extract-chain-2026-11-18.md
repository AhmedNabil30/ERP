# The extract chain and collections, end to end — 2026-11-18

Written by the journey-analyst for Nabil, to take to Karim. It covers modules 4 and 5 of release 1: from the
site engineer's quantities to the day the client's money reaches the safe, plus change orders on the extract
and the dispute path. Every step carries one source tag: `SPEC §n`, `RULED D-nnn`, `ASSUMED` (a `RulingNote`
default nobody has confirmed, named), `PRACTICE [url]`, or `PROPOSAL` (mine). Where this journey differs from
`spec.md`, the spec stands and the difference is a question in section 4.

Read for this: `spec.md` §5.1, §6.1, §6.4, §6.5, §6.7, §6.9, §7, §9, §13, §14, §15; stories KAFF-500, 508, 509,
511, 512, 316, 317; `ux/journey-project-module-2026-11-09.md`; `ux/research/` headings; the extract list and
detail templates and `extracts.api` consumers; every `Endpoint`, `Request` and response record under
`src/Api/Features/Extracts`, `Cheques`, `ChangeOrders`, `Adjustments`; `PermissionCatalogue.cs`;
`src/Domain/Rulings/{Collection,Adjustment,ExtractPreparationScope,ExtractPartialApprovalScope}*`; D-049, D-189,
D-198, D-199, D-202, D-204, D-214. Nothing was run. No stack was started.

## 0. What I checked, and what the brief had wrong

- **The server is further along than "no screen" suggests, and less complete than "built and tested" suggests.**
  Routes exist for prepare, QC, Technical Office approval, deductions, Owner approval, issue, rejection at
  each gate, evidence revision, collect, collection read, collection reversal, cheque deposit, clearance
  and bounce, dispute raise and resolve, credit note raise, approve, reject and reverse, and the whole
  change-order chain. What is missing is listed in section 5. The biggest: **no endpoint sets a contract's
  withholding category** (`WithholdingCategory` appears nowhere under `src/Api`), so withholding at source
  can never be non-zero in a running system.
- **Two screens already exist.** `extracts` (list) and `extracts/:extractId` (detail) are real, with every
  gate button hanging off server flags, a reject-with-reason panel, an evidence-revision panel and the
  six-figure document. This journey changes them; it does not start them.
- **The web's `ExtractStatus` type stops at `Collected`.** The server has `Disputed` and `Void`. The detail
  and list read `pill[extract.status]` and `statusKeys[...]`, so an extract in `Disputed` would break both.
  Fix before any dispute screen (gap 12).
- **The story files say "Status: BLOCKED" in their bodies.** Their trailers say BUILT. The bodies are stale.
  Where they disagree with code, I used code and the decision entries.
- **"Any rejection returns to Draft" holds for extracts only.** A change order rejection ends at `Rejected`
  (`SPEC §13`, `RULED D-199`). A credit note rejection ends at `Rejected`. Both store a reason.
- **"Cash reaches the safe only at Collected" is true, and it is the wrong guard for a cheque.** The
  collection posts the full cash leg to the safe at the moment Finance records it, whatever the method
  (`D-198`, "Not done"). A cheque still in the drawer is counted as safe cash, and a bounce posts nothing
  (`ChequeBounceEffect`, Q101). That is the largest money risk in these two modules (question 1).
- **`spec.md` §13 says `Paid`; the code and the screens say `Collected` / تم تحصيله.** `SPEC §14` and
  `D-198` accept this. I use Collected.

## 1. The journey

Roles: Site Engineer (SE), Supervisor, Technical Office (TO), Finance, Owner. Phone first for SE and Supervisor.
Owner, Finance and TO will also open these on a phone. Names of states use Kaff's own Arabic as the app shows
them today.

### Phase A — Prepare and gate (module 4)

1. **SE** opens the project. The shell's primary action says "مستخلص جديد" and opens **E2 New extract**. Shown only
   if no extract is in flight. `SPEC §7` (the site engineer prepares quantities). One in flight at a time is
   `ASSUMED ExtractPreparationScope`.
2. **SE** picks the period, checks what will be billed, types the value of material on site and material
   installed, and creates. The server derives certified work from the frozen BOQ and the daily logs; nothing
   about work value is typed. Lands on **E3 Extract detail**, status مسودة. `SPEC §7`, `RULED D-193/D-194`. The
   two typed money values are `ASSUMED ExtractPreparationScope` (a site engineer types money, which `spec.md` §9
   otherwise denies him).
3. **SE** sees on E3 who the extract waits on ("ينتظر المشرف"). `RULED D-204` (the waiting line).
4. **Supervisor** opens the extract from the project's waiting line, checks the quantities, and submits the
   weekly QC report. Extract moves to عند المكتب الفني. `SPEC §7`. Holder is `ASSUMED` (`D-189 §3`, unresolved).
   What the report contains, and the supervising engineer's confirmation of field quantities, are not built
   (`RULED D-214` names it as unbuilt; question 6).
5. **TO** opens E3, sees the quantity figures, and approves in full or rejects. `SPEC §7`, `RULED D-189 §1`.
   The creator and anyone who already acted this cycle is barred, and the button is absent. `SPEC §9`,
   `RULED KAFF-500 rule 5`.
6. **TO** may instead approve a percentage and hold the rest for an NCR (**E4 Approve part**). Extract moves
   to معتمد جزئيًا. `SPEC §7`. Naming an NCR needs an NCR object that does not exist (gap 5). What the
   percentage scales is `ASSUMED ExtractPartialApprovalScope`.
7. **Any rejection** (TO, Finance, Owner): a reason is required, the extract returns to مسودة, the reason shows
   at the top of E3 for the SE. `SPEC §7`, `CLAUDE.md`. The SE can correct the two material values through
   "تعديل" (`RULED D-195`); work value is re-derived, not typed.
8. **Finance** opens E3 at عند الحسابات, confirms "تطبيق الخصومات". The server computes hold, advance recovery,
   تشوينات and net payable; Finance types nothing. The six figures and the printed column appear. `SPEC §7`,
   `RULED D-189 §2`. The rounding is `ASSUMED DeductionRounding` (Q82/Q21).
9. **Owner** opens E3 at عند المالك, reads the six figures and the column ("المجموع مطابق"), and approves or
   rejects. Every extract, no threshold. `SPEC §7`, `RULED KAFF-500 rule 2`.
10. **Owner** issues the extract in a second confirmation. Status صادر. Issue is by the Owner's row
    (`FinancialMovementApprove`), which is `ASSUMED` (no issue row exists, `KAFF-500 stage 2`). A stopped
    project hides the button. `SPEC §7`, `RULED AC-500-H`.
11. Posting moment: the accrual posts at Owner approval; no cash moves. `RULED D-190`; the moment is
    `ASSUMED ExtractPostingMoment`.

### Phase B — Collect (module 5)

12. **Finance** sees صادر extracts with an outstanding amount in the list, opens one, and the shell primary
    says "تسجيل التحصيل" (**E5 Record collection**). `SPEC §6.5`, `SPEC §7`.
13. **Finance** picks method (نقدًا, شيك, تحويل), date, reference, and confirms. The server posts cash and, for a
    corporate client with a rate on the contract, the withheld amount, in one commit. Extract becomes تم تحصيله.
    `SPEC §6.5, §6.7`, `RULED D-198`. Whether the Owner approves a receipt is `ASSUMED CollectionApproval`
    (Q97). One payment per extract, in full, is `ASSUMED CollectionSettlement` (Q98). No amount can be typed,
    `ASSUMED CollectionShortfall` (Q100).
14. **Finance** sees, before confirming, the expected cash and the expected withheld amount, each labelled by
    where it came from. `SPEC §6.7`, `ASSUMED WithholdingBase` (Q114). The server does not return these two
    figures before collection (gap 3).
15. **Cheque:** the same confirmation also creates a cheque in "مستلم". On **E6 Payment** Finance moves it to
    "تم إيداعه", then "تم صرفه" or "مرتد". `SPEC §6.5`, `RULED D-198`. A bounce posts nothing and leaves the extract
    تم تحصيله (`ASSUMED ChequeBounceEffect`, Q101). Accounts for the three moves wait on Q91.
16. **Mistake:** Finance reverses a collection with a stored reason; the extract returns to صادر.
    `SPEC §6.1`, `RULED D-200`. What happens to a cheque already deposited is `ASSUMED CollectionReversalEffect`.
17. **Withholding set-up** (before any of this): Finance sets the withholding category on the contract.
    `SPEC §6.7 amendment`, `RULED D-049`. **No endpoint or screen does this today** (gap 1).

### Phase C — Dispute (module 5, second half)

18. The client disputes an issued extract. **Finance** opens E3, "تسجيل اعتراض", types the reason (**E8**).
    Extract becomes معترض عليه. `SPEC §7`, `RULED D-202`. Holder is `ASSUMED` (Q106).
19. **Finance** resolves it on **E9** as a credit note: amount (at most net payable less credit already
    raised) and reason. The extract returns to صادر and a credit note is raised in the same save. `SPEC §7`,
    `SPEC §6.9`, `RULED D-202`. A revised extract is refused by the server (`ASSUMED ExtractDisputeResolution`,
    Q106), so E9 does not offer it.
20. **Owner** opens the credit note (**E10**) and approves or rejects with a reason. Approval writes one
    CreditNote posting; the hold is never touched. `SPEC §6.9`, `RULED D-202`. The raiser cannot approve.
21. **The dead end:** while a credit note is raised or approved against an extract, collection is refused
    ("extract.credit_note_open"). An approved credit note therefore blocks collection permanently
    (gap 9, question 4).

### Phase D — Change orders on the extract

22. **TO** opens "أوامر التغيير" (**E11**), starts a new one (**E12**): title and priced lines (description,
    unit, quantity, unit price). Lump Sum only. `SPEC §13`, `RULED D-199`. Who prepares is `ASSUMED
    ChangeOrderApprovers` (Q105).
23. **TO** submits, a **second TO** user approves the technical review, a **third** records the client's
    approval, then the **Owner** approves. Each person acts once. `SPEC §13`, `SPEC §9`, `RULED D-199`. The
    recording of the client's approval is `ASSUMED` (Q103); it carries no date, reference or attachment
    (gap 10). Detail is **E13**.
24. Approved: contract value rises (derived). The next extract bills every unbilled approved change order,
    whole, in its own section at its own price. `SPEC §5.1`, `RULED D-199`. The user cannot leave one out
    (gap 10). Hold and advance apply to it by default (`ASSUMED ChangeOrderBillingTreatment`, Q102).
25. Cost Plus and Design: TO logs the change order; no price, no money. `SPEC §13`.

## 2. Where today's app breaks the journey

| # | Break | Step |
|---|---|---|
| 1 | No screen creates an extract. Every extract on a real database came from a developer. | 1, 2 |
| 2 | The junior site engineer cannot open the extract list or the waiting line: `ExtractRead` excludes him (`PermissionCatalogue`, D-217 item 37), though the list is built to be money-free for him. He can create an extract and cannot see where it is. | 1, 3 |
| 3 | The Technical Office "quantity gate" shows four money totals and no quantities. Nobody sees which lines, in what quantity, were billed. | 4, 5 |
| 4 | The QC report is a bare button. It carries no content, no NCR, no confirmation of quantities (D-214). | 4 |
| 5 | A split approval needs an NCR id. No NCR exists to pick, and `ReleaseHeldPortion` has no route. | 6 |
| 6 | An issued extract is a dead end on screen. No collection, cheque or reversal screen. Largest hole by business value. | 12–16 |
| 7 | `ExtractActions` has no flag for collect, reverse collection, raise dispute or resolve dispute. The "absent, not disabled" rule cannot be met for them. | 12, 16, 18, 19 |
| 8 | Withholding is always zero: no way to set the category (gap 1), and no way to record what the client actually withheld (gap 3). | 14, 17 |
| 9 | A cheque counts as safe cash on the day it is received, and a bounce changes nothing. | 15 |
| 10 | `Disputed` and `Void` crash the existing list and detail (web type stops at `Collected`). | 18 |
| 11 | A credit note blocks collection for good. | 21 |
| 12 | Nobody is told. The waiting line covers extracts only. A raised credit note, a change order at any gate and a cheque due for deposit have no waiting line. The Owner has no cross-project inbox. | 20, 23 |
| 13 | A change-order chain needs three different Technical Office users plus the Owner. A five-person firm may have one or two. | 23 |
| 14 | The next extract silently bills every approved change order. The preparer sees neither the list nor the total before pressing create. | 24 |
| 15 | A site engineer must type two money values but may not see money. | 2 |

## 3. What comparable systems do, and whether it transfers

Searched and fetched 2026-11-18. Third-party pages are marked; none is treated as authority for Kaff.

- **Procore locks a bill once it is under review.** The schedule of values cannot be edited while an owner
  invoice is Under Review or Approved
  ([Procore transition guide](https://support.procore.com/products/online/modernized-owner-invoicing-transition-guide),
  from a search summary, page not opened). Admins can approve or reject individual line items on the schedule
  of values card (same source). **Transfers, partly.** Kaff already locks by state. Line-level rejection does
  not transfer: Kaff's gate is whole-extract, and a split is a percentage by `SPEC §7`. If TO cuts quantities
  on particular lines in practice, that is question 3's cousin, not a rule I can add.
- **Procore lets owner invoices run through configurable workflows, and retainage can be released across all
  lines of a client invoice** ([Progress Billings](https://support.procore.com/products/online/user-guide/project-level/progress-billings),
  fetched). **Does not transfer.** Kaff's five-step chain is fixed by `SPEC §7`, and the hold releases once at
  handover, not per invoice (`SPEC §5.1`).
- **Sage 300 CRE style retainage release is a separate invoice that goes through the normal receivable cycle
  before cash is applied**
  ([third-party guide](https://www.cleverence.com/articles/sage-documentation/releasing-retention-for-progress-bills-sage-3487/),
  not Sage documentation). **Transfers to the closure module**, not here: the hold release at handover is a
  collection like any other (`SPEC §15` last row), so E5 and E6 must work for it unchanged. Out of scope today.
- **Odoo post-dated cheque add-ons track registered, deposited, done or cleared, and bounced; a bounce reverses
  the entries made at deposit** ([oe_pdc, a third-party Odoo app](https://apps.odoo.com/apps/modules/17.0/oe_pdc)).
  **Transfers.** It is the pattern behind question 1: a cheque's money follows its state. Kaff's four cheque
  states match, but Kaff's code books cash at receipt.
- **Egyptian withholding: the payer, if a public-sector, business-sector or capital company, deducts 1% on
  contracting and supplies, hands over a discount notice (إشعار خصم), and the contractor attaches the notices
  to its annual return to offset tax**
  ([Daftra, a commercial accounting vendor](https://www.daftra.com/hub/ضريبة-الخصم-والإضافة), fetched). A
  payer that holds a certificate of exemption does not deduct
  ([Almal News, reporting the tax authority](https://almalnews.com/1808436/الضرائب-تطبيق-نظام-الخصم-تحت-حساب-الضريبة-على-جهات-الدولة-صاحبة-الأنشطة-الاقتصادية/),
  search summary, page not opened). **Transfers to E5.** It supports `SPEC §6.7` and suggests Finance should
  be able to note the notice number, and that "corporate" in the spec is narrower than "is a company": the
  test is whether the payer is obliged. Neither is a rule here; both are questions (9).
- **Egyptian and Gulf periodic billing: an interim monthly extract, the consultant's approval, then a final
  extract at الاستلام الابتدائي**
  ([Qoyod, a Saudi accounting vendor](https://www.qoyod.com/blog/business-management/مستخلص-المقاولات/),
  search summary, page not opened). **Partly transfers.** The client or consultant certifying quantities
  before paying is normal, and a cut in quantities is the most common reason an issued extract is not paid
  in full. Kaff has no consultant role (`CLAUDE.md`, out of scope), and its only built resolution is a credit
  note. That gap is question 3. A Saudi page is not Egyptian practice; I found no Egyptian procedural
  source for the technical/accounts review steps and say so.
- **Showing the advance recovery as its own line, not hidden in a net**
  ([Wafeq, a Saudi accounting vendor](https://www.wafeq.com/ar-sa/الضرائب-والإقرارات/مستخلصات-المقاولات-والفوترة-الإلكترونية-كيفية-معالجة-الدفعات-المقدمة-والمبالغ-المحتجزة-مع-هيئة-الزكاة-والضريبة-والجمارك),
  search summary). **Already done.** The existing printed column does this.

## 4. Questions for Karim

Each names the step it would change, a recommendation, and what changes if ruled otherwise. All are
`PROPOSAL` recommendations, not rules.

1. **When does cash reach the safe for a cheque?** Step 15, E5, E6. Now: at the moment of recording, and a
   bounce posts nothing (`ChequeBounceEffect`, Q101, Q91). **Recommend:** a cheque extract is تم تحصيله only
   when the cheque is "تم صرفه"; until then it shows "شيك مستلم" and the safe is not credited; a bounce returns
   the extract to صادر. Cash and transfer unchanged. **If ruled the other way** (cash at receipt): E6 needs a
   persistent warning on any uncleared cheque and the safe balance on the Owner's overview must be read as
   "including cheques in hand". Needs Q91 account ruling either way.
2. **Part payments and a client who pays a different amount.** Step 13. Now: one full payment, no amount
   field (Q98, Q100). **Recommend:** E5 takes the amount received and the amount withheld, shows the expected
   figures beside them, records what arrived, writes no balancing entry (already required by `SPEC §6.7`),
   and the extract becomes تم تحصيله when received plus withheld reaches net. **If ruled otherwise:** E5
   stays three fields and Finance cannot record reality when a client short-pays.
3. **A client cut the quantities: is that a credit note or a revised extract?** Step 19, E9. Now only a
   credit note is built (Q106). **Recommend:** price or deduction disputes resolve as a credit note;
   quantity disputes resolve as a revised extract that runs the full chain again, with the original set to
   باطل and its postings reversed except the hold. **If ruled "credit note only":** E9 stays as briefed and
   the revised path is struck from `SPEC §7`'s sentence.
4. **After a credit note, how is the extract collected?** Step 21. Now collection is refused while a credit
   note is raised or approved. **Recommend:** the collection posts net payable less approved credit notes, and
   a raised-but-undecided note blocks collection with a clear refusal naming the note. **If ruled otherwise:**
   the extract never reaches تم تحصيله after any credit note.
5. **Who types the two material values, and may a site engineer see money?** Steps 2, 15 of section 2.
   Now the preparer types them (`ExtractPreparationScope`). **Recommend:** the SE enters quantities only;
   the Supervisor enters the two values at the QC step, because he already confirms field quantities
   (`SPEC §7`). **If ruled "SE types them":** E2 keeps two money fields on a screen that otherwise shows no
   money, and `spec.md` §9 needs a one-line exception.
6. **Who confirms the real quantity, and where?** Step 4 and D-214. **Recommend:** the Supervisor, on the
   extract, line by line, before QC is submitted, with the daily-log figure shown as "تقريبي" and the
   confirmed figure beside it, cumulative to date. **If ruled otherwise:** the extract keeps billing the
   daily-log approximation, and the TO gate is a gate over numbers nobody confirmed.
7. **Who records a collection, and does the Owner approve a receipt?** Step 13 (Q97). **Recommend:** Finance
   records, the Owner reads. **If the Owner approves receipts:** E5 gains a waiting state "ينتظر المالك", the
   cash leg waits for approval, and the Owner needs a collection read (today `ExtractCollect` is Finance only).
8. **Who records the client's approval of a change order, and can a small firm run the chain?** Steps 22–23
   (Q103, Q105). The chain needs three different TO users and the Owner. **Recommend:** TO prepares, the
   Supervisor does the technical review, Marketing (who holds the client relationship) records the client's
   approval with a date and a reference to the signed paper, the Owner approves. **If ruled "TO for all three":**
   Kaff needs three TO accounts, or the separation rule gets a stated exception for the client step.
9. **Withholding: what base, which clients, and what the client actually withheld.** Steps 14, 17 (Q99, Q114,
   Q30). **Recommend:** keep `WithholdingBase = NetPayable` until Karim rules; show it as "المتوقع"; let Finance
   type the amount the client actually withheld and the discount-notice number; treat an exempt payer as
   category none. Rate changes after the first issued extract are refused. **If ruled otherwise:** E5's
   withheld field becomes read-only and any difference becomes a Finance problem outside the system.
10. **Who is told?** Step 20, 23, section 2 break 12. **Recommend:** extend the waiting line to credit
    notes (Owner), change orders at each gate (named role), and cheques received but not deposited
    (Finance), and give the Owner one list across projects. **If ruled "extracts only":** the Owner finds
    credit notes and change orders only by opening each project.
11. **May the junior site engineer read the extract list?** Section 2 break 2. **Recommend:** yes, money-free
    (the response already omits money). **Otherwise:** he creates an extract and never sees it again.
12. **What is in the weekly QC report, and what is an NCR?** Steps 4, 6. **Recommend:** the report is a note
    plus optional photos plus a list of NCRs raised this week; an NCR is a small record (title, extract,
    open/closed) the TO can pick when splitting. **Otherwise:** E4 cannot be built, and a partial
    approval is a free-text fiction.
13. **Does the held portion bill later, and on which extract?** Step 6 (`ExtractPartialApprovalScope`).
    **Recommend:** the next extract carries released work as its own evidence. **Otherwise:** held work is
    certified and never billed.
14. **Who sets the withholding category, on which screen, and when?** Step 17 (`D-049`: Finance, at
    contract creation or approval). **Recommend:** a field on the project's terms screen, shown to Finance only.
    **Otherwise:** every collection withholds zero.

## 5. Gaps where the server cannot yet support the journey

1. **Withholding category has no endpoint.** `Project.SetWithholding` exists; no route, no request. Needed for steps 14, 17.
2. **`ExtractActions` lacks flags** `collect`, `reverseCollection`, `raiseDispute`, `resolveDispute`, `releaseHeldPortion`. Each is role AND assignment AND state AND project state.
3. **Expected collection figures** (cash, withheld, source label) are not returned before collecting, and the actual received and withheld amounts cannot be sent (Q99, Q100). Needs `expectedCollection` on the extract read, and optional `cashReceived`, `withheld`, `noticeReference` on `collect`.
4. **No cheque list.** Only `GET cheques/{id}`. Needs `GET projects/{id}/cheques` with state, extract number, amount, date; the cheque response lacks the extract number; deposit and clearance moves take no date or bank.
5. **No NCR object, no `ReleaseHeldPortion` route.** `HeldForNcrId` is a bare guid.
6. **No line-by-line quantity read** for an extract (what was billed, per BOQ line, cumulative and previous, executed against confirmed). TO and the Supervisor cannot do the gate they own. Also a quantities-only preview before `prepare` for the SE.
7. **Waiting line covers extracts only.** Credit notes, change orders and cheques have none; the adjustment response has no extract number or client.
8. **Revised extract** is refused (`extract.dispute_revision_unruled`) and `Void` is unreachable.
9. **Credit note then collect** is refused for good (`extract.credit_note_open`).
10. **Change order client approval takes no body**: no date, no reference, no attachment. Draft lines cannot be edited (D-199). `prepare` cannot exclude an approved change order, and there is no preview of which will be billed.
11. **`ExtractRead` excludes the junior SE**; `TreasuryReadProject` includes the SE (`D-173`), so an engineer can read credit note amounts through `GET adjustments`.
12. **Web only:** `ExtractStatus` needs `Disputed` and `Void`; `extract.status.*` and `extract.gate.*` need Arabic for the dispute gates; the list's `waitingText` must handle them.
13. **QC report has no content and no body.**
14. **Collection read is Finance only** (`ExtractCollect`). The Owner sees "collected" and "outstanding" on the list but cannot open the payment.

## 6. The three changes that would most improve the journey

1. **Record collection and follow the cheque (E5, E6), with the cheque rule decided first (question 1).** Money
   the client owes has no way into the system, and when it does arrive it is counted as cash on day one.
   Every other improvement is smaller than closing the loop on cash. Needs gaps 2, 3 and 12.
2. **Let the Technical Office and Supervisor see the quantities they are approving (gap 6, question 6).** The
   chain's whole point is a quantity gate. Today it is a gate over totals.
3. **Start the chain from a screen (E2) and fix the engineer's blind spot (break 2, question 11).** Nothing
   in the chain can be demonstrated, or even tested by hand, until an engineer can start an extract and see where
   it is.

## 7. The order to build the screens in

Each slice ends with something a real person can use.

1. **Slice 1: start and walk the chain.** Web status fix (gap 12) → E2 New extract → E3 changes (waiting line,
   absent-not-disabled for every gate, rejection reason at the top). Ends at: an engineer starts an extract, and
   it can be walked to صادر on a phone. Needs question 11 and gap 2 of section 2 settled. Fits `AC-500-A`.
2. **Slice 2: money in.** Server gaps 2, 3 (flags and expected amounts) → E5 Record collection → E6 Payment
   and cheque. Ends at: Finance closes an issued extract, and a cheque can be followed. Needs question 1
   ruled first, or built to the current rule with the warning in question 1.
3. **Slice 3: withholding is real.** Gap 1 (endpoint) and its field (question 14) → E5's withheld line begins to show a
   figure. Ends at: a corporate client's collection reconciles. Small, and it is where `SPEC §6.7` is tested.
4. **Slice 4: change orders.** E11 → E12 → E13. Ends at: TO raises, the chain runs, the next extract bills it.
   Needs question 8 for who sees which buttons.
5. **Slice 5: disputes.** E8 → E9 → E10. Ends at: an issued extract can be disputed and settled with a credit note.
   Needs questions 3 and 4 before E9 is final.
6. **Slice 6: oversight.** E7 Cheques in hand, E4 Approve part (needs NCR, question 12), the Owner's cross-project
   waiting list (question 10).

## 8. Screen briefs

Thirteen screens: two changed (E1, E3), eleven new. Phone first at 390 wide, RTL. The primary action sits in
the shell slot. A control the caller may not use is absent, never disabled; a primary is never disabled for a
fixable gap (the shell shows what is missing instead). Arabic labels are suggestions for the copy review, not
final copy. No screen below carries an id, a code or an enum name.

---

### E1. Extracts list — `projects/:projectId/extracts` (exists, changed)

- **Brief rests on:** steps 1, 3, 12, 18. `RULED D-204` for the waiting line. No `ASSUMED` item. Settles with question 11 for the SE.
- **Who.** Owner, Finance, TO, Supervisor, and the SE if question 11 is ruled yes. Phone.
- **Entry.** Project workspace tab المستخلصات; the project overview's waiting line.
- **Data from.** `GET /api/projects/{id}/extracts` (`ExtractRead`, role and assignment). Money is null for a caller without extract money.
- **The one job.** See every extract of this project, which one needs me, and open it.
- **Primary action.** "مستخلص جديد" (SE or whoever holds `ExtractPrepare`), only when none is in flight. After pressing: E2.
- **What they must see.** 1. Extract number and status pill. 2. Who it waits on, by role and name; barred holders named ("لا يمكنه الاعتماد: …") (`RULED D-204`, `BarredHolders`). 3. Net payable. 4. Collected and outstanding. 5. Period.
- **What stays off.** Ids. Money for Supervisor and SE (the response already hides it). Cheque or reference detail. The enum names.
- **Words beyond labels.** None.
- **States.** Empty: "لا توجد مستخلصات" and, for the preparer, the primary. Loading: skeleton rows. Failed: existing refusal with retry. Refused: role without extract read sees no tab. `Disputed` status must render (gap 12).
- **Next.** Whoever the row's waiting text names. They find out from the same line on the project overview.

### E2. New extract — `projects/:projectId/extracts/new` (NEW)

- **Brief rests on:** steps 1–2. `SPEC §7`, `RULED D-193/D-194, D-214`, `ASSUMED ExtractPreparationScope`. **Depends on question 5 (who types the two values) and gap 6 (quantities preview).**
- **Who.** SE (any assigned engineer, `ExtractPrepare`). Phone.
- **Entry.** E1's primary; the project workspace primary slot.
- **Data from.** Today: only the form. Needs a quantities preview (gap 6). Submit: `POST /api/projects/{id}/extracts` with `periodStart`, `periodEnd`, `materialValueOnSite`, `materialInstalled`.
- **The one job.** Start the next extract for the period.
- **Primary action.** "إنشاء المستخلص". After pressing: E3 for the new extract, status مسودة, waiting line "ينتظر المشرف".
- **What they must see.** 1. Period (start defaults to the day after the last extract's end). 2. What will be billed, by باب, as quantities to date and since the last extract, no prices (gap 6). 3. Material on site and material installed (if question 5 keeps them here). 4. Approved change orders that will go on this extract, titles only. 5. The last extract's number and status.
- **What stays off.** Work value, hold, advance, any figure derived from price. Net payable. The BOQ rates. Ids.
- **Words beyond labels.** One line under the two material fields, only if question 5 keeps them: "قيمة بالجنيه، تُدخل مرة لكل فترة". Reason: a site engineer sees money nowhere else, so a bare currency field would look like a mistake.
- **States.** Empty (nothing executed yet): "لا توجد كميات منفذة في هذه الفترة", no primary. Loading. Failed: reason in plain words. Refused: another extract is in flight → names its number and status and links to it ("مستخلص ٢ ما زال مفتوحًا"); BOQ not signed → "لم يُعتمد جدول الكميات بعد". A stopped project: absent.
- **Next.** The Supervisor. He finds out through the project waiting line.

### E3. Extract detail — `projects/:projectId/extracts/:extractId` (exists, changed)

- **Brief rests on:** steps 3–11, 18. Mostly `SPEC §7` and `RULED D-189`, `D-193–D-195`, `D-204`.
- **Who.** All five roles. Each sees only the controls whose server flag is true.
- **Entry.** E1 row; waiting line; E2 after create.
- **Data from.** `GET /api/projects/{id}/extracts/{id}` (`ProjectRead`). Document and evidence only for the three money permissions. Needs flags (gap 2), a line-by-line quantity block (gap 6), and the dispute reason and credit-note summary.
- **The one job.** See where this extract stands and take the one step that is mine.
- **Primary action.** One, in the shell slot, by state and role: "تسليم تقرير الجودة" (Supervisor), "اعتماد الكميات" (TO), "تطبيق الخصومات" (Finance), "اعتماد" (Owner), "إصدار" (Owner), "تسجيل التحصيل" (Finance, opens E5), "تعديل" (SE after a rejection). After pressing: a one-line confirmation where it moves money, then the same page in the next state with a new waiting line. Reject, split, dispute, and reversal are secondary and sit below.
- **What they must see.** 1. Status and who it waits on. 2. If rejected: the reason, by whom, when, at the top. 3. The six figures and the printed column with the "المجموع مطابق" mark (existing). 4. Change orders in their own section with their own totals (`SPEC §5.1`). 5. History of every gate with actor, time and reason.
- **What stays off.** Money for the SE and Supervisor. Derivation panels stay behind "التفاصيل" as now. Ids. The names of users as codes. Collection method and reference (those are E6).
- **Words beyond labels.** The existing "no action" sentence when nothing is mine, which names why (separation of duties vs stage): the layout cannot say "you prepared this one" without a word.
- **States.** Loading, failed with retry (existing). Refused: a rejection with a reason shows in a banner. Empty document: "لم تُطبق الخصومات بعد" (existing). Project stopped: issue absent, and one line saying why. Disputed: status معترض عليه, dispute reason shown, primary "حل الاعتراض" (Finance).
- **Next.** The role named in the waiting line; the page shows holders and barred holders.

### E4. Approve part — sheet on E3 (NEW, Slice 6)

- **Brief rests on `ASSUMED ExtractPartialApprovalScope` and `PROPOSAL` for the NCR picker. Settles with questions 12 and 13.**
- **Who.** TO (`QuantityGateApprove`), phone.
- **Entry.** Secondary link under E3's primary "اعتماد الكميات": "اعتماد جزء".
- **Data from.** `POST .../technical-approval` with `approvedPortion` and `heldForNcrId`. Needs an NCR list (gap 5).
- **The one job.** Approve a share for payment and name the NCR that holds the rest.
- **Primary action.** "اعتماد النسبة". After pressing: E3 with status معتمد جزئيًا and "نسبة المعتمد ٧٠٪" in history.
- **What they must see.** 1. Percentage (1–99). 2. The NCR chosen from open ones. 3. What the percentage covers (work value, not material) if the ruling says so. 4. The amount approved and held, if the caller sees money.
- **What stays off.** Ids, NCR codes (title only). Money for a TO without money.
- **Words beyond labels.** One line: "المتبقي ينتظر إغلاق ملاحظة الجودة", because the consequence is not visible from two fields.
- **States.** No open NCR: "لا توجد ملاحظات جودة مفتوحة" and a way to add one (question 12). Refused: percentage out of range.
- **Next.** Finance; waiting line.

### E5. Record collection — `projects/:projectId/extracts/:extractId/collect` (NEW)

- **Brief rests on:** steps 12–14. `SPEC §6.5, §6.7`, `RULED D-198`; `ASSUMED CollectionApproval, CollectionSettlement, CollectionShortfall`. **Settles with questions 1, 2, 7, 9.**
- **Who.** Finance (`ExtractCollect`). Phone and desktop.
- **Entry.** E3 primary when status is صادر; E1 row; cheques list.
- **Data from.** `POST .../extracts/{id}/collect` with `method`, `date`, `reference`. Expected figures need gap 3. Prior collection (if reversed): `GET .../collection`.
- **The one job.** Record that the client paid this extract.
- **Primary action.** "تسجيل التحصيل". After pressing: E6 for this extract, status تم تحصيله (for cheque, "شيك مستلم" if question 1 is ruled that way).
- **What they must see.** 1. Method: نقدًا / شيك / تحويل. 2. Date (default today). 3. Reference (receipt, transfer or cheque number). 4. Expected cash and expected withheld, with the contract's category named. 5. The client's name.
- **What stays off.** Cheque states and bank details (E6). The tax category's code. Other ledgers (hold, advance). Any balancing field.
- **Words beyond labels.** None. If question 2 is not ruled and the amount cannot be typed, one line: "يُسجل المبلغ المتوقع كاملًا", because Finance must know no other amount can be entered.
- **States.** Empty: extract not صادر → the page names its status and links to E3. Loading. Failed with the server's reason. Refused: credit note open ("يوجد إشعار دائن لم يُحسم" naming the note, gap 9); project state refuses; no reference. A missing field names the field and keeps the primary live, no disabled button.
- **Next.** Nobody, unless question 7 puts the Owner in (then the Owner). For a cheque: Finance, to deposit.

### E6. Payment and cheque — `projects/:projectId/extracts/:extractId/payment` (NEW)

- **Brief rests on:** steps 15–16. `SPEC §6.5`, `RULED D-198, D-200`; `ASSUMED ChequeBounceEffect, CollectionReversalEffect`. **Settles with question 1 and Q91.**
- **Who.** Finance (`ExtractCollect`). Phone.
- **Entry.** E5 result; E3's "الدفعة" link when collected; E7.
- **Data from.** `GET .../extracts/{id}/collection` and `GET .../cheques/{id}`; moves `POST .../cheques/{id}/deposit|clearance|bounce`; `POST .../extracts/{id}/collection-reversal` with `reason`.
- **The one job.** See what was received and move the cheque to its next state.
- **Primary action.** The next legal cheque move, one only: "إيداع الشيك" when مستلم, "تأكيد الصرف" when تم إيداعه. After pressing: same page, new state, the history line, and the next primary. Bounce ("مرتد") is a secondary danger action beside clearance. "تصحيح التسجيل" (reverse) is a quiet link opening a reason panel.
- **What they must see.** 1. Cash received. 2. Withheld at source. 3. Method, date and reference. 4. Cheque state with the date it changed. 5. If reversed: the reason.
- **What stays off.** Ids. Withholding category code. The account names. Other extracts' payments.
- **Words beyond labels.** None.
- **States.** Not a cheque: no cheque block. Reversed record: shown struck with its reason. Failed move: names the state it is in now (another user may have moved it). Refused: illegal move is absent (clearance on مستلم) rather than disabled.
- **Next.** For cheque states, Finance. After a bounce, whoever question 1 names.

### E7. Cheques in hand — `projects/:projectId/cheques` (NEW, Slice 6)

- **Brief rests on `PROPOSAL`, backed by `PRACTICE` [oe_pdc](https://apps.odoo.com/apps/modules/17.0/oe_pdc) and gap 4. No spec step requires a register.**
- **Who.** Finance, Owner reads if question 7 allows. Phone.
- **Entry.** Project workspace money group; the waiting line.
- **Data from.** `GET /api/projects/{id}/cheques` (does not exist, gap 4).
- **The one job.** See every cheque not yet cleared and which needs a move.
- **Primary action.** None on the list; each row opens E6. (A list's job is finding.)
- **What they must see.** 1. Extract number. 2. Cheque state. 3. Amount. 4. Date received. 5. Days since the state last changed.
- **What stays off.** Cheque and reference numbers beyond the row's secondary line. Ids. Bank codes.
- **Words beyond labels.** None.
- **States.** Empty: "لا توجد شيكات معلقة". Loading, failed with retry.
- **Next.** Finance, from the row.

### E8. Raise a dispute — `projects/:projectId/extracts/:extractId/dispute` (NEW)

- **Brief rests on `SPEC §7`, `RULED D-202`, `ASSUMED` holder (Q106).**
- **Who.** Finance (`ExtractDisputeRaise`). Phone.
- **Entry.** E3 secondary "تسجيل اعتراض", when status is صادر.
- **Data from.** `POST .../extracts/{id}/dispute` with `reason`.
- **The one job.** Record that the client disputes this extract, and why.
- **Primary action.** "تسجيل الاعتراض". After pressing: E3, status معترض عليه, reason in history, primary "حل الاعتراض".
- **What they must see.** 1. Reason (required). 2. Extract number, net payable and what is already collected. 3. Who disputed (the client name).
- **What stays off.** Credit-note amount (E9). Hold balance. Ids.
- **Words beyond labels.** None.
- **States.** Loading. Failed. Refused: only صادر can be disputed; a تم تحصيله extract names the rule ("تم تحصيله، صحح التحصيل أولًا"), pending Q106. Blank reason names the field.
- **Next.** Finance, to resolve; the Owner is not told until a credit note exists (question 10).

### E9. Resolve a dispute — `projects/:projectId/extracts/:extractId/dispute-resolution` (NEW)

- **Brief rests on `SPEC §7, §6.9`, `RULED D-202`. Revised-extract option is `ASSUMED` refused. Settles with questions 3, 4.**
- **Who.** Finance (`ExtractDisputeResolve`). Phone.
- **Entry.** E3 primary when status is معترض عليه.
- **Data from.** `POST .../extracts/{id}/dispute-resolution` with `kind`, `reason`, `amount`.
- **The one job.** Settle the dispute with a credit note.
- **Primary action.** "إصدار إشعار دائن". After pressing: E3, status صادر, and a line "إشعار دائن بانتظار المالك".
- **What they must see.** 1. Dispute reason. 2. Amount of the credit note (positive, at most net payable less earlier credit). 3. Reason for the resolution. 4. Net payable and credit already raised.
- **What stays off.** The hold (never touched, never shown here). The target account. A "revised extract" option until the server accepts it.
- **Words beyond labels.** None. A line is needed only if question 4 is ruled "reduces collection": "يخفض المبلغ المستحق التحصيل".
- **States.** Loading. Failed. Refused: amount above the cap names the cap; blank reason; extract is not معترض عليه.
- **Next.** The Owner, for the credit note. He finds out only if question 10 is ruled yes.

### E10. Credit note — `projects/:projectId/adjustments/:adjustmentId` (NEW)

- **Brief rests on `SPEC §6.9`, `RULED D-202`, `ASSUMED` approver (Q107).**
- **Who.** Owner decides; Finance reads. Phone.
- **Entry.** The extract's detail ("إشعار دائن بانتظار المالك" link); the project waiting line once question 10 is ruled.
- **Data from.** `GET /api/projects/{id}/adjustments/{id}`; `POST .../approval`; `POST .../rejection` with `reason`.
- **The one job.** Decide whether to approve this credit to the client.
- **Primary action.** "اعتماد الإشعار الدائن". After pressing: this page as approved, and the extract's detail shows the credit under it. "رفض" is secondary and needs a reason.
- **What they must see.** 1. Amount. 2. Reason. 3. The extract it credits (number, net payable). 4. Who raised it and when. 5. Credit already approved on this extract.
- **What stays off.** The posting accounts. Ids. Hold balance.
- **Words beyond labels.** None.
- **States.** Already decided: shows who decided and the reason, no actions. Raiser cannot approve: the primary is absent and one line says why. Failed. Refused.
- **Next.** Finance, who learns the extract can be collected again (gap 9, question 4).

### E11. Change orders — `projects/:projectId/change-orders` (NEW)

- **Brief rests on `SPEC §13`, `RULED D-199`; `ASSUMED ChangeOrderApprovers`.**
- **Who.** TO, Owner, Supervisor, anyone with `ProjectRead`. Money only for holders of a change-order permission.
- **Entry.** Project workspace; extract detail's change-order section.
- **Data from.** `GET /api/projects/{id}/change-orders`.
- **The one job.** See every change order and where each one stands.
- **Primary action.** "أمر تغيير جديد" for the preparer. After pressing: E12.
- **What they must see.** 1. Number and title. 2. Status in Kaff's chain words. 3. Who it waits on (needs gap 7). 4. Total (if money is visible). 5. Whether it has been billed on an extract.
- **What stays off.** Lines (E13). Ids. Money for the Supervisor.
- **Words beyond labels.** None.
- **States.** Empty: "لا توجد أوامر تغيير". Cost Plus and Design projects show "مسجل" and no total.
- **Next.** The role in the status.

### E12. New change order — `projects/:projectId/change-orders/new` (NEW)

- **Brief rests on `SPEC §13`, `RULED D-199`. No Draft editing exists, so a mistake means withdraw and recreate (gap 10).**
- **Who.** TO (`ChangeOrderPrepare`). Phone and desktop.
- **Entry.** E11 primary.
- **Data from.** `POST .../change-orders` with `title`, `lines[description, unit, quantity, unitPrice]`.
- **The one job.** Describe the variation and price it, line by line.
- **Primary action.** "حفظ أمر التغيير". After pressing: E13 in مسودة, primary "إرسال للمراجعة".
- **What they must see.** 1. Title. 2. Lines. 3. The running total, derived. 4. For Cost Plus or Design: no price fields at all.
- **What stays off.** Catalogue ids; the line is a copy. BOQ lines (never merged).
- **Words beyond labels.** None.
- **States.** Empty lines: the primary stays live and refuses with "أضف بندًا واحدًا على الأقل". Failed. Refused: zero or negative quantity or price names the line.
- **Next.** The technical reviewer, after submission.

### E13. Change order — `projects/:projectId/change-orders/:id` (NEW)

- **Brief rests on `SPEC §13, §9`, `RULED D-199`; `ASSUMED` for who holds each step (Q103, Q105). Settles with question 8.**
- **Who.** Everyone with `ProjectRead`; the step controls by flag. Phone.
- **Entry.** E11; waiting line.
- **Data from.** `GET .../change-orders/{id}`; `POST .../submit`, `technical-review`, `client-approval`, `owner-approval`, `technical-rejection`, `client-rejection`, `owner-rejection`, `withdrawal`, `log`.
- **The one job.** Move this change order one step, or see why it stopped.
- **Primary action.** The step that is mine, one only: "إرسال للمراجعة", "اعتماد المراجعة", "تسجيل موافقة العميل", "اعتماد المالك", "تسجيل". After pressing: new status and next role. Rejection (reason required) and withdrawal are secondary.
- **What they must see.** 1. Status and who it waits on. 2. Lines and total (money holders). 3. Gate history with reasons. 4. Whether it was billed, and on which extract. 5. The revised contract value, once approved.
- **What stays off.** Ids. Prices for the Supervisor. Any control the caller cannot use.
- **Words beyond labels.** One line on client approval, "تسجيل موافقة العميل لا يُنهي الأمر، بقي اعتماد المالك", only if question 8 is ruled so that the same screen records it with no date field; layout cannot say what the step is not.
- **States.** Approved: "سيُحاسب في المستخلص القادم". Rejected: reason and by whom, no actions. Failed. Refused: barred actor sees the absence and one line.
- **Next.** The role in the status.

## 9. One end-to-end chain test per module, for the Verifier

Both run through the real UI at 390 wide in Arabic, one user per role, the §15 acceptance project, and compare to
the server's own document. Neither tests the rulings that are still open.

**Module 4 — `ExtractChain_Phone_SiteEngineerToIssued_Section15Extract1`**

1. SE creates extract 1 on E2 (period, material 100,000 on site, 0 installed). Status مسودة. SE sees no money anywhere.
2. SE (still the creator) opens the same extract: no gate control is present, and the "no action" line says why.
3. Supervisor submits QC. TO rejects with a blank reason: refused. TO rejects with a reason: مسودة, reason visible to SE. SE revises evidence. Supervisor submits again.
4. TO approves. Finance applies deductions. Printed column shows work 300,000, hold 60,000, advance 75,000, تشوينات +75,000, net **240,000**; "المجموع مطابق".
5. Owner approves, then issues. Status صادر. Safe balance unchanged (assert through the ledger read). Receivable shows 240,000.
6. Every user who acted is refused when sent directly to a later gate, and a user with the role but no assignment gets nothing.

**Module 5 — `Collections_Phone_IssuedToCollected_ChequeWithholdingAndDispute`**

1. Contract for a corporate client with a 1% category (needs gap 1). Finance opens extract 1 (240,000 net) on E5: expected withheld 2,400, expected cash 237,600.
2. Finance records a cheque with reference. Extract is تم تحصيله (or "شيك مستلم" per question 1). Assert the safe by the ruling.
3. E6: deposit, then bounce. Assert the state, the audit lines, and the extract per the ruling.
4. Finance reverses the collection with a reason. Extract returns to صادر. Postings are reversed through the original, none edited.
5. Finance records a transfer, extract تم تحصيله; safe +237,600, tax withheld asset +2,400, hold ledger unchanged.
6. Variant on extract 2: issued, Finance raises a dispute with a blank reason (refused) then a reason, resolves with a credit note above net (refused) then within it. Owner approves; the creator cannot. Hold balance unchanged; collection behaves per question 4.

Change orders get their own short test once E11–E13 exist: `ChangeOrder_FourDistinctActors_BilledOnNextExtractInOwnSection`.
