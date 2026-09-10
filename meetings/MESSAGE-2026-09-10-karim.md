# Message to Karim — 2026-09-10

**Six questions. They are the last 16 points of slice 2 (Masters).** Four stories — `KAFF-209`,
`KAFF-210`, `KAFF-211`, `KAFF-212` — are refined, estimated and **cannot start** until these are
answered. `KAFF-204`'s data is the sixth.

**Send it as one message.** These were asked separately over three days and none of them can be
answered by us. Nothing here is a preference; each one is a rule that would otherwise be invented,
and an invented rule is always plausible, which is why it survives review and surfaces during
acceptance months later.

**Answers come back to Nabil, who rules or forwards.** Do not let an agent answer any of these.

---

## The message

> يا كريم، ست حاجات محتاجين ردك عليها عشان نكمل. كل واحدة منهم واقفة قدام شغل جاهز.
>
> **١ — الموبايل المتكرر (`Q70`).** لما حد يسجّل عامل ورقم موبايله يطلع مسجّل قبل كده على عامل تاني —
> النظام يرفض الحفظ خالص، ولا يحذّر ويسيبه يحفظ؟ **واسأل نفس السؤال للمقاولين من الباطن وللموردين،
> كل واحد لوحده** — الرد ممكن يختلف من نوع للتاني وده سبب إننا مش دامجينهم في قاعدة واحدة.
> *ليه بنسأل:* التلاتة دلوقتي بيرفضوا، والقاعدة اللي اتحطت للعملاء (تحذير ثم تأكيد) **اتقال لنا بالنص
> ما تمدّهاش لدول — اسأل.**
> *ملحوظة مهمة أياً كان الرد:* المطابقة هتكون على الشكل الموحّد للرقم، يعني `+20 10…` و `0020 10…`
> و `010…` كلهم يعتبروا نفس الرقم.
>
> **٢ — مين يسجّل العامل (`Q71`).** المهندسين بيسجّلوا العمالة من الموقع. **أنهي دور بالظبط له الحق
> يعمل ده — وعلى المشاريع المسنودة له بس، ولا على أي مشروع؟**
> *ليه بنسأل:* المواصفات بتقول المهندس بيسجّل، ومفيش مهندس عنده صلاحية توصله للشاشة دي. **والحل مش إننا
> ندّيله صلاحية شئون العاملين** — دي هتفتحله سجل الموظفين بالمرتبات كمان.
>
> **٣ — إيه هي "المرة" الواحدة (`Q72`).** لما نقول تاريخ تشغيل عامل — الوحدة هي **يوم في الموقع، ولا
> فترة شغل، ولا مشروع كامل؟** ولما نقيّم العامل، **بيتقيّم من كام؟**
> *ليه بنسأل:* من غير الأول، كلمة "بيشتغل معانا كتير" مالهاش معنى نقدر نحسبه.
>
> **٤ — أسعار المقاول من الباطن (`Q73`).** ملف المقاول نفسه بيشيل الأسعار اللي متفقين عليها معاه، ولا
> الأسعار بتعيش على مقايسة كل شغلانة لوحدها؟
>
> **٥ — الخصم عند السداد (`Q29`).** قلت لنا قبل كده إن نسبة الضريبة بتاعة العقد مش بتاعة العميل.
> **نفس الكلام ينطبق على المقاولين والموردين اللي بندفعلهم؟** يعني النسبة صفة في الشغلانة ولا في الشركة
> نفسها؟ ومعاها: **مين اللي بيحط الرقم الضريبي على ملف العميل** دلوقتي بعد ما النسبة اتنقلت للمالية؟
>
> **٦ — الأبواب (`Q75`) — وده اللي محتاج ورق مش رد.** **إيه هي أبوابكم، وكل باب نسبة الإضافة بتاعته
> كام؟** هنبعتلك ملف إكسيل بالأعمدة جاهزة، إنت تملاه.
> *ليه بنسأل:* الـ ١٥٪ للخرسانة والـ ٣٠٪ للتشطيبات المكتوبين في المواصفات **دول مجرد أمثلة، مش نسبكم**.
> لو حد بنى الشجرة من غير ردك، هيستخدم الرقمين دول وهيبانوا كأنهم بيانات حقيقية.

---

## Notes for Nabil, not for Karim

- **`Q75` is the only one that needs an attachment**, not a reply — the trades sheet from `Q60`'s
  template. It blocks `KAFF-204`'s **data**, not its behaviour: the tree, the screens and the markup
  rules can all be built and tested against fixtures. So `KAFF-204` can start before Karim answers.
  The other five block their stories completely.
- **`Q70` is one question asked three times on purpose.** If the answer comes back as a single rule
  covering all three populations, that is a legitimate answer — but it has to be said, not assumed.
- **`Q29` is a slice-3 question we are asking in slice 2** because the party masters are where the
  field would live. If Karim defers it, `KAFF-211` and `KAFF-212` can still be built with the rate
  held on the job; the cost of guessing wrong is a migration, not a rebuild.
- Sources: `stories/questions-for-karim.md` rows `Q70`, `Q71`, `Q72`, `Q73`, `Q75`, `Q29`.
