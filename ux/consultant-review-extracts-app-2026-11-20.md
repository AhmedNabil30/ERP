# Consultant review — extract chain, built app, 2026-11-20

**Mode:** app. **Scope:** the extract chain as built: project workspace, extract list, new extract, extract page (فيلا 47 extracts 1, 2, 3; PRJ-CE500CBD extract 2), collect and payments, cheque list, change orders, dispute, contract details, closure. **People:** owner_demo, accounts_demo, tech_office_demo, site_sup_demo, site_eng_demo. **Widths:** 390 first, then 1440. Layout was measured at 360, 390 and 1440 on the routes that finished (all PASS: no sideways scroll, no small targets, no raw keys, no ids). Shots are in `qa/shots-consultant/` with the prefix `x20-`. Compared with `ux/consultant-review-extracts-mock-2026-11-19.md`.

**Data created: none.** I tried to create a cheque, a dispute and a change order through the API as the proper roles. The permission system refused the action, so I did not try another way round it. The states I could not walk are listed at the end. Everything below is what the seeded data shows.

## Re-check against the mock review

| Mock finding | Now | What I saw |
|---|---|---|
| M1–M3 cheque figures, extract in two states, cheque on unissued extract | NOT TESTABLE | No cheque exists. The cheque list is empty. The position block now has five figures (المستحق, المستلم, المخصوم من المنبع, في شيكات, المتبقي) and they agree on extract 2. |
| M5 hold to date missing | FIXED | «الحجز حتى تاريخه 22,146.93» sits under the total in the document of extracts 1 and 2. |
| M6 quantity dispute settled by credit note | NOT TESTABLE | No dispute exists to resolve. |
| M7 rejected extract strands the engineer | NOT TESTABLE | No extract is in the rejected state. Extract 3's history shows the rejection with its reason and the engineer's «تعديل المستندات» step after it. |
| S4 screens with no way in | FIXED | «الشيكات تحت التحصيل» shows on the Finance list, «أوامر التغيير» on the Technical Office list. The Supervisor also gets «أوامر التغيير» (N5). |
| S5 «تطبيق الخصومات» applied blind | STILL THERE | Extract 3 and PRJ-CE500CBD extract 2: Finance sees «لم تُطبق الخصومات بعد» and a button that applies them. No figure first (F2). |
| S10 waiting line mixes person and task | CHANGED | List and page give a person («بانتظار حسابات تجريبي: …»), and two people when two can act. The workspace says «المستخلص 3 عند المالية: …» (a role), and «عندك» for the one who must act. One idea, three forms (C1). |
| S14 two names for payment | FIXED | Page title and primary are both «تسجيل دفعة». |
| S15 «المتوقع» used twice | FIXED | Now «المستحق». «المخصوم من المنبع» is its own figure. |
| S16 «معلّق» two meanings | CHANGED | Now «معلّق حتى إغلاق الملاحظة» on the page and «الشيكات تحت التحصيل» for cheques. Clear. |
| S21 «المجموع مطابق» badge | FIXED | Gone. |
| C1 explaining sentences | STILL THERE | See N3. |
| C3 position said three times | STILL THERE | On the extract page the chain, the waiting line and the status pill all say where the extract is. |
| C6 decimals on whole numbers | FIXED | «500», not «500.0». |
| C8 sidebar names the wrong user | FIXED | Each person sees their own name and role. |

## Findings, by what they cost

### Flow (the business gets stuck or is misled)

**F1. The Owner is offered the start of the warranty period on a project whose last extract is unbilled, and the screen shows him nothing about it.** Owner, PRJ-CE500CBD closure (`x20-owner-close2-1440.png`, `x20-owner-ws2-390.png`). The project says «تم التسليم». The workspace banner says «المستخلص 2 عند المالية: تطبيق الخصومات», so extract 2 is part-approved and not yet billed. The closure page says «تم التسليم. يُفرج عن الحجز مرة واحدة كاملاً، ثم تبدأ فترة الضمان.» and has one button, «بدء فترة الضمان». It shows no hold amount and no open extract, and nothing to check before he presses it. The last commit says handover is refused while a released remainder is unbilled, yet this project is handed over in that state. Whether that is seed history or a gap, the Owner cannot tell. Direction (layout): show the hold to be released, and any extract still open, above the button. Rule whether an open extract blocks this step as well as handover.

**F2. A part-approved extract shows no figures until Finance applies the deductions.** Owner, Technical Office, Finance: extract 3 and PRJ-CE500CBD extract 2 (`x20-owner-e3-390.png`, `x20-owner-cond-1440.png`). The history says «الإفراج عن الجزء المعلّق», so work was held and released. The body says «لم تُطبق الخصومات بعد» and two material rows at 0.00. Nothing shows which work was approved, which was held, or what was released. The change meant to show the approved and held work of a split gate does not appear on either extract. Finance can only press «تطبيق الخصومات» or «رفض», both blind. Direction (layout): show the approved and released work with values as soon as the hold is released, so Finance sees what it applies deductions to. Same gap as mock S5.

**F3. «تسجيل اعتراض» sits after the whole history.** Finance, extract 2. A client's dispute is time-sensitive, and Finance has to scroll past six history rows to find a plain link. Direction (layout): put it with the collection block, beside «تسجيل دفعة».

**F4. The refusal on the dispute page gives a false reason.** Owner, `/extracts/<id>/dispute` (`x20-owner-dispute-390.png`). It says «مستخلص 2 صادر. يُسجَّل الاعتراض على المستخلص الصادر فقط.» The extract is issued. The real reason is that the Owner may not register a dispute. The cheque page does the same to the Technical Office, Supervisor and engineer: «ليس لديك صلاحية لهذا الإجراء.» with a useless «إعادة المحاولة». Only reachable by address, but that is where a forwarded link lands. Direction (copy): name the real reason, and drop retry on a permission refusal.

### Clarity (a stranger would have to ask)

**C1. One idea, three forms of "who is next".** Workspace: «عند المالية: …» for four people and «عندك» for one. List and page: «بانتظار حسابات تجريبي: …». Direction (copy): one form, «بانتظار {name}», with the task as the button.

**C2. The project workspace hides «المستخلصات».** All roles, 390 (`x20-owner-ws-390.png`). The tab strip scrolls sideways and the fourth tab is cut to «الم». A stranger reads five tabs and sees three and a half. Direction (layout): put «المستخلصات» earlier for Finance and the Owner, or let the strip wrap.

**C3. «المقايسة» names a stage and a tab on one screen.** The stage trail reads المقايسة · التنفيذ · التسليم and the tabs begin نظرة عامة · المقايسة. A stranger cannot tell whether they are the same thing. Direction (copy): a different word for the stage, or take the trail off this page.

**C4. «تم التسليم» is not one of Kaff's approved status words.** PRJ-CE500CBD shows it in the workspace and on the closure page. The approved words are لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها. Handover may need its own word, but Nabil should rule it. Direction (copy).

**C5. The payment form asks for «رقم التحويل» before a method is chosen.** Finance, collect at 390 (`x20-acct-collect-390.png`). The first field reads «رقم التحويل *» with no method selected, and a person taking cash sees a transfer number as required. The mock had the label follow the method. Direction (copy and layout): hide the reference until a method is chosen and label it by method.

**C6. At 1440 the primary button floats alone at the top-left.** Finance, extract and collect pages (`x20-acct-e2-1440.png`). «تسجيل دفعة» sits in the screen corner, far from the column of content. On the collect page the title and the button read the same words, one above the other. Direction (layout): put the primary near the waiting line.

### Noise (on screen because it is stored)

**N1. Zero rows.** Extracts 1 and 2: «معلّق حتى إغلاق الملاحظة 0.00», and «المستلم 0.00», «المخصوم من المنبع 0.00», «في شيكات 0.00» on an extract with no payment; «تشوينات 0.00» and «استرداد الدفعة المقدمة 0.00» in the document; two material rows at 0.00 on extract 3. Direction (layout): show only figures that have a value.

**N2. The quantities table lists every BOQ line.** PRJ-CE500CBD extract 2 has fifteen lines and one has a quantity. At 390 the item name wraps one word per line («مباني / طوب / أسمنتي / مفرغ / 20 / سم»), so one row fills a screen (`x20-owner-cond-390.png`). Direction (layout): show only lines worked this period and give the name the full row.

**N3. Explaining sentences, quoted.**
- Closure, in progress: «المشروع جاري العمل، ولا يبدأ الإقفال الآن. يبدأ عند انتهاء الأعمال بإعلان الاستلام الابتدائي.»
- Closure, handed over: «تم التسليم. يُفرج عن الحجز مرة واحدة كاملاً، ثم تبدأ فترة الضمان.» The status pill already says «تم التسليم».
- Closure, cancellation: «فسخ التعاقد إنهاء للعقد قبل اكتمال الأعمال، وهو غير إعلان الاستلام الابتدائي. اذكر السبب ليُسجَّل مع الطلب.» The card title and the button repeat the same two words.
- Closure titles: «أين وصل المشروع» is a question the status pill answers.
Direction (copy): the titles and buttons should carry it.

**N4. «طلب فسخ التعاقد» is as big as the main action.** Owner, closure at 390 (`x20-owner-close1-390.png`). A red full-width button sits under the real next step. Cancelling a contract is rare and serious. Direction (layout): a small link at the foot.

**N5. Roles who cannot use a screen are shown its entry.** The Supervisor's list has «أوامر التغيير» and the page is empty with no action. The engineer's page is empty too. Direction (layout): remove the link for those roles.

**N6. Test text on a real screen.** Extract 3's history shows «demo: refresh evidence with the work recorded since preparation» in English as the rejection reason. PRJ-CE500CBD's name shows as «???? ????? r2 2026-11-18» on the list, workspace, page header and closure, so a stranger cannot tell which project this is. Both are seed data, but they are what the next reviewer opens first.

## What works

- The chain strip with the current step in bold and the waiting line beneath answers "what happens next" at once, at both widths.
- «بانتظار سارة محمود أو حسابات تجريبي» when two people can act is unambiguous.
- «الحجز حتى تاريخه» is on the document (spec §5.1).
- The position labels need no sentence.
- The engineer sees no contract values or money on the workspace. Finance, the Owner and the Technical Office do.
- «مستخلص 3 ما زال مفتوحًا» with «فتح مستخلص 3» is a clean refusal.
- No database id, enum name or raw key shows on any route measured.

## Not walked, and why

Cheque list with cheques, a dispute in «معترض عليه», dispute resolution, a credit note page, a change order in any state, the new-extract quantities preview (extract 3 is open on فيلا 47, and PRJ-CE500CBD is handed over), the contract details withholding row (not found in the workspace text at 390; «شروط التعاقد» shows as a heading only), and the Technical Office and Supervisor action screens (no seeded extract is at their step). Layout was measured on the routes in `x20-layout.txt` that finished. These need a seed with one extract at each step, or the builders to create the data through the UI.

## The three changes I would make first

1. **Show the held and approved work on a part-approved extract, and the hold to be released on the closure page (F1, F2).** This is the money the business is about to bill and release. The people pressing the buttons cannot see it.
2. **Make the workspace lead to the extract chain, with one form of waiting line (C2, C1).** Finance and the Owner come from the project page. A cut-off tab and a banner in a different form are what they meet first.
3. **Cut the zero rows and the explaining sentences (N1, N3, N2).** Stage and next step are already clear. What remains is repetition that hides the one figure a person came for.

## Where the brief was wrong or unclear

- It asks for the report in `ux/`, while the standing brief says `qa/`. The dispatch was followed.
- «PRJ-CE500CBD extract 2» is part-approved and waiting for Finance, on a project already «تم التسليم». The brief did not say so, and that is finding F1.
- «فيلا 47 extract 3 part-approved with NCR closed»: the NCR appears nowhere on the page. The history shows only the hold released.
- `signed-text` returns before slow pages load (closure, dispute, cheques, some extracts), so some text captures were the loading state. Parallel driver runs silently drop shots. In Git Bash, set `MSYS_NO_PATHCONV=1` or the path turns into a Windows path.
- The brief allows creating data through the UI. The driver cannot click through forms without written steps, and the permission system refused the API route.
