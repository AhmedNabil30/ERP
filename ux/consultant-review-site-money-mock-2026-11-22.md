# Consultant review (mock mode): Release 2 site money, `site-money.html`

Date: 2026-11-22. Scope: 13 screens (S1-S13) of `.design/v2/team-a/site-money.html`, judged against `plans/release-2-site-money/screens.md`, `journey.md` and `site-money-notes.md`.

Method: default view of every screen at 390 and 1440 in light and dark (S1-S7 all four; S8-S13 390 light, 1440 light, 390 dark), plus S1 refusal, S2 return, S3 refused, S6 owner/cap, S7 sheet and refused, S11 certify and pay, S13 hidden. Every screenshot was opened. They are in `qa/shots-consultant/sm/`. Dark mode and 1440 were not in the designer's run; both are clean (nothing unreadable, the pills and the S9 bar hold in dark).

Caveats on my own shots:
- The fixed bottom action bar overlaps the middle of long pages (S6, S11) in a full-page capture. That is the capture, not the mock; I have not counted it.
- I guessed some `view=` names (S1 `refused-cap`, `refused-earlier`, S6 `owner-cap`). They fell back silently to the normal view, so the S1 refusal sentences and the S6 breach state were not seen. A list of the real view names in the notes would fix this.

Ranked by what each costs the business. Fix type in brackets: layout, copy or flow.

## A. Flow problems (a person gets stuck or does the wrong thing)

1. **S5, the Owner approves blind. [layout + flow]** Only the first item is expanded. The other two (a 169,290 subcontractor extract and a 250,000 firm advance) show a title, a project and a preparer, nothing else. The Owner cannot tell what the 250,000 pays for without opening it, and he is asked to approve real money. Show a one-line purpose on every row and show the amount in the same form on every row (today only the first row has «ج.م»). The open item is simply the first; with ten items there is no order. Sort by age and open the oldest.
2. **S7 refused, Finance reaches a dead end. [flow]** The sheet says «لا تملك صلاحية تسجيل الضخ» and «يسجّله المالك», then only «إغلاق». The refused payment is not kept (Q7 default), so Finance must remember it, find the Owner and start again. Name the next move: a way to tell the Owner (an item in S5) or keep the payment pending. Settle with Q7; as drawn the mock teaches Finance to give up.
3. **S3, the leftover cash is never said to go back. [flow + layout]** The holder sees «غير المصروف 1,500» and «إرسال للمحاسبة». A stranger does not learn that 1,500 must be handed back, or when. Name the figure by the action (what to return), not by the arithmetic. The receipt camera is an empty icon on every line with no attached state; draw it, and decide whether a line without a photo blocks the send.
4. **S1, a draft has no owner and no next step. [flow]** «مسوداتي» lists drafts with a clock pill «مسودة». A clock says "waiting", but a draft waits for the supervisor, and nothing says so (journey Flow 1 step 5, "who tells him", is open). The junior cannot open, edit or delete a draft. Show who holds it, and change the pill icon so draft and waiting differ.
5. **S4, one list mixes what Finance can act on with what it cannot. [flow]** Row 4 «بانتظار المالك» is an Owner step, yet it sits among the rows to pay. Three different words say "your turn" («جاهز للصرف», «جاهزة للاعتماد», «جاهز للتأكيد»). Put "waiting on others" in its own group and use one phrase for "your turn". Nothing on a row says it can be opened.
6. **S11, the first phone screen is the chain, not the money. [layout]** The five-step chain with names takes about 380 px; the figures start below it, and net payable shows «—» at the first steps. The Technical Office must scroll past the chain to the number he certifies. Collapse the chain to one line («الدور على: ...») as S10 already does, expand on tap. Also confirm with Karim that Technical Office may see retention and net money; the brief does not say.
7. **S13, the supervisor types money he may not be allowed to see. [flow]** The hidden view still asks for 80000 and 20000 EGP. The normal view adds a 60,000 outstanding figure above two fields. If that figure does not move as he types, it carries no meaning. State in the notes that the hero must update as he types.

## B. Clarity problems (words and meaning)

8. **One word, two meanings: صرف.** It is Finance's pay button (S4, S11), a state («جاهز للصرف»), a spend line («بنود الصرف», S3), spent («المصروف») and the S4 row title «مصروف» for an expense draft; S10 also uses «مصروف» as a paid state. A site engineer and Finance use one root for "I spent" and "I pay out". Split: «دفع» for Finance paying, «مصروف» for an expense. Rename the S4 row title to «مصروف موقع» so it does not read as a status.
9. **One word, three acts: اعتماد.** Owner approves (S5), Technical Office certifies quantities (S11 «اعتماد الكميات»), Finance clears a settlement (S4 «اعتماد التسوية»). Keep «اعتماد» for the Owner; give certify and clear their own verbs.
10. **One idea, several words: the withholding deduction.** «الاستقطاعات» (S11 chain step), «استقطاع الخصم والإضافة» (S11 row), «الخصم والإضافة المستحق توريده» (S12 title), «المخصوم» (S12 column), «تطبيق الاستقطاعات» (button, per the notes). Also «الضمان» for subcontractor retention. Pick one noun per idea. Nabil to rule on «الضمان» against the client-side term.
11. **Accountant or developer talk aimed at site and Owner.** «العجز» (S7 hero; «الناقص» is plain), «مبلغ الضخ» and «ضخ» (pump word; the Owner says «توريد من المالك» or similar), «الأساس / النسبة» (S12), «سلفة على عميل» on S5 and S6 with no client name on either screen, and «تقرير الجودة» for a screen where the engineer enters money values. Check against the invented-Arabic list with Nabil.
12. **One idea, two words on one screen (S13).** Hero «تشوينات متبقية», first field «مواد مشوّنة بالموقع». If it is the same thing use one word; if the hero is an advance balance, call it «رصيد التشوينات» and keep the field as a material value.
13. **Sentences on screen that explain the screen (quoted).**
    - S7: «الخزنة لا تكفي لهذا الصرف» sits directly under the title «الخزنة لا تكفي». Same words twice. Delete the sentence; title plus the shortfall figure carry it.
    - S3 refused: the banner «مجموع البنود أكبر من المبلغ المستلم» and the red figure «غير المصروف -500» say the same. Keep one.
    - S7 refused: «لا تملك صلاحية تسجيل الضخ», a lock icon and a dashed empty-state box. Box reads as something to fill; a lock and one line is enough.
    - S1 and S6 sentences listed in the brief were not seen (see caveat).

## C. Noise (on screen because the data exists, not because the role needs it)

14. **S4 phone rows repeat «من», «المبلغ», «التاريخ» on every row.** The labels outweigh the data. Drop them on the phone; keep the column heads at 1440.
15. **S10 phone cards repeat «الأسبوع», «القيمة», «الدور على» on every card.** The week number «47» alone does not say a week of what; use the date range.
16. **S11 shows five money lines to the first role.** At the certify step the Technical Office needs the certified value; retention to date and a «—» net are noise. Show lines when they exist.
17. **S9, stray chevron and no warning.** The row chevron floats under the bar, detached from the title and figures. A project at 97% of its cap (680,000 of 700,000) looks the same as one at 64%; mark the one near the limit so the Owner does not do the sum.
18. **S2 and S5: «ج.م» on the first row only.** Name the currency once per list or on every amount.
19. **1440: content fills the right two-thirds and the left third is empty; S1, S10 and S11 put the primary action at the far left, about 900 px from the title.** Finance and the Owner on a laptop travel across the page to the action. Put the action beside the title or under the table.

## D. What works (keep)

- One filled button per screen at 390 and 1440, light and dark. The open-item pattern in S2 and S5 avoids a button per row.
- Money is a figure with the currency named once, each ledger alone.
- S3's phone order (received, lines, add line, left over, send) is the order a site person follows.
- S8 hero «مستحق للمالك», S10 «الدور على» and the S11 chain answer "what stage, who next" without a sentence.
- «جاري الاعتماد» and «بانتظار المالك» are new status words, not the approved five; Nabil to approve.

## Brief errors

- The brief gave no view names beyond the pattern; several guessed names fell back to the normal view without any sign.
- The brief did not say if dark mode is in scope for site screens. I judged it; nothing failed.

## Three changes first

1. **Wording pass on صرف / اعتماد / الاستقطاع (8, 9, 10).** Copy only, cheapest, and these words sit on the buttons that move money.
2. **S5 carries purpose and amount on every row (1).** The Owner approves real money; the mock asks him to decide on rows he cannot read. Layout only.
3. **Close the two dead ends: S7 refused (2) and the S3 hand-back (3).** Both leave a person unsure what to do next with money in hand or a payment stopped. These are flow; settle them with the Q7 and Q1 answers before the build.
