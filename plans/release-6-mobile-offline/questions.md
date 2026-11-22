# Slice 9 questions, most expensive to get wrong first

Each question says what it changes. "Today" is what the code does now. Recommended default is given only where the spec leans; the spec stands until Karim answers.

## Q1. أي تليفونات مع المهندسين؟ / Which phones do site engineers hold?
عربي: المهندسون في الموقع معاهم أندرويد ولا آيفون؟ التطبيق بيشتغل من المتصفح. الآيفون بيمسح اللي اتحفظ على التليفون لو المهندس ما فتحش التطبيق 7 أيام، فشغل اليومية اللي لسه ما اتبعتش ممكن يضيع.
English: Android or iPhone? The app runs in the browser. Safari clears script-saved data after 7 days without a visit (MDN, https://developer.mozilla.org/en-US/docs/Web/API/Storage_API/Storage_quotas_and_eviction_criteria, fetched 2026-11-22). A native app would break the pinned stack, which needs a decision in decisions.md.
Changes: S9-1, S9-10, the whole slice. Rebuild cost: highest (platform).
Default recommended: browser app; Nabil or Karim confirm the phone mix. Spec does not say.

## Q2. شغل اتسجل بالليل ووصل بعد نص الليل: يُحسب على أي يوم؟ / Entry made offline, sent after midnight: which day?
عربي: لو المهندس سجّل كميات الساعة 11 بالليل من غير نت، والنت رجع الساعة 1 بالليل، هل الكمية تتحسب على اليوم اللي اتعملت فيه ولا اليوم اللي وصلت فيه؟ دلوقتي اليوم اللي وصلت فيه، والنظام ممكن يرفضها لأنه مفيش حضور في اليوم الجديد. الكميات دي بيتبني عليها المستخلص.
English: Today the day it arrived, and the server can refuse it for "no check-in today". Q-219-1.
Changes: step 15, S9-9, S9-2. Cost: high, extracts bill from these quantities.
Recommended default: the day it was made, if still inside the window. Spec leans this way: §8 says offline sync is additive and D-188 keeps `OccurredAt` apart from `CreatedAt` so offline is honest.

## Q3. لو التليفون فضل من غير نت أكتر من 3 أيام؟ / Phone offline longer than 3 days?
عربي: التسجيل مسموح لحد 3 أيام ورا. لو المهندس في موقع بعيد أسبوع من غير نت، شغله هيترفض. نعمل إيه؟ نرفض ونحذّره قبلها، ولا المشرف يقبلها بإذن؟
English: Entries older than 3 days are refused (D-217 item 7). Options: refuse and warn on day 2; or supervisor may accept late entries.
Changes: steps 16, S9-2 AC6, S9-8 AC3. Cost: high if a new accept step is needed.
Recommended default: refuse and warn. A supervisor accept step is not in the spec.

## Q4. وقت الحضور: نصدّق ساعة التليفون؟ / Attendance time: trust the phone's clock?
عربي: الحضور بيتسجل بساعة التليفون لو مفيش نت، وبيتخزن معاه وقت وصوله للسيرفر. الحضور ممكن يدخل في تقييم الأداء الأسبوعي (تسليم التقرير في ميعاده). ساعة التليفون الغلط ممكن تبوّظ ده. هل الحضور له أي علاقة بالمرتب؟ المواصفات ما فيهاش كشف حضور للموظفين.
English: Check-in uses device time offline, with server time stored on arrival (D-207). Attendance feeds the performance score (spec §10 line 516, 🟡). Does attendance ever affect pay? Does a device clock far from the server get flagged?
Changes: S9-4, S9-9 AC3, screen 2. Cost: medium to high if pay is involved.
Recommended default: keep both times, flag a big gap, no pay effect. Spec does not say.

## Q5. عهدة أوفلاين: مين يعمل إيه؟ / عهدة offline: who does what?
عربي: المهندس الجونيور يكتب طلب العهدة مسودة من غير نت. المشرف يرسله والمحاسب يصرفه، والاتنين أونلاين فقط. حد الـ10,000 والسقف و"ما يتقفلش عهدة قبل الأولى" بيتفحصوا لما المسودة توصل، مش على التليفون. موافق؟ (الشاشة نفسها لسه ما اتبنتش أونلاين.)
English: Junior drafts offline; supervisor submit and Accounts pay stay online. Cap, ceiling and previous-cleared checks run on arrival. KAFF-609 and 610 are not built.
Changes: step 12, screen 5, S9-7. Cost: medium.
Recommended default: as above. Spec §9 line 378 and line 250 lean this way. The ceiling and cap are still Karim's 🟡 assumption 4.

## Q6. صورة الفاتورة تتربط بإيه؟ / What does an invoice photo attach to?
عربي: المواصفات بتقول "صور الفواتير" في التطبيق. نربطها بمصروف مسودة، ولا بتسوية العهدة، ولا بكليهما؟ دلوقتي مفيش مكان ليها.
English: Spec §8 line 359 lists invoice photos; no endpoint exists. Attach to an expense draft, to a عهدة clearing receipt, or both?
Changes: step 10, screen 4, S9-7 AC5. Cost: medium (new endpoint and screen).
Recommended default: expense draft and عهدة clearing both; unruled.

## Q7. عامل جديد بتليفون مكرر وأنت أوفلاين / New worker with a duplicate phone, offline
عربي: المهندس يسجل عامل جديد من الموقع. التكرار بيتكشف بالتليفون لما العامل يوصل. لو التليفون لموظف بمرتب يترفض، ولو لعامل تاني يتحذّر. شاشة العمال حالياً ما بتعرضش التليفون. نسمح بتسجيل العامل أوفلاين ولا أونلاين بس؟
English: Dedup is by phone (spec §10 line 514; D-144/D-146). The add sheet hides the phone (D-217 item 6). Allow offline registration and resolve on arrival, or online only?
Changes: step 7, screen 7, S9-11 AC3. Cost: medium.
Recommended default: allow offline, resolve on arrival. Spec silent.

## Q8. تليفون ضاع، أو مستخدم اتقفل وعنده شغل ما اتبعتش / Lost phone or deactivated user with unsent work
عربي: لو التليفون ضاع، فيه أسماء عمال وكميات المشروع (من غير أسعار). لو الحساب اتقفل، الشغل المعلق يتعمل فيه إيه؟ نمسحه ولا يفضل ظاهر للمهندس؟
English: The phone holds worker names, phones and quantities, no money. What happens to unsent items when the account is deactivated or the phone is lost?
Changes: step 18, S9-1 AC5, S9-11. Cost: medium.
Recommended default: no remote wipe; refuse on arrival, keep visible. Spec silent; D-053 session rules apply to sign-out.

## Q9. نشر الصور: مين وأونلاين فقط؟ / Photo publishing: who, and online only?
عربي: الصور تظهر للعميل بعد النشر المتعمد (مش تلقائي). مين ينشر؟ المهندس ولا المكتب الفني؟ وهل يتم أونلاين فقط؟
English: Spec §8 line 357 says published deliberately, not who. KAFF-606 is unbuilt.
Changes: step 9, screen 3. Cost: low to medium.
Recommended default: online only, not the engineer; unruled.

## Q10. تعديل كمية اتبعتت من غير نت؟ / Correct a sent quantity while offline?
عربي: التعديل على كمية وصلت السيرفر مسموح أونلاين بس؟ والتعديل على كمية لسه ما اتبعتتش بيغيّر المسودة نفسها. موافق؟
English: Today a correction is a signed-difference entry referencing a saved entry (D-219 item 1). Allow offline for pending entries only?
Changes: step 5, S9-3 AC7. Cost: low.
Recommended default: yes. PROPOSAL; spec silent.
