# Release 5 portal and design screens (`portal.html`)

One file, 14 screens. `?s=<id>&view=<state>`; `?embed=1` hides the preview strip. Screens P1 to P8 are the client portal (phone first, own entry, own top bar with the wordmark and sign-out; no staff shell). S1 to S6 are staff screens drawn as page content only: the staff shell is `layout.html` and is not redrawn. Source of the briefs: `plans/release-5-portal/screens.md`. Every question dependency below is drawn with the default from `plans/release-5-portal/questions.md`.

Nothing here is approved. Nabil approves the mock before a build.

## Screens and states drawn

| Id | Screen | `?view=` states |
|---|---|---|
| p1 | Portal sign-in | normal, busy, failed, change (forced password change) |
| p2 | My projects | normal, empty, loading, failed |
| p3 | Project home | normal, none (nothing pending, no primary action), noplan, nolog, design (design project), loading, failed |
| p4 | Extracts | list, detail (paid), unpaid, empty, loading, failed |
| p5 | Change order | normal, confirm, refuse, busy, failedact, approved, refused, withdrawn, notfound, loading, failed |
| p6 | Design stage | normal, confirm, sendback, limit, busy, failedact, approved, paid, upcoming, dlfail, loading, failed |
| p7 | Released files | normal, empty, loading, failed, dlfail |
| p8 | Published photos | normal, open, empty, loading, failed |
| s1 | Publish photos | normal, selected, busy, failed |
| s2 | Design workspace | progress, review, empty, failed, busy |
| s3 | Design closure | unmet, met, refused, busy, closed |
| s4 | Create furnishing project | normal, busy, failed, refused |
| s5 | Create client account | normal, created, refused, busy |
| s6 | Change order step (staff) | normal (step reads "with the client", no action) |

P2 is skipped by the app when the client has one project (brief); it is still drawn.

## Reused from the kit, unchanged

Buttons (primary, secondary, quiet), pill, badge, banner (error, note), saved-in-place record (`.rec`), state block (empty, refused), skeleton, field and control, back link, link, step chain (S6), tokens and colours. No colour literal outside the copied token block.

## New in this mock (kit candidates)

- Portal top bar: the phone header of `babs.html` with a sign-out icon button; used at every width.
- Sticky action bar (`.abar`) at every width, one filled button. On the phone this is the kit's bottom-bar rule; on desktop it is the same bar, centred at 720 px.
- Project card with pending badge and paid/remaining meter (P2).
- Meter (8 px bar, fill plus percentage in text, so colour is never the only carrier).
- Key/value group (`.kvs`) for money and contract lines.
- In-place confirm group (`.cfm`): restates the total before approve; same pattern as the kit's two confirmations.
- Photo card and two-column photo grid (P8, S1).
- Five-segment stage bar (P6).
- Checkbox in a 44 px label (S1).
- Responsive table that becomes labelled rows at 820 px and under (S2).
- Condition list with a settle action per unmet row (S3).

## What each screen leaves out on purpose

No project code, no contract type name, no staff names, no cost, margin, hold-ledger name, advance or withholding lines, QC or Technical Office text. Money is two decimals; the currency name appears once per money group. Status words are Kaff's own (جاري العمل, لم تبدأ).

## Question dependencies

- Q1, Q7: P1 uses phone plus password; S5 hands over a temporary password (shown once after create). Reset is the Owner's act, so P1 has no "forgot password" link.
- Q2, Q6: P6 treats "approved" as the client's approval, fee shows as due after approval; watermark pill until paid, clean copy after (paid state). P7 shows the same two pills.
- Q3: P5 is the client's approve/refuse; S6 drops the staff "record client approval" action and shows the step as "with the client".
- Q4: S2 review step waits on "المالك" (Owner reviews until Head of Design is on). Rounds drawn as "1 of 2" with a made-up agreed number of 2.
- Q5: P6 `limit` state: send-back becomes "طلب تعديل إضافي" with the one consequence line that Kaff prices it. Wording is a guess (Q5 unanswered).
- Q8: P4 shows work this period, cumulative work, one deductions total, net, paid state. Hold, advance and withholding are not itemised. Cost Plus has the same screen shape (no quantity lines exist on it); not drawn separately.
- Q9: P3 time plan is start and planned end, plus a planned end under each section. `noplan` removes the whole block.
- Q10: S3 asks the Owner to record the IP transfer (action "تسجيل النقل") and to close.
- Q11: S4 only shows the form; the refused state is drawn with the reason shown as a refusal line; P2 lists the furnishing project as its own card (one login shows both).
- Q12: S1 allows unpublish ("سحب النشر") on published photos.
- Q13: no automatic message. P3 and P2 show the pending badge and pending block first; nothing else tells the client.
- Q14: P5 only shows from client approval onward; decided ones show the outcome (approved, refused, withdrawn).

## Invented Arabic (guesses; Karim or Nabil confirm)

Stage names (P3, P6, S2): المفهوم الأولي, المخطط المبدئي, التصور ثلاثي الأبعاد, تطوير التصميم, الوثائق النهائية.
Labels and states: بانتظارك, عند كف, عند العميل, تمت موافقتك, تمت الموافقة, مقدَّم من, فريق كف, بعلامة مائية, نسخة نظيفة, نسخة نظيفة, سحبه كف, مرفوض, مدفوع, غير مدفوع, مدفوعة, غير مدفوعة, الأتعاب, المستحق, جولات التعديل, إرجاع للتعديل, طلب تعديل إضافي, التعديل خارج الجولات المتفق عليها ويُسعَّر من كف, مراجعة داخلية, اعتماد وإرسال للعميل, إرجاع للتنفيذ بسبب, رفع المسلَّم, تسجيل تحصيل, تسجيل النقل, سحب النشر, نشر المحدد, كلمة السر المؤقتة, الدور.
Sign-in refusal line: رقم الموبايل أو كلمة السر غير صحيحة.
Sample data (names, amounts, dates) is made up.

## Could leak cost or internal state to a client (checked)

- P5 lines show quantity, unit and unit price: this must be the sell price copied to the change order, never the catalogue cost. Build must send the sell price only.
- P4 and P3 "remaining" and "paid" are cash figures; if the API computed them from cost-plus cost lines they would expose cost. Needs the API to send only the client-facing figures.
- P6 fee is the contract fee, not hours or salary.
- P3 per-section percentages are progress, not quantities or cost.
- Build must not send reviewer names, audit data or internal reasons; P5 refusal reason is typed by the client and goes to Kaff, not the other way round.

## Not decided

- Whether the 5-colour state for "waiting" on P2 uses a count or only a mark.
- Real photo thumbnails (drawn as placeholders).
- Staff shell around S1 to S6 (see `layout.html`).
- Desktop width for the portal (drawn single column, 720 px).
