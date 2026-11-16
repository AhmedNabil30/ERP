# Consultant review: D-227 mocks, 2026-11-16

Mode: mock. Static files only, no stack. Rendered with the driver `shot` command (file URLs, light theme).

Rendered at 390: project-team.html views normal, noeng, add, adderr, addeng, addfail, addnone, confirm, added, removed, readonly, loading, failed; boq.html views sign, signerr, pending; project.html view signed and type=ds views setup and normal. Rendered at 1440: project-team.html normal and project.html signed. I opened and looked at every one of these renders. Before the interruption: team normal, noeng, addeng, adderr, addfail, confirm, added, readonly, boq sign, signerr, pending, project signed (390 and 1440), team normal (1440), ds setup, ds normal. After the interruption: team add, addnone, removed, loading, failed. Screenshots are in qa/shots-consultant/ (d227-*).

Verdicts: A project-team.html approve after must-fixes. B boq.html approve after must-fixes. C project.html approve after must-fixes (the design project part needs its stage line redrawn).

## Must fix before approval

1. **Design project, stage line contradicts the primary** (project.html?type=ds&view=normal). The stage line has التنفيذ as the current stage and the hero says 50%, yet the only primary is «تحويل إلى تنفيذ». A stranger asks: am I not in execution already? Either the stage line shows the real design stage, or the primary is not offered here. This also touches the designer's question 8; the business rule must come from Nabil.
2. **Rate labels on the sign sheet** (boq.html?view=sign). «نسبة الدفعة المقدمة» and «نسبة استرداد الدفعة المقدمة» differ by one word and sit one above the other. The person signing cannot tell, without being told, that one is what the client pays up front and the other is how it is taken back from extracts. Use two plainly different words; Nabil to choose, because «استرداد» is accounting talk to the Technical Office.
3. **Same idea, two words: «نسبة المحجوز» (boq.html sign sheet) vs «نسبة الحجز» (project.html terms and the app).** The designer flagged it. The sign sheet is where the number is first typed, so it must carry the word the project page and the hold use later. Fix in the mock before approval, not at build.
4. **The first step after signing is wrong for the person who signed** (project.html?view=signed). The Technical Office signs; only the Owner may add people. A Technical Office user gets a notice and a primary «إضافة مهندس موقع» that leads to a team tab with no add and no remove button (the readonly view). The mock draws only the Owner. Draw the Technical Office version: no primary, notice as information only. The Owner version is right.
5. **Team tab: the person cannot see which tab he is on at 390** (project-team.html, all views). The tab strip is scrolled so that الفريق is off-screen, نظرة عامة is the first visible tab, and nothing on the page says "team". The list starts with no heading. Scroll the strip so the current tab is visible.

## Should fix

1. **Status pill «جاري العمل» on a project signed a minute ago at 0%** (project.html?view=signed). The approved word for nothing started is «لم تبدأ». Check the pill says what is true on day one.
2. **Terms with all three rates are below the first screen** (project.html?view=signed at 390 and 1440). The README says the terms open with all three; the first screen shows only the notice, the hero and the Bab list. The signer cannot see his three numbers took effect without scrolling.
3. **Design project, unsigned: one explaining sentence** («المشروع غير موقّع. تنتهي فترة المتابعة في السبت 5 ديسمبر.», ds setup). The pill «لم تبدأ» and the primary «توقيع التعاقد» already carry it; keep the date, drop the first sentence.
4. **Design project: «لم تُحدَّد بعد» with an info icon on each row** (ds setup). Two marks for one fact, and the icon invites a tap that does nothing.
5. **Disabled primary on pending BOQ** (boq.html?view=pending). The reason line «بنود بلا كمية: 1» is clear, but nothing points to which line; at 390 the user scrolls a long list to find it. Also the README says disabled is only for what the user cannot fix on the screen, yet he can fix this here: rule and drawing disagree.
6. **Item count 9 in pending vs 8 in sign** (boq.html). Make the numbers agree or a reader thinks an item was lost.
7. **Added view: the new row is below the fold** (project-team.html?view=added at 390). The banner «أُضيفت نورا سعيد» is enough to know it worked, but the tinted row hides behind the bar.
8. **«مهندس موقع» vs «مهندس الموقع»** (project.html notice and primary vs team list). Choose one; the app says «مهندس الموقع».

## Fine

- Team normal at 390 and 1440: one primary «إضافة شخص» (bar on phone, header on desktop); each row reads name, role, quiet badge. No ids, no emails.
- Owner not listed: right. The list is "who else works here".
- Removal: the consequence sentence «يفقد وصوله إلى المشروع وتبقى أعماله السابقة. يمكنك إضافته من جديد.» is needed; it answers the one question a person has ("do I lose his work?"). Neutral tile, no red, secondary button on the row: correct.
- Finance has no remove button, absent not greyed: right.
- Add sheet: nothing preselected, level only for a site engineer, field-specific errors. A stranger finishes in under a minute and the saved banner confirms it.
- «مبتدئ» / «مشرف» next to «مهندس الموقع»: Kaff people know the words; no sentence needed.
- «تلقائي» and «أنشأ المشروع»: quiet, only two rows, they help. Not clutter.
- Read-only view: same list, no buttons, no primary; the bar shows only «القائمة». Clear.
- Design project: stage word «التعاقد» fits; rate per م² beside area and fee reads well; one primary at a time (ds setup «توقيع التعاقد», ds normal «تحويل إلى تنفيذ»).
- Third sign field: full-width label, error under it, percent affix. Order hold, advance, recovery matches the terms list.

- Remaining team states (second pass): add (empty select, nothing preselected) fine; addnone shows one line with an info tile and one button «إغلاق», fine apart from the word «مستخدم»; removed shows the banner «أُزيل خالد منصور» and the row gone, fine; loading is a skeleton with no title, fine; failed shows «تعذّر تحميل الفريق.» with «إعادة المحاولة» but the page title falls back to the bare word «المشروع» with no project name, minor (should fix).

## The designer's nine questions

1. Owner remove Finance: no. Finance is on every project so money reaches it.
2. Owner remove the creator: yes. A convenience, not a role; the Owner may hand the project over.
3. Two supervising engineers: yes, no cap. A cap is an invented rule.
4. Removed person's past work: stays under his name; row leaves the list; audit log keeps who acted.
5. Add back after removal: yes. The confirmation promises it.
6. First step after signing leads to the team tab: right for the Owner, wrong for the Technical Office (must-fix 4).
7. Rate per م² beside the fee everywhere: yes. It lets the signer check the fee.
8. When «تحويل إلى تنفيذ» appears: needs Nabil's business rule. Right after signing contradicts the stage line (must-fix 1).
9. First stage word «التعاقد»: good. Matches «توقيع التعاقد», does not suggest a BOQ.

## New words

- Natural: «إضافة شخص», «إزالة», «الشخص», «الدرجة», «اختر شخصاً», «اختر الدرجة», «أُضيفت {name}», «أُزيل {name}», «لا يوجد مهندس موقع», «تلقائي», «أنشأ المشروع», the confirmation text.
- System talk: «لا يوجد مستخدم متاح للإضافة» («مستخدم» is the system's word; the Owner thinks "no one left to add"); «نسبة استرداد الدفعة المقدمة» (must-fix 2).

## Nabil's rules check

- Explaining sentences: the confirmation consequence (needed); the existing sign sheet lead «التوقيع يثبّت قيمة التعاقد على إجمالي المقايسة…» (longest sentence on the sheet, a candidate to cut); the unsigned design notice (should fix 3). No others.
- Codes or ids: none seen.
- Kaff status words: verbatim; one doubt (should fix 1).
- One term one meaning: three mismatches above (المحجوز/الحجز, مهندس موقع/مهندس الموقع, near-twin rate labels).
