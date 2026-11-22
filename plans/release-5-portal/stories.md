# Release 5 draft stories

Draft, not yet in `stories/`. Points are estimates (Fibonacci-ish), backlog values kept where a backlog story exists (stories/backlog.md lines 1193-1199). Each AC cites its source. "HELD" means the AC waits on a question in questions.md.

Order: R5-A, KAFF-810, KAFF-811 first (the gate), then reads, then approvals, then the design module, then closure and furnishing.

| Id | Title | Pts | Source of the points |
|---|---|---:|---|
| KAFF-810 | Portal surface: separate host, `/api/portal/*`, unshared response types, client session | 8 | backlog |
| KAFF-811 | Reflection test and leak tests: the gate | 5 | backlog |
| R5-A | Client account and portal sign-in | 3 | new, estimate |
| R5-B | Portal reads: projects, progress, time plan, extracts, paid versus remaining | 8 | new, estimate |
| R5-C | Publish a photo to the client | 3 | new, estimate |
| KAFF-812 | Client approves change orders and design stages | 5 | backlog; the design half depends on R5-D |
| R5-D | Design phase module: stages, deliverables, rounds, internal review, stage billing | 13 | new, estimate; no backlog story exists |
| KAFF-813 | Deliverables watermarked until paid | 3 | backlog; the file store is in R5-D |
| KAFF-808 | Design closure | 5 | backlog |
| KAFF-809 | Furnishing as a linked child project | 3 | backlog |
| | Total | 56 | |

Not scoped: KAFF-814 closure marketing assets and manual referral (3 points). spec.md §11 line 532 requires it; the brief for this plan does not name it. Also out of this release: the two closure rejections KAFF-802 and 803, which the plan places in Release 1.

---

## KAFF-810 · Portal surface (8)

Depends on: Q1 (host shape, N7). Not buildable until Q1 is answered (stories/backlog.md line 1212).

- AC-810-A. Given the portal host, when any request arrives, then only `/api/portal/*` and the portal's static files answer; every other path returns 404. [D-051 Q33; D-035]
- AC-810-B. Given a staff token, when it calls any `/api/portal/*` endpoint, then it is refused. [D-035; D-062 mirrored — PROPOSAL for the direction]
- AC-810-C. Given a portal token, when it calls any `/api/projects/*` endpoint, then it is refused. [D-035: Role.Client holds no ProjectRead]
- AC-810-D. Given a Client with a project of his own client file, when he calls a portal read, then it succeeds; given another client's project id, then 404. [spec §12 line 542; PermissionCatalogue.cs line 679]
- AC-810-E. Given the portal session cookie and CORS, then the portal origin has its own cookie and its own session boundary, and CORS names both origins explicitly. [stories/backlog.md line 1209; D-050] HELD on Q1.
- AC-810-F. Every portal response type lives under one portal namespace and shares no DTO with any staff feature. [D-035; backlog KAFF-810]
- AC-810-G. Every portal state change writes an audit record with the Client user as actor. [CLAUDE.md audit rule]

## KAFF-811 · The gate: a reflection test (5)

- AC-811-A. Given every type reachable from every `/api/portal/*` response, when the test runs, then it fails the build if any reachable type is a Domain entity or a staff response type. [D-035 "still owed"]
- AC-811-B. Given the same reachable set, when the test runs, then it fails if any property name contains a name on the banned list. PROPOSAL for the list: cost, margin, markup, profit, catalogue, subcontractor, supplier, internal, note (internal), audit, ncr, qc, technicalOffice, petty, safe, treasury. An allow-list entry is allowed only with a reason in the test, so the contract type value `CostPlus` is allowed and named. [D-035; the list itself is PROPOSAL, not ruled]
- AC-811-C. Given a deliberately added `costPrice` to a portal response, when the suite runs, then it fails. [backlog KAFF-811 "fails the build"]
- AC-811-D. Given the seeded Client A and Client B, when A calls every portal endpoint with B's ids, then every call returns 404 and no body names B. [spec §12 line 542]
- AC-811-E. Given the seed, then at least one portal Client user exists, so the boundary has UI evidence. [stories/backlog.md line 234, V-33-E]
- AC-811-F. Portal money values carry two decimals and no more. [spec.md line 222]

## R5-A · Client account and portal sign-in (3)

Depends on: Q7.

- AC-A-1. Given the Owner, when he creates a user with Role.Client against a client file, then it succeeds, refuses a department, and refuses a missing client file. [spec §9 amendment item 5; D-035] [exists: CreateUser]
- AC-A-2. Given a Client credential, when it is sent to the staff sign-in, then 401 with the standard envelope. [D-062; D-063] [exists]
- AC-A-3. Given a Client credential on the portal host, when sign-in succeeds on first use, then the client is made to change the password before anything else. [PROPOSAL: same rule as staff, KAFF-101 forced password change; confirm in Q7]
- AC-A-4. Given five wrong passwords, then lockout behaves as for staff. [PROPOSAL: reuse staff lockout; not ruled for clients]
- AC-A-5. How the client gets his first password, and a reset. HELD on Q7 and N7 delivery half.

## R5-B · Portal reads (8)

Depends on: KAFF-810. Time plan HELD on Q9; extract line detail HELD on Q8.

- AC-B-1. Given a client with two projects, when he lists them, then he sees both, and a project of another client is absent. [spec §12 line 540, 542]
- AC-B-2. Given a project, when its home is read, then it returns section-level progress, paid, remaining and pending approvals, and no cost field. [spec §12 line 540] [exists: GetProgress for staff, not reused as a type]
- AC-B-3. Given the spec §15 worked example, when the client reads paid versus remaining after extract 2, then paid equals the client's cash received and remaining equals the contract value less that. The figure is derived from postings. [spec §15 lines 600-607; CLAUDE.md derived balances] — the formula for "remaining" is a PROPOSAL: confirm in Q8.
- AC-B-4. Given an extract in Draft, QC, TechnicalOffice, Accounts or OwnerApproval, when the client lists extracts, then it is absent; given Issued or Paid, then it is present. [spec §13 line 565] — PROPOSAL for the visibility cut, Q8.
- AC-B-5. Given a Cost Plus project, when the client reads its statement, then it is a summary with no line items. [spec §5.2 line 182; spec §16 line 641] ASSUMED, assumption 20.
- AC-B-6. Given a project with only unpublished photos, when the client lists photos, then the list is empty. [spec §8 line 357]
- AC-B-7. Time plan. HELD on Q9.

## R5-C · Publish a photo (3)

- AC-C-1. Given an unpublished photo, when the supervising engineer or Technical Office publishes it, then `IsPublished` is true, an audit record is written, and the photo shows on the portal. [spec §8 line 357] [exists: IsPublished, never set] — the permission holder is ASSUMED, no note exists; add a RulingNote.
- AC-C-2. Given a Junior engineer, when he calls publish, then refused. [spec §9 line 378 junior drafts, supervisor submits — PROPOSAL to treat publishing as a submit]
- AC-C-3. Unpublish. HELD on Q12.
- AC-C-4. Given a published photo, when the portal returns it, then no location, no author and no log reference travel with it. [spec §12 line 542; D-035]

## KAFF-812 · The client approves (5)

Change order half (2 points) can ship after KAFF-810. Design half (3 points) waits on R5-D.

- AC-812-A. Given a change order at ClientApproval and a client of that project, when he approves, then it moves to OwnerApproval, the stored actor is the Client user, and an audit record exists. [spec §13 line 569; spec §12 line 541; ChangeOrderApprovers note] HELD on Q3 for whether staff recording stays.
- AC-812-B. Given the same, when he rejects without a reason, then refused; with a reason, then Rejected, reason stored. [CLAUDE.md rejection rule; ChangeOrderRejection ASSUMED]
- AC-812-C. Given a change order in any state other than ClientApproval, when the client approves, then refused. [spec §13 line 569]
- AC-812-D. Given a Cost Plus or Design change order, when the client lists, then none shows an approve action. [spec §13 line 569, line 541 "priced"]
- AC-812-E. Given the portal approve action, when the Owner has not yet approved, then the contract value is unchanged. [spec §13 line 569; ChangeOrderDisplayedValue]
- AC-812-F. Given a client of project X, when he approves a change order of project Y, then 404. [spec §12 line 542]
- AC-812-G. Given a design stage at DeliverableSubmitted or the state Q2 names, when the client approves, then the stage becomes billable and the fee is the stage weight of the design total. [spec §5.3 lines 188-190; exists: DesignStageWeights] HELD on Q2.
- AC-812-H. Given a send-back, then the stage returns to InProgress with the client's note stored and one round counted. [spec §5.3 line 193; spec §13 line 567]
- AC-812-I. Given a send-back past the agreed rounds, then the outcome follows Q5. HELD.
- AC-812-J. Separation of duties: the client approves what he did not create. Nobody who prepared the change order can act as the client. [CLAUDE.md; PROPOSAL that a Client user is never staff, which D-035 already enforces by refusing department]

## R5-D · Design phase module (13)

New. No backlog story. Depends on Q2, Q4, Q5. Without it KAFF-808, 812 design half and 813 cannot start.

- AC-D-1. Given a Design project with terms set, when it is read, then five stages exist, NotStarted, with weights 30, 20, 20, 20, 10 and fees of area × rate × weight. [spec §5.3 lines 186-188; exists: DesignStageWeights, SetDesignTerms] Rounding follows the BOQ's. [exists: Money rounding — PROPOSAL, confirm]
- AC-D-2. Every legal transition of NotStarted, InProgress, DeliverableSubmitted, InternalReview, Approved, Invoiced passes, and every other is refused; rejection returns to InProgress with a reason. [spec §13 line 567]
- AC-D-3. Given a user who submitted a deliverable, when the same user reviews it internally, then refused. [CLAUDE.md; spec §9 line 376]
- AC-D-4. Given a Lump Sum project, when design stage endpoints are called, then refused. A design project has no BOQ, no extract, no hold, no تشوينات. [spec §5.3 line 194]
- AC-D-5. Given the fee of a stage, when it is invoiced, then the amount equals `DesignBillingCalculator`'s fee and no posting uses float. [exists: Calculators.cs line 151; CLAUDE.md money] The document the invoice is: HELD on Q2.
- AC-D-6. Given a design daily log, then it records phase progress %, not materials. [spec §5.3 line 195] — to confirm that exists; not read in this planning pass.
- AC-D-7. Given a stage, when the agreed rounds are exceeded by a send-back, then Q5 decides. HELD.
- AC-D-8. Given the stage fee and the spec §5.3 deposit, then the 30% Concept stage is the deposit. [spec §5.3 line 188] Whether the deposit is billed at signing or at approval of Concept: HELD on Q2.
- AC-D-9. Every state change writes an audit record. [CLAUDE.md]
- AC-D-10. Stage sequence: whether stage 2 may start before stage 1 is paid. HELD on Q4.

## KAFF-813 · Watermark until paid (3)

Depends on R5-D file store and Q6.

- AC-813-A. Given an unpaid stage, when the client downloads its deliverable, then the file is watermarked. [spec §5.3 line 191]
- AC-813-B. Given the stage's fee collected per Q6's definition of paid, when he downloads, then the clean file is served. [spec §5.3 line 191] HELD on Q6.
- AC-813-C. Given the final deliverable, when it is delivered, then it is released on delivery, before the last 10% is collected. [spec §5.3 line 192]
- AC-813-D. Given any request, then the clean file's path is never returned in an unpaid state. [spec §12; D-035 shape] — the watermark is applied server-side; the client never receives the clean bytes before the unlock. PROPOSAL for the mechanism.

## KAFF-808 · Design closure (5)

Depends on R5-D. Closer and IP record HELD on Q10.

- AC-808-A. Given a Design project with all five stages collected and the final deliverable released, when the closer closes it, then it moves Active to Closed. [spec §11 line 530; exists: Project.cs DesignTransitions]
- AC-808-B. Given the last 10% uncollected, when closure is attempted, then refused and the unmet condition is named. [spec §5.3 line 192; spec §11 line 530; exists pattern: ClosureReadings.Unmet in ClosureConditions note]
- AC-808-C. Given a Design project, then no snag, no handover, no hold, no warranty exist for it. [spec §11 line 530; exists: NotApplicableToDesign]
- AC-808-D. Given the IP transfer, when it is recorded, then the date and the recorder are stored. [spec §11 line 530] HELD on Q10.
- AC-808-E. Given a Closed design project that was linked to an execution project, then the execution project's 30% credit is unaffected. [spec §5.4 line 199; exists: Adjustment.RaiseDesignCredit]
- AC-808-F. Every closure writes an audit record. [CLAUDE.md]

## KAFF-809 · Furnishing as a child project (3)

Depends on Q11.

- AC-809-A. Given a parent execution project, when furnishing is created, then a new execution project exists with its own BOQ, accounts and billing, and `LinkType = ParentChild`. [spec §5.4 line 200-201; exists: LinkTo]
- AC-809-B. Given the same client, then the child carries the parent's client. [spec §5.4 line 198]
- AC-809-C. Given the parent, then no project changes type. [spec §5.4 line 202]
- AC-809-D. Given the parent's accounts, then the child's postings never touch them, and the five ledgers never net across the pair. [CLAUDE.md; spec §6.4]
- AC-809-E. Given the portal, then the client lists both projects under one login and each shows its own paid and remaining. [spec §5.4 line 198]
- AC-809-F. Allowed parent states, who creates, and whether parent closure waits on the child. HELD on Q11. No RulingNote exists; add one.
