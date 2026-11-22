# Consultant review, mode mock: Release 4 accounting (A1 to A14)

Date: 2026-11-22. Mock: `.design/v2/team-a/accounting.html`. Briefs: `plans/release-4-accounting/screens.md` and `journey.md`.

Method: read the briefs and the designer's notes, read the mock's state code to learn what each state shows, opened screenshots at 390 and 1440 of A1 (difference), A5 (safe too small), A6 (partial), A7 (posted), A9, A10 (months open), A12 and A13 (owner), and ran `layout-url` on every state of A1, A2, A3, A5, A9, A10, A11, A12, A13 and A14 at 390 and 1440, and A8 difference at 1440. Screenshots are in `qa/shots-consultant/acc-*.png` (gitignored). Screens and states not opened as screenshots (A2, A3, A4, A8, A11, A14 and most failed, loading and forbidden states) were judged from the mock source and the layout measurements only.

Overall: the set is disciplined. Each screen has one job and one primary action, no ids or codes appear, there are almost no explaining sentences, and disabled buttons are explained by what is on the page. The weaknesses are in the flow between screens, in several Arabic words that carry two meanings, and in a few places where the layout hides what the person needs.

## Ranked findings

Ordered by what each costs the business. Tags: kind of problem (flow, clarity, noise) and kind of fix (layout, copy, flow).

### 1. A1, A2, A4, A14: the opening figures and their detail screens disagree on who owns the truth [flow]
A1 asks Finance to type one "الأصول الثابتة" number. A2 then lists every asset with its cost and accumulated depreciation. Nothing says which is entered first or how they tie. At go-live Finance will type 1,300,000 in A1, add assets in A2 that sum to something else, and the opening balance sheet and the first depreciation run will not agree. The same gap exists for prepayments and accruals (A4), loans (A14) and accumulated depreciation. The five ledgers (client advance, hold, firm advance, petty cash, owner account) have no opening line in A1 at all, which matters if Kaff goes live with projects already running.
Fix (flow): either the detail screens are filled first and A1 shows their totals as read-only lines that link back to them, or A1 owns the totals and the detail screens must sum to it before they save. Add opening lines for the five ledgers, or state that they start at zero. Raise with Nabil as a question; do not let the builder choose.

### 2. A6 and A10: the Owner approves a lock he cannot undo, and sees no figures [flow]
The "owner approves" states in A6 and A10 show the same page Finance saw, with two buttons. The journey says a closed month cannot be reopened. In A10 the profit and retained earnings are on the page, which is right. In A6 the Owner sees four step names and a count, but not the month's profit, nor the trial balance totals he is approving. The reject button has no reason sheet drawn. The project rule is that a rejection returns to origin with a stored reason; the notes list this as "not decided", but a month lock deserves it drawn before the build.
Fix (flow, layout): in the A6 owner state show the month's result and the trial balance totals above the steps. Draw the reject-with-reason sheet for A6 and A10.

### 3. Words that carry two meanings, and ideas given two words [clarity, copy]
- "رفض": the red pill on a failed step in A6, and the Owner's button that sends a month back (A6, A10). A system refusal and a human decision share one word.
- "الأصل": the asset (A2 column, A3), the loan principal ("الأصل المتبقي", "الأصل" in A14) and, with "عقد", a contract asset (A7). Three meanings.
- "الموازنة" (budget, A12 and A13) sits in the same sidebar group as "الميزانية" (balance sheet, A9). They differ by one letter. A person scanning the menu will open the wrong one.
- "بند": the open items in A6 ("2 بنود"), the accrual and prepayment lines in A4, and the expense category in A5 (label "البند").
- "مفتوح": an unfinished step in A6, an unreleased accrual in A4, an open month in A10.
- "الملتزم به" (committed) in A12 and A13 is accountant's talk. A site owner reads it as "the one who commits". Nothing called committed exists in the system yet, so the word is free to change.
- "أصل عقد" and "التزام عقد" (A7) are international-reporting terms. The Owner needs to see who owes whom, for example "مستحق من العميل" and "مقدّم من العميل".
- Status words in A12 ("ضمن الموازنة", "قريب من الحد", "تجاوز") are new words in a product whose approved status words are لم تبدأ، جاري العمل، انتهت، متعثرة، تم تأجيلها. They are a different idea, so new words are acceptable, but they reuse the same pill colours as those (see finding 5).
Fix (copy): the Owner button becomes "إرجاع" and the failed-step pill "تعذّر". Loan principal becomes "المتبقي من القرض". Make the balance sheet visibly different in the menu. A5 label "البند" becomes "نوع المصروف". Replace "الملتزم به" and the two contract terms. Check all of these with Karim's accountant. Fix the plural: "2 بنود" should be "بندان".

### 4. A6: the checklist does not say what to do next, and the count adds unlike things [flow, clarity]
In the partial state (390 and 1440) one step is done, three read "مفتوح", the button is disabled and "البنود المفتوحة" reads 6. The 6 is 2 accruals plus 3 projects plus 1 trial balance, three different units added together. Every open row looks identical, so the person does not know which to do first, and the trial balance (which has to be last) looks as clickable as depreciation (which has to be first). The rows are links, but nothing looks tappable: no arrow, no pressed state.
Fix (layout): number the steps, give the next one an arrow and the stronger look, grey the trial balance until the other three are done, and replace the sum with "3 من 4" steps.

### 5. A12: the status pills are inverted, and the list is not sorted by danger [noise, flow]
At 390 and 1440 the project within budget wears the darkest, most solid pill (the same look as a finished step in A6). The project that is over wears the palest. The Owner glancing at his phone sees comfort first. Rows follow project order, so with twenty projects the one over budget can sit below the fold. Nothing shows how far over: no percent of the limit, and the tolerance that decides the status is not shown on the overview.
Fix (layout): sort over, then near, then within. Make "تجاوز" the strongest pill and "ضمن الموازنة" the quietest. Show the percent of the limit used. Do not reuse the pill treatment of the project statuses, so green here is not read as "work finished".

### 6. A13: the tolerance box sits above the figures it changes [layout, clarity]
The tab opens on the tolerance field. The Owner types a percentage with no sight of the limit it creates, which is lower down. The tab also does not say whether this project is within, near or over, which is the question the Owner opened it for. It carries a heading "الموازنة (ج.م)" under a tab already called الموازنة.
Fix (layout): order the tab as status, baseline, tolerance, limit, committed, paid, and update the limit as he types. Drop the repeated heading.

### 7. A5: the pay button is live when the amount is over the safe, and the error shows twice [noise, flow]
In the "safe too small" state a banner says "رصيد الخزنة لا يكفي" with the owner injection button, and the amount field repeats "المبلغ أكبر من رصيد الخزنة". Same message twice. The "دفع" button stays dark and active; Finance finds out by pressing it. The banner also puts an owner injection button in front of Finance; who may press it, and what happens after, is not drawn. The balance foot note under "الدفع من" shows the safe; it does not change when the bank is chosen.
Fix (layout): keep the field error, disable "دفع", keep one injection button, and show the bank balance when the bank is picked. Confirm who presses the injection button.

### 8. A7: two columns repeat one figure, signs disagree, dashes are silent [noise, clarity]
After posting, "الفرق" and "الرصيد المرحّل" show the same number in the first month. Direction is shown by a pill for one column and by a minus sign in the others (the liability project shows 200,000 as the difference but -200,000 as the carried balance and -40,000 as the change). A reader sees a minus and thinks loss. A project with no progress reading shows dashes in every cell with no reason, so the page looks broken. After posting, the only way back to the checklist is the small breadcrumb.
Fix (layout, copy): show amounts without sign and let the pill carry direction, or sign every column. Drop "الرصيد المرحّل" until it differs from the difference. Put a short reason in the dash row ("لا توجد قراءة تقدم"). After posting, make "العودة إلى الإقفال" the primary action.

### 9. A9: the five ledgers float, and the heading is a count [clarity, flow]
At 1440 and 390 "الدفاتر الخمسة" is a block under the three totals, and nothing says whether the 400,000 client advance is inside the 455,000 liabilities. The notes admit this is undecided. A heading that counts things ("the five") means nothing to a stranger.
Fix (flow): decide, and show each ledger inside its group (liabilities or equity), each on its own line, never netted. Drop the separate block and its heading. Needs Nabil's ruling.

### 10. A1 on a phone: the difference is far from the save button [clarity]
At 390 the date, totals and difference are at the top and twelve fields follow. Finance fills the lower fields, then scrolls up to learn why save is disabled. At 1440 the date field is full width for one date and pushes the totals down.
Fix (layout): keep the totals bar fixed while scrolling, or repeat the difference beside the save button; narrow the date field.

### 11. Navigation: a stranger cannot reach A7, A10, A11, A13 or A14 [flow]
The sidebar lists eight entries (A1, A2, A4, A5, A6, A8, A9, A12). Year close, overhead spread and loans have no entry. The sidebar also marks "إقفال الشهر" as current on every screen in the mock (a mock fault, but the real shell must be right). The notes admit this is open.
Fix (flow): link year close from the month-close page when the month is December; give overhead spread and loans a menu entry or a link from the trial balance and the close page.

### 12. A6 and A10: a person's name is on the page [noise]
"بانتظار أحمد نبيل: اعتماد إقفال ..." names one person. If the Owner changes, the screen is wrong, and the person who requested is not named. Use the role and the date: "بانتظار المالك منذ ...".

### 13. A10: a forecast looks like a fact [clarity]
"الأرباح المحتجزة بعد" shows 590,000 while two months are still open and the year is not closed. Label the pair "قبل الإقفال" and "بعد الإقفال", or show the "after" only when the button is live.

### 14. A8: the difference is shown, not where it comes from [flow]
The red banner names the amount. The person next wants the account. Nothing helps. At minimum, group by side on the phone: the list repeats "مدين" or "دائن" under every account name, which is noise; two headed groups say it once.

### 15. A11: the report-only sentence is allowed; a quieter route exists [clarity]
The one sentence the brief permits is there. Keep it. The column "تكلفة المشروع مع الحصة" can be read as real project cost by someone who skips the banner. Marking the column heading itself ("للعرض") would make the sentence unnecessary.

### 16. A14: the page needs two primary actions and one is missing [flow]
The top action is "إضافة قسط" which needs a loan. The empty state offers "إضافة قرض", but once one loan exists the page has no way to add a second. The add-loan form is not drawn.

### 17. Drawn-later screens that the flow already depends on [flow]
Expense list (to reverse a wrong tag, A5), owner injection (A5), reject reason (A6, A10), add loan (A14). Listed by the designer; repeated because three of them are steps on the main road.

## What works, keep it
- One job per screen, one primary action at the top, repeated in the bottom bar on a phone.
- Every state is drawn, with the table frame while loading and a retry when failed.
- No codes, no ids, no explaining paragraphs. The error lines name the cause, not how the system works.
- The disabled close buttons are explained by the open steps and the named open months. No extra text.
- A3 shows only the next monthly charge when the rate changes, which is exactly what Finance needs.
- A13: the baseline has no edit control, and a change order shows in the history.

## Sentences on screen to explain the screen
Only one: A11 "للعرض فقط، غير مسجل في الدفاتر" (allowed by the brief, see finding 15). Error and refusal lines (month closed, tag missing, safe too small, out of balance, months open) name the reason, and are acceptable. "لا تملك صلاحية فتح هذه الشاشة" on forbidden states is an approximation; use the standard forbidden page.

## Where a stranger would have to ask
- A1: which comes first, A1 or the asset register (finding 1).
- A6: which step first, and what "6" counts (finding 4).
- A9: whether the five ledgers are inside the totals (finding 9).
- A12 and A13: what "الملتزم به" is (finding 3).
- A7: what a dash row means (finding 8).
- Menu: where year close and loans are (finding 11).

## Brief check
- Brief says A9 needs "change month" as primary action: drawn as a month picker at the top, not a header button. Fine.
- Brief for A13 lists the tolerance as Owner-only on save, and the Finance state hides the field. Drawn that way. Good.
- Brief for A12 says "Owner also on phone" and lists the primary action as open a project. The overview has no obvious tap target apart from the underlined name; the whole row should open.

## Layout measurements (`layout-url`, 390 and 1440)
See the section at the end of this file.

## Three changes to make first
1. Settle the opening-balance ownership (finding 1) and the five-ledger placement (finding 9) with Nabil. Both change what the screens carry. Cost of finding out after build: a lane.
2. Draw the Owner's approve view with figures and the reject-with-reason sheet (finding 2), and rename the two "رفض" meanings (finding 3). A locked month cannot be reopened, so the approver must see what he locks.
3. Make A6 and A12 carry the next move: number the steps and mark the next one (finding 4), sort budgets by danger and invert the pill weight (finding 5). These two are the screens read every month and every day.

## Layout measurements result

`layout-url` was run on every state of A1, A2, A3, A5, A9, A10, A11, A12, A13 and A14 at 390 and 1440, plus A8 difference at 1440. 119 result lines in all. Every line was PASS: no sideways scroll, no controls under 44 px, no raw i18n keys, no database GUIDs, no clipped text, nothing outside the page. The measurement therefore adds no finding. Every finding above comes from reading the screens.

The designer's skipped runs are now done, with one exception noted here: the A8 difference state was run at 1440 only, as the brief asked.

Note on the brief: it asks for the runs "a9, a13, a14, a3, a5, a2, a12, a10, a11, a1 states". The run took about an hour because other lanes were using the same browser at the same time. Run `layout-url` for one screen at a time, not in a single loop, to avoid the driver timing out.
