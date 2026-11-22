# Consultant review, sales mock (`.design/v2/team-a/sales.html`), 2026-11-22

Mode: mock. Scope: S1 to S7, roles Marketing, Owner, Technical Office. Judged against `plans/release-3-sales/screens.md` and the README section "Sales". Layout was reported PASS by the caller and is not repeated.

Seen: S1 normal at 390 and 1440, follow-up at 390 dark; S2 results, repeated phone, no match at 390; S3 normal, pending, stalled, negotiation (Marketing), after request, hold, lost, won, close-as-lost at 390, normal and office at 1440, normal at 390 dark; S4 normal; S5 list and add; S6 owner at 1440 and 390, owner 1440 dark, Marketing 390, add-item 390, old-pricing banner 1440, print 1440 and 390 dark; S7 normal at 1440. Not walked: empty, loading, failed and refused states, S7 other states. Screenshots are in `qa/shots-consultant/` (`s1-*` to `s7-*`).

## Answers to the two direct questions

**KF-1101 in the S6 search results: noise.** Searching by code is fine and should keep working. Showing it is not. The salesperson at the client's table does not know catalogue codes. The two results (خرسانة مسلحة للأسقف and خرسانة عادية للأساسات) already differ by name, so the code adds nothing to the choice. What a person needs in that row is the unit and the sale price. The README treats the code as an exception to the no-codes rule only because the brief says "search by code". Searchable is not the same as displayed. Fix (layout): match on the code, but show unit and price in the second line of the row. If Karim says estimators think in these codes, show it only in the Owner view, never in the Marketing view.

**«طلب فتح المشروع» for Marketing: sensible as a handoff, wrong as drawn.** Marketing must not open the project (D-052), so something has to reach the Technical Office. But the mock builds a new concept out of a business event that already exists. The stage line has a last stage عقد and a status تم الفوز. "The client agreed" is that event. The mock shows three names for it (the request, the stage عقد, the status تم الفوز), and the stage line stops one step short of عقد with no way for Marketing to move it. The request is also a dead end: after it Marketing has no control, cannot withdraw, and the activity list does not show who asked and when (findings 1 to 3). Preferred shape, pending Q11: Marketing's primary at تفاوض is a stage move to عقد (label «العميل وافق»), which puts the opportunity in front of the Technical Office; the Technical Office then sees «فتح المشروع». One idea, one word, no extra state.

## Ranked findings

Ranked by what each costs the business. Type: F = flow, C = clarity (copy and wording), N = noise.

### Flow

1. **S3 negotiation to won, F. The Technical Office has no way to find the request.** S1 is for Marketing and Owner only. The Technical Office needs an entry (a filter «بانتظار فتح المشروع» on S1, with S1 open to that role). Without it the request goes unseen and a signed client waits. The waiting line tells Marketing that someone should act; nothing tells the Technical Office. Cost if missed: a won contract and nothing starts.
2. **S3 after the request, F. Marketing is left with an empty bottom bar.** At 390 the only control left is «القائمة». Marketing cannot withdraw a mistaken request, and cannot see who was asked or when. Fix: a request line in the activity list («طلب فتح المشروع، هالة محمود، 21 نوفمبر») and one quiet action to withdraw.
3. **S3 won, F. The page does not lead to the project.** It shows the stage line, fee, quotation total, expenses and, in activity, «فتح المشروع، كريم حسن». There is no link to the project that now exists. Fix: a «المشروع» row with the project name as a link.
4. **S3 on hold, F. No way back.** «تعليق» exists; the reverse does not. A client put on hold is parked for good. Fix: «استئناف» as the primary on hold, mirroring «إعادة فتح» on lost.
5. **S6, F. Marketing cannot enter the one thing a quote needs: quantity.** Quantity and unit are plain text on every line. Add-item search shows results and then nothing happens: no quantity step, no remove, no edit. A stranger cannot build a quote from this. Fix: after picking a result, a bottom sheet with quantity and a primary «إضافة»; per-line edit and remove.
6. **S6 owner view, F. The result is below the whole table.** At 1440 the first screen shows lines and per-line percentages; total cost, total sell and profit percentage are off screen. The owner scrolls past every line to learn what the job earns. Fix: margin block directly under the title, lines after it. Same at 390.
7. **S6 print view, F. The paper goes dark in dark theme.** The preview of a printed client sheet should stay light in both themes, or it misleads about what prints. The title «عرض سعر» also appears twice (page title and paper heading). Fix: fixed light paper, one heading.
8. **S7, F. Contract type is pre-selected as مقاولة بالمقطوعة.** Contract type decides how the client is billed. A silent default lets the Technical Office open the project with the wrong type by pressing the primary. Fix: no default, primary disabled until one is chosen.
9. **S2, F. «متابعة» after a repeated phone does not say what it does.** It creates a second client with the same number. A stranger cannot tell whether it joins the existing client or duplicates. Fix: two actions that say what happens: «استخدام أحمد سمير» and «إنشاء عميل جديد بنفس الرقم».

### Clarity

10. **One word, two meanings: «متابعة».** S2 uses it for "proceed" (button). S1 and S3 use «تحتاج متابعة» for "needs follow-up". Rename the S2 button (finding 9).
11. **One word, two meanings: «عرض».** Quotation is «عرض السعر». The expenses row action on S3 is also «عرض» (view), next to the quotation row whose action is «فتح». Use one verb for both rows.
12. **Status words collide with Kaff's approved words.** Approved: لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها. The mock invents نشطة, متوقفة, معلّقة, مفقودة, تم الفوز. «متوقفة» sits close to «متعثرة» but means something else (no calls for N days, not stuck). «معلّقة» is the same idea as «تم تأجيلها». Karim decides. Recommendation: drop the «متوقفة» pill (finding 13) and use «تم تأجيلها» for on hold.
13. **S1 and S3, N. Stalled is said three times.** One S1 row shows the pill «متوقفة», the line «هادئة منذ 6 أيام» and the badge «تحتاج متابعة». All three come from one number. Keep the number line and the badge, drop the pill.
14. **«هادئة منذ N أيام» reads oddly.** «هادئة» is a tone, not the state of a deal. The reader would say «بلا نشاط منذ 6 أيام». The S1 desktop column header «الهدوء» means nothing alone. Use «آخر نشاط» as the header and «منذ 6 أيام» as the value.
15. **S6, «الهامش» means markup on cost, while the block says «نسبة الربح» on sell.** A 15 percent line markup does not give 15 percent profit, and the owner will compare the two. Call the line column «نسبة الزيادة», or show both on one base. Karim confirms the base (README open item).
16. **S6 group totals have no label.** The cream bar over each باب shows 179,630.00 and nothing says it is the group total. Add «إجمالي الباب» in small text.
17. **S7 «رصيد رسوم المعاينة 500.00».** A stranger asks what a credit does. The row should say what happens to it («تُخصم من العقد»), or be hidden if the deduction happens elsewhere.
18. **S3 fee rows at 1440.** The label sits at the right edge and its action («تسجيل», «بدء العرض», «فتح», «عرض») at the left edge, about 800 px apart, bare verbs with no object. Narrow the card or put the verb next to its value.
19. **S6 banner «3 عروض مفتوحة على أسعار قديمة.»** It has no action. Which offers? What should the owner do? Make it a link to those offers or drop it.

### Noise

20. **S6 owner table.** Eight columns at 1440 with a boxed input on every line; at 390 each line becomes six small label and value pairs, with unit and quantity in separate cells («42.5» apart from «م³»). Join quantity and unit in one cell; keep cost and markup in the owner view only.
21. **S1 row at 390.** Up to five items on a row (stage, status, quiet days, follow-up badge, amount). With finding 13 applied it falls to three.
22. **Mock data.** The same opportunity (فيلا التجمع الخامس) is 1,250,000 on S1 and 415,098 on S3 and S6. The sidebar user is «منى سامي، المالية» on screens built for Marketing and Owner. Both look like errors to a first reader.

## Explaining sentences on screen

Allowed by the rule (state, label, refusal, waiting line): «بانتظار المكتب الفني: فتح المشروع»; «هادئة منذ N أيام»; the S7 refusal lines «لا يمكن فتح مشروع: الفرصة ليست في مرحلة التفاوض.» and «تم فتح مشروع لهذه الفرصة من قبل.». Dead weight: «3 عروض مفتوحة على أسعار قديمة.» (finding 19). «هذا الرقم مسجّل للعميل أحمد سمير.» on S2 gives the reason for a prompt and is acceptable once the prompt has two clear actions (finding 9). No other explanatory sentence found.

## Codes and ids on screen

Only the KF-1101 style catalogue code in S6 search results, and phone numbers. Phone numbers are fine: they are typed and searched.

## Dark mode

Dark holds up at 390 (S1, S3, S6 print) and at 1440 (S6 owner): borders stay visible and status pills keep shape plus word. Two items: the print preview paper (finding 7), and the grey "quiet days" text on S3, which is faint against the dark surface.

## First three changes

1. **Fix the handoff (findings 1, 2, 3 and the Marketing request).** This is where a signed deal turns into a project. As drawn, the Technical Office cannot find the request, Marketing cannot see or withdraw it, and nobody can get from the won page to the project. It costs days on a won contract.
2. **Make S6 buildable and move the margin to the top (findings 5, 6).** A quote that cannot take a quantity cannot be built, and the owner reads the margin only after scrolling every line. This is the screen the sales stage exists for.
3. **Clean the vocabulary (findings 9 to 15).** The same word means two things (متابعة, عرض), invented status words collide with Kaff's approved ones, and stalled is said three times. This is copy and layout, cheap now and expensive once the strings sit in `ar.json`.

## Anything wrong in the brief

- The standing brief says to write to `qa/consultant-review-<date>.md` and nothing outside `qa/`; the task asked for `ux/consultant-review-sales-mock-2026-11-22.md`. I followed the task. Screenshots went to `qa/shots-consultant/` as instructed.
- The task says dark at 1440 was not drawn. It renders correctly from the theme tokens, so no separate drawing is needed.
- The driver `shot` command is slow (about 30 seconds per image); 31 shots took roughly 15 minutes.
