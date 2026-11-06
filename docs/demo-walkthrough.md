# Kaff ERP — walk it yourself

For Nabil. Sit down, follow the steps, compare what you see with what is written here. Every step in the main flow was walked by me in a real browser (headless Chromium, driven by script) on 2026-11-02, signed in as the user named in the step. What I could not walk is in "Not ready" at the foot.

The screen was in dark mode when I walked it. The "الوضع" button on the side navigation switches it; the content is the same.

## 1. Bring the stack up

Three things must be running: the database, the API, the web app. If you already see the two URLs below answer, skip this.

1. Database (Docker Desktop must be running): `docker compose up -d db`. If the container already exists, `docker start kaff-db` is gentler on the API's open connections.
2. API: `dotnet run --project src/Api/Kaff.Api.csproj` from the repo root. It must listen on port 5080; the web app's proxy points there and nowhere else. In a PowerShell window set `$env:ASPNETCORE_ENVIRONMENT='Development'` and `$env:ASPNETCORE_URLS='http://localhost:5080'` first. In Development it applies migrations and installs the database guards on start.
3. Web: `cd src/Web`, then `npm ci` (first time only), then `npm start`.

Confirm it is up:

- http://localhost:5080/api/health must answer 200 (the API root `/` answers 404; that is normal).
- http://localhost:4200 must answer 200 and show the sign-in screen.

The demo data comes from seed scripts in `scripts/`. They go through the real API and are safe to run twice (a second run prints "0 created, N existed"). In order: `scripts/seed-demo.ps1`, `node scripts/seed-demo-rich.mjs`, `node scripts/seed-demo-boq.mjs`, `node scripts/seed-demo-engineer.mjs`, `node scripts/seed-demo-extract-roles.mjs`, `node scripts/seed-demo-extract-pending.mjs`. On this machine all of them have already been run; you do not need to run anything to follow this walkthrough.

## 2. The five people

| Person | User name | Password | After sign-in you land on | Side navigation |
|---|---|---|---|---|
| Owner (ناصر الشريف) | `owner_demo` | `Demo#Owner1` | المشاريع (the project list) | المشاريع، الكتالوج، أبواب، الموظفون، مقاولو الباطن، الموردون، العملاء، then under الإدارة: المستخدمون، الأقسام، سجل التدقيق |
| Junior site engineer (مهندس الموقع التجريبي) | `site_eng_demo` | `Demo#Site1New` | المشاريع | المشاريع only |
| Supervisor (مشرف الموقع التجريبي, role shown as مهندس الموقع) | `site_sup_demo` | `Demo#Sup1New` | المشاريع | المشاريع only |
| Technical office (المكتب الفني التجريبي) | `tech_office_demo` | `Demo#Tech1New` | الكتالوج (not the project list) | المشاريع، الكتالوج، أبواب، مقاولو الباطن |
| Accounts (حسابات تجريبي, role المالية) | `accounts_demo` | `Demo#Acct1New` | المشاريع | المشاريع، الموردون |

To switch person: open the side navigation, click تسجيل الخروج, sign in as the next one. On a phone the side navigation opens from the "القائمة" button at the bottom.

## 3. Journey

### Step 1. Project list, as Owner

Sign in as `owner_demo`. You land on المشاريع. The heading now reads "1 مشروعاً", not 2: the leftover test project was deleted on 2026-11-04 (see "What I found").

Row one: فيلا 47 — التجمع الخامس, code PRJ-377EC1A2, client شركة النيل للتطوير العقاري, type مقاولة بالمقطوعية, status جاري العمل, contract value 4,672,694.01. The row has no links inside it: the whole row is one link that opens the project (next step).

### Step 2. The project workspace, as Owner

Click row one anywhere. The page opens at `/projects/01a0de49-4a8c-7707-b19a-0f7592bbfdb0` (before this change a click went to day labour). At the top: the back link المشاريع, the title فيلا 47 — التجمع الخامس, the state جاري العمل, and one line with the code PRJ-377EC1A2, the client and the badge مقاولة بالمقطوعية. There is no متعثرة or تم تأجيلها tag: those two have no data yet.

Under the header a line of five links sticks to the top as you scroll: الفهم، الكميات، التنفيذ، المال، التنظيم. Each opens its section. On a wide window the group name and its question sit in a column beside the content; on a phone they sit above it. What each section holds:

- الفهم: قيمة التعاقد 4,672,694.01 with the chip منقول, نسبة الحجز 20%, نسبة استرداد الدفعة المقدمة 25%.
- الكميات: عدد البنود 15, إجمالي المقايسة 4,672,694.01, the badge مجمّدة في 01/11/2026, and a link المقايسة.
- التنفيذ: a link عمالة اليومية (the day-labour screen). The Owner has no اليومية link because the Owner does not write the daily log.
- المال: عدد المستخلصات and آخر مستخلص, and links المستخلصات and إثبات الأرقام.
- التنظيم: فريق العمل with the number of people assigned. The page prints the number the server sends (4 for this project). The design shows 6; I did not copy the design's number.

The Arabic names of the five groups, their questions, the labels above and the words for the five states that have no ruled word are my working text. They all live under `projects.workspace.vocab.*` in `ar.json`, one line each, so a correction is one line. Setup, Active and Closed use لم تبدأ، جاري العمل، انتهت.

The section الكميات links to the BOQ. Click المقايسة. The page opens at `/projects/01a0de49-4a8c-7707-b19a-0f7592bbfdb0/boq`.

You see:

- A box "قيمة التعاقد" with 4,672,694.01 and a badge "مجمّدة في 01/11/2026".
- Fifteen lines, each with item name, code, الكمية, سعر البيع and الإجمالي. The first is مباني طوب أسمنتي مفرغ 20 سم (MAS-3004), quantity 1,146 م٢, price 410.55, total 470,490.30. The last is نقطة بريزة عادية (ELE-3002), 94 عدد, 561.60, total 52,790.40.
- Nothing you can type into. I counted: the page holds zero input fields, zero text boxes, no edit button.

### Step 3. Daily log on a phone, as Junior site engineer

Sign out, sign in as `site_eng_demo`. Make the browser window 390 pixels wide (or open it on a phone). You land on المشاريع and see one project only. Click its row. The workspace shows only two sections for him, التنفيذ and التنظيم: no money, no المال section, no الفهم or الكميات section. The primary button سجل يومي جديد is in the page header (on a phone the shell puts it at the top, not in the bottom bar). Under التنفيذ there are two links: اليومية and عمالة اليومية.

Click اليومية. The page title is اليومية with the project name under it, and the line "لم يُسجَّل شيء في يومية اليوم بعد." Under "إضافة إلى اليومية" there are four tabs: حضور وملاحظات، عمالة، خامات، الكميات. Click الكميات. You see a drop-down "بند المقايسة" listing the 15 BOQ lines ("1. MAS-3004 · …" to "15. ELE-3002 · …"), a box "المنفذ في هذه الفترة", and the hint that you enter only what was done this period and it is added to what was recorded, not replacing it. Then a حفظ button.

What I measured at 390 px wide (page width 390, visible width 390, so no sideways scrolling; no element's right edge goes past 391):

| Element | Left to right (px) | Top (px) | Height (px) |
|---|---|---|---|
| English button | 16 to 100 | 6 | 44 |
| Tab حضور وملاحظات | 281 to 357 | 287 | 50 |
| Tab عمالة | 203 to 281 | 287 | 50 |
| Tab خامات | 126 to 203 | 287 | 50 |
| Tab الكميات | 48 to 126 | 287 | 50 |
| بند المقايسة drop-down | 47 to 358 | 378 | 48 |
| المنفذ في هذه الفترة box | 47 to 358 | 501 | 48 |
| حفظ | 275 to 358 | 691 | 48 |
| القائمة | 258 to 374 | 774 | 60 |

I did not press حفظ. Saving a quantity adds to the project's executed work and would change the next extract.

### Step 4. The extract list, as Junior site engineer

On a wider window (anything over 390 px), open اليومية as in step 3, then click the link المستخلصات in the strip of links at the top of that page. (The workspace hides it from him, because extracts are money.) The list shows:

- مستخلص رقم 1, من 01/10/2026 إلى 31/10/2026, صادر.
- مستخلص رقم 2, من 01/11/2026 إلى 30/11/2026, which was مسودة when I started.

Extract 2 is now صادر; I walked it (step 5). Extract 3 is the one waiting for you (see step 6).

### Step 5. One extract through its gates (what I did on extract 2)

The chain, verified in the screens, is: Supervisor submits the QC report, technical office approves, accounts applies deductions, Owner approves, Owner issues. Each person opens the extract from the project's المستخلصات list; the page has a box "ما يمكنك فعله" that holds only the buttons that person may press. Status words and what moved on extract 2:

| # | Person | Click | Status before | Status after | What was added to "مسار المستخلص" |
|---|---|---|---|---|---|
| 1 | `site_sup_demo` | تقديم تقرير الجودة | مسودة | عند المكتب الفني | تقرير الجودة, اجتاز, with the time |
| 2 | `tech_office_demo` | اعتماد للصرف (the other button is رفض مع ذكر السبب) | عند المكتب الفني | عند الحسابات | المكتب الفني, اجتاز, "النسبة المعتمدة: 100%" |
| 3 | `accounts_demo` | تطبيق الاستقطاعات, then نعم، تابع to confirm (the other button is رفض مع ذكر السبب) | عند الحسابات | عند المالك | الحسابات, اجتاز; the figures document appears |
| 4 | `owner_demo` | اعتماد المالك, then نعم، تابع | عند المالك | معتمد من المالك | المالك, اجتاز |
| 5 | `owner_demo` | إصدار المستخلص, then نعم، تابع | معتمد من المالك | صادر | الإصدار, اجتاز; the box reads "انتهى هذا المستخلص. لا يمكن فعل شيء آخر به هنا." |

Before the accounts step nobody sees figures: the page says "تظهر الأرقام بعد أن تطبّق الحسابات الاستقطاعات." After it, the document "مستند المستخلص" is shown to technical office, accounts and Owner.

### Step 6. Your extract: number 3

Extract 3 (`/projects/01a0de49-4a8c-7707-b19a-0f7592bbfdb0/extracts/01a19942-f102-77a9-bdfa-121670ae7ab7`) is parked as مسودة at the first gate. I did not click any gate on it. Walk it with the same five people in the same order as step 5; the buttons and statuses are the same.

Its figures. Extract 3 was first prepared with no new work, so its evidence read cumulative certified 110,734.65 and previously certified 110,734.65. I then sent it through one real rejection and a revision (the Technical Office rejected it with the reason "demo: refresh evidence with the work recorded since preparation", then the engineer revised the evidence, which re-derives it from the BOQ). Its evidence now reads cumulative certified 220,237.65 and previously certified 110,734.65, read from the API. It is back in مسودة at the first gate, and I clicked no gate after the revision. The figures document does not exist until the accounts step, so the page shows none before then. At the accounts step I expect work value 109,503.00 (220,237.65 minus 110,734.65, my arithmetic), hold this period 21,900.60 and net payable 87,602.40, the same as extract 2. The hold and net are my arithmetic (20 percent of the work value), not read from the server, so compare them with what you see. The audit trail on extract 3 will show one rejection with its reason and one "EvidenceRevised" entry before your clicks. That is honest history, not a fault.

### Step 7. The issued document, as Owner (extract 2)

Open extract 2 as `owner_demo`. The six figures in "مستند المستخلص", each tagged "محسوب" with a link "كيف وصلنا إلى هذا الرقم":

| Figure | Extract 1 | Extract 2 |
|---|---|---|
| قيمة الأعمال | 1,231.65 | 109,503.00 |
| المحجوز هذه الفترة | 246.33 | 21,900.60 |
| المحجوز حتى تاريخه (tagged "للعلم") | 246.33 | 22,146.93 |
| استرداد الدفعة المقدمة | 0.00 | 0.00 |
| تشوينات | 0.00 | 0.00 |
| صافي المستحق | 985.32 | 87,602.40 |

The extract 1 column is from the seeding script's own listing (not clicked by me this run); the extract 2 column is what I read on screen. Extract 2 came from the three quantities the seed recorded: 120 m² interior plaster at 172.50 (20,700.00), 250 m² paint at 144.30 (36,075.00) and 40 m² porcelain at 1,318.20 (52,728.00), which add to 109,503.00.

Below the figures is a block "العمود المطبوع": قيمة الأعمال +109,503.00، تشوينات 0.00، المحجوز هذه الفترة −21,900.60، استرداد الدفعة المقدمة 0.00, مجموع العمود 87,602.40, صافي المستحق 87,602.40, and the line "العمود يطابق الإجمالي المطبوع."

### Step 8. صفحة إثبات الأرقام, as Owner

From the project workspace, in the section المال, click إثبات الأرقام (the same link is on the extract pages). It opens `/projects/01a0de49-4a8c-7707-b19a-0f7592bbfdb0/proof`. Owner and accounts have this link. The Junior engineer's project row has no such link in my walk.

What you see: "القيود الفعلية بجانب ما تتوقعه قواعد العقد، سطراً سطراً. للقراءة فقط: لا شيء هنا يُصحَّح تلقائياً." A stamp "من قاعدة بيانات الإنتاج" with the read time, and the project line "4,672,694.01 ج.م · حجز 20% · استرداد الدفعة 25%".

Then, honestly:

- The headline reads "لا حكم نهائي بعد: 0 من 5 فحوص مطابقة". Five checks are listed (الحجز = 20% من العقد، الدفعة المقدمة تنتهي عند صفر تماماً، تشوينات: ما دخل = ما استُرد، ما حصَّله العميل = قيمة العقد، الخزنة لا تنزل تحت الصفر), all marked "لم يُحكم بعد". They wait for handover.
- The table "المتوقع مقابل الفعلي" has a row per extract. The "الفعلي" row is real: EXT-1 shows 1,231.65 / −246.33 / 0.00 / 0.00 / 0.00 and EXT-2 shows 109,503.00 / −21,900.60 / 0.00 / 0.00 / 0.00. The "المتوقع" row says "غير متاح: حاسبة العقد لم تُنفَّذ" for both. So expected versus actual does not compare yet.
- "الدفاتر الخمسة": the الحجز card reads 22,146.93 with two entries, which equals 246.33 + 21,900.60. The other four ledgers read 0.00 with no entries. The page says "لا مجموع كلي لهذه الدفاتر الخمسة. عمداً." and "مطابقة: رصيد كل دفتر يساوي مجموع قيوده."
- A list of assumptions at the foot, each tagged A1, A2 and so on, with what is not decided.

### Step 8a. Start a project yourself — the thing that was missing

**Until 2026-11-06 you could not start a project by clicking at all.** The contract value derives from
the signed BOQ, and there was no screen to build or sign one, so every project in this database was made
by a script. That is fixed, and it is the first thing to try.

1. On المشاريع, press **+ مشروع جديد** (also in the sidebar, and in the list's empty state).
2. Name it, pick the client, pick the contract type. **There is no field for the contract value**, and
   that absence is deliberate — a banner tells you where the value comes from instead. For Lump Sum you
   may override the hold and advance percentages or leave them blank for the defaults.
3. It lands you on the BOQ. Add lines from the catalogue, edit a quantity, remove a line.
4. Press **توقيع التعاقد…**. The first press signs nothing: it asks, names the figure that is about to
   become the contract value, and says the lines freeze and it cannot be undone. **This is the most
   irreversible button in the application.**
5. After signing, look for an add, edit, remove or sign control. **There is none** — not greyed out,
   absent. The database itself refuses to alter a frozen line.
6. The totals row reads إجمالي المقايسة before signing and قيمة التعاقد after, so it never claims a
   contract value that is not yet fixed.

**Walked end to end before this shipped**, against a throwaway copy of the database: three lines added,
one edited, one removed, lines of 24,466.25 and 11,502.30, and after signing `contractValue` came back
**35,968.55** — the sum of the lines, derived and not typed. A line added after signature was refused
`409 boq.frozen` by the real server.

⛔ Design and Cost Plus show an empty state instead of a BOQ builder. Design has none by `spec.md` §5.3.
**Cost Plus is an open question for you:** the server would allow a Cost Plus BOQ and the screen does not
offer one. Say which you want.

### Step 8b. The project overview, rebuilt — this is the screen you complained about

Open the project. Each group on the page prints a question; the complaint was that it printed the question
and not the answer. Now:

- **الفهم — "وكم أُنجز منه؟"** a **progress bar** with the percentage, the executed value against the
  contract value, **which باب is running now**, and a row per باب with its own percentage and an active
  badge. Progress is **derived** from executed quantities on the BOQ, never stored.
- **التنفيذ — "ما الذي يجري في الموقع؟"** the date of the last daily log, and the active باب.
- **المال — "ما المستحق؟"** certified, billed, collected and outstanding. It used to show a count and
  the word مسودة with no amount at all.
- **التنطيم — "من يعمل عليه؟"** the people's names. It used to show the number 4.

**And a strip under the header says what the project is waiting for and on whom:** "المستخلص رقم 3:
بانتظار تقرير الجودة من مشرف الموقع". **Sign in as the Supervisor and check this**, because it was
the sharpest finding in the consultant's review: the system knew he held the next action, told him
nothing, had no link to the extracts on his screen at all, and told everyone else "لا يوجد ما تفعله في
هذه المرحلة". He now gets the badge **الدور عليك الآن**, a header action that opens the extract, and
an المستخلصات link.

**Three things to check deliberately, because they are where this is most likely wrong:**
- **Cost Plus must show no progress bar at all** — `spec.md` §5.2 says so in those words. It shows cost
  and supervision figures instead. **A Design project shows "unavailable", never 0%.**
- **The junior site engineer must see no money anywhere**, including on this screen and on the extract
  list. His قيمة التعاقد leak on the project list was fixed this run.
- **The bar must fill from the right** under RTL.

### Step 8c. The button that invited you to end your own project

⛔ **This was a live hazard and it is the reason to re-walk the workspace.** On an active project with an
extract mid-chain, the only filled control on the whole screen was **إعلان الاستلام الابتدائي** — declare
the job finished. And `إنهاء المشروع`, which reads as *finish the project*, actually performed contract
termination and landed on تم فسخ التعاقد. Two different business acts behind one label, sitting under a
completion button.

Now: the workspace header never offers a completion act on a running project. Practical completion is on
the الإقفال screen only, as a secondary control. Termination is `فسخ التعاقد` throughout, behind a
destructive-styled control at the foot of the page, and its text now says in words that it is not the
same act as practical completion.

**One thing for you to settle:** Technical Office genuinely holds the permission to declare practical
completion — the catalogue grants it to Owner and Technical Office. That contradicts "the Owner declares".
No permission was changed. Tell us which is right.

### Step 9. إثبات الأرقام now judges, instead of saying it cannot

The 2026-11-04 walk found this page's "المتوقع" row reading "غير متاح: حاسبة العقد لم تُنفَّذ" for every
extract. The billing calculators landed since, and the page now judges each extract event against the
real calculator rather than declining to compare. Re-walk it and read the expected column.

**I have not re-photographed this page myself this session.** It changed in commits `94b1fa4` and
`f7d5a86`, recorded as D-197. If the expected row still says غير متاح, tell me — that would be a
regression and not a known state.

### Step 10. الإقفال, the closure screen, as Owner and as Finance

**This is the new screen, and it is the one worth your time.** From the project workspace, the hub now has
a sixth group, الإقفال, holding a link to `/projects/{id}/closure`. One screen carries the whole end of a
project's life: the stage, the hold, snags, warranty callbacks, and termination.

What to try, and what each person should see:

- **As Owner**, the header's primary action follows the stage, and it is the same slot that offers
  فتح جدول الكميات at the start of a project: declare practical completion while the project is running,
  then hand over, then begin warranty, then close. One act at a time, never a menu of four.
- **Try handing over with a major snag open.** It is refused, and the refusal **names the snag's number**
  rather than saying "cannot hand over". A minor snag does not block it.
- **Try closing before the project is settled.** It is refused and **lists which conditions are unmet**.
- **As Finance, at تم التسليم or في فترة الضمان**, the hold panel appears. Look at it closely:
  it shows the amount **and the fact that it was computed from N postings, not typed**. There is no
  amount field, and there is deliberately no way to type one. The release endpoint accepts no amount.
  That is the spec rule made visible: the hold only grows, and it releases once, in full, at handover.
- **Try releasing twice.** The second is refused. A database trigger refuses it even if the application
  somehow did not.
- **As Owner or Technical Office, press the termination control.** It returns the honest answer: the
  settlement is not decided. See section 5.

Roles, and the pattern this project holds to — a control you may not use is **absent, not greyed out**:
Finance alone sees the hold panel; the Technical Office raises, fixes, verifies and resolves snags and
raises callbacks; the Owner declares, hands over, begins warranty and closes. A Design project gets an
empty state, because practical completion does not apply to it.

**Photographed by the lane that built it, not by me**: Owner at 1440 and 390 across جاري العمل and
في انتظار التسليم, a refused handover naming the snag, a refused close naming the unmet conditions, and
the termination note; Technical Office at both widths on the snag list and forms; Finance at both widths
through the hold panel, the release confirmation and the released state. After that release was clicked
in a real browser, the database held exactly one `HoldRelease` posting and a hold balance of 0.0000.

⛔ **Not seen by anyone, so treat as unphotographed:** dark mode, the English UI, Cost Plus and Design on
a real screen, the callback-resolve click, and the Technical Office view at 390 under warranty.
⛔ **The Arabic copy on this screen was written by a model and no human has read it.** If a sentence reads
wrong to you, it is wrong — say so and it changes.

### Step 11. The money that now exists behind the screens, and how to see it

Three things shipped on the server with no screen, so you can only reach them through the API. They matter
because each closes a hole in the money spine:

- **A collection turns an issued extract into money in the safe.** Before this, `BuildCollection` existed
  and nothing called it, so an issued extract never became cash. A collection records method, date and
  reference, none optional. Withholding at source comes out of the same act: a corporate client at 1%
  puts 2,400 into the withholding account and 237,600 into the safe, and the two sum to the 240,000 net.
  An individual client withholds nothing. **Cash reaches the safe only at تم التحصيل and not one stage
  earlier** — that timing was a real defect once, and it is now asserted per stage, not just per total.
- **A mistaken collection is reversed, never edited.** The reversal writes mirror postings that point at
  the originals. Nothing in this system updates or deletes a posting, ever.
- **A change order changes the contract value without touching the signed BOQ.** The project reads lead
  with the revised value, with the original and the approved total beside it. It reprices nothing that
  is already issued, and it never touches the hold.

## 4. The three things worth your attention

### (a) The contract value is derived, and cannot be typed

The 4,672,694.01 on the project list and on the BOQ page is the sum of the 15 signed BOQ lines, each frozen by copy at signing (badge "مجمّدة في 01/11/2026"). I looked for an input on the BOQ page and found none: zero input fields, no edit control. The project list has no field for contract value either. The BOQ page does not print the word "derived"; it shows the number, the frozen badge and the lines it adds up from. Check: 470,490.30 + 425,643.75 + 140,113.13 + 155,877.33 + 855,182.25 + 525,632.25 + 250,052.40 + 451,081.80 + 144,723.15 + 495,381.25 + 113,400.00 + 182,850.00 + 332,580.00 + 76,896.00 + 52,790.40 = 4,672,694.01.

### (b) A control you may not use is absent, not greyed out

Pair 1: in the project workspace, Owner sees five sections, including الكميات with the link المقايسة and المال with المستخلصات and إثبات الأرقام. The Junior engineer sees two sections, التنفيذ and التنظيم, and no المقايسة link at all. I typed the BOQ address by hand as the engineer: the app took me to `/forbidden`, page title "غير مصرح بالدخول", "ليس لديك صلاحية لهذا الإجراء." (The server refuses, not only the menu.)

Pair 2, same screen, same extract, same role name (مهندس الموقع): on extract 2 as مسودة, the Supervisor sees the button تقديم تقرير الجودة. The Junior sees no button; the box says "لا يوجد ما تفعله في هذه المرحلة." The same pattern repeats at every gate: after the Supervisor's click, he sees "أنت من جهّز هذا المستخلص أو سبق أن تصرفت فيه، لذلك يقرر فيه شخص آخر في هذه المرحلة." and no button.

Pair 3: side navigation. Owner has ten entries; Junior and Supervisor have one (المشاريع); accounts has two.

### (c) The extract column foots against its own total

On the issued document (step 7), the "العمود المطبوع" block adds the lines 109,503.00 + 0.00 − 21,900.60 + 0.00 = 87,602.40 and prints the check line "العمود يطابق الإجمالي المطبوع." On the proof page (step 8) the الحجز card, 22,146.93, equals the two hold entries 246.33 + 21,900.60, and the page prints "مطابقة: رصيد كل دفتر يساوي مجموع قيوده."

## 5. Not built

**Four lines that used to live here are now built, and this section is the record of that.** Collections,
change orders, project closure/handover/warranty, and expected-versus-actual on the proof page have all
shipped since the 2026-11-04 walk. They are described in steps 9 to 11 above. What follows is what is
genuinely still missing.

- **(Built 2026-11-06.)** Creating a project, and building and signing a BOQ, now have screens. See
  step 8a. That was the gap that mattered most, because nothing else could be reached without it.
- **(Built 2026-11-06.)** Progress, the active باب, the money figures, the team, and what the project is
  waiting for and on whom. See step 8b.
- **No screen creates an extract.** The chain is driven from the extract list and the extract page, and
  extracts are seeded by script. "مستخلص جديد" has no screen behind it and is not offered.
- **No screen records a collection.** `POST .../extracts/{id}/collect` exists, takes method, date and
  reference, and is tested; nothing on any page calls it. To see a collection you have to call the API.
- **No screen raises or approves a change order.** The whole chain exists on the server, with its own
  gates, rejections and separation of duties. There is no change-order screen at all.
- **No screen raises a dispute or an adjustment, and none releases subcontractor retention.** Server only.
- **Cheques have no screen.** The states (received, deposited, cleared, bounced) are a real state machine
  on the server with audit, and nothing renders them.
- Still not on the project workspace, because nothing serves it: executed-versus-surveyed progress, the
  list of daily logs, open NCRs, subcontractors, the five-ledger strip, the pending-extract chain, the
  team roster, linked projects, the stop control, Cost Plus supervision rate and classification, Design
  area, rate and stages.
- **`Terminated` settles nothing, deliberately.** `spec.md` §13 names the state and §6.9 says "termination
  settlement", and nothing anywhere says what settles. The transition refuses with a stored reason and
  writes no posting. The closure screen shows that refusal as a note. **This is waiting on Karim, not on
  code.** Do not read the refusal as a bug.
- **No invoice.** "فاتورة جديدة" has no screen and no server route.
- Cost Plus and Design projects have never been walked in a browser — the demo holds one Lump Sum project.
  Their behaviour is covered by tests only.

## 6. Not ready (things I could not walk or that do not work)

- Extract 3 carries one rejection and one evidence revision on its audit trail (see step 6). I did the rejection to give it real figures. I did not click any gate after that.
- I did not press حفظ on the daily log (it would change the next extract). I did not click رفض مع ذكر السبب at any gate (it would send the extract back and consume it).
- The daily log's other tabs (عمالة، خامات) and the day-labour pages were not walked.
- Phone-width screens other than the daily log and the project list were not measured.

## 7. What I found

- **(Fixed.)** The Owner's project list used to show a second project, مشروع KAFF-934 (1,000,000.00, لم تبدأ). It was test leftover, not seed code — nothing in the repository created it, so there was nothing to delete in source. It was database rows with no postings and no extracts, and they were deleted on 2026-11-04. The list now shows one project.
- (Fixed.) Each project row used to carry a link with no visible text that opened day labour. It is gone; the whole row now opens the project workspace.
- **(Gone with it.)** That project had a contract value and no BOQ, so its workspace showed a منقول chip claiming the value came from a signed BOQ when it did not. The chip is still capable of saying that about any project in the same state, so it is worth knowing: the claim is only true where a signed BOQ exists.
- **(Changed.)** The proof page's expected row used to be unavailable because no calculator existed. The calculators landed and the page now judges each extract against them (D-197). The five end-of-project checks still read "لم يُحكم بعد" until handover, which is correct — they are handover checks.
- The pending-extract seeding script could not make a second extract with new work. It reused the first run's quantity ids, so extract 3 was prepared with nothing new. I fixed the ids (commit `03d5755`), and the script now also sends such an extract through a reject-and-revise so its evidence picks up the new work. That is how extract 3 got its figures.

## 8. Things I changed for you

The Owner now lands on the project list, not the user management screen (D-196). The "المستخدمون" row in the side navigation still reaches `/users`. This is yours to confirm or revert.

## 9. What I need from you

1. The five Arabic status words for Stopped, HandoverPending, Handover, UnderWarranty and Terminated.
2. The Arabic names for the three contract types.
3. Whether a change order shows the original contract value, the revised one, or both.
4. What happens to an extract that is mid-chain when its project is stopped.
5. Whether an engineer types an expense amount (D-192).
6. Whether an inactive worker may be charged hours (Q118).

Added since the last walk, in rough order of what they cost if guessed wrong:

7. **Roughly ninety Arabic strings written this run are a model's guesses, not yours.** They cover the
   create form, the BOQ builder, the overview and the extract list. The run's hand-back lists every key
   and value for one correction pass. Two worth your eye: **المحصَّل** for the collected figure and
   **المطالَب به** for billed.
8. **Should a Cost Plus project have a BOQ?** The server allows one; the screen does not offer one.
9. **May the Technical Office declare practical completion?** The permission catalogue says yes; the
   documented flow says the Owner declares. Nothing was changed either way.
10. **What a termination settles.** `spec.md` §13 names `Terminated` and §6.9 says "termination
   settlement", and nothing says what settles. Nothing was invented: the transition refuses. This is the
   largest unanswered business question in the project module, and it moves money at the end of a
   contract, which is exactly where a guess surfaces too late to fix cheaply.
11. **After a credit note is approved, how does the client pay the reduced amount, and does withholding
   apply to that reduced amount?** Collection is refused while a credit note is open, so today an extract
   with an approved credit note can never reach تم التحصيل. That is deliberate — collecting the stored
   net would over-collect — but it is a dead end until Karim answers.
12. **A dispute resolved as a revised extract.** Does the original go `Void`, are its postings reversed,
   and how is its hold leg handled without breaking "the hold only grows"? Refused today.
13. **Who approves what, in five places nobody has ruled:** a collection, an adjustment, the hold
    release, a change order, and a subcontractor retention release. Each runs on an assumed default,
    each default is written down as a `RulingNote`, and each is a one-line change once Karim answers.
14. **Subcontractor retention:** confirm it releases at the warranty end rather than with the client
    hold, and how the 5% is zeroed per subcontractor.
15. **Whether the same person may fix a snag and then verify it.** The server does not currently stop
    them, and the snag response does not even say who did which, so no screen could hide the control.
16. **The five Arabic status words are still guesses.** `متوقف`, `في انتظار التسليم`, `تم التسليم`,
    `في فترة الضمان`, `تم فسخ التعاقد` are on screen today and were written by a model, not given by you.
    Your five status words for the states you did name are used verbatim; these five are not yours yet.
