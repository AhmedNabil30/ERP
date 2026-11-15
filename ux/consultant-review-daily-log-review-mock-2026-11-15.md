# Consultant review: the owner's daily log mock (mode: mock)

Mock: `.design/v2/team-a/daily-log-review.html`. Serves D-225 item 1. Screenshots of the `two` view at 390 and 1440 are in `qa/shots-consultant/` (dlr-two-390.png, dlr-two-1440.png). The other states were judged from the mock's data and markup, not screenshotted.

Verdict: approve after must-fixes.

## Must fix before approval

1. The crew total in the link row does not match the men listed below it (top of screen versus each block's العمالة group). On today the row says 9 workers and 62 hours, while the two blocks list 5 men and 39 hours. أمس says 5 workers and 34 hours against 2 men and 14 hours listed. السبت says 4 workers and 30 hours against 1 man and 8 hours. An owner who adds it up thinks men are missing or hours are invented. Either the groups must carry every man the row counts, or the row must say it counts something else (for example crew logged by the supervisor). The designer has to decide which and make the data agree.
2. An engineer who has not checked out shows nothing at all (Hiba's attendance strip, today view). A missing الانصراف reads as an error or a gap. The owner cannot tell "still on site" from "left without checking out". Name the state in the strip, using whatever word the daily log already uses for a person still on site.
3. "بعيد عن الموقع" gives no distance. The owner's question was "how far from site". The pill says that he was far but not how far. If the server has the distance, show it in the pill; if it does not, the brief's question cannot be answered here and the owner should be told that in review, not discovered later.
4. The case of an engineer who checked in and logged nothing is not drawn. The code shows his attendance strip alone, with no entries and no sign that nothing was logged. This is the case the owner most wants to see (someone was on site and recorded nothing). It needs a drawn state: the strip plus a single dim line, using the approved empty wording of `daily-log.html`.
5. The `none` state hides everything, including attendance. If an engineer checked in on that day but logged nothing, the owner is told "nothing logged" and never learns anyone was on site. State 4 and this one must be designed together.

## Should fix

6. «مصحَّحة بواسطة هبة فؤاد» says that a quantity was changed and by whom, but not from what or to what (Khaled's tiling row, today view). The owner cannot judge whether the correction matters. Showing the original figure in the dim tone is enough. The pill and the name alone raise a question and answer none.
7. The corrected mark sits on a second line under the row, so it is easy to scroll past at 390 and the figure it refers to looks like any other. It should sit on the same row as the figure, or the figure itself should look different.
8. «لم يُسجَّل شيء في يومية الأحد بعد.» carries "بعد" (yet) on a day that is over. For a past day the owner reads it as "someone still has to log it". Use the sentence without "بعد" for past days and keep it for today.
9. The stopped banner «المشروع متوقف» uses a word that is not one of Kaff's five (لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها), and the status pill disappears, so the project has no status at all in the header. The owner sees a blank where the status was. Nabil has to name the word; until then, keep the pill's place filled rather than removing it.
10. The day switch offers four days only, with no way to reach an earlier one. The owner who wants last week has no path and no hint that none exists. Add one control for an earlier day or state the limit.
11. The weekday names are used as labels for the last two days (الأحد, السبت) next to اليوم and أمس. The date is written once in the row, but only for the selected day. The owner has to press to learn which date الأحد is. Low cost, acceptable, but write the date under the weekday if room allows.
12. The order of blocks (people first, then groups within each person) makes the owner read the same group heading twice when two engineers log the same project. See the answer to question 1 below.

## Fine

- No money, no code, no id, no edit button on screen. Kaff status word جاري العمل verbatim. No explaining sentence anywhere except the empty-state sentence already approved on `daily-log.html` and the failure sentence in the refusal banner.
- The path to the crew screen is obvious: one row with an icon, a title, a summary and a chevron, directly under the day switch. It is the same row as in `daily-log.html`. It does not say that the owner can edit there; the owner finds out on arrival, which is acceptable because the rule is that the screen carries no explanation.
- Failed load: the frame, tabs and day switch stay, the sentence says what is true, the retry button is right there. Good.
- Loading: skeleton with no tab bar, consistent with `project.html`.
- Two columns from 1000 px with the crew row full width above them (1440 screenshot): readable, nothing cut off.
- At 390 the stage line, tabs and day switch take most of the first screen; the first engineer's attendance and first quantity appear at the fold. Acceptable for a read screen, but see answer 5 of the open questions below.

## The designer's five open questions

1. Crew lines inside each block plus the link row: keep both, but only after must-fix 1; two crew sources that disagree are worse than one.
2. Attendance on past days: keep. Attendance is what the owner checks, and hiding it on a past day would hide the exact back-dated cases.
3. Back-dated flag per log or per entry: per log. The owner asks "was this day written afterwards", not "which line". One pill in the strip answers it.
4. Stopped project status word: keep the banner for now, but do not remove the pill; ask Nabil for the word (must-fix 9 / should-fix 9).
5. Owner edit of a corrected quantity: keep none. The owner records nothing here (D-225), and the crew screen is where he edits.

## Block structure: one per engineer or by work?

The owner thinks by work first and by person second. One block per engineer is right when he needs to ask "who is to blame / who was there", and wrong when two engineers log the same trade on the same day, because he then reads two quantity groups and adds them in his head. The mock's data does not show this clash (the two engineers log different work). I would keep the engineer block for this release because the server returns logs per engineer and D-225 words it that way, and put the stoppage reason and corrected quantities first in each block (stoppage already is). A combined by-work view should be a question for Nabil, not decided here.

## The two new words

- «مصحَّحة»: natural. An Egyptian owner reads it as "corrected". The tashkeel is not needed; plain «مصححة» reads the same and prints cleaner on a phone.
- «بواسطة هبة فؤاد»: understandable but a little formal for a site owner. «بواسطة» is acceptable in an app, and Kaff users see it in other audit contexts. If a shorter form is wanted, «عدّلتها هبة فؤاد» is more spoken, but it fits only if the name goes after the verb and the gender is known. Keep «بواسطة», it avoids a gender problem.

## Things I could not open or judge

- I screenshotted the `two` view only (390 and 1440). `one`, `none`, `stopped`, `loading` and `failed` were judged from the mock's markup and data, not seen rendered.
- The mock's "today" is Tuesday 10 November; the real date today is 15 November. Preview data only; not a finding.
- LTR/English not judged.
