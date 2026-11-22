# Release 3, sales spine: the journey

Planning only. Date 2026-11-22. Nothing here is built unless a step says `[exists: path]`.

Tags: `[spec §x line n]` spec.md · `[D-nnn]` decisions.md · `[exists: path]` in the tree today · `[practice: source]` context only, never a rule · `[QUESTION Qn]` open, see questions.md.

## What exists today (checked 2026-11-22)

- `src/Domain/Sales/Opportunity.cs`: entity with code, client, title, stage (Lead..Contract), status (Active, Stalled, OnHold, ClosedWon, ClosedLost), `LastActivityAt`, `ConvertedProjectId`. Methods: `Create`, `RecordActivity` (revives Stalled), `CloseLost(reason)`. Its own remarks say the pipeline behaviour is "NOT implemented here".
- `KaffDbContext.cs:98` has the `Opportunities` DbSet. Nothing in `src/Api` or `src/Web` mentions an opportunity. No endpoint, no screen.
- `Permission.OpportunityManage`, granted to Owner and Marketing, company-wide [exists: src/Domain/Authorization/PermissionCatalogue.cs:403]. `ClientManage` also Owner and Marketing (:341).
- `Project.OpportunityId` exists as a nullable field [exists: src/Domain/Projects/Project.cs:94,107]. No API sets it.
- `ProjectCreate` is Owner and Technical Office only [D-052; spec §2 lines 73-88].
- The BOQ builder prices lines with `lineRate = baseSellRate x (1 + conditions) x (1 + lineMarkup)` and keeps `CostPrice` per line [exists: src/Domain/Boqs/BoqLine.cs:63-72,100-127]. An unsigned project's BOQ can be priced and printed as a quotation [D-208; KAFF-417 AC-417-E]. No margin screen exists (no "profit"/"margin" hit in `src/Api/Features/Boqs` or the BOQ web files).
- Unsigned projects are archived after a follow-up period, default 30 days, an assumption [exists: src/Domain/Rulings/FollowUpPeriod.cs; D-208].
- `AccountType` has no account for an opportunity or a site-visit deposit; accounts are built per project [exists: src/Domain/Treasury/AccountType.cs, ProjectAccountSetFactory.cs]. No notification mechanism exists in `src/Domain`.

## The journey

Roles: Marketing (M), Owner (O), Technical Office (TO), Finance (F).

### A. Lead

1. M registers or finds the client. A repeated phone number shows which client holds it and asks whether to proceed; it never blocks. [spec §2 lines 56-71; D-049] [exists: stories KAFF-119, KAFF-124]
2. M opens an opportunity on that client: title, nothing else required. Stage is Lead, status Active. [spec §3 line 94; exists: Opportunity.Create]
3. The opportunity code is generated, never typed. [PROPOSAL, copying D-049's rule for client codes; the current `Create` takes a typed code]
4. M moves the opportunity through Meeting, SiteVisit, Quotation, Negotiation. [spec §3 line 94, §13 line 548] Stage names are unconfirmed. [spec §16 assumption 6] [QUESTION Q3]
5. Every logged action (stage move, call note, deposit, expense, quotation change) counts as activity and resets the clock. [spec §3 line 96; exists: RecordActivity] What counts as activity is not defined. [QUESTION Q5]

### B. Inactivity

6. Day 2 without activity: first alert. Day 4: second alert. Day 7: status becomes Stalled. Days are configurable. Activity revives it to Active at the same stage. [spec §3 line 96; exists: Opportunity.RecordActivity]
7. Who is alerted, and how, is not stated. Proposed: the pipeline list shows a coloured "days quiet" chip and an "needs follow-up" filter, computed on read, no stored alert (the same on-read approach as the follow-up period). [PROPOSAL; FollowUpPeriod.cs "archiving is derived on read"] [QUESTION Q5]

### C. Site visit (معاينة) deposit

8. M records that the client paid the معاينة fee: amount, date, method (cash, cheque, transfer), reference. [spec §3 line 99; spec §6.5 line 270 for method and reference]
9. The fee is held as a deposit against the opportunity. It is not revenue. [spec §3 line 99] Which account holds it is not ruled, and none of the five ledgers fits. [spec §6.4 lines 246-251] [QUESTION Q1]
10. Money lands in the safe or bank, so it goes through Finance like any collection. [spec §6.1; PROPOSAL that M records and F confirms, to honour "nobody creates and approves the same movement" per CLAUDE.md] [QUESTION Q1]
11. If the client does not pay for the visit, the record does not exist. Whether a visit may go ahead unpaid is not stated. [QUESTION Q2]
12. M attaches the site-visit report (notes, photos) to the opportunity. It travels to the project at conversion. [spec §3 line 102; exists: nothing] Report content is not specified. [QUESTION Q9]

### D. Pre-contract expenses

13. M or O records a cost spent winning the job (transport, survey, printing): amount, date, what for, receipt. [spec §3 line 100]
14. It is tracked against the opportunity and not recovered from the client. [spec §3 line 100]
15. It pays out of the safe, so it is a money movement. Spec §6.10 says every expense is tagged project or company, never neither. An opportunity is neither. [spec §6.10 line 323] [QUESTION Q4]
16. If the opportunity is lost, the expense stays on its record. Whether it then moves to company expense is not stated. [QUESTION Q4]

### E. Quotation and margin

17. M builds the estimate on the opportunity: add catalogue items by code or description, quantities, باب sections auto-created, line markup defaulted from the باب, conditions entered free per opportunity. [spec §4.2 lines 112-119, §4.5 lines 133-138; exists: BoqLine pricing, BOQ builder on projects]
18. The quantities are the Estimated state. The Technical Office sets Surveyed (حصر) later. [spec §4.3 lines 121-126]
19. A margin view shows total cost, total sell and profit % as three separate figures. A single blended figure is forbidden. [spec §4.2 line 119]
20. Cost and margin never appear on the client's printout. [spec §4.2 line 118, §12 line 542]
21. Who may see cost and margin is not ruled for Marketing. Today internal staff may read catalogue cost [D-177 §3 via KAFF-940 rule 4]; Marketing is not named. [QUESTION Q6]
22. The quotation prints for the client: client, date, lines by باب with quantity, unit price, line total, total. [D-208; KAFF-417 AC-417-E]
23. Where the quotation lives conflicts between sources. Spec §3 line 102 puts the estimate on the opportunity and makes it a reference. D-207/D-208 say the quotation is the BOQ on an unsigned project, written because the Opportunity module was not built. [QUESTION Q7]
24. Open quotations on old prices: the system says "you have X open offers on old pricing" and a human re-prices per offer. [spec §4.4 line 131; KAFF-417 "Not in this story"]
25. Stage moves to Negotiation. Quotation changes during negotiation are kept as dated versions. [PROPOSAL; spec silent] [QUESTION Q8]

### F. Lost, hold, reopen

26. M may put an opportunity on hold. It stops inactivity alerts. [spec §13 line 548 lists OnHold; "stops alerts" is PROPOSAL]
27. Closed Lost requires a reason. [spec §3 line 97; exists: CloseLost] The reason is free text today. [exists: Opportunity.cs:128] [QUESTION Q10]
28. If a deposit exists on a lost opportunity, what happens to it (refund, keep as revenue) is not stated. [QUESTION Q1]
29. Reopening attaches to the same client and the same opportunity returns to a live stage. [spec §3 line 98, §13 line 548; D-017 open on how Reopened is modelled] [QUESTION Q3]

### G. Conversion

30. At Closed Won the Technical Office or Owner opens the project from the opportunity. Marketing cannot. [D-052; spec §2 lines 73-88] The "who clicks Closed Won" step is not stated. [QUESTION Q11]
31. The contract type is chosen at conversion: Lump Sum, Cost Plus or Design. [spec §3 line 102; spec §5]
32. The project carries the client, the site-visit report and the estimate. [spec §3 line 102] The project's `OpportunityId` is set. [exists: Project.OpportunityId]
33. The estimate becomes a reference for the Technical Office, never the binding BOQ. The TO surveys (حصر), builds the binding BOQ, and the BOQ freezes by copy at signature. [spec §3 line 102, §4.3 line 123, §4.4 line 129] This differs from KAFF-417, where the priced BOQ itself is signed. [QUESTION Q7]
34. The deposit transfers as a credit against the new contract. [spec §3 line 99] Which ledger it lands in, and whether it shows in extract maths, is not stated. The spec's worked example has no such credit. [spec §15 lines 598-607] [QUESTION Q1]
35. Pre-contract expenses stay on the opportunity and are not carried into project cost. [spec §3 line 100 "not recovered"] Whether they enter project cost is not stated. [QUESTION Q4]
36. The opportunity records the project it became. [exists: Opportunity.ConvertedProjectId]
37. A design-to-execution conversion is a different link on the project side. [spec §5.4; D-209; KAFF-415] Out of this journey.

## Comparable practice (context only, not a rule)

Not fetched in this lane: the brief limits sources to the repo, and nothing here depends on outside practice. Where a comparable CRM shows a "days since last activity" chip on a pipeline card, that is the basis of step 7, and it stays a PROPOSAL until Karim answers Q5.
