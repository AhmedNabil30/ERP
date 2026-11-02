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

Sign in as `owner_demo`. You land on المشاريع, and the heading reads "2 مشروعاً".

Row one: فيلا 47 — التجمع الخامس, code PRJ-377EC1A2, client شركة النيل للتطوير العقاري, type مقاولة بالمقطوعية, status جاري العمل, contract value 4,672,694.01. The row has no links inside it: the whole row is one link that opens the project (next step).

Row two: مشروع KAFF-934, code PRJ-C7871BB1, status لم تبدأ, contract value 1,000,000.00. This is leftover test data, not part of the demo (see "What I found"). Ignore it.

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

- The project workspace is built only as far as the API serves it. Not on the page yet, because nothing serves it: executed-versus-surveyed progress, change orders, the list of daily logs, open NCRs, subcontractors, the five-ledger strip, the pending-extract chain, the team roster, linked projects, the stop control, Cost Plus supervision rate and classification, Design area, rate and stages. Cost Plus and Design projects were not walked in a browser (the demo has only Lump Sum projects); their screens are covered by the unit tests.
- The primary buttons مستخلص جديد and فاتورة جديدة: there is no screen to create an extract or an invoice yet, so the workspace offers only فتح جدول الكميات (Owner, project in لم تبدأ) and سجل يومي جديد (site engineer).
- Collections (client payments in).
- Change orders.
- Project closure, handover, warranty.
- Expected-versus-actual on the proof page (the contract calculator is not implemented; the page says so).

## 6. Not ready (things I could not walk or that do not work)

- Extract 3 carries one rejection and one evidence revision on its audit trail (see step 6). I did the rejection to give it real figures. I did not click any gate after that.
- I did not press حفظ on the daily log (it would change the next extract). I did not click رفض مع ذكر السبب at any gate (it would send the extract back and consume it).
- The daily log's other tabs (عمالة، خامات) and the day-labour pages were not walked.
- Phone-width screens other than the daily log and the project list were not measured.

## 7. What I found

- The Owner's project list shows a second project, مشروع KAFF-934 (1,000,000.00, لم تبدأ). It looks like a test leftover, not a demo project.
- (Fixed.) Each project row used to carry a link with no visible text that opened day labour. It is gone; the whole row now opens the project workspace.
- The second project مشروع KAFF-934 has contract value 1,000,000.00 and no BOQ, so its workspace shows "لا توجد مقايسة لهذا المشروع بعد" in الكميات next to a contract value chip منقول. The chip says the value is carried from a signed BOQ, which is not true there. It is leftover test data.
- The proof page's five checks are all "لم يُحكم بعد" and the expected row is unavailable, so for now the page proves the actual postings add up, not that they match the contract rules.
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
