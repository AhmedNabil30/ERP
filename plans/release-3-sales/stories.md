# Release 3, sales spine: draft stories

Planning only. Points are rough (1 = hours, 3 = a day, 5 = two to three days, 8 = most of a week). Every AC cites a source. ACs that rest on an open question say `[Q]` and are not to be built before it is answered. Source tags: spec lines are spec.md; journey step numbers are journey.md.

Order: R3-01 to R3-04 can start now. R3-05 to R3-09 wait on named questions. Total 62 points.

## R3-01 Open and list opportunities (API) · 5 points

| AC | Criterion | Source |
|---|---|---|
| A | Create an opportunity on an existing client with a title; stage Lead, status Active | spec §3 line 94; exists Opportunity.Create |
| B | The code is generated, not typed | PROPOSAL (copies D-049); the current `Create` takes a typed code, so the signature changes |
| C | Needs `OpportunityManage`; Marketing and Owner pass, others get 403 | exists PermissionCatalogue.cs:403 |
| D | List with filter by stage and status, and search by client name or phone | PROPOSAL; spec silent on filters |
| E | Every create and stage change writes an audit record with before and after | CLAUDE.md Audit |
| F | A client is created or found through the existing client endpoints; a repeated phone warns and does not block | D-049; spec §2 line 56 |

## R3-02 Move through the stages, hold, and the stage rules · 3 points · waits on Q3

| AC | Criterion | Source |
|---|---|---|
| A | Move forward one stage at a time from Lead to Negotiation | spec §3 line 94 |
| B | Put on hold and resume; hold returns to the same stage | spec §13 line 548 |
| C | Every move resets the last-activity time and revives a Stalled opportunity | spec §3 line 96; exists RecordActivity |
| D | Which stages are legal to skip or return to [Q] | spec §16 assumption 6; D-017 |

## R3-03 Inactivity: quiet days, alerts, Stalled · 3 points · waits on Q5 for the alert channel

| AC | Criterion | Source |
|---|---|---|
| A | Day 2 and day 4 flag the opportunity; day 7 shows status Stalled. All three day counts are settings | spec §3 line 96 |
| B | Any activity revives it to Active at the same stage | spec §3 line 96; exists RecordActivity |
| C | Stalled and the quiet flags are computed when read, not stored | PROPOSAL; same approach as FollowUpPeriod.cs |
| D | A follow-up filter lists flagged opportunities | PROPOSAL (journey step 7) |
| E | An OnHold opportunity does not flag or stall | PROPOSAL (journey step 26) |

## R3-04 Close lost with a reason, and reopen · 3 points · waits on Q10

| AC | Criterion | Source |
|---|---|---|
| A | Close lost is refused without a reason; the reason is stored | spec §3 line 97; exists CloseLost |
| B | Reopen returns the opportunity to a live stage on the same client; no second client is created | spec §3 line 98, §13 line 548 |
| C | Both write audit records with the reason | CLAUDE.md Audit |
| D | Reasons are free text or a short list [Q] | spec silent; Q10 |

## R3-05 Site-visit deposit · 8 points · waits on Q1, Q2

| AC | Criterion | Source |
|---|---|---|
| A | Record a fee on an opportunity with amount, date, method, reference | spec §3 line 99; spec §6.5 line 270 |
| B | The fee is not revenue | spec §3 line 99 |
| C | The deposit lands in an account that is none of the five ledgers and is never netted against them [Q] | spec §6.4 lines 246-251 |
| D | The person who records it cannot confirm it | CLAUDE.md Permissions |
| E | A posting is never edited; a mistaken fee is reversed by a new posting | CLAUDE.md Money |
| F | Money lands in the safe or bank only after Finance confirms [Q] | PROPOSAL; spec §6.1 |

## R3-06 Pre-contract expenses · 5 points · waits on Q4

| AC | Criterion | Source |
|---|---|---|
| A | Record an expense on an opportunity: amount, date, purpose, receipt | spec §3 line 100 |
| B | The expense is not charged to the client | spec §3 line 100 |
| C | It is tagged to the opportunity, which is neither project nor company, so spec §6.10 must be amended or the expense must be tagged company [Q] | spec §6.10 line 323 |
| D | Total spent shows on the opportunity | PROPOSAL (journey step 14) |

## R3-07 Estimate and margin view · 8 points · waits on Q6, Q7

| AC | Criterion | Source |
|---|---|---|
| A | Price lines as `baseSellRate x (1 + sum of conditions) x (1 + lineMarkup)`; conditions are additive and free entry | spec §4.2 lines 113-116; exists BoqLine.Price |
| B | lineMarkup defaults from the باب and can be overridden per line | spec §4.2 line 117 |
| C | Margin shows total cost, total sell and profit % as three figures; no single blended figure | spec §4.2 line 119 |
| D | Cost and margin never appear in any client-facing output | spec §4.2 line 118; spec §12 line 542 |
| E | Who sees the margin block is enforced by the server, not by hiding [Q] | CLAUDE.md Permissions; Q6 |
| F | Empty estimate shows an explicit empty state | spec §4.5 line 138 |
| G | A custom item is flagged for Technical Office review and does not write to the catalogue | spec §4.5 line 137 |
| H | Quantities are the Estimated state; the Surveyed state is not set here | spec §4.3 lines 122-123 |
| I | An estimate on an opportunity never holds a foreign key that reaches a signed contract | spec §4.4 line 129 |

## R3-08 Print the quotation and warn on old pricing · 5 points · waits on Q7

| AC | Criterion | Source |
|---|---|---|
| A | The printout has client, date, lines by باب with quantity, unit price and line total, and the total; no cost, no margin | D-208; KAFF-417 AC-417-E; spec §4.2 line 118 |
| B | Prints through the browser print with print CSS; no PDF library | KAFF-417 AC-417-E |
| C | A count of open quotations priced before the last catalogue price change is shown; re-pricing is decided per quotation by a person | spec §4.4 line 131 |

## R3-09 Convert to a project · 8 points · waits on Q1, Q7, Q11

| AC | Criterion | Source |
|---|---|---|
| A | Only Technical Office and Owner may convert; Marketing is refused | D-052; spec §2 lines 73-88 |
| B | Conversion creates a project with the chosen contract type, carries the client and sets `OpportunityId`; the opportunity records the project | spec §3 line 102; exists Project.OpportunityId, Opportunity.ConvertedProjectId |
| C | The estimate and site-visit report are readable on the project as a reference; they are not the BOQ, which starts empty | spec §3 line 102 |
| D | The deposit transfers as a credit on the new contract [Q: which ledger] | spec §3 line 99 |
| E | A second conversion of the same opportunity is refused | PROPOSAL; follows from `ConvertedProjectId` being single |
| F | The §15 worked example still passes unchanged for a project with no deposit | spec §15 lines 598-614 |
| G | Conversion writes an audit record | CLAUDE.md Audit |

## R3-10 Screens: pipeline list, new, opportunity page · 8 points

| AC | Criterion | Source |
|---|---|---|
| A | S1, S2, S3 in screens.md, Arabic, RTL, mobile width | screens.md; CLAUDE.md RTL |
| B | No codes, ids or explanatory sentences on screen | project instructions (Nabil, 2026-11-10) |
| C | All strings through i18n | CLAUDE.md |
| D | A menu item hidden for a role is also refused on the server | CLAUDE.md Permissions |

## R3-11 Screens: deposit, expenses, quotation, convert · 6 points

| AC | Criterion | Source |
|---|---|---|
| A | S4, S5, S6, S7 in screens.md, built after R3-05 to R3-09 settle | screens.md |
| B | The print view contains nothing from the margin block | spec §4.2 line 118 |
| C | Same copy, i18n and RTL rules as R3-10 | CLAUDE.md |
