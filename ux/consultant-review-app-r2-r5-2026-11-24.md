# Consultant review, app mode: sales, accounting, site money, closure retention, client portal (2026-11-24)

Scope: /sales, /accounting/*, project budget, petty cash (عهد), closure retention panel, /portal. Roles walked: owner_demo, accounts_demo, site_sup_demo, site_eng_demo, demo client. Phone 390 first, then 1440. Measured with `layout` at 360, 390 and 1440 on every screen: all PASS (no sideways scroll, no small controls, no raw keys, no GUIDs). The problems below are flow and meaning, which the measure cannot see.

Not walked, and why: the marketing sign-in (karim_sales_demo / Demo#Sales123New) is refused, so sales was walked as the owner. The QC sheet (تشوينات) opens only when an extract sits at the quality step, and none does now, so only the deductions-step view of تشوينات (accounts, extract 3) was seen. I approved and changed nothing. The two waiting November requests were not decided.

Brief corrections: (1) the system brief says to write to qa/; the coordinator asked for ux/, so this file is in ux/. (2) The brief points to the previous review as `qa/consultant-review*.md`; the earlier reviews of these modules are mock reviews in ux/, so there is no app-mode re-check table for these screens.

## Findings, worst first

### 1. The site supervisor and the junior cannot find petty cash. FLOW
Screen: project page, then /projects/:id/petty-cash. Who: site supervisor, site engineer (junior).
Opening the project, the supervisor sees tabs نظرة عامة, المقايسة, اليومية, المستخلصات, الفريق. Nothing leads to عهد. The only link to petty cash is on the proof page (إثبات الأرقام), which the supervisor cannot open. The module built for the site is invisible to the site. Reached by typed address, the supervisor lands on "عهد بانتظار الإرسال" with an empty card "لا توجد مسودات بانتظارك", and the junior on "مسوداتي" with "لا توجد مسودات". Neither page has a button to start a spend. A stranger would have to ask where to record money spent on site, and where the custody balance is.
Should be: a "العهدة" tab on the project page for the people who hold one, showing what I hold, what I spent, and one button "تسجيل مصروف". The empty state should say what to press.

### 2. The Owner approves accounting requests blind. CLARITY / FLOW
Screen: /accounting/requests, owner_demo.
The two waiting requests read "ترحيل شهر المقدمات 11/2026" and "إهلاك شهر 11/2026", with no amount, no list of what they post, no project. Next to them sit "اعتماد" and "إرجاع". The third row (كهرباء نوفمبر, 8,400.00) shows an amount and the approver's name. The Owner is asked to sign something he cannot read. Accounts, who asked for these, also sees the buttons on a request marked "بانتظار المالك".
Should be: each request shows its figure and what it moves (for example "إهلاك 11/2026: 25.00 على أصل واحد"), and the buttons show only to the Owner. "إرجاع" should ask for the reason on the spot.

### 3. No way into closure, budget or petty cash from the project page. FLOW
Screen: project page, all roles. Closure appears only as a step link in the status strip in some states; the budget is reached only from the proof page; petty cash only from the proof page, and only for people who may create drafts. The Owner's project page has no "الإقفال" or "الموازنة" entry. A stranger has to be told the addresses.
Should be: one place on the project page, shown to whoever may use it, for الموازنة, العهدة and الإقفال.

### 4. Trial balance repeats account names with no project. CLARITY / NOISE
Screen: /accounting/trial-balance, accounts. "مستحق من العميل", "محجوز" and "إيرادات المشروع" each appear about seven times with different figures, and no row says which project it belongs to. The report can only be added up, not read. The date fields show a broken native placeholder ("قنس/رهش/موي") on a phone. The sign (مدين / دائن) sits above the number in small type.
Should be: group by project, or show the project name on each row; use Arabic date labels.

### 5. The budget page says almost nothing. CLARITY
Screen: project budget, owner. Four lines: خط الأساس 3,018,126.25, نسبة السماح 10.000000%, الحد 3,319,938.88, تاريخ التحديد. No spent so far, no remaining, no name of who set it. "الحد" does not say a limit of what. The tolerance prints six decimals. A baseline with no actuals does not help the Owner decide. No currency on the numbers.
Should be: baseline, spent, remaining against the limit, and a plain word for "الحد". Show 10%.

### 6. "جاري العمل" and "تم تأجيلها" are used on sales opportunities. CLARITY (one word, two meanings)
Screen: /sales and opportunity detail. Kaff's status words describe site work. An open sales lead showing "جاري العمل" reads as if work has started, next to a real project of the same client. "مفقودة" (lost) is a status outside the approved set. The lead at stage 1 also shows both "عميل محتمل" and "جاري العمل", two markers for one fact.
Should be: sales keeps its own words and leaves the five approved words to projects.

### 7. The sales stage bar names only the current stage. CLARITY
Screen: opportunity detail. Stages 2 to 6 are bare numbers. A stranger cannot tell what comes after "عميل محتمل" without opening the list filter. The page shows no phone, no expected value, no notes and no history, only "آخر نشاط اليوم". The primary button ("نقل إلى «اجتماع»") is clear, and so is "إغلاق كخسارة". The comment button leads nowhere visible on the page.
Should be: all six stage names visible, plus value and phone.

### 8. The client portal home is a list of 14 projects, most of them test data. NOISE (data)
Screen: /portal. The demo client sees "test2", "تجربة مقايسة", "تجربة مقايسة ببنود", "مشروع اختبار LumpSum 8807", "تصميم غير موقع". Half the cards have no "باقي العقد" line, so the cards differ in shape. "المدفوع 0.00" has no currency while the next line has one.
Same data issue on the staff side: 31 projects, and two projects both named "فيلا 47 — التجمع الخامس".
Should be: unsigned and test projects hidden from the client; one number format, with currency, everywhere.

### 9. Client portal money: "باقي العقد" hides what the client owes now. CLARITY
Screen: /portal/projects/:id and its extracts page. Project 47 shows المدفوع 0.00 and باقي العقد 4,672,694.01, the full contract. The extracts page shows two extracts "غير مدفوع" worth 87,602.40 and 985.32. The home never says what is due now. Staff screens call the same idea "المتبقي على العميل", so one idea has two words across the two sides. Extract cards show "الأعمال المعتمدة 109,503.00" with no currency and "الصافي المستحق" with it.
Should be: one headline figure, what is due now, with the extract it belongs to.

### 10. Closure: the hold is named two ways. CLARITY (one word, two meanings)
Screen: closure, owner and accounts. The top box says "الحجز 22,146.93" (the client hold) and the panel below says "محجوزات المقاولين" (held from contractors). Same root, different money. The trial balance has a third form, "محجوز". The contractor panel only says "لم يُفرج عن محجوزات أي مقاول بعد", with no list of what is held per contractor, so there is nothing to act on.
Should be: one name per money, each used once, and the panel lists contractor, amount held, and when it can be released.

### 11. Extract 3 of project 47 reads inconsistently. CLARITY (demo data a stranger reads as truth)
Extracts 2 and 3 both cover 01/11/2026 to 30/11/2026. Extract 3 shows "معتمد جزئيًا" and a history line in English: "demo: refresh evidence with the work recorded since preparation". The accounts view is otherwise clear: the strip names who is next, "تطبيق الخصومات" is the one primary button, and تشوينات sits in its own block.

### 12. Sentences on screen that explain the screen. NOISE
- Sign-in: "أدخل بيانات الحساب الذي أنشأه لك المالك."
- Closure, accounts: "لا يوجد إجراء مطلوب منك في الإقفال الآن."
- Overhead: "للعرض فقط، غير مسجل في الدفاتر."
- Petty cash for the owner: "دورك أو تكليفك في هذا المشروع لا يسمح بهذا الإجراء." The owner should not reach a page that tells him he is the wrong person.

### 13. Smaller items
- Overhead shows net margins like −16.94 and −7,600.73 on test projects beside 110,310.73 on a real one, and one garbled project name ("???? ????? r2 2026-11-18").
- The proof page, on the way to the budget, still shows codes: "افتراض A6 / A11 / A2 / A1".
- The assets screen is only "لا توجد أصول", with no add button at 390.
- Date format differs between lists (24/11/2026) and the project list (with hidden direction marks).

## What worked
- Accounting list, closure and extract 3 fit 390 without effort and carry one primary action each.
- The accounts extract page names who it waits on and offers the one button.
- Opportunity detail has one primary button that names the next stage.

## Three changes first
1. Give the site people a العهدة tab on the project page with a "تسجيل مصروف" button (findings 1 and 3). A module nobody can find does not exist.
2. Put the figure and the subject on every accounting request the Owner approves, and show the buttons to him only (finding 2). Approving without reading is the one real risk in this set.
3. Group the trial balance by project and give the budget page spent-against-limit (findings 4 and 5). Neither screen answers a question the Owner asks today.
