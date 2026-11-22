# Release 4 screen briefs

All screens are new. Arabic, RTL, desktop, for Finance and the Owner, unless a brief says otherwise. Codes and
ids stay off every screen. No explanatory sentences unless a brief lists one. Money shows two decimals,
stored at four (spec §6.1 amendment, line 220). Step numbers refer to `journey.md`. A brief whose first line
says "Rests on ASSUMED" waits on the named question.

Common empty, loading and failed states unless overridden: empty says what is missing and offers the one
action that fills it; loading shows the table frame; failed says it did not load and offers retry; refused
(no assignment or role) shows the standard forbidden page.

## 1. Opening balances
- Route: `NEW /accounting/opening`. Who: Owner, Finance. Desktop.
- Rests on ASSUMED fiscal year start and opening counter-account (Q2, A2).
- Job: enter the go-live balances once.
- Primary action: save opening balances. After saving, the balance sheet opens with the same date.
- Must see: the list of balance lines with amounts; running total assets; running total liabilities plus
  equity; the difference (must be zero); the go-live date.
- Off screen: account codes, posting types.
- Words beyond labels: none.
- States: empty is the line list with all lines at zero. Refused: save disabled while difference is not zero,
  and the difference shows in red. Failed: nothing saved, entries kept.
- Next: Finance continues to the asset register. The Owner sees the balance sheet.
- Sources: steps 1-3, spec §6.6 line 278, §16 #17.

## 2. Asset register
- Route: `NEW /accounting/assets`. Who: Finance reads and edits, Owner reads. Desktop.
- Job: list every asset with cost, rate and book value.
- Primary action: add asset (opens screen 3).
- Must see: name, category, cost, accumulated depreciation, book value, monthly charge.
- Off screen: ids, posting detail, other roles' data.
- Words beyond labels: none.
- States: empty offers add asset. Fully depreciated rows are marked and stay listed.
- Next: nothing waits on anyone.
- Sources: step 8, spec §6.6 line 274.

## 3. Add or edit asset
- Route: `NEW /accounting/assets/new` and `/accounting/assets/:assetId`. Who: Finance. Desktop.
- Rests on ASSUMED method, start month, salvage and disposal (Q7).
- Job: record one asset and its depreciation rate.
- Primary action: save. After saving, the register shows the row with the first monthly charge.
- Must see: name, category, cost, purchase date, rate (prefilled from category, editable).
- Off screen: the law article, account names.
- Words beyond labels: none.
- States: a rate change shows only the next charge, since past months are fixed. Refused when the purchase
  date is in a closed month.
- Next: depreciation appears in the next close checklist.
- Sources: step 8, spec §6.6 line 274.

## 4. Accruals and prepayments
- Route: `NEW /accounting/accruals`. Who: Finance. Desktop.
- Rests on PROPOSAL for months and linking (step 9-10).
- Job: record and track costs incurred but unpaid, and costs paid for later months.
- Primary action: add. After saving, the new row shows in the open list.
- Must see: kind (accrual or prepayment), description, amount, tag (company or project name), months left
  or whether still open.
- Off screen: posting types and accounts.
- Words beyond labels: none.
- States: empty offers add. Refused: a missing tag blocks save.
- Next: amortisation and releases appear in the close checklist.
- Sources: steps 9, 10, 16; spec §6.2 line 233, §6.6 line 275, §6.10 line 323.

## 5. Record a company expense
- Route: `NEW /accounting/expenses/new`. Who: Finance. Desktop.
- Job: record one expense with its category and its tag.
- Primary action: pay. After saving, the expense shows in the list with the safe or bank balance reduced.
  An insufficient safe prompts an owner injection (existing rule).
- Must see: category (six), amount, date, paid from (safe or bank), tag (company or a project).
- Off screen: account types.
- Words beyond labels: none.
- States: refused when the tag is empty or both. Refused when the safe would go negative, with the owner
  injection prompt.
- Next: nothing waits. A wrong tag is corrected by a reversal the Finance user raises from the list.
- Sources: steps 5, 6; spec §6.10 line 323, §6.1 line 218. Category storage is a PROPOSAL (a field on the
  source document, since the posting shape adds nothing).

## 6. Month-end close checklist
- Route: `NEW /accounting/close`. Who: Finance. Desktop.
- Job: take one month from open to closed.
- Primary action: close month. After closing, the page shows the month as closed and the next month as open.
- Must see: the month; each step with a done mark (depreciation, prepayments and accruals, revenue
  recognition, trial balance balances); anything blocking, by name; the number of open items.
- Off screen: ids, posting types.
- Words beyond labels: none. The button is disabled while a step is open; the open step is the explanation.
- States: all steps open shows them in order. Failed: names the step that refused. Refused: only Finance.
- Next: if Q3 says Owner approves, the Owner is told on his dashboard and the month shows "waiting for
  owner". If not, nobody waits.
- Sources: steps 14-21; spec §6.6 line 276; Q3.

## 7. Revenue recognition review
- Route: `NEW /accounting/close/revenue`. Who: Finance, Owner reads. Desktop.
- Rests on ASSUMED treatment of the difference (Q1) and which contract types (Q12).
- Job: show executed against billed for every project at month end.
- Primary action: post the adjustment. After posting, each row shows its contract asset or liability.
- Must see: project, executed value, billed value, difference with direction (asset or liability), change
  since last month.
- Off screen: quantities detail, catalogue costs, margins.
- Words beyond labels: none.
- States: empty when no active project. A project with no progress reading shows a dash and is skipped.
- Next: the close checklist step ticks.
- Sources: steps 17-18; spec §6.6 line 281, §16 #14.

## 8. Trial balance
- Route: `NEW /accounting/trial-balance`. Who: Finance, Owner. Desktop.
- Job: show every account's balance and prove debits equal credits.
- Primary action: export. After pressing, a file downloads.
- Must see: account name, debit column, credit column, totals row, date range.
- Off screen: ids, posting types.
- Words beyond labels: none.
- States: out-of-balance shows the difference in red and the export is still allowed.
- Next: nothing waits.
- Sources: steps 19, 23; spec §6.6 line 277. Format Q10.

## 9. Balance sheet
- Route: `NEW /accounting/balance-sheet`. Who: Finance, Owner. Desktop.
- Rests on PROPOSAL: the spec names a balance sheet only as the aim, not as a screen.
- Job: show assets, liabilities and equity at a month end and prove they balance.
- Primary action: change month. After picking, figures reload.
- Must see: assets total, liabilities total, equity total, difference (zero), and the five ledgers each on
  its own line, not netted.
- Off screen: ids, posting types.
- Words beyond labels: none.
- States: out-of-balance shows the difference in red.
- Next: nothing waits.
- Sources: step 24; backlog Slice 7 gate; spec §6.4 line 246.

## 10. Year close
- Route: `NEW /accounting/year-close`. Who: Finance prepares; Owner confirms if Q3 says so. Desktop.
- Rests on ASSUMED (Q4, A1).
- Job: roll the year's profit into retained earnings.
- Primary action: close year. After closing, retained earnings shows the new figure and current-year profit
  shows zero.
- Must see: year, profit for the year, retained earnings before, retained earnings after, months all closed
  (yes or the open ones named).
- Off screen: transfer details.
- Words beyond labels: none.
- States: refused while any month of the year is open, naming it.
- Next: the Owner, if he must confirm.
- Sources: steps 25-26; spec §6.6 line 278.

## 11. Overhead spread report
- Route: `NEW /accounting/overhead`. Who: Finance, Owner. Desktop.
- Rests on ASSUMED basis of size (Q9).
- Job: show each project's share of company overhead for a month.
- Primary action: change month.
- Must see: project, size basis, share of overhead, overhead amount, project cost plus share.
- Off screen: any suggestion this is posted. It is a report.
- Words beyond labels: one line, "report only, not in the books". Reason: the number sits next to real
  project cost and a reader may take it for a posting.
- States: empty when no overhead exists.
- Next: nothing waits.
- Sources: step 22; spec §6.10 line 325, §16 #8.

## 12. Budgets overview
- Route: `NEW /accounting/budgets`. Who: Owner, Finance. Owner also on phone.
- Rests on ASSUMED "committed" (Q6).
- Job: show which projects are near or over budget.
- Primary action: open a project (screen 13).
- Must see: project, baseline, committed, paid, status (within, near, over).
- Off screen: line detail, catalogue cost.
- Words beyond labels: none.
- States: empty when no signed BOQ exists.
- Next: the Owner reads the alert; no one else waits.
- Sources: steps 12-13; spec §6.8 line 317.

## 13. Project budget
- Route: `NEW projects/:projectId/budget` (tab in the project workspace). Who: Owner sets tolerance; Finance
  reads. Desktop.
- Job: show one project's baseline, committed and paid, and set the tolerance.
- Primary action: save tolerance (Owner only).
- Must see: baseline (signed BOQ cost), tolerance, limit, committed, paid.
- Off screen: how the baseline is built; nothing can edit it here.
- Words beyond labels: none.
- States: refused for anyone but the Owner on save. The baseline row has no edit control, and a change order
  raising it shows in the history.
- Next: an alert shows on the budgets overview when over.
- Sources: steps 4, 12, 13; spec §6.8 line 317.

## 14. Loans (only if Q8 answers yes)
- Route: `NEW /accounting/loans`. Who: Finance. Desktop.
- Job: record a loan and each instalment.
- Primary action: add instalment. After saving, remaining principal drops and interest shows as expense.
- Must see: lender, remaining principal, next instalment, principal and interest of each paid.
- Off screen: account types.
- Words beyond labels: none.
- States: empty offers add loan. Not built if Q8 is no.
- Next: nothing waits.
- Sources: step 11; spec §6.6 line 279, §16 #16.
