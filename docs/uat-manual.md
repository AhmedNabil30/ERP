# Kaff ERP — tester's manual

This is for the people testing Kaff ERP before it goes live. You do not need to know anything about programming to use it. Work through it in order, write down anything that looks wrong, and hand your notes back.

The screens are in Arabic and read right to left. There is an English button on the side menu if you want to see the English labels, but test in Arabic, because Arabic is what Kaff will use.

A note before you start. Some of the Arabic wording on screen was written by us as a guess, not by Kaff. If a word sounds wrong to you, it probably is wrong. Say so. Those corrections are the most useful thing you can give us.

## 1. Getting in

Open http://localhost:4200 in Chrome. You should see the sign-in screen.

There are seven people to sign in as. Each one sees a different system, because what you can see and do depends on your job.

| Who | User name | Password |
|---|---|---|
| Owner, ناصر الشريف | `owner_demo` | `Demo#Owner1` |
| Site engineer, junior | `site_eng_demo` | `Demo#Site1New` |
| Site supervisor | `site_sup_demo` | `Demo#Sup1New` |
| Technical office | `tech_office_demo` | `Demo#Tech1New` |
| Technical office, second person | `tech_office2_demo` | `Demo#Tech2New` |
| Accounts | `accounts_demo` | `Demo#Acct1New` |
| Client, portal (a phone number is the user name) | `01012340010` | `Demo#Portal2`, a temporary password: the first sign-in asks for a new one |

If the client sign-in is missing, the person running the system creates it with `node scripts/seed-demo-portal.mjs` (local API only).

To change person, open the side menu and press تسجيل الخروج, then sign in as the next one. On a phone the side menu opens from the القائمة button at the bottom.

There is one project in the system already: فيلا 47 — التجمع الخامس, a lump sum contract worth 4,672,694.01. Most of the tasks below use it.

The second technical office person exists because a change order's technical review must be decided by someone other than the technical office person who drafted it.

Three more projects hold extracts at every step you may want to see, so you do not have to build each step by hand:

| Project | What is waiting there |
|---|---|
| فيلا 12 — الشيخ زايد | Extract 1 issued, with one cheque received and one deposited. Extract 2 partly approved, with its تقرير عدم المطابقة open. |
| عمارة 5 — المعادي | Extract 1 disputed by the client. Extract 2 waiting for the technical office. One change order waiting for the client, one approved. |
| شقة 9 — مدينة نصر | Extract 1 issued, with a credit note waiting for the owner. Extract 2 a draft, waiting for its QC report. |

If these three are missing, the person running the system creates them. With the API running on port 5080, run the other `scripts/seed-demo-*.mjs` scripts first, then:

```
node scripts/seed-demo-extract-states.mjs
```

It talks to the local API only and refuses any other address. It is safe to run again: it finds the projects by name and only takes the steps that are not done yet.

## 2. What to test, in order

### Task 1. Start a new project and build its quantities

Sign in as the Owner.

On the project list, press + مشروع جديد. Give the project a name, choose the client, choose the contract type.

You will notice there is no field for the contract value. That is on purpose. The value comes from the quantities you are about to enter, and nobody types it in. A note on the form explains this.

Press save and you land on the quantities page (المقايسة). Add some lines from the catalogue. Change a quantity. Remove a line. Watch the total at the top change as you work.

When you are happy, press توقيع التعاقد. The first press does not sign anything. It asks you first, shows you the figure that is about to become the contract value, and warns you that the lines freeze and this cannot be undone. Read that message carefully, because it is true. This is the most final button in the whole system.

After you sign, try to add a line. Try to edit one. You will find there is nothing to press. The buttons are gone, not greyed out. The database itself will refuse to change a signed line, so even if a button appeared it would not work.

Things to check:
- The total before signing is called إجمالي المقايسة, and after signing it is called قيمة التعاقد. It should never call itself the contract value before you have signed.
- The total should be the sum of your lines. Add them up on a calculator and compare.
- Cost Plus and Design projects show an empty page here instead of a quantities builder. For Design that is correct. For Cost Plus we are not sure yet and we need Kaff to tell us.

### Task 2. Read the project page and tell us if it answers you

Sign in as the Owner and open فيلا 47 from the list. Click anywhere on the row.

This page is divided into sections, and each section asks a question at the top. Your job is to decide whether the section answers its own question. That was the main complaint about the old version: it asked good questions and then showed numbers that did not answer them.

What you should see:
- الفهم asks how much has been done. There is a progress bar, the value of work done against the contract value, and a line for each باب with its own percentage. The باب being worked on now has a badge.
- التنفيذ asks what is happening on site. It shows the date of the last daily log and which باب is running.
- المال asks what is owed. It shows the work approved, what has been billed, what has been collected, and what is still outstanding.
- التنظيم asks who is working on it. It shows people's names.

Under the project name there is a strip saying what the project is waiting for and who it is waiting on. Today it should say extract 3 is waiting for the quality report from the site supervisor.

Now sign in as the site supervisor and open the same project. He is the person that strip names. He should see a badge saying الدور عليك الآن, a button that takes him straight to the extract, and a link to the extract list. Tell us if he does not.

Three things to look at closely:
- The progress bar must fill from the right, not the left, because the screen is Arabic.
- The junior site engineer must not see money anywhere. Sign in as him and look at the project list and the project page. If you see a single figure in pounds, that is a fault, and an important one.
- A Cost Plus project should show no progress bar at all. It shows cost and supervision figures instead.

### Task 3. The daily log, on a phone

Sign in as the junior site engineer and use a phone, or make the browser window narrow, about 390 pixels. This screen is built for the site, so a phone is the real test.

Open the project, then اليومية. There are four tabs: حضور وملاحظات, عمالة, خامات, الكميات.

Open الكميات. Pick a line from the drop-down, then type how much was done in this period. Read the hint under the box. It says you enter only what was done this period, and it gets added to what was recorded before. It does not replace it. This matters: if you enter a total instead of the day's work, the client gets billed twice for the same work.

You can press حفظ if you want to test saving. Be aware it adds to the project's work done and will change the next extract.

While you are here, tell us honestly what you think of this screen. We already know it needs work. Your notes on it are wanted.

### Task 4. Take an extract through all five people

This is the heart of the system, so take your time.

An extract is a claim for work done in a period. It has to pass through five people in order, and no person can do two steps on the same extract. That rule is the whole point, so test it.

Extract 3 is sitting and waiting. Open it from the project's المستخلصات list. Each person sees a box called ما يمكنك فعله which holds only the buttons that person is allowed to press.

Go in this order:

| Step | Sign in as | Press |
|---|---|---|
| 1 | Site supervisor | تقديم تقرير الجودة |
| 2 | Technical office | اعتماد للصرف |
| 3 | Accounts | تطبيق الاستقطاعات, then confirm |
| 4 | Owner | اعتماد المالك, then confirm |
| 5 | Owner | إصدار المستخلص, then confirm |

After each step, look at مسار المستخلص at the bottom. It records who did what and when. Nothing can be removed from it.

Things to check:
- Before the accounts step, nobody sees any figures. The page says the numbers appear after accounts apply the deductions. Check that no figure leaks out before then.
- The junior site engineer should never see a button on this extract, at any step.
- After the supervisor has acted, he should see a message telling him someone else decides the next step, and no button.
- Each of the three رفض buttons sends the extract back with a reason. You can test one, but it will send the extract back to the start of its cycle, so do it last or on a different extract.

When the extract is issued, read مستند المستخلص. There is a block called العمود المطبوع that adds the figures up in front of you and prints a line saying the column matches the total. Check that arithmetic by hand. For extract 2 it reads 109,503.00 minus 21,900.60 equals 87,602.40.

### Task 5. The numbers page

Sign in as the Owner, open the project, and in المال press إثبات الأرقام.

This page puts the actual accounting entries next to what the contract rules say they should be, line by line. It changes nothing. It only reports.

The five checks at the top stay unjudged until handover, because they are handover checks. That is correct and not a fault.

Look at the five ledgers near the bottom. There is no grand total, and the page says so in words. It means it. Those five are never added together, and one is never offset against another.

This page is one we expect you to push back on. It shows a lot of internal working out. Tell us what you would not want a client or an owner to see.

### Task 6. Ending a project

Sign in as the Owner and open الإقفال from the project page.

One screen holds the end of a project's life: the stage it is at, the hold, the snag list, warranty callbacks, and termination.

The header offers one action at a time, following the stage. Declare practical completion, then hand over, then begin warranty, then close. You never get a menu of four things to choose from.

Try these on purpose, because they are meant to be refused:
- Raise a major snag, then try to hand over. It should refuse and name the snag's number, not just say no.
- Try to close the project before it is settled. It should refuse and list what is unmet.
- Sign in as Accounts at the تم التسليم stage. The hold panel appears, with the amount and a note saying it was worked out from a number of entries, not typed. There is no field to type an amount and there is no way to change it. Release it. Then try to release it again. The second attempt must be refused.
- Press the termination control. It will tell you the settlement rules are not decided yet and nothing has been recorded. That is the honest answer and not a fault. Kaff has not told us what a termination settles.

One question for Kaff here. The technical office can currently declare practical completion as well as the Owner. We were told the Owner declares. Tell us which is right.

## 3. What is not built yet

Please do not report these. We know. They are listed so you do not waste time looking for them.

There is no screen to record a collection. This is the big one. An extract can be approved and issued, but there is no way to record that the client paid. On extract 2 the client owes 87,602.40 and no screen will tell you that. The system behind it works and is tested; it just has no page yet.

There is also no screen to create a new extract, to raise or approve a change order, to raise a dispute, to release a subcontractor's retention, or to move a cheque through its stages. There is no invoice screen.

On the project page these are still missing: the list of daily logs, open NCRs, subcontractors, the five ledgers side by side, the team roster, linked projects, and the control to stop a project.

Cost Plus and Design projects have never been opened in a browser by anyone. The test system has one lump sum project. If you want to test those two, say so and we will set them up.

## 4. What we most want from you

Four things, in order of how much they help us.

First, wording. About ninety Arabic phrases on the new screens were written by us and have never been checked by anyone at Kaff. The money labels matter most: المحصَّل for what has been collected, المطالَب به for what has been billed, المعتمد من الأعمال for work approved, and المتبقي على العميل for what is outstanding. Also these five state words, which we invented: متوقف, في انتظار التسليم, تم التسليم, في فترة الضمان, تم فسخ التعاقد. If a contractor would not say it that way, tell us what he would say.

Second, anything you could not work out without asking someone. If you had to ask what a button does, or where to go next, write down exactly where you were. A screen that needs explaining is a screen we got wrong.

Third, anything on screen that you would not show to the person using it. There is a lot of internal detail on some pages that is there because the system stores it, not because anyone needs it.

Fourth, any figure that looks wrong. Check the arithmetic by hand where you can. Money errors are the ones that matter most and the ones hardest to spot later.

## 5. Reporting a problem

For each problem, write down:

- Which person you were signed in as
- Which screen you were on, and the address in the browser bar
- What you did
- What happened
- What you expected instead

A screenshot helps. If it involves money, include the figures you saw and the figures you expected.

One last thing. If something is refused and the message explains why, that is usually the system working rather than failing. The rules behind those refusals are the reason Kaff asked for this system at all. You cannot approve your own work. You cannot change a signed quantity. You cannot release the hold twice, and you cannot collect money on an extract that has not been issued. What is worth reporting is a refusal with no explanation, or one that explains itself badly.
