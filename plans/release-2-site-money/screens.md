# Release 2, site money: screen briefs

Date: 2026-11-22. Phone-first for site roles. Codes and ids stay off every screen, and a screen carries no
explanatory text unless this file lists the sentence (Nabil, 2026-11-10; `memory: screens explain themselves`).
Each brief names the journey flow and step it rests on. Briefs that rest on an unanswered question say so first.

Words beyond labels: none, unless listed.

---

## S1. عهدة request (new)

- **Route:** NEW, under `projects/:projectId`.
- **Rests on** Q6 (cap, ceiling), Q1. Flow 1 steps 1-4.
- **Who:** Junior site engineer. Phone.
- **Purpose:** raise one request for money to spend on site.
- **Data shown:** amount, purpose, and the project. Nothing about the safe, balance or ceiling.
- **Actions:** one primary, "حفظ كمسودة". After it, the draft shows in the engineer's list with state «مسودة».
- **States:**
  - Empty: his list has no drafts, one button to raise one.
  - Loading: skeleton of the list.
  - Failed: a retry offer; the typed text stays.
  - Refused (cap, or an earlier عهدة not cleared): a short message that names the reason and offers the earlier عهدة
    to settle. The ceiling figure is not shown to him. [spec §9 line 378; spec AC-600-J says the engineer sees no
    cost, balance or amount of the project's money; see Q9]
- **Words beyond labels:** the refusal sentence, because the reason is the only way he learns why.
- **Next:** the supervisor. The supervisor sees a count on his project screen. [QUESTION: who tells him; Flow 1 step 5]

## S2. عهدة drafts to submit (new)

- **Route:** NEW, under `projects/:projectId`.
- **Rests on** Flow 1 step 5. [spec §9 line 378]
- **Who:** Supervising site engineer. Phone.
- **Purpose:** submit a junior's draft, or send it back.
- **Data shown:** junior's name, amount, purpose, date.
- **Actions:** primary "إرسال". Secondary "إرجاع" opens a reason field; the reason is required.
- **States:** empty (no drafts waiting), loading, failed with retry, refused (supervisor is not assigned: the
  screen does not open).
- **Next:** Finance (Flow 1 step 6). Finance finds out from its inbox (S4).

## S3. Settle a عهدة (new)

- **Route:** NEW, under `projects/:projectId`.
- **Rests on** Flow 1 steps 9-11. Q1 (whether settlement is the expense record) and Q6.
- **Who:** the holder of the عهدة, supervisor or junior (Q6). Phone.
- **Purpose:** show what the عهدة was spent on and give back what is left.
- **Data shown:** amount received; a list of spend lines, each with an amount and a receipt photo; amount unspent.
- **Actions:** primary "إرسال للمحاسبة".
- **States:** empty (no spend lines yet, prompts the first); loading; failed (photos stay on the phone until upload
  succeeds; money is not moved offline per [spec §8 line 359]); refused (spend lines exceed the amount received,
  because the account cannot go negative [spec §6.4 amendment lines 255-256]).
- **Words beyond labels:** the refused sentence.
- **Next:** Finance (S4). A return with a reason shows on top of this screen when Finance rejects it.

## S4. Money waiting on Finance (new)

- **Route:** NEW, `projects/:projectId/money` (or company level; see Q3).
- **Rests on** Flow 1 steps 6 and 10, Flow 5 step 3. Rests on Q3 for what appears before and after Owner approval.
- **Who:** Finance. Desktop first, readable on a phone.
- **Purpose:** pay, clear, confirm or reject what site has asked for.
- **Data shown:** per row, what it is (عهدة request, عهدة settlement, expense draft), who raised it, amount, date.
  A row opens a detail with the receipts or the draft's description.
- **Actions:** primary is per row type: "صرف" for a request, "اعتماد التسوية" for a settlement, "تأكيد" for an
  expense draft. "رفض" with a required reason on all.
- **States:** empty; loading; failed; refused: when the safe cannot cover a payment, the row stays and the overdraw
  sheet (S7) opens.
- **Next:** the holder or engineer sees the result; a rejection returns to them with its reason.
- **Note:** Flow 5 (expense drafts) and Flow 1 share this list. If Karim answers Q1 with "separate", split into two
  lists.

## S5. Money waiting on the Owner (new)

- **Route:** NEW, company level.
- **Rests on** Q3, which decides every row type that appears here.
- **Who:** Owner. Phone and desktop.
- **Purpose:** approve or reject every money act waiting for him.
- **Data shown:** per row: what it is, project, amount, who prepared it. The extract chain already has a waiting
  view (`ExtractWaiting`, `Api/Features/Extracts/ExtractWaitingOnView.cs`); this adds the other row types.
- **Actions:** primary "اعتماد". Secondary "رفض" with a required reason. The Owner cannot approve what he prepared.
  [spec §9 line 376]
- **States:** empty ("لا شيء ينتظرك"); loading; failed; refused (the Owner prepared this act).
- **Next:** Finance disburses. Finance sees the item move to the "approved" state in S4.

## S6. Firm advance (new)

- **Route:** NEW, under `projects/:projectId`.
- **Rests on** Q5. Flow 2.
- **Who:** Finance prepares, Owner approves. Desktop.
- **Purpose:** prepare a firm advance for a project and approve it under the cap.
- **Data shown (in order):** project; amount asked; what it pays for; the cap and what is already out against it
  (only if Q5 says the cap is per project); the effect on total exposure.
- **Actions:** Finance: "إرسال للمالك". Owner: "اعتماد". A breach of the cap disables the Owner's action. Per
  [D-229 item 3] the button is disabled only for what the user cannot fix on this screen, so a breach shows
  the figure and the limit.
- **States:** empty; loading; failed; refused (over cap, with the figures).
- **Words beyond labels:** the over-cap sentence.
- **Next:** on approval, Finance sees "approved" and posts the advance. A short safe opens S7.

## S7. Safe is short (sheet, new, shared)

- **Route:** NEW. A sheet over any payment screen: S4, S6, S10, S11.
- **Rests on** Flow 4 steps 1-6. [spec §6.1 line 218]
- **Who:** Finance, and the Owner when he pays.
- **Purpose:** turn a refused payment into an owner injection.
- **Data shown:** shortfall amount; the owner injection button.
- **Actions:** primary "تسجيل ضخ من المالك" for at least the shortfall (the figure is [PROPOSAL]; the spec says
  "prompts" only).
- **States:** failed (injection refused, reason shown); refused (this user may not record an injection, Q7: the
  sheet then names who may).
- **Words beyond labels:** one sentence saying the safe cannot cover the payment. The layout alone cannot say why
  a payment did not go through.
- **Next:** Finance retries the payment. The refused payment is not kept (Flow 4 step 6; Q7).

## S8. Owner current account (new)

- **Route:** NEW, company level.
- **Rests on** Q7. Flow 4.
- **Who:** Owner and Finance. Desktop.
- **Purpose:** record an injection, a withdrawal or a repayment, and see what Kaff owes the Owner.
- **Data shown:** what Kaff owes the Owner now; movement list (date, kind, amount); for a withdrawal, whether it is
  a returnable advance or a final drawing.
- **Actions:** primary "تسجيل حركة" with kind chosen first.
- **States:** empty (no movements); loading; failed; refused (the preparer is the approver, Q7).
- **Next:** the other of Owner and Finance approves (Q7).

## S9. Owner's firm-advance exposure (new, read)

- **Route:** NEW, Owner dashboard tile (no dashboard route exists).
- **Rests on** Flow 2 step 5. [spec §6.4 line 249]
- **Who:** Owner. Phone.
- **Purpose:** show how much Kaff has out for clients across all projects.
- **Data shown:** total out; per project, amount out and the cap.
- **Actions:** none. A row opens S6.
- **States:** empty ("لا سلف"); loading; failed.
- **Next:** none.

## S10. Subcontractor extracts of a project (new)

- **Route:** NEW, `projects/:projectId/subcontractor-extracts`.
- **Rests on** Q2. Flow 6.
- **Who:** supervising engineer, Technical Office, Finance, Owner. Desktop first; the supervisor may hold a phone.
- **Purpose:** list each subcontractor's weekly extracts and say who the next step waits on.
- **Data shown:** subcontractor, week, certified value, state, whose turn it is.
- **Actions:** primary "مستخلص جديد" for the role that prepares (Q2).
- **States:** empty; loading; failed; refused (not assigned).
- **Next:** the roles of S11.

## S11. Subcontractor extract (new)

- **Route:** NEW, `projects/:projectId/subcontractor-extracts/:id`. Follows `extract-detail`.
- **Rests on** Q2, Q3, Q4. Flow 6 steps 2-9, Flow 7.
- **Who:** the role holding the next step. One primary per role, as the client extract detail does.
- **Purpose:** carry one subcontractor extract through certify, gate, deductions, Owner approval and payment.
- **Data shown, in order:** certified value this week; retention this week (5%) and retention to date; withholding;
  net payable; whose step it is.
- **Actions:** per role: "اعتماد الكميات" (Technical Office), "تطبيق الاستقطاعات" (Finance), "اعتماد" (Owner),
  "صرف" (Finance). "رفض" with a required reason returns it to Draft.
- **States:** empty draft with no lines; loading; failed; refused (own creation, Q3; project not assigned).
  A stopped project still pays, but does not issue client extracts. [spec §13 amendment]
- **Next:** on Owner approval, Finance (pay). On rejection, the preparer.

## S12. Withholding carried (new, read)

- **Route:** NEW, company level.
- **Rests on** Q4. Flow 7.
- **Who:** Finance. Desktop.
- **Purpose:** show how much withholding Kaff has deducted and still owes to remit.
- **Data shown:** per month and payee, base, rate, amount withheld; total owed.
- **Actions:** none until Q4 says whether remittance is built.
- **States:** empty; loading; failed.
- **Next:** none.

## S13. Extract detail, تشوينات balance (existing, changed)

- **Route:** existing `projects/:projectId/extracts/:extractId`, QC step.
- **Rests on** Flow 3. Q8, Q58 (assumed).
- **Who:** supervising engineer. Phone.
- **Purpose:** when he enters the two material values, he sees how much تشوينات is still outstanding.
- **Data shown:** outstanding تشوينات; the two values he enters.
- **Actions:** unchanged (submit the report).
- **States:** unchanged. A recovery that would pass the outstanding amount shows the capped figure.
- **Next:** unchanged.
- **Open:** whether this supervisor may see an outstanding money figure. [D-218 item 6: SupervisorSeesProjectMoney,
  Nabil ruled, Karim has not confirmed.]
