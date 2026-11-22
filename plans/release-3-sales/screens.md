# Release 3, sales spine: screen briefs

Planning only. Arabic, RTL, mobile first for Marketing. Codes and ids stay off screens (Nabil, 2026-11-10 per the project instructions). Step numbers refer to journey.md. Where a brief rests on an open question it says so first.

## S1. Pipeline list (NEW, `/sales`)

Rests on journey steps 4, 6, 7. Stage names and the quiet-days rule are unconfirmed (Q3, Q5).

- **Who:** Marketing, Owner. Phone and desktop.
- **Purpose:** see every live opportunity and which ones need a call today.
- **Data:** client name, title, stage, days quiet, status (Active, Stalled, OnHold), quotation total when one exists. Cost and margin never here.
- **Actions:** primary "new opportunity". Per row: open. Filter by stage, status, "needs follow-up". Search by client name or phone.
- **States:** empty ("no opportunities yet", button to create); loading; failed (retry); refused (no access, no data shown).
- **Next:** opening a row goes to S2.

## S2. New opportunity (NEW, `/sales/new`)

Rests on steps 1 to 3.

- **Who:** Marketing, Owner.
- **Purpose:** start an opportunity on an existing or new client.
- **Data:** client (search by name or phone, or add new inline using the existing client form fields), title.
- **Actions:** primary "create". Repeated phone shows the holder of the number and asks to proceed; it never blocks [D-049]. After save: S3 for the new opportunity.
- **States:** empty client search ("no match, add as new"); saving; failed; refused.
- **Next:** the opportunity is Lead and quiet-clock starts; no one is notified (nothing to notify).

## S3. Opportunity page (NEW, `/sales/:id`)

Rests on steps 4 to 36. The deposit, expense and conversion panels rest on open questions Q1, Q2, Q4, Q11.

- **Who:** Marketing, Owner; Technical Office read-only plus convert.
- **Purpose:** run one opportunity from lead to won or lost.
- **Data (in order):** client and title; stage with the next stage as the primary action; days quiet; deposit paid (amount, date) or "not paid"; quotation total; pre-contract spend total; activity list.
- **Actions:** primary "move to next stage". Secondary: record deposit, add expense, open quotation, put on hold, close as lost (asks reason), reopen (when lost), convert (when Negotiation, Technical Office and Owner only).
- **States:** loading; failed; refused; a stalled opportunity shows its stage and a "quiet for N days" line, with one action that records activity. Stage moves that are not allowed are not offered.
- **Next:** after a stage move, the list shows it with the clock reset. After conversion: S7 on the new project.

## S4. Record site-visit fee (NEW, dialog on S3)

Rests on steps 8 to 11. Blocked on Q1 (which account).

- **Who:** Marketing records; Finance confirms. Phone.
- **Purpose:** record that the client paid the معاينة fee.
- **Data:** amount, date, method, reference.
- **Actions:** primary "record fee". After: S3 shows "deposit paid" and a pending confirmation until Finance acts.
- **States:** amount empty or not positive: refused with the field marked; failed save; refused for other roles.
- **Next:** Finance sees it in its collections queue (the existing collections screen, if Q1 settles on that route).

## S5. Pre-contract expenses (NEW, panel on S3)

Rests on steps 13 to 16. Blocked on Q4.

- **Who:** Marketing, Owner. Phone (receipt photo).
- **Purpose:** record a cost of winning the job.
- **Data:** list with amount, date, what for, receipt; running total.
- **Actions:** primary "add expense". After: row appears, total updates, activity clock resets.
- **States:** empty ("no expenses"); failed; refused.
- **Next:** Finance disburses through the existing flow once Q4 chooses the route; until then the record is a note, not money.

## S6. Quotation with margin view (NEW, `/sales/:id/quotation`)

Rests on steps 17 to 25. Reuses the BOQ builder pattern [exists: BOQ page]. Where the lines are stored rests on Q7. Who sees cost rests on Q6.

- **Who:** Marketing builds; Owner and (if Q6 allows) Marketing see margin. Desktop first, phone usable.
- **Purpose:** price the job and see what it earns.
- **Data (in order):** lines grouped by باب with quantity, unit, unit price, line total; conditions (free entry); line markup with its باب default; margin block with total cost, total sell, profit % as three separate figures; "N open offers on old pricing" banner when it applies.
- **Actions:** primary "print for client". Add item is always visible, searches catalogue by code or description. Custom item allowed, flagged for Technical Office review [spec §4.5 line 137].
- **States:** empty BOQ shows an explicit empty state, never phantom rows [spec §4.5 line 138]; loading; failed; refused. The print view has no cost, no margin, no internal notes.
- **Next:** stage moves to Quotation when the first priced line exists (PROPOSAL, Q8). Client signs outside the system.

## S7. Convert to project (NEW, dialog on S3)

Rests on steps 30 to 36. Blocked on Q11 and Q7.

- **Who:** Technical Office, Owner. Desktop.
- **Purpose:** open the project from a won opportunity.
- **Data:** client, title (editable project name), contract type choice, site-visit fee credit shown if any, quotation total shown as reference.
- **Actions:** primary "open project". After: the project page in Setup, with the estimate reachable as a reference and a link back to the opportunity.
- **States:** refused when the opportunity is not in Negotiation or already converted; failed; no access for Marketing.
- **Next:** Technical Office surveys (حصر) and builds the binding BOQ. Marketing sees the opportunity as won on S1.
