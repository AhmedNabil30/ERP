# Consultant review: module 1 (project setup) and module 2 (site day), 2026-11-16

Mode: app, current HEAD, rebuilt API. Phone 390 first (RTL), 1440 glanced. Driven as tech_office_demo, owner_demo, site_eng_demo, site_sup_demo. Screenshots are in `qa/shots-consultant/` (names given per finding, all prefixed m1- or m2-). `layout` at 360, 390 and 1440 gave PASS on every screen measured (daily log, crew, owner log, BOQ, overview, new project): no sideways scroll, no small targets, no raw keys, no GUIDs.

Verdict: Module 1 is NOT demo-ready as the Technical Office (one blocker). It is demo-ready as the Owner. Module 2 is demo-ready for the junior, the supervising engineer and the Owner, with the should-fix items below.

Data I created on the running database (nothing deleted): client «شركة الرؤية للتطوير»; projects «فيلا 47 — التجمع الخامس» (client الرؤية, signed 142,203.03, hold 10, advance 20), «تصميم شقة الرؤية» (signed 54,000, converted) and «تصميم شقة الرؤية - تنفيذ»; on فيلا 47 a quantity line of 4 م² on today, a material, a note and a stoppage on Saturday, a note by the supervisor on Friday, crew hours today.

## Blocks the demo

1. **Technical Office creates a project and is thrown out of it.** After «إنشاء المشروع» the Technical Office lands on a «غير مصرح بالدخول / ليس لديك صلاحية لهذا الإجراء» page. The new project is not in their list afterwards (they still see one project) and the BOQ address is refused. The Owner sees the project and its الفريق tab lists only two finance people, with no way to add the Technical Office. So the person who is supposed to build the BOQ cannot reach what they just created, and the Owner has no screen to fix it. Flow problem. Role: Technical Office. Screens: new project, then any project tab. Shots: `m1-to-forbidden-390.png`. (I built the BOQ and signing as the Owner instead.)

## Should fix before acceptance

2. **Price changes between picking and the line, with no word about it.** The catalogue sheet shows 2,127.50 for a concrete item; the BOQ line shows 2,446.63. A custom item entered at 12,500 shows 16,250 on the line. A stranger thinks the system corrupted their number. Either show one price in both places or say once what the line price is. Role: Owner/Technical Office. Screens: «الكتالوج», «بند مخصص», BOQ lines. Shots: `m1-boq-add-390.png`, `m1-boq-lines-390.png`.
3. **After signing, the screen does not move on.** Signing a lump sum BOQ leaves you on the BOQ page (no confirmation, no step forward). The overview then still offers «فتح المقايسة» as the bottom-bar primary action and the stage line still sits on «المقايسة». The next job (start site work, add the team, first daily log) is not offered. Role: Owner. Shots: `m1-after-sign-390.png`, `m1-overview-signed-owner-390.png`.
4. **Contract terms show a third rate nobody entered.** Signing asks for hold and advance. The terms then show «نسبة استرداد الدفعة المقدمة 25%» as well. A reader cannot tell where 25% came from or whether it can be changed. Role: Owner. Screen: «شروط التعاقد» (`m1-terms-open-390.png`).
5. **The crew link forgets the day.** On the owner's log with «يوم آخر» set to 10 Nov, and on the engineer's log on Saturday, tapping «العمالة» opens the crew screen on today. The reader edits or reads the wrong day without being told. Roles: engineer, supervisor, Owner. Screens: daily log to crew.
6. **The crew screen says «تم الحفظ» the moment you arrive.** Nothing was saved in that visit. It is a state, not an event, and reads as a confirmation. Role: all. Screen: crew (`m2-crew-sat-390.png`).
7. **Crew counts do not match the rows.** The summary says «3 عمال» (or «1 عمال») while four names are listed, three of them with «—». The reader cannot tell who is in the crew and who is only a name on the list. Role: engineer. Screen: crew view.
8. **The supervising engineer has no «تعديل» on today's crew and is given no reason.** The junior's crew shows saved; the supervisor sees no edit control, while the Owner does. On back days he can add. A dead end with no explanation. Unconfirmed whether this is intended. Role: supervising engineer. Screen: crew today.
9. **On the engineer's phone the form for the chosen kind is below the first screen.** Tapping «كمية», «خامات», «صورة» or «سبب التوقف» only highlights the tile; the actual field is under the bottom bar and the person has to scroll to find it. Role: engineer. Screen: daily log (`m2-kind-quantity-390.png`).
10. **Quantity «تعديل» exists only on today.** On Saturday the saved quantity has no edit control (0 found), so a correction to an earlier day cannot be made, and nothing says so. Role: engineer. Screen: daily log, earlier day.
11. **Owner's log shows a corrected quantity as two bare numbers.** «مصححة ≈ 5 ≈ 4 م²»: which is the old and which the new value is not labelled. Role: Owner. Screen: اليومية tab (`m2-owner-log-390.png`).
12. **Design project: the BOQ tab and the stage «المقايسة» are shown for a project that has no BOQ.** The tab is an empty page. «تحويل إلى تنفيذ» sits above «توقيع التعاقد» before the contract is signed, as a second button of equal weight. Signing asks only for area and gives 54,000 without saying the rate per م². Role: Owner/Technical Office. Screens: design overview (`m1-design-created-390.png`, `m1-design-sign-ask-390.png`).
13. **Disabled primary buttons give no reason.** «توقيع المقايسة» is greyed on a BOQ with lines that have no quantity (the only hint is a small «بند بلا كمية» badge); «حفظ» on the daily log and crew is greyed with no word until you find the field. Role: Technical Office/engineer. Screens: BOQ, daily log, crew.
14. **«فترة المتابعة» is never explained.** «المشروع غير موقّع. تنتهي فترة المتابعة في 2026/12/15» and «متابعة حتى 15/12/2026» in the list: a new person does not know what happens on that date. It is also written in two date formats (2026/12/15 and 15/12/2026). Role: Owner. Screens: overview, project list.

## Polish

15. **Project codes on screen.** «PRJ-377EC1A2» under every project name in the list, for every role including the junior, who cannot use it. Nabil's no-codes rule. Role: all. Screen: project list.
16. **«مطلوب» printed beside every starred field**, and again as the error. The star already says it. Screens: new project, quick-add client, sign sheet.
17. **Sign sheet carries a long sentence** («التوقيع يثبّت قيمة التعاقد…») under the fields. It is a real irreversible warning so I would keep it, but the layout plus the button name could carry most of it.
18. **«مجمّدة في 15/11/2026» and the signing date show the 15th** when I signed at about 01:00 on the 16th (Cairo). Looks like a UTC date, so a late-night signing is dated a day early. Check before it appears on a printed contract.
19. **Tab strip cut at the edge on a phone.** With six tabs (Owner, signed project) the first or last tab is clipped («المـ») and is reached only by swiping. Screens: overview, owner log.
20. **Selected state is weak** on the contract type chips in the convert sheet and on the project-type choice (outline only). Screen: `m1-convert-ask-390.png`.
21. **Previous-project picker** shows names only; with two projects of the same name you cannot tell them apart (no client or date). Screen: «ابدأ المقايسة» (`m1-boq-start-390.png`).
22. **Duplicate and near-duplicate catalogue entries.** Two lines named «خرسانة مسلحة للأعمدة» with different prices; two bab names «أعمال خرسانة» and «أعمال خرسانة مسلحة» in the custom item list; a stray «تحيا مصر» item with unit «m3». Looks like test data but a stranger will pick the wrong one.
23. **Custom item unit is free text** while the daily log material unit is a list. The same thing (unit) is entered two ways, and the free text will produce «عدد», «عدد.» and «ع».
24. **Total printed as «93,453.03ج.م»** with no space before the currency on the BOQ total and overview.
25. **Owner's log shows distance «بعيد عن الموقع · 16,571 متر».** True for the demo, but a reader should see kilometres past a few thousand metres.
26. **Photo form:** the driver cannot attach a file, so I judged the form only: «التقاط صورة» and a description field, no file choice (known: opens the camera directly).

## Not reported again (known list)

Plural «1 عمال», «الكمية المقاسة» over an estimate, no stockpile control, a man cannot be removed from a saved day, camera opens directly, `/day-labour/pool`, `?????` seed rows, empty «مقايسات جاهزة», the three owner-log departures (date control own row, chips without day number, switch while loading), worker registration Enter key. All still present as described; none looked worse.

## Checked and fine

Duplicate project name is refused inline («يوجد مشروع بهذا الاسم لهذا العميل»). Quick-add client works from the picker and fills the name. The overview frame, stage line and the contract terms with hold and advance read well. The BOQ gives a clear first-line «ابدأ المقايسة» choice. The sign sheet refuses empty and 150% («من 0 إلى 100»). Design sign confirms the figure, convert pre-fills «… - تنفيذ» and the two projects link to each other. Junior sees no money on list, overview or log (only percent progress and quantities). Quantity pick, save, and correct (value pre-filled, «حفظ التعديل» in the bar) all work. Material unit comes from a list. Three days back works with the label «تنفيذ الجمعة (تقديري)». Crew: half steps work, 17 is refused inline («الحد الأقصى 16 ساعة»), the supervisor's back-dated entries carry «بأثر رجعي». Owner sees the log of every engineer with names, and «يوم آخر» opens the chosen day.

## Could not reach

- Fresh check-in as an engineer: today was already checked in by an earlier run, so I could not judge the first-time check-in flow or the denied-location case.
- «نفس عمالة أمس» (copy yesterday): it shows only on today, with no men, with a crew yesterday. Today already had men and a man cannot be removed from a saved day. Not reached.
- Photo attach: the driver cannot give a file.
- Technical Office BOQ building and signing (blocked by finding 1; done as Owner).
- Hold or advance on a Design project's terms (the screen asks neither).

## Errors in the brief

- «As Technical Office then Owner» assumes the Technical Office can open a project it creates; it cannot (finding 1).
- «Land on the overview» after signing: it stays on the BOQ page (finding 3).
- Stack was not running at the start; I started and stopped it as asked.

## Three changes first

1. **Fix finding 1** (the Technical Office must reach a project it creates, or the Owner must be able to add the Technical Office on the الفريق tab). The module cannot be demonstrated as designed until this works.
2. **Make signing land somewhere useful** (findings 3 and 4): after signing, show the next step in the bar and the terms including the recovery rate, so the demo has a visible handover to the site day.
3. **Make the day follow the person** (findings 5, 6 and 9): the crew link keeps the chosen day, «تم الحفظ» appears only after a save, and the form for the chosen kind is in view after the tap. These are the three places a site engineer on a phone will hesitate on day one.
