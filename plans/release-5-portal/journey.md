# Release 5 journey: client portal and remaining closure

Written 2026-11-22. Planning only. Nothing here changes `src/`.

Tags on every step: `[spec §x line n]` required by spec.md; `[D-nnn]` decided; `[exists: path]` already in code; `[ASSUMED: note]` running on a RulingNote nobody confirmed; `[practice: source]` context only, never a rule; `[QUESTION Qn]` unruled, see questions.md.

Line numbers are lines of `d:\ERP\spec.md` as read on 2026-11-22.

## 0. What exists and what does not

| Area | Today | Source |
|---|---|---|
| Portal permissions | `PortalRead` and `PortalApprove` exist, project-scoped, Client role only. No endpoint uses them. | [exists: src/Domain/Authorization/PermissionCatalogue.cs line 680] |
| `Role.Client` cannot hold `ProjectRead`, nor a department | Fixed after the D-035 defect. | [D-035] |
| Staff sign-in refuses a Client credential with 401 | Built. No client sign-in exists anywhere. | [D-062][D-063] |
| Portal host | Ruled "a separate host, a completely isolated interface". Separate deployment or second origin is open (N7), and it must be settled before KAFF-810. | [D-051 Q33] [stories/backlog.md line 1205] [QUESTION Q1] |
| `/api/portal/*`, unshared DTOs, reflection test | Not built. D-035 lists both as "still owed, before slice 8". | [D-035] |
| Change order client approval | Built as a staff step: `POST /api/projects/{id}/change-orders/{id}/client-approval`, held by the assigned supervising engineer, who records it on the client's behalf until the portal exists. | [exists: src/Api/Features/ChangeOrders/RecordClientApproval/Endpoint.cs] [D-241 item 4, quoted in ChangeOrderApprovers note] |
| Client rejection of a change order | No client-side reject. Staff reject exists and ends in `Rejected`, terminal. | [exists: RejectChangeOrder] [ASSUMED: ChangeOrderRejection] |
| Design stage weights and the stage billing calculator | Built as values only (`DesignStage`, `DesignStageWeights`, `DesignBillingCalculator`). | [exists: src/Domain/Contracts/ContractDefaults.cs line 49; Calculators.cs line 151] |
| Design phase entity, deliverables, files, revision rounds, watermark, internal review | None. No state machine, no endpoint, no screen. | grep of src, 2026-11-22 |
| Design closure | `CloseProject` refuses Design with `NotApplicableToDesign`; the Design transition table already allows Active to Closed. KAFF-808 is unbuilt. | [exists: src/Domain/Closure/ProjectClosure.cs lines 118-126; Project.cs lines 56-72] |
| Linked projects | `LinkTo` and `ProjectLinkType.ParentChild` exist. `ConvertToExecution` handles design to execution. No endpoint creates a furnishing child. | [exists: Project.cs line 445; ProjectStatus.cs line 74] |
| Photos | `PhotoEntry.IsPublished` exists, starts false, and "nothing here sets it". No publish action. | [exists: src/Domain/DailyLogs/LogEntry.cs line 150] |
| Section progress | `GetProgress` exists for staff. | [exists: src/Api/Features/Projects/GetProgress] |
| Project time plan | No entity. Grep for TimePlan, PlannedEnd, Schedule, Milestone in `src/Domain` finds nothing relevant. | [QUESTION Q9] |
| Client in the seed | No portal Client user in the seed, so §12 has no screen evidence. | [stories/backlog.md line 234, V-33-E] |

Finding that changes the plan: the design half of Release 5 (KAFF-808, 812 design part, 813) has nothing underneath it. The backlog has no story for the design phase itself. A new story, R5-D in stories.md, is needed first.

## A. Client approves a change order (Lump Sum, priced)

1. Technical Office drafts and submits a change order; a different Technical Office user does the technical review. [spec §13 line 569] [exists: CreateChangeOrder, SubmitChangeOrder, ApproveTechnicalReview] [ASSUMED: ChangeOrderApprovers]
2. The change order reaches `ClientApproval`. The client is told that something waits. No SMS or WhatsApp provider exists in the pinned stack. [spec §13 line 569] [QUESTION Q13]
3. The client signs in on the portal host with a Client account. A phone shows pending approvals first. [spec §12 lines 536-541] [D-051 Q33] [QUESTION Q1, Q7]
4. The client opens the change order. He sees the description, each line's quantity, unit and unit price as a sell figure, the total, and what the contract value becomes. He sees no cost price, no margin, no Technical Office notes. [spec §12 line 542] [spec §4.2 line 118] [spec §4 line 222: two decimals to a client]
5. The client approves, or rejects with a typed reason. [spec §12 line 541] [QUESTION Q3] The reason is stored and the change order goes to `Rejected`. [spec §13 line 569] [ASSUMED: ChangeOrderRejection]
6. On approve, the change order moves to `OwnerApproval`. The approving actor stored is the Client user, not a staff user. [spec §13 line 569] [exists: RecordClientApproval, to be re-pointed; the note says "the ClientApproval step takes a Client actor instead"] [QUESTION Q3]
7. The Owner approves; the contract value rises, derived. Nothing changes in the portal flow here. [spec §13 line 569] [exists: ApproveChangeOrderAsOwner]
8. The client sees the revised contract value next time he opens the project. Which value leads, original or revised, is a standing assumption. [ASSUMED: ChangeOrderDisplayedValue]
9. Cost Plus and Design change orders are logged and unpriced. The client approves nothing there; the portal shows no approval step for them. [spec §13 line 569] [spec §12 line 541: "priced change orders"]

Practice, context only: Procore names one designated reviewer on a change order and requires tool permission plus, for private contracts, an access list [practice: https://support.procore.com/products/online/user-guide/project-level/change-orders/tutorials/approve-or-reject-prime-contract-change-orders, search snippet 2026-11-22, page not opened]. It matches Kaff's one-client, one-approver shape. It does not transfer where Procore's owner is a staff-side reviewer in the same product; Kaff's client has a separate host (D-051).

## B. Client approves a design stage

1. Head of Design or a stand-in sets the stage `InProgress`. Five stages, fixed weights 30, 20, 20, 20, 10. The 30% is the deposit. [spec §5.3 line 188] [exists: DesignStageWeights] [QUESTION Q4: Head of Design is phase 2]
2. The designer uploads the stage deliverable and submits it: `DeliverableSubmitted`. [spec §13 line 567] [PROPOSAL: file store is not described anywhere in spec.md; upload is the obvious reading of "deliverable"]
3. Internal review: `InternalReview`. Reviewer unnamed. Rejection returns to `InProgress` with a reason. [spec §13 line 567] [CLAUDE.md: rejections return with a reason] [QUESTION Q4]
4. The reviewer releases the deliverable to the client: `Approved` in the machine reads as the client's approval, so the step that makes it visible in the portal has no state of its own. [spec §13 line 567] [QUESTION Q2: confirm whether `Approved` means internally or client approved; spec §5.3 line 190 says "client approves its deliverable in the portal"]
5. The client opens the stage in the portal and sees a watermarked copy until paid. [spec §5.3 line 191] [QUESTION Q6]
6. The client approves, or sends it back with a note. A send-back inside the agreed rounds is free rework and returns the stage to `InProgress`. [spec §5.3 line 193] [spec §13 line 567]
7. A send-back beyond the agreed rounds is a billable mini change order referencing stage and round number. But §13 says a Design change order is `Draft → Logged`, unpriced. The two lines disagree. [spec §5.3 line 193 vs spec §13 line 569] [QUESTION Q5]
8. On the client's approval the stage is billable: `Invoiced`. [spec §5.3 line 190] [spec §13 line 567] [exists: DesignBillingCalculator gives the fee] What the invoice is has no answer: §5.3 says design has "no extract". [spec §5.3 line 194] [QUESTION Q2]
9. Staff record the collection through the existing collection flow. Payment unlocks the clean deliverable. [exists: collections, KAFF-316] [QUESTION Q6: what counts as paid]
10. The next stage opens. Nothing in spec.md says stages run strictly in order. [QUESTION Q4 sub-point; default in Q4]

Practice, context only: one Egyptian-region interior design agreement found by search has 30% on signing, then 20% at concept/schematic completion and 20% at design development, with several revisions inside the price [practice: https://www.scribd.com/document/664014373/CDA-Agreemet, search snippet 2026-11-22, page not opened]. It is one contract, not a norm, and the stage names differ. An Egyptian consultancy brochure ties later deliverables to client sign-off of schematic design [practice: https://issuu.com/oekoplan/docs/okoplan_copmany_brochure, snippet, not opened]. Kaff's five fixed weights are ruled; practice adds nothing to them.

## C. Client reads progress, extracts, money and photos

1. The client lists his projects: design, execution and furnishing under one login, each with its own accounts. [spec §5.4 line 198-200] [QUESTION Q11 on what he sees per linked project]
2. Project home: section-level progress, time plan, paid versus remaining, pending approvals. [spec §12 line 540] [exists: GetProgress for staff; portal read to be built, unshared type] [QUESTION Q9 for the time plan]
3. Extracts: the client sees his مستخلصات. What an extract line shows him (deductions, hold, advance, withholding) is not stated. Two decimals only. [spec §12 line 540] [spec line 222] [spec §15 lines 600-607 shows hold and advance as client-side figures] [QUESTION Q8]
4. A Cost Plus client sees a summary statement, not line items. [spec §5.2 line 182] [spec §16 line 641, assumption 20, owner Karim]
5. Photos: only photos a staff member published deliberately. Today nothing can publish. [spec §8 line 357] [exists: IsPublished, never set] [QUESTION Q12]
6. Released deliverables: design deliverables after release; final deliverable on delivery; last 10% collected after. [spec §5.3 lines 191-192] [QUESTION Q6]
7. The client never sees costs, margins, catalogue, subcontractors, internal notes, or any other client's data. [spec §12 line 542] The boundary is a separate host, separate DTOs, and a reflection test. [D-035] [D-051 Q33] [stories/backlog.md KAFF-810, 811]

## D. Design closure

1. All five stages are `Invoiced` and collected; the final deliverable is released on delivery. [spec §5.3 line 192] [spec §11 line 530]
2. Last 10% is collected after the final deliverable. [spec §5.3 line 192] [spec §11 line 530]
3. IP transfers. How the transfer is recorded is unstated. [spec §11 line 530] [QUESTION Q10]
4. The project goes Active to Closed. No snag list, no handover, no hold, no warranty. [spec §11 line 530] [exists: Project.cs DesignTransitions lines 56-72]
5. Who closes it: the Owner is the assumed closer for execution. [ASSUMED: ClosureActors, ProjectClose to the Owner] [QUESTION Q10]
6. Closure's marketing asset and manual referral (KAFF-814) are in spec §11 line 532 and in the backlog, but outside the brief for this plan. Listed in stories.md as not scoped.

## E. Furnishing as a child project

1. After or alongside the parent, a user creates a furnishing project linked to the parent. It is a small execution project with its own BOQ and subcontractors, separate accounts and billing. [spec §5.4 lines 199-200, 201] [spec §11 line 531] [exists: LinkTo, ParentChild]
2. A project never changes type. Furnishing is created as a new execution project. [spec §5.4 line 202]
3. The client sees parent and child together under one portal login. [spec §5.4 line 198]
4. Who creates it, in which parent state, and whether the parent's closure waits on it: all open. [QUESTION Q11] [ASSUMED nothing: no RulingNote exists for furnishing]

## F. The gate: the portal leaks nothing

1. Every portal response is its own type, in its own namespace, with no reference to a Domain entity or a staff response type. [D-035] [stories/backlog.md KAFF-810]
2. A reflection test walks every portal response type, transitively through properties, and fails the build when a Domain entity type or a cost-shaped name is reachable. The banned-name list is a PROPOSAL, shown in stories.md R5-811. [D-035] [stories/backlog.md KAFF-811]
3. A portal token cannot call any `/api/projects/*` endpoint, and a staff token cannot call `/api/portal/*`. [D-035] [D-062] [PROPOSAL: the mirror test]
4. Client A cannot read client B's project by guessing an id. [spec §12 line 542] [exists: ProjectAccessPolicy matches Project.ClientId to User.ClientId, per PermissionCatalogue.cs line 679]

## Where today's app breaks this journey

1. No client can sign in anywhere. The staff screen refuses the credential and no portal host exists.
2. A client's approval of a change order is recorded by an engineer. The client does not act, and the record names the wrong actor if the portal takes it over.
3. No design project can run: no stages, no deliverables, no files, no review step, so nothing for the client to approve.
4. A published photo cannot exist. The client's photo feed would always be empty.
5. A design project cannot close. `CloseProject` refuses Design.
6. A furnishing child cannot be created through any endpoint found.
7. No time plan exists to show.
8. Nothing tells the client that an approval waits.
