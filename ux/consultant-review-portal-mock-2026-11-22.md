# Consultant review: Release 5 portal mock (mode: mock)

Mock: `.design/v2/team-a/portal.html` (P1-P8 client, S1-S6 staff). Judged against `plans/release-5-portal/screens.md`.
Seen at 390 light for every screen. Extra states seen: p3 none and design, p4 detail and unpaid, p5 confirm, refuse and approved, p6 limit, confirm, paid, sendback and upcoming, p2 dark 390, p3 light 1440, p5 dark 1440. Not seen: other dark and 1440 states, loading and failed states, S-screens beyond their normal state. Shots are in `qa/shots-consultant/pt-*.png`.

Overall: the client half is clean. One filled button per screen. No code, no cost, no reviewer name, no staff name on P1-P8. P5 shows unit price and total; the build must send the sell price only (a build rule, not visible in the mock). The problems are money clarity and wording, not layout.

## Ranked findings

### A. Flow and money: what the client owes

1. **No screen says "you owe X now".** P2, P3 and the P3 design view show "المدفوع / المتبقي" (paid / remaining). "Remaining 1,630,000" is the rest of the contract, not what is due. The one real debt, extract 3 net 355,000 unpaid, is visible only after P3, المستخلصات, مستخلص 3. P3 does not put it under "بانتظارك". A homeowner reads 1.63 million as "what I must pay". Fix (layout): in P3 "بانتظارك" and in the P2 card badge, add unpaid extracts as a row with the amount, like change orders. Rename "المتبقي" so it is plainly the contract balance, or drop it from the P2 card.
2. **P6: when do I pay?** Before approval the screen shows fee 10,800 and "الحالة: غير مدفوعة" on a stage the client has not approved. It reads as already overdue. Only the confirm box says "المستحق 10,800", so approval creates the debt, but the main view hides that. Fix (layout): before approval show the fee as "الأتعاب" only; show the paid mark only after approval; after approval show the amount as due from the client.
3. **P6 limit state: price unknown before pressing.** The brief says the client must know before pressing. The line says Kaff prices the extra round, but gives no figure and no promise of a quote first. "إرجاع للتعديل" and "طلب تعديل إضافي" also look like two different actions. Fix (flow, depends on Q5): show the extra-round fee, or state that nothing is charged until Kaff quotes and the client accepts.
4. **P4 unpaid extract is a dead end.** "غير مدفوع" with no due date, no way to pay, nobody to call. A stranger would phone Kaff. Fix (flow): decide what Kaff shows: account details, a contact number, or a due date. A decision for Nabil.
5. **P3 with two pending items, one button.** The bar says "مراجعة أمر التغيير" (singular) while both rows above are tappable. A client cannot tell which change order the bar opens. Fix (layout): with two or more pending, drop the bar and let the rows act; keep the bar for exactly one.
6. **P1: no way back in.** No forgot-password, by design (the Owner resets). A locked-out client has no contact on screen. Fix (flow): show Kaff's phone number on sign-in. A number is data, not an explaining sentence.
7. **P5 approved state says "عند كف".** After approval the status is "عند كف" with no sign of what Kaff does next (Owner approval). Also "تمت موافقتك" on P5 and "تمت الموافقة" on P6 and S2 are two phrases for one idea. Fix (copy): one phrase; show the next step as a stage line, not "عند كف".

### B. Clarity: wording a homeowner will not know

8. **P3 per-section date is unlabeled.** A bare 2026-10-31 under كهرباء 60% reads as a finish date, and 2026-05-31 under مباني 100% reads as a completion date. They are planned ends. Today is 2026-11-22: electrical (10-31, 60%) and plumbing (11-15, 30%) are past plan and nothing marks them late. The approved vocabulary has متعثرة. Fix (layout and copy): label the date "النهاية المخططة" and mark late sections.
9. **"أمر تغيير" and "المستخلص" are contract and accountant words.** A homeowner may say "تعديل على العقد" and "دفعة". Confirm with Nabil which words clients use. "الخصومات" on P4 (55,000, no hint what) needs the same check; per-type lines are a Q8 decision.
10. **Three words for the deliverable:** الملفات (P3, P7), المسلَّم (S2 button "رفع المسلَّم"), المخرج النهائي (S3). Pick one.
11. **S2 "قيد التنفيذ"** is not an approved status word. Use جاري العمل.
12. **"بعلامة مائية" carries an X icon** on P6 and P7. X reads as refused. Use a neutral mark. P7 also does not say what makes the file clean (payment); the client must go to P6 to find out.
13. **P6 stage bar:** five unnamed segments plus "المرحلة 2 من 5" repeat each other. The stage list in the P3 design view carries the meaning better. Reuse it or drop the bar.
14. **Dates and digits:** ISO dates (2026-11-22) and Latin digits throughout. Confirm one format with Nabil (day/month/year is what Egyptian readers expect).

### C. Noise: on screen without a client reason

15. **P6 and P7 file names** repeat the stage title and carry ".pdf". The extension is a file detail; drop it.
16. **P5 confirm box** repeats the total and contract value shown directly above it, and three buttons are then visible (sticky bar, confirm, back). The restate is intended, but hide the sticky bar while the confirm box is open.
17. **P2 cards:** the meter has no label for which side is paid; "المتبقي ج.م" puts the currency mid-line. Minor.
18. **Explaining sentences on client screens:** one, on P6 limit: "التعديل خارج الجولات المتفق عليها ويُسعَّر من كف". The brief allows it, but finding 3 says it should be a figure. None on P1-P5, P7, P8. None on S-screens.
19. **P5 has no date** raised and no description beyond the title (the brief lists "description").

### D. Staff screens

20. **S1:** "سحب النشر" wraps to two lines at 390 and its target is small. Heading "صور اليوم" lists photos from 10-28 and 11-05; call it "صور الموقع". Select then publish: one primary action, fine.
21. **S2** at 390: each stage is a five-line card, with "—" in the "بانتظار" row for stages nobody waits on. Show that row only when someone is waited on. The current stage highlight is good. "السداد" here and "الحالة" on P6 name the same paid status: use one word.
22. **S3** is clean: four conditions, one action. The unmet view (a settle action per condition) was not seen. **S4 and S5** are clear forms with one action each. S4 asks for "نوع العقد" on a furnishing project; confirm the Owner chooses it. S5's temporary-password view was not seen.
23. **S6** is fine: step chain shows "عند العميل" and no action.

### E. Light, dark, 1440

24. Dark (P2 at 390, P5 at 1440): contrast fine; pending badge and meter invert correctly; the sticky Approve turns light beige and still reads as primary. At 1440 the column is 688 px and the sticky bar 720 px, so bar and cards do not line up. A 720 px column on 1440 leaves two empty thirds; acceptable for a phone-first client. Make bar and cards the same width.

## Previous review findings
None for portal scope. This is the first review of this mock.

## Three changes first
1. Add "what you owe now" to P3 and P2 (finding 1) and fix the P6 paid and due order (2). The portal exists to get an approval and a payment. Today the payment is buried.
2. Replace the P6 limit sentence with a price, and decide the P4 unpaid next step (3, 4). These are flow decisions for Nabil and cost a paragraph now.
3. Settle the word list before build (7, 9, 10, 11, 14): one phrase each for approved, deliverable, change order, extract, and the date format. Each costs a line in the mock now and a lane after the build.

## Brief checks
- State names in `portal-notes.md` matched what rendered for every state I opened.
- `health` fails when the stack is down, but `shot` on file URLs still works.
- A batch of 18 shots in one PowerShell call passes the 120 s timeout; run it in the background.
