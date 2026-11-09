# Arabic copy review, 2026-11-09

Scope: keys added or changed in `git diff 543e1e5 HEAD -- src/Web/public/locales/ar.json` (commits 0b554cc, 0d76c30, 97c8567, 1cdf70f, 181f89f, f465aad). Read against `boq-page.html`, `project-workspace-page.html/.ts`, spec.md sections 5.1 and 15, D-208, D-209. Nothing in `ar.json` was edited. All suggested Arabic is the reviewer's suggestion, not Kaff's ruling.

Flag 1 (Kaff's five states): no new key touches them. `status.kaff.*` unchanged. Clean.
Flag 5 (label contradicts action): none found. The two identical "تحويل إلى تنفيذ" labels (open and submit) match their actions.

## Flag 3: one meaning, two words

| # | Key | Current | Shows | Problem | Suggested |
|---|---|---|---|---|---|
| 1 | `boq.print.title`, `boq.print` | "عرض سعر" / "طباعة عرض السعر" | Printed page heading; print button on the BOQ screen titled "المقايسة" (`boq.title`) | D-208: quotation = the BOQ. Same document, two names. Question to Nabil: which word? Reviewer's lean: contractors say مقايسة among themselves and عرض سعر to the client. The page goes to the client, so keep "عرض سعر" on the printed page only and leave the screen and button as مقايسة ("طباعة المقايسة"). Alternative: print heading "عرض سعر (المقايسة)". | Nabil's ruling needed. Does not block. |
| 2 | `billing.component.DesignCredit` (shown via `boq.credit.raised` label) | "رصيد التصميم" | BOQ banner after signing an execution project | Same 30% item is "خصم التصميم" in `errors.adjustment.design_*`. Also "رصيد" reads as a ledger balance, and the five ledgers must not be confused. | "خصم التصميم" in both |
| 3 | `projects.sign_design.title`, `.fee`, `.confirm_detail` | "توقيع عقد التصميم", "قيمة العقد" | Design sign step | BOQ sign step says "توقيع التعاقد", "قيمة التعاقد" (17 uses). Two words for one idea. | "توقيع التعاقد", "قيمة التعاقد" |

## Flag 2: one word, two meanings

| # | Key | Current | Shows | Problem | Suggested |
|---|---|---|---|---|---|
| 4 | `daily_log.location.denied` | "…فعّل إذن الموقع لهذا الموقع…" | Check-in refusal | الموقع = the site, the GPS position and the website, in one sentence. On a site visit this is unreadable. | "…فعّل إذن تحديد المكان (Location) لهذا الموقع الإلكتروني من إعدادات المتصفح أو الهاتف…" |
| 4b | `daily_log.checkin.hint` | "يُسجَّل موقعك ووقت جهازك." | Under check-in buttons | Reads "your site". | "يُسجَّل مكانك الحالي (GPS) ووقت جهازك." |
| 4c | `daily_log.location.unavailable`, `.timeout`, `errors.daily_log.location_required`, `daily_log.checkin.locating` | "تحديد موقعك", "الوصول إلى الموقع للمتصفح", "جارٍ تحديد موقعك" | Check-in errors | Same clash. Keep الموقع for the site only. | "تحديد مكانك", "إذن تحديد المكان للمتصفح", "جارٍ تحديد مكانك…" |

`far_from_site` "بعيد عن الموقع" is correct: there الموقع is the site. Existing, outside this diff: `daily_log.form.field` "البند" is the entry type, while "بند المقايسة" is a BOQ line. This is the known البند clash, still open.

## Flag 4: developer talk

| # | Key | Current | Shows | Problem | Suggested |
|---|---|---|---|---|---|
| 5 | `errors.daily_log.check_in_uses_check_in_route` | "يُسجَّل الحضور والانصراف مع الموقع، وليس كحقل عادي." | Error if a check-in is sent as a plain field | "حقل عادي" is the API talking. The user needs to know what to do. | "سجّل الحضور والانصراف من زر الحضور والانصراف، فهو يسجّل مكانك ووقتك." |

Existing, outside this diff, same flag: `errors.adjustment.termination_settlement_unruled` ("لم يُقرَّر ما الذي يُسوّى"); `closure.status.handover_pending` and `closure.status.handover` still carry "[PLACEHOLDER … awaiting Nabil]" in the visible string.

## Wording and format

| # | Key | Current | Problem | Suggested |
|---|---|---|---|---|
| 6 | `boq.credit.raised` | "رُفع {label} بنسبة 30%…" | "رُفع" can read as "lifted/removed". | "أُنشئ {label} بنسبة 30% من قيمة مشروع التصميم، وبانتظار اعتماد شخص آخر." |
| 7 | `errors.adjustment.design_value_missing` | "…خصم التصميم ٣٠٪" | Only key in the diff using Arabic-Indic digit and ٪. Everything else uses 30% / 20. | "30%" |
| 8 | `errors.project.not_archived` | "لا يمكن إعادة فتح إلا المشروع المؤرشف." | Clumsy. | "إعادة الفتح متاحة للمشروع المؤرشف فقط." |
| 9 | `boq.sign.hint.hold` | "تُحجز هذه النسبة من كل مستخلص حتى التسليم." | Meaning is right (spec 5.1: 20% of period work value, released once at handover). Does not say when it is paid back. Optional. | "تُحجز هذه النسبة من قيمة أعمال كل مستخلص، وتُفرج مرة واحدة كاملة عند التسليم." |
| 10 | `projects.convert.default_name` | "{name} - تنفيذ" | Answer to the designer: acceptable. The user can edit it. With a Latin project name the hyphen can flip sides in RTL. Optional. | "تنفيذ {name}" |

`boq.sign.hint.advance` ("نسبة من قيمة التعاقد يدفعها العميل مقدماً عند التوقيع"): meaning is right. Spec section 15 has the advance as 25% of contract, paid at signing. No change.

## Keys invented by a lane, never confirmed by Kaff

Kaff confirmed only the five status words and the D-207/D-208 facts (follow-up period, quotation = BOQ, location required). No Arabic below was reviewed by Nabil.

- `boq.print.title`, `boq.print`, `boq.print.client/project/date/unit`
- `boq.sign.hint.hold`, `boq.sign.hint.advance`, `boq.sign.field.*`, `boq.sign.error.rate_format`
- "فترة المتابعة" and "مؤرشف": `boq.archived`, `boq.follow_up`, `projects.state.archived`, `projects.state.follow_up_until`, `projects.archived.*`, `projects.follow_up.*`, `projects.list.filter`, `projects.list.show_archived`, `errors.project.archived`, `errors.project.not_archived`
- `boq.credit.raised`, `billing.component.DesignCredit` ("رصيد التصميم"), `errors.adjustment.design_*`
- `projects.link.*`, `projects.convert.*`, `errors.project.not_design`, `.already_converted`, `.execution_type_not_allowed`
- `projects.sign_design.*`, `boq.design_area`, `boq.design_area_unit`
- `daily_log.checkin.*`, `daily_log.location.*`, `errors.daily_log.location_required`, `errors.daily_log.check_in_uses_check_in_route`

## Count

10 corrections (items 1 to 10; 4 covers 3 sub-rows). Blocking: none. Item 1 needs Nabil's ruling; the print can ship with the current word meanwhile. Items 4 and 5 are the most worth doing before site use.
