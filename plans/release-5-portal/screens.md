# Release 5 screen briefs

Portal screens are for the Client role, on a phone, Arabic RTL, on the portal host (Q1). They live in their own Angular entry, not the staff shell. Codes and ids are off every screen by default (Nabil, 2026-11-10). Words beyond labels: none unless listed. Money shows two decimals (spec.md line 222).

Journey step numbers refer to journey.md sections A to F.

## Portal screens (all NEW)

### P1. Portal sign-in
- Route: NEW, portal host `/sign-in`.
- Who: Client, phone.
- One job: let a client in.
- Primary action: sign in. Next the client sees his project list, or his single project if he has one.
- Must see: phone number field, password field, sign-in button.
- Off: any staff wording, any reference to the staff app.
- Words beyond labels: none.
- States: failed shows one refusal line with the same text for every cause (staff sign-in uses one envelope, D-063). Loading: button busy. A forced password change opens the change screen first (same rule as staff).
- Next: nobody waits. 
- Sources: journey A3, C1. Rests on PROPOSAL (phone plus password as the identifier) and Q1, Q7.

### P2. My projects
- Route: NEW `/projects`.
- Who: Client, phone.
- One job: pick which of his projects to open.
- Primary action: tap a project card. Opens P3.
- Must see: project name, a badge when something waits on him, the stage of the project in Kaff's own status words, paid against remaining as one line.
- Off: contract type name, project code, margins, anything staff.
- Words: none.
- States: empty (no project assigned) says there is no project yet, offers nothing. Failed: retry. Skipped entirely when only one project exists.
- Next: staff register new projects against his client file; he finds new ones here.
- Sources: journey C1; spec §5.4, §12. Rests on Q11 for how linked projects group.

### P3. Project home
- Route: NEW `/projects/:id`.
- Who: Client, phone.
- One job: show where his project stands and what waits on him.
- Primary action: open the first pending approval (change order or design stage). Opens P5 or P7. If nothing is pending there is no primary action.
- Must see, in order: pending approvals; progress by section; paid versus remaining; the time plan; links to extracts, photos, deliverables.
- Off: cost, quantities, hold ledger name, subcontractors, internal notes, staff names.
- Words: none.
- States: empty progress (no log yet) shows the sections with zero. No time plan shows that block absent, not a message (Q9). Failed: retry.
- Next: a client with nothing pending waits on Kaff; staff reach him through the pending badge only.
- Sources: journey C2, A3, B5; spec §12 line 540. Rests on Q9 (time plan source) and Q13 (notification).

### P4. Extracts list and one extract
- Route: NEW `/projects/:id/extracts`, `/projects/:id/extracts/:extractId`.
- Who: Client, phone.
- One job: show what Kaff certified and what he pays.
- Primary action: none, read only.
- Must see: extract number, period work this time and cumulative, deductions he bears, net he pays, paid or not paid.
- Off: QC report, Technical Office gate notes, NCRs, quantity lines of Kaff's engineers, internal rejection reasons, any draft or pre-issue state.
- Words: none.
- States: empty means no extract issued, an empty list. Failed: retry.
- Next: none.
- Sources: journey C3; spec §12, §15. Rests on Q8: whether hold, advance and withholding lines show. Cost Plus clients get a summary, no line items (spec §16 line 641, assumption 20).

### P5. Change order for approval
- Route: NEW `/projects/:id/change-orders/:coId`.
- Who: Client, phone.
- One job: let him approve or refuse a priced change.
- Primary action: approve. After pressing, the screen shows approved with the date and the revised contract value; the pending badge drops.
- Secondary: refuse, which asks for a typed reason first (stored, shown to Kaff).
- Must see: what changes (description), lines with quantity, unit and price, total, contract value before and after, who raised it as a Kaff team (not a name).
- Off: cost price, margin, technical review notes, reviewer names, audit, catalogue references.
- Words: none beyond labels. Approving moves money terms; a confirmation step that restates the total is a layout, not a sentence.
- States: already decided shows read-only with outcome. Withdrawn by Kaff shows withdrawn. Failed approval: stays on the screen, nothing recorded, retry. Refused by server (not his project): shows not found.
- Next: Owner approval waits on the Owner, who sees it in his queue. The client sees "with Kaff".
- Sources: journey A4 to A8; spec §12 line 541, §13 line 569. Rests on Q3 (replaces the engineer's record) and ASSUMED ChangeOrderRejection (rejected is terminal).

### P6. Design stage for approval
- Route: NEW `/projects/:id/design/:stage`.
- Who: Client, phone.
- One job: approve a stage deliverable or send it back.
- Primary action: approve. After pressing, the stage shows approved; its fee shows as due; the next stage shows.
- Secondary: send back with a note, which uses one of the agreed rounds.
- Must see: stage name, the deliverable (watermarked until paid), its fee and weight as an amount, revision rounds used of agreed, paid status.
- Off: designer internal notes, internal review comments, hours, other stages' internal state.
- Words: a line that a send-back past the agreed rounds is billable. The layout cannot carry it: the client must know before pressing. Wording comes from Q5.
- States: nothing submitted yet shows the stage as upcoming. Watermarked and paid copy are separate files; failed download: retry. Round limit reached: send-back becomes a request that Kaff prices (Q5).
- Next: Technical Office or Accounts bill the stage; Accounts collect. The client sees the status change.
- Sources: journey B5 to B8; spec §5.3 lines 190-193. Rests on Q2, Q4, Q5, Q6.

### P7. Released deliverables
- Route: NEW `/projects/:id/deliverables`.
- Who: Client, phone.
- One job: let him download what is his.
- Primary action: download a file. The file opens or saves; the watermark state is the file's, not the screen's.
- Must see: stage, deliverable name, released date, clean or watermarked mark.
- Off: file paths, versions history, draft files, internal revisions.
- Words: none.
- States: empty none released. Failed: retry.
- Next: none.
- Sources: journey B9, C6; spec §5.3 lines 191-192, §12. Rests on Q6.

### P8. Published photos
- Route: NEW `/projects/:id/photos`.
- Who: Client, phone.
- One job: show site photos Kaff chose to publish.
- Primary action: none; tap opens a photo.
- Must see: photo, date, caption. Newest first.
- Off: who took it, location, unpublished photos, log entries.
- Words: none.
- States: empty shows no photos yet. Failed: retry.
- Next: none.
- Sources: journey C5; spec §8 line 357, §12. Rests on Q12.

## Staff screens (changed or NEW, existing staff shell)

### S1. Publish photos
- Route: NEW action on the daily-log photo grid, existing log route.
- Who: supervising site engineer, Technical Office; phone.
- One job: choose which photos the client may see.
- Primary action: publish selected. After pressing, each shows published and appears on P8.
- Must see: photo, caption, date, published or not.
- Off: client name, other projects' photos.
- Words: none.
- States: failed publish keeps the photos unmarked. Unpublish: Q12.
- Next: nobody waits; client sees it on next open.
- Sources: journey C5. Rests on ASSUMED (no note yet; add a RulingNote) and Q12.

### S2. Design stage workspace (staff)
- Route: NEW `/projects/:id/design`.
- Who: Head of Design or stand-in, Owner; desktop and tablet.
- One job: move a design stage from in progress to the client.
- Primary action: submit deliverable (upload file). After pressing, the stage reads under internal review.
- Secondary actions, one per state: approve internally and release to client; return to in progress with reason.
- Must see: five stages with state and fee, revision rounds used, current deliverable, paid status, who the stage waits on.
- Off: other projects, internal salary or hours.
- Words: none.
- States: empty before terms are set: shows nothing; setup is the project screen. Failed upload: file stays unattached, retry.
- Next: reviewer for internal review; then the client, who is told per Q13.
- Sources: journey B1 to B4. Rests on Q4 (reviewer) and Q2.

### S3. Design closure
- Route: existing closure page `closure`, a Design variant.
- Who: Owner, desktop.
- One job: close a design project.
- Primary action: close the project. After pressing, status reads Closed.
- Must see: five stages collected, final deliverable released, last 10% collected, IP transferred.
- Off: handover, snag, hold, warranty sections.
- Words: none.
- States: unmet conditions listed as the conditions themselves, with the action that settles each. Refused: stays open.
- Next: nobody; project is Closed.
- Sources: journey D1 to D5. Rests on ASSUMED ClosureActors (Owner closes) and Q10.

### S4. Create furnishing project
- Route: NEW action on the parent project's detail page.
- Who: Owner, Technical Office; desktop.
- One job: open a linked furnishing project.
- Primary action: create. After pressing, the new project opens in Setup with the parent named.
- Must see: parent project, client (fixed, same as parent), name, contract terms as for any execution project.
- Off: BOQ rows of the parent.
- Words: none.
- States: refused when the parent is in a state Q11 forbids; failed: stays.
- Next: Technical Office builds the BOQ.
- Sources: journey E1 to E4; spec §5.4, §11. Rests on Q11.

### S5. Create client portal account
- Route: existing user form, Client role.
- Who: Owner (only the Owner creates users, spec §9).
- One job: give a client a sign-in.
- Primary action: create. After pressing, the Owner sees the temporary password to hand over.
- Must see: client picked from the master, phone, role fixed to Client.
- Off: department field (refused for Client, D-035), project assignment (clients match by client file).
- Words: none.
- States: refused when the client file has no phone.
- Next: the client signs in on P1 and must change the password.
- Sources: journey A3; D-035. Rests on Q7.

### S6. Change order detail, step changed
- Route: existing change-order screens (staff).
- Who: Technical Office, supervising engineer, Owner.
- One job: show a change order waiting on the client, instead of letting an engineer record it.
- Change: at `ClientApproval`, the staff action "record client approval" is removed once the portal is live; the step shows "with the client".
- Words: none.
- States: unchanged otherwise.
- Next: the client; Owner afterwards.
- Sources: journey A2, A6. Rests on Q3.
