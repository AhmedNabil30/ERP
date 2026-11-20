# Consultant review, round 2 — extract chain, built app, 2026-11-20

**Mode:** app. **Scope:** what round 1 could not walk (cheque list and moves, dispute resolution, credit note approval, change orders, Technical Office part approval, open NCR close, QC submit, new-extract preview, contract-details withholding row, closure page), plus a re-check of round-1 findings F2, F3, F4, C1, C2, C5 and the noise items. **People:** accounts_demo, owner_demo, tech_office_demo, tech_office2_demo, site_sup_demo, site_eng_demo. **Widths:** 390 first, 1440 second. `layout` at 360, 390 and 1440 was run on every new route: all PASS (no sideways scroll, no small targets, no raw keys, no ids). Shots are in `qa/shots-consultant/` with the prefix `x20b-`. Round 1 is `ux/consultant-review-extracts-app-2026-11-20.md`.

## Actions taken (state changed, demo data)

1. فيلا 12, extract 1, accounts_demo: cleared cheque 10041; deposited cheque 10042; bounced cheque 10042 with the reason «رصيد غير كاف». (A first clear attempt was cut off by the driver closing the page and did nothing; the second worked.)
2. عمارة 5, extract 1, accounts_demo: tried to resolve the dispute with a revised extract (refused, see F6); then resolved it with a credit note of 2,500.00 and the reason «الكميات تحتاج مراجعة». Extract 1 went from معترض عليه back to صادر, waiting for the Owner.
3. شقة 9, extract 1, owner_demo: approved the 15,000.00 credit note.
4. عمارة 5, change order 1, site_sup_demo: recorded the client's approval; it moved to the Owner.
5. عمارة 5, extract 2, tech_office_demo: part approval at 70%. It now stands at Finance with 77,620.08 held.
6. فيلا 12, extract 2: accounts_demo applied the deductions; tech_office_demo closed the open NCR with «تم إعادة تنفيذ البند». It now waits for the Owner with the released 103,493.44 shown. I did not approve it.
7. شقة 9, extract 2, site_sup_demo: submitted the QC report with 0 and 0. It now waits for the Technical Office.

## Re-check against round 1

| Round-1 finding | Now | What I saw |
|---|---|---|
| F1 closure offers the warranty with no hold and no open extract | CHANGED | The closure page now shows «الحجز 0.00» and the open extract «مستخلص 2 · معتمد جزئيًا» as a link, above «بدء فترة الضمان». The open extract is visible. The hold row says 0.00 with no word on whether it was released, so it still tells the Owner little (F8). |
| F2 part-approved extract shows no figures | CHANGED | Both figures now show with values: «المعتمد للدفع 155,240.16» and «معلّق حتى إغلاق تقرير عدم المطابقة 103,493.44», and after the NCR closes «الجزء المعلّق المعتمد للدفع». Finance still applies deductions blind, the quantities table does not say which lines are held, and the released part is missing from the document totals (F1, F4). |
| F3 «تسجيل اعتراض» after the history | FIXED | It sits under the collection block with «تسجيل دفعة» in the bottom bar (`x20b-acct-e1-tall-390.png`). |
| F4 false reason on the dispute refusal | FIXED | Owner on the dispute page: «دورك أو تكليفك في هذا المشروع لا يسمح بهذا الإجراء.» and one link «فتح المستخلص». No retry. |
| C1 three forms of "who is next" | CHANGED | The workspace now says «المستخلص 2 · بانتظار ناصر الشريف: الاعتماد النهائي», the same form as the list and page. The task word still differs: «الاعتماد النهائي» on the workspace, «الاعتماد» on the page, bare «اعتماد» on the button (C3). |
| C2 «المستخلصات» tab cut off | FIXED | Five tabs fit at 390: نظرة عامة · المقايسة · اليومية · المستخلصات · الفريق. The strip is tight. |
| C3 «المقايسة» stage and tab | FIXED | The stage trail is gone from the workspace. |
| C5 payment form asks for «رقم التحويل» before a method | FIXED | No reference field until a method is chosen. Choosing شيك gives «رقم الشيك» (`x20b-acct-collect-cheque-390.png`). |
| C6 primary floats top-left at 1440 | STILL THERE | Same on collect, payments, credit note, extract and change-order pages. |
| N1 zero rows | STILL THERE | «المخصوم من المنبع 0.00» and «شيكات لم تُصرف 0.00» on every position block; «تشوينات 0.00» and «استرداد الدفعة المقدمة 0.00» in the document; «المخصوم من المنبع 0.00» again inside every cheque row. |
| N2 quantity table lists every BOQ line | CHANGED | Draft and Technical Office pages list only lines worked this period. Each row still stacks name, unit, quantity and value on four lines at 390. |
| N3 explaining sentences on closure | FIXED | Closure is now a title, a status pill, figures and buttons. |
| N4 cancellation as big as the main action | STILL THERE | «طلب فسخ التعاقد» is still a full-width card under the real step. |
| N5 links for roles who cannot use the screen | FIXED | The engineer's list has no «أوامر التغيير» link. The Supervisor now has a real action there. |
| N6 test text, «????» project name | STILL THERE | «???? ????? r2 2026-11-18» on the closure page and workspace. |

## Findings, by what they cost

### Flow (the business is misled or stuck)

**F1. The released part of a part-approved extract does not reach the document the Owner approves.** Owner and Finance, فيلا 12 extract 2 (`x20b-owner-e2-released-390.png`). After the NCR is closed the top says 155,240.16 approved plus 103,493.44 released, so 258,733.60 of work. The document below still reads «قيمة الأعمال 155,240.16 · صافي المستحق 139,716.14 · الحجز حتى تاريخه 54,334.06». The Owner presses «اعتماد» against a net figure that leaves the released 103,493.44 out, and nothing says where that money is billed or whether it carries the hold and deductions. Layout and rule: the document must carry the released part, or say plainly that it is billed next.

**F2. A part approval is one percentage field.** Technical Office, عمارة 5 extract 2 (`x20b-tech-part-390.png`, `x20b-tech-part-1440.png`). «اعتماد جزء» opens one box, «النسبة المعتمدة %». It does not show what 70% means in money, which lines are held, or why. A تقرير عدم المطابقة is created from it with no description of what is wrong; the extract page then says «معلّق حتى إغلاق تقرير عدم المطابقة» and never shows the report. The person closing it types «ما تم تصحيحه» without having read what was reported. Layout and flow: show approved and held figures as the percentage is typed, name the held lines, and require what the defect is.

**F3. Two people must act on one extract and the line names one.** Technical Office, فيلا 12 extract 2 before the NCR closed (`x20b-tech-ncr-390.png`). The waiting line says «بانتظار سارة محمود أو حسابات تجريبي: تطبيق الخصومات», but the Technical Office person's bottom button is «إغلاق تقرير عدم المطابقة». A stranger reads both and cannot tell which one blocks the extract. Layout: a second line for the open report and who closes it.

**F4. Finance applies deductions blind (round-1 F2, still).** فيلا 12 and عمارة 5 extract 2, accounts_demo. «تطبيق الخصومات» shows «لم تُطبق الخصومات بعد» and no figure: stock, advance recovery and hold are unknown until after the press. Layout: show what will be applied.

**F5. The extract list and the extract page disagree about who is next.** عمارة 5 extract 1 after the credit note was raised. The page says «بانتظار ناصر الشريف: اعتماد الإشعار الدائن»; the project's extract list, for the Owner and for the engineer, still says «بانتظار حسابات تجريبي أو سارة محمود: تسجيل دفعة». The list drops the step that blocks. Copy and flow: if a credit note is pending, the list says so.

**F6. The dispute screen offers a choice that fails.** Finance, عمارة 5 extract 1 (`x20b-acct-resolve-qty-after-390.png`). «الكميات» → «مستخلص معدّل» is offered, but extract 2 exists, so it fails after the form is filled: «تعذّر حل الاعتراض. أُعدّ مستخلص بعد هذا المستخلص، فلا يُلغى بمستخلص معدّل. لم يتغير شيء.» The message says what cannot happen and not what to do (use the credit note). «أُعدّ» is hard to read. The client's complaint here was about quantities, and quantities is the choice that is refused. Layout and flow: take the option off, or say beside it that it closes once a later extract exists.

**F7. The cheque page does not say which cheque the button moves.** Finance, فيلا 12 extract 1 with two cheques (`x20b-acct-payments-390.png`). The bottom button «تأكيد الصرف» and the red «الشيك مرتد» act on one cheque the screen never marks. The other cheque (received, not deposited) has no button until the first is done. I pressed and learned afterwards that it was 10041. Layout: put each move on its cheque row, or name the cheque in the button.

**F8. Handover with an open extract (round-1 F1) is shown but not resolved.** Owner, PRJ-CE500CBD closure (`x20b-owner-closure-390.png`). The open extract and «الحجز 0.00» now show. Nothing says whether the button is safe with extract 2 open, and 0.00 reads as "nothing held" when the spec says the hold is released at handover. Direction (copy and rule): «أُفرج عن الحجز» with amount and date, or hide the row; rule whether an open extract blocks «بدء فترة الضمان».

*On the known point:* I saw «الحجز» once, with 0.00, on PRJ-CE500CBD. No other project is handed over with an unreleased hold, so I could not see it twice. One row is right; 0.00 alone is not.

### Clarity (a stranger would have to ask)

**C1. The Supervisor's QC form asks for money under a stock title.** site_sup_demo, شقة 9 extract 2 (`x20b-sup-qc-click-390.png`). The button says «تسليم تقرير الجودة»; the sheet is titled «قيم التشوينات» and asks two required money fields, «قيمة التشوينات بالموقع» and «قيمة الخامات المركّبة». The title does not match the button, and a supervisor is asked for a value with no hint where it comes from. Copy and flow.

**C2. A credit note does not show its effect before approval.** Owner (`x20b-owner-cn-390.png`). «صافي المستحق 349,290.36» sits beside «15,000.00» with no figure for after. After approval «المستحق بعد الإشعار 334,290.36» appears (`x20b-owner-cn-approved-390.png`). The Owner needs it before he presses. On the extract page «بعد إشعار دائن 15,000.00» under the net due reads as a second balance. Layout.

**C3. Three names for one step: «الاعتماد النهائي», «الاعتماد», «اعتماد».** Workspace line, extract line and button for the Owner's approval; change orders use the bare «الاعتماد» too. Copy: one phrase.

**C4. «عند …» pill plus «بانتظار …» line.** Pills read «عند المالك», «عند العميل», «عند المكتب الفني» while the line below says who. The pill adds nothing the line does not say. Copy.

**C5. Change-order list rows carry no amount.** Owner and Technical Office (`x20b-sup-colist-390.png`). A change order is a money decision; the list shows title, number and status, and long titles wrap to three lines. Layout.

**C6. «فتح» is a bare underlined link** under «إشعار دائن 2,500.00 ج.م» on the extract page. Open what? Copy.

**C7. Mixed date and digit forms.** «20/11/2026» in most places, «2026/11/20» in contract terms and the workspace's last log, «٢٠/١١/٢٠٢٦» in the date input; units read «م٢» in Arabic-Indic next to Western numerals. Copy.

**C8. Two «خصم» ideas in the contract terms.** «نسبة الخصم من كل مستخلص 0%» (advance recovery) and «الخصم من المنبع: بدون خصم» (withholding, with a «تعديل» button) sit four lines apart (`x20b-owner-terms-390.png`). The withholding row is there and reads clearly; the recovery label is the one that is not. Copy: «استرداد الدفعة المقدمة».

### Noise (on screen because it is stored)

**N1. Zero rows, still.** See the table. Show only figures with a value; cut «المخصوم من المنبع 0.00» from every cheque row.

**N2. Sentences that explain the screen.** «سيُحاسب في المستخلص القادم.» on an approved change order (next-step information, but «القادم» does not say which extract); «يُعتمد 103,493.44 ج.م للدفع.» in the NCR sheet (the figure is needed, the words are not); the dispute refusal in F6. Everything else on the new screens needs none.

**N3. Test text on a real screen.** The «????» project name; «حسابات تجريبي أو سارة محمود» and «المكتب الفني التجريبي أو المكتب الفني التجريبي الثاني» in waiting lines.

**N4. Cancellation is still a full card** (see the table).

## What works

- The cheque flow does the whole life: receive, deposit, clear, bounce. The position block moves with it (المستلم 87,322.59 after clearing; المتبقي rises when a cheque bounces) and the bounce reason shows on its row.
- The bounce sheet is one required field and one red button.
- The dispute-resolution page is clear: two choices with the outcome under each and a primary that follows the choice.
- After a credit note is raised the extract goes back to «صادر», waits for the Owner by name, and approval changes the due figure.
- The change-order strip, one waiting line and one button work for the Supervisor and the Owner.
- The QC submit and the NCR close each end in one clear state with no leftover button.
- The engineer sees no money; the dispute refusal is now true.

## Not walked

- **New-extract quantities preview.** Every project these roles can reach has an open extract, so «مستخلص جديد» refuses with «مستخلص N ما زال مفتوحًا». It needs a project with all extracts closed and a recorded daily log.
- The Owner's final «اعتماد» on فيلا 12 extract 2 and the issue and collection after it (stopped before approving).
- The credit-note refusal («رفض») and the NCR for a role that may only read it.
- The «الحجز» twice case (no handed-over project with an unreleased hold).

## The three changes I would make first

1. **Make the part-approval split show and carry its money (F1, F2, F4).** Show approved and held figures as the percentage is typed, name the held lines, make the NCR carry a description, and make the document the Owner approves include the released part. This is the money about to be billed, and it is wrong or blind on three screens.
2. **Put each cheque's move on its row and take dead choices off the dispute screen (F7, F6).** Finance cannot tell what a button will do until it has done it, on money that has left or is about to leave the bank.
3. **One line for who is next, on list and page (F5, F3, C3).** The same words for the same step everywhere, and the list must show the credit note or open NCR that actually blocks.

## Where the brief was wrong or unclear

- It names the report under `ux/`; the standing brief says `qa/`. Dispatch followed.
- «Closure page shows الحجز twice»: only one row appears on PRJ-CE500CBD.
- «New-extract quantities preview»: no reachable project is in a state to show it.
- «Cheque list with rows and deposit/clear/bounce»: the list page has no buttons; the moves are on the extract's payments page, which the row opens.
- Driver: `click:` steps cannot set a radio by its label text (use `[data-testid='dispute-kind-…']`); a shot taken straight after a click can close the page before the request ends (add `x` steps); a 900 px page hides everything below the fold, so I used a taller copy of the driver for full-length shots.
