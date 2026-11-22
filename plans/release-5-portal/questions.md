# Release 5 questions

Ordered by rebuild cost, highest first. Arabic is the version for Karim; English is for Nabil. "Default" is what the build uses until answered, filed as a RulingNote (ASSUMED). Where spec.md already leans, the line says so.

Q1 is the Architect's and Nabil's, not Karim's (stories/questions-for-karim.md N7).

## Q1. Is the client portal a separate deployment, or the same API behind a second web address?

**عربي:** بوابة العميل هتبقى برنامج منفصل على سيرفر لوحده، ولا نفس البرنامج بس بعنوان تاني؟ (ده قرار فني بين نبيل والمهندس، مش كريم.)

**English:** D-051 Q33 rules the portal is a separate host. It does not say whether that is a separate deployment or the same API on a second origin. Cookies, CORS and hosting all depend on it. Blocks KAFF-810 (stories/backlog.md line 1212). Changes journey step A3 and every portal screen's sign-in.

**Default:** same API process, second hostname, only `/api/portal/*` and the portal static files answer there, its own cookie. Spec leans to isolation (D-051); this is the cheapest form of it. Rebuild cost if wrong: hosting, auth and CI.

## Q2. How is a design stage billed, given a design project has no extract?

**عربي:** مشروع التصميم مفيهوش مستخلص. لما العميل يوافق على مرحلة، إيه الورقة اللي بتتطلب فلوسها؟ وهل الـ ٣٠٪ مقدم بتتحصل وقت التوقيع ولا وقت موافقة العميل على مرحلة الـ Concept؟ وكلمة «Approved» في حالات التصميم معناها موافقة الشركة الداخلية ولا موافقة العميل؟

**English:** spec §5.3 line 194 says no extract; §5.3 line 190 says a stage bills when the client approves; §13 line 567 has `Approved → Invoiced`. Three open points: what document is the stage invoice; whether the 30% deposit is collected at signing or at Concept approval; whether `Approved` is internal or the client's. Changes journey steps B4, B8, B9 and R5-D AC-D-5, AC-D-8.

**Default:** `Approved` means client approved. A stage becomes `Invoiced` with the fee from `DesignBillingCalculator`; no extract; Accounts collect through the existing collection flow. The deposit is a collection at signing, not tied to Concept. ASSUMED.

## Q3. When the portal opens, does the client's own tap replace the engineer recording his approval?

**عربي:** حاليًا المهندس المشرف بيسجل موافقة العميل على أمر التغيير. لما البوابة تشتغل، العميل نفسه هو اللي يوافق، ويتشال زر المهندس؟ ولو العميل رفض، يكتب السبب وأمر التغيير ينتهي (مرفوض)؟

**English:** D-241 item 4 had the supervising engineer record it before the portal. The ChangeOrderApprovers note says the step "takes a Client actor instead" if the client records it. Confirm replacement, and that a client rejection ends the change order, as the ChangeOrderRejection assumption has it. Changes journey A5, A6 and screen S6.

**Default:** replace. The engineer's endpoint is removed once the portal is live; a client rejection needs a reason and ends in `Rejected`. spec §12 line 541 leans this way.

## Q4. Who reviews a design deliverable internally, and may stages overlap?

**عربي:** مين بيراجع الملف جوه الشركة قبل ما يتبعت للعميل؟ (مسؤول التصميم لسه مرحلة تانية.) وهل ينفع مرحلة تبدأ قبل ما اللي قبلها تتدفع؟ وكل مرحلة كام جولة تعديل مجانية؟

**English:** spec §9 line 367 marks Head of Design phase 2, yet §13 line 567 needs an `InternalReview` step. Also whether stage N+1 may start before stage N is paid, and how many free revision rounds a stage has (§5.3 line 193 says "agreed rounds" with no number). Changes journey B1, B3, B10.

**Default:** the Owner reviews until Head of Design is switched on. Stages may overlap. Rounds are a number entered on the design contract at signing; no built-in number. ASSUMED.

## Q5. A design revision beyond the agreed rounds: billable change order, or logged only?

**عربي:** في المواصفات سطرين مختلفين: تعديل بعد الجولات المتفق عليها «أمر تغيير مصغّر بيتحاسب»، ولكن أمر التغيير في التصميم «بيتسجل بس من غير سعر». أنهي صح؟ ولو بيتحاسب، السعر بيتحدد إزاي؟

**English:** spec §5.3 line 193 says a billable mini change order referencing stage and round. spec §13 line 569 says Design change orders go `Draft → Logged`, unpriced. KAFF-509 lists this as not in its scope. The two lines conflict and the spec gives no price. Changes journey B7, AC-D-7, AC-812-I and the change order state machine for Design.

**Default:** a send-back past the rounds creates a Design change order that is logged (§13 wins, per the standing rule that spec.md stands), the stage waits, and Kaff prices it outside the system. Rebuild cost if wrong: the change order machine for Design.

## Q6. When is a deliverable "paid", so the watermark comes off?

**عربي:** العلامة المائية بتتشال امتى بالظبط؟ لما الفلوس تتسجل، ولا لما الشيك يتصرف؟ ولو العميل دفع جزء من المرحلة؟ والملف النهائي بيتسلم قبل تحصيل آخر ١٠٪، صح؟

**English:** spec §5.3 line 191 says watermarked until paid. Cheques have states received, deposited, cleared, bounced (§6.5 line 270). The final deliverable releases on delivery with the last 10% after (§5.3 line 192). Open: which state counts as paid, and a partial payment. Changes AC-813-B.

**Default:** a stage is paid when its fee is fully collected and any cheque for it has cleared. Partial stays watermarked. The final deliverable is released clean on delivery. ASSUMED.

## Q7. How does a client get his first password, and a reset?

**عربي:** العميل بيدخل بإيه (رقم موبايل وكلمة سر)؟ ومين بيدّيله كلمة السر الأولى؟ ولو نسيها، الرابط بيوصله إزاي؟

**English:** Only the Owner creates users (spec §9 amendment item 5). No SMS or WhatsApp provider exists, and N7's delivery half is open. Changes R5-A and screen S5, P1.

**Default:** the Owner creates the account and hands over a temporary password from his own phone, as he does for staff; a forced password change follows. A reset is the Owner's act. ASSUMED.

## Q8. What does the client see of an extract, and how is "remaining" worked out?

**عربي:** العميل لما يشوف المستخلص، يشوف إيه؟ الأعمال، والمحجوز، وخصم الدفعة المقدمة، والضريبة المخصومة؟ واحنا نعرض «المتبقي» إزاي: قيمة العقد ناقص اللي اتدفع؟ والمستخلصات اللي لسه جوه الشركة تظهر ولا لأ؟

**English:** spec §12 line 540 says "his مستخلصات, paid versus remaining". Hold and advance are client money in §15's table, so they are not Kaff cost, but nothing states they show. Open: line detail; whether remaining is contract value (revised) less cash received; whether pre-Issued extracts show. Changes screen P4, AC-B-3, AC-B-4.

**Default:** show only Issued and Paid extracts, each with work, deductions, net and paid state; remaining equals revised contract value less collected cash. Cost Plus shows a summary (§16 assumption 20). ASSUMED.

## Q9. Where does the project time plan come from?

**عربي:** المواصفات بتقول العميل يشوف «الجدول الزمني». مفيش جدول زمني في النظام دلوقتي. مين بيدخله، وبأي تفصيل: تاريخ بداية ونهاية للمشروع بس، ولا لكل باب؟

**English:** spec §12 line 540 lists it. No entity exists. Without an answer the portal shows start and end dates only, if those exist. Changes journey C2 and AC-B-7.

**Default:** start date and planned end date per project, plus a planned end per باب, entered by the Technical Office. ASSUMED; until then the block is absent.

## Q10. Who closes a design project, and how is the IP transfer recorded?

**عربي:** مين بيقفل مشروع التصميم؟ وانتقال الملكية الفكرية للعميل بيتسجل إزاي: خطاب موقّع، ولا الأونر بيعلّم إنه اتسلّم؟

**English:** spec §11 line 530 names the three conditions, not the closer or the record of IP transfer. Changes journey D3, D5 and AC-808-D.

**Default:** the Owner closes (ClosureActors assumption). The Owner records the transfer with a date. ASSUMED.

## Q11. Furnishing: who creates it, when, and does the parent's closure wait for it?

**عربي:** مشروع الفرش بيتفتح مين؟ ومن امتى (والمشروع الأصلي شغال ولا بعد التسليم)؟ وهل قفل المشروع الأصلي بيستنى الفرش؟ والعميل يشوف الاتنين مع بعض؟

**English:** spec §5.4 line 200 and §11 line 531 describe furnishing as an optional linked mini-execution project. Not stated: creator, allowed parent states, whether parent closure waits (ClosureConditions note lists no such condition). Changes journey E1, E4 and screen S4.

**Default:** Owner or Technical Office; parent Active through UnderWarranty; parent closure does not wait; one login shows both. ASSUMED; add a RulingNote.

## Q12. Who publishes a site photo, and may it be taken back?

**عربي:** مين يقدر ينشر صورة للعميل؟ وهل ينفع نسحبها بعد النشر؟

**English:** spec §8 line 357 says photos are published deliberately. Nobody is named. Changes screen S1 and AC-C-3.

**Default:** supervising engineer and Technical Office publish; unpublish allowed. ASSUMED.

## Q13. How is the client told something waits for him?

**عربي:** لما أمر تغيير أو مرحلة تصميم تستنى موافقة العميل، هو يعرف إزاي؟ إحنا مفيش عندنا إرسال رسائل. نكتفي إن حد من عندنا يكلمه، والبوابة تعرض المعلّق أول حاجة؟

**English:** No messaging provider exists in the pinned stack (N7 delivery half). Changes journey A2.

**Default:** no automatic message. The portal shows pending items first; Kaff staff call the client. ASSUMED.

## Q14. Which change orders does the client see?

**عربي:** العميل يشوف أوامر التغيير من أنهي مرحلة؟ لما توصله بس، ولا حتى وهي مسودة؟ ويشوف المرفوضة والمسحوبة؟

**English:** spec §12 says he approves priced change orders; it does not say what else appears. Changes P5 and AC-812-C, D.

**Default:** only from `ClientApproval` onward, plus the outcome of the ones he decided. Draft and TechnicalReview never show. ASSUMED.
