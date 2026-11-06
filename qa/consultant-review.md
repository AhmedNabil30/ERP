# Kaff ERP — outside review of the business and the flow

Walked on 2026-11-06 against the running stack (API 5080, web 4200), signed in as all five seeded
people, at 1440 px and at 390 px. One project in the database: فيلا 47 — التجمع الخامس, status
جاري العمل, three extracts (1 and 2 issued, 3 parked at the first gate).

Screenshots and the full on-screen text of every screen I opened are in `qa/shots-consultant/`
(`<name>.png` with its `<name>.txt` beside it, e.g. `owner-workspace.png` / `owner-workspace.txt`).

Nothing was changed. I pressed no gate button, saved no daily log, and clicked nothing that moves
money or state.

---

## The short answer to Nabil's four questions

1. **Is this the best flow?** No, and the reason is not the order of the steps — the order is right.
   The problem is that the flow has no beginning and no end a person can click. A project cannot be
   created, its BOQ cannot be signed, an extract cannot be raised, and the money that was approved
   can never be recorded as collected. What exists is the *middle* of the chain: five people
   approving a document that nobody on screen can create and nobody on screen can get paid for.
2. **Are the project's steps clear?** No. The project screen tells you the contract value, the hold
   percentage and the number of extracts. It never tells you what stage the project is at beyond one
   word, what happened last, or what has to happen next — and on today's data it tells four of five
   roles "لا يوجد ما تفعله في هذه المرحلة" while an extract is sitting waiting for one of them.
3. **Can a stranger use it?** Partly. Sign-in, the project list and the catalogue are
   self-explanatory. Everything that moves a project forward is not: I had to read a document to
   learn where the extract list is for the role that owns the next action, and I could not have
   guessed the proof page, the "من قاعدة بيانات الإنتاج" stamp, or what "طلب إنهاء المشروع" does.
   Eleven specific points are listed in section C.
4. **Why is so much shown that nobody needs?** Three screens carry almost all of it: the proof page
   (an internal verification document shown as a user screen), the audit trail (875 sign-in rows and
   raw entity names such as `ExtractGateRecord`), and the catalogue's `English description` column.
   Specifics in section D. Separately, and more costly: several screens show too *little* — the
   extract list is a list of money documents with no money on it.

---

## What a real user can and cannot do by clicking

This is the frame for everything below, so it comes first.

**Can do by clicking:** sign in; see the one project; read the signed BOQ; read the extract list and
one extract's figures and history; push an existing extract through its five gates in the right order
with the right people refused at the right points (this part works and it is good); record a daily log
entry; manage the catalogue, babs, clients, users, employees, subcontractors, suppliers, departments;
read the audit trail; read the proof page; declare practical completion and attempt a termination.

**Cannot do by clicking at all:** create a project; sign a BOQ; create an extract; record that the
client paid; reverse a mistaken collection; raise or approve a change order; raise a dispute or an
adjustment; release subcontractor retention; move a cheque between its states; record withholding;
see the safe balance; see what any of the five ledgers holds outside the proof page.

What this does to the journey, concretely: the Owner's screen says "عدد المستخلصات 3 · آخر مستخلص
مسودة" and offers two links. Extract 2 is صادر — issued, invoiced, owed. There is no screen anywhere
that says the client owes 87,602.40, no screen that records the cheque when it arrives, and no screen
that shows the money reaching the safe. **A contractor's single most-asked question — "who owes me
what, and what came in this week" — has no answer anywhere in this application.** The system
currently automates the approval of a document and stops exactly where the cash is.

---

## A. Flow problems — the steps are wrong, missing, or out of order

### A1. The journey has no entry point. (Project list · Owner · flow)

**What a stranger experiences.** The Owner signs in and lands on المشاريع: a table with one row and
no buttons. There is no "مشروع جديد". A new villa job arrives on Sunday and there is no way to put it
into the system. The same is true one level down: the BOQ screen is read-only by design, so even if a
project existed, there is no way to build or sign its bill of quantities.

**What it should be.** A primary "مشروع جديد" on the project list, and the setup-stage project
workspace leading the Owner through: client → contract type → hold % → advance % → build BOQ → sign.
Until that exists, the demo only works because a script seeded the data.

**Fix:** flow (new screens).

### A2. Nothing on screen creates an extract — the money document the whole app is built around. (Extract list · all roles · flow)

**What a stranger experiences.** `/projects/{id}/extracts` lists three extracts and has no create
button. The five-gate approval chain — the best-built thing in this product — can only ever be
applied to extracts a developer inserted. At the end of a real month nobody can start one.

**What it should be.** "مستخلص جديد" on the extract list for the preparing role, with the period and
the executed quantities pulled from the daily logs.

**Fix:** flow.

### A3. An issued extract is a dead end — there is no way to say the client paid. (Extract detail · Accounts, Owner · flow)

**What a stranger experiences.** Extract 2 is صادر. Its page says "انتهى هذا المستخلص. لا يمكن فعل
شيء آخر به هنا." For the business, an issued extract is where the work *starts*: chase it, receive the
cheque, deposit it, clear it, record the 1% withholding. On screen, issuing it is the end of its life.

**What it should be.** The issued extract keeps a live panel: outstanding amount, aged days, and
"تسجيل تحصيل" for the role that owns it. The server already does all of this.

**Fix:** flow. This is the largest hole in the product by business value.

### A4. The loudest button on an active project invites you to end the project. (Project workspace and closure · Owner and Technical Office · flow)

**What a stranger experiences.** Open the project. The single action button on the entire screen —
top of the page, filled, the only filled control anywhere — is **إعلان الاستلام الابتدائي**
(`owner-workspace.png`). The project is جاري العمل with an extract mid-chain. The one thing the screen
invites me to do is declare the job finished. On the الإقفال screen the same button is joined by
**طلب إنهاء المشروع** (`owner-closure.png`), and those two destructive acts are the *entire* content
of that screen for a running project.

I also see إعلان الاستلام الابتدائي as **Technical Office** (`tech-workspace.png`, `tech-closure.png`).
Either the Technical Office may declare completion — which contradicts the walkthrough's own
"the Owner declares, hands over, begins warranty and closes" — or a control is being shown to someone
who cannot use it, which breaks this project's otherwise excellent rule that a control you may not use
is absent rather than greyed out.

**What it should be.** The primary slot on a running project belongs to the next thing that actually
has to happen (see A5). Practical completion belongs on the الإقفال screen only, and only for the role
that may declare it. Termination belongs behind a secondary, clearly destructive control.

**Fix:** flow, plus one permission question to settle.

### A5. Nobody is told what the project is waiting for, or on whom. (Project workspace and extract detail · all roles · flow)

**What a stranger experiences.** Extract 3 is مسودة and the next act is the Supervisor's QC report.
Signed in as Owner, as Technical Office and as Accounts, the project screen says
"**لا يوجد ما تفعله في هذه المرحلة**" and the extract page says the same. Signed in as the
**Supervisor**, who actually holds the next action, his project workspace has no المال section and no
link to the extracts at all — his only route to the button he must press is: اليومية → a
المستخلصات link buried in the daily log's header strip, or typing the URL. I only found it because a
document told me.

So the system knows exactly who it is waiting for and tells that person nothing, while telling
everyone else "nothing to do".

**What it should be.** Two changes. (a) Anywhere the chain is paused, name the step and the holder:
"بانتظار تقرير الجودة من مشرف الموقع" instead of "لا يوجد ما تفعله". (b) Give every role a landing
list of what is waiting on them; at minimum, put the المستخلصات link in the Supervisor's project
workspace.

**Fix:** flow plus copy. This is the cheapest high-value change in the report.

### A6. The extract trail reads as finished when the extract has restarted. (Extract detail · all roles · clarity/flow)

**What a stranger experiences.** Extract 3's مسار المستخلص (`owner-extract3.png`) reads: التجهيز
اجتاز · تقرير الجودة **اجتاز** · المكتب الفني **مرفوض** · تعديل المستندات **اجتاز**, and then stops.
Every node is resolved; nothing marks a current position. A reader concludes the extract has been
revised and is moving on. In fact it went back to مسودة and needs the QC report again — the اجتاز on
تقرير الجودة belongs to the *previous* cycle. Nothing on screen distinguishes the two cycles.

**What it should be.** An explicit "أنت هنا" node for the current step with the role that owns it, and
superseded cycle entries visually demoted or grouped as "دورة سابقة".

**Fix:** flow of the component plus copy.

### A7. The Technical Office lands on the price list, not on its work. (Landing · Technical Office · flow)

**What a stranger experiences.** `tech_office_demo` signs in and arrives at كتالوج الأسعار — 94
catalogue items. This role's job in the chain is to approve extracts and to own snags. Its home screen
is a reference table.

**What it should be.** Land on the work: extracts awaiting this role, then projects. The catalogue is a
navigation entry, not a destination.

**Fix:** flow (one routing decision).

### A8. The warranty vocabulary describes a sequence Egyptian contracting does not use. (All project screens · all roles · copy, but a business decision first)

The five unapproved words are متوقف · في انتظار التسليم · تم التسليم · في فترة الضمان · تم فسخ التعاقد.
As an Egyptian contractor would read them:

- **تم فسخ التعاقد** — correct and unambiguous. Keep it. But the button that reaches it says
  **إنهاء المشروع** / **طلب إنهاء المشروع**, which reads as "finish the project" — the *opposite*
  meaning, sitting directly beneath a completion button on the same screen. That pairing is a real
  risk of a wrong click on a real contract.
- **في انتظار التسليم / تم التسليم** — too vague to be safe here. Egyptian practice has two distinct
  handovers: **الاستلام الابتدائي** and **الاستلام النهائي**, and the whole warranty/maintenance period
  sits *between* them. "تم التسليم" does not say which one, and it is the word that decides whether the
  hold is released. A status word that is ambiguous about a 20% retention is a dispute waiting to
  happen.
- **في فترة الضمان** — understood, but on site this period is usually called **فترة الصيانة**.
  More importantly, the chain as built reads "تم التسليم → في فترة الضمان", while contractors read it
  as "الاستلام الابتدائي → فترة الصيانة → الاستلام النهائي". The screen's own first button already says
  الاستلام الابتدائي correctly; the status words then stop using that vocabulary.
- **متوقف** — understandable, but it competes with Kaff's own approved **متعثرة**. Two words for
  "this project is not moving" will be used interchangeably by users and will never be reportable.

**What it should be.** Ask Karim for five words, and ask specifically whether the two handovers are
ابتدائي/نهائي. Until then these five should not be on a production screen.

**Fix:** copy, after a business ruling.

---

## B. Clarity problems — the right steps, badly explained

### B1. The project overview answers none of the questions it asks. (Project workspace · Owner · copy + layout)

The screen is organised as five or six groups, each with a question printed under it. The questions are
good. The answers are not there:

| Group | Its printed question | What it actually shows |
|---|---|---|
| الفهم | "ما هذا العقد، **وكم أُنجز منه**؟" | contract value, hold %, advance recovery %. **Nothing about how much is done.** |
| الكميات | "بماذا تعاقدنا، وبأي كميات؟" | 15 items, total, frozen badge, link. Fair. |
| التنفيذ | "**ما الذي يجري في الموقع**؟" | one link: عمالة اليومية. For the Owner, literally nothing about the site. |
| المال | "ما المستحق، **وما الذي يتحرك الآن**؟" | a count (3) and a word (مسودة). No amount, no due, no collected. |
| التنظيم | "من يعمل عليه؟" | the number 4. Not who. |

A stranger asked "what stage is this project at and what happens next" can answer only "جاري العمل",
which is the same thing the list row already said. There is no start date, no contract duration, no
target completion, no date of the last daily log, no percentage complete, no money in or out.

**What it should be.** Either answer each question in its group with one real figure (percent executed,
last log date, amount certified / collected / outstanding, the names of the team), or stop printing the
question. Printing a question above an answer that is not there is worse than printing neither.

**Fix:** layout and content (the figures exist server-side; progress and collections do not yet).

### B2. "إثبات الأرقام" tells the Owner that 0 of 5 checks match. (Proof page · Owner, Accounts · copy)

**What a stranger experiences.** The headline reads **"لا حكم نهائي بعد: ⁨0⁩ من ⁨5⁩ فحوص مطابقة"**
(`owner-proof-390.png`). An owner reads "zero out of five checks match" as "the system says my books
are wrong". The explanation that these five are end-of-project checks that cannot be judged before
handover is in smaller text below. The headline is a false alarm on an otherwise healthy project.

Directly below it the page contradicts itself: the summary says
"المطابقة صفاً صفاً غير متاحة ما دام المتوقع لكل حدث غير متاح", and then the table underneath shows
المتوقع filled in for EXT-1 and EXT-2 with the verdict **مطابق** on both. The summary copy is stale
relative to the table. (To answer the walkthrough's step 9 check: the expected row is *not* showing
غير متاح — that part works. It is the sentence above it that was not updated.)

**What it should be.** Headline the two extracts that *do* reconcile — "مطابق: 2 من 2 مستخلصات" — and
state the five handover checks as "تُفحص عند التسليم", not as a failing score. Delete the stale
sentence.

**Fix:** copy.

### B3. The closure screen's copy, as a reader. (Closure · all roles · copy)

Reading it cold:

- "**أين وصل المشروع**" — good. Plain, right register.
- "**المشروع قيد التنفيذ. يبدأ الإقفال بإعلان الاستلام الابتدائي.**" — good, and the term
  الاستلام الابتدائي is the correct one.
- "**إنهاء المشروع لم يُقرَّر حكمه بعد. يمكنك طلبه، وسيعرض النظام ما إذا كان متاحاً.**" — this is the
  development team talking to the customer. It admits in production that a rule is undecided, and it
  offers a button whose stated purpose is to find out whether the button works. No contractor will
  understand "لم يُقرَّر حكمه بعد". And "إنهاء المشروع" is the wrong word for فسخ التعاقد (see A8).
- "**لا يوجد ما تفعله في هذه المرحلة.**" shown to Accounts on this screen — correct but blunt; it
  should say what the project *is* waiting for.
- Breadcrumb reads "المشروع" rather than the project's name.

**What it should be.** Remove the undecided-rule sentence and the button behind it until Karim rules
on what a termination settles; a control that only ever refuses should not ship. Rename the act
فسخ التعاقد. Everything else on this screen reads correctly.

**Fix:** copy, plus removing one control.

### B4. "البند" means two different things, one of them on the engineer's phone. (Daily log · Site engineer · copy)

On the BOQ and in the daily log's الكميات tab, **البند** means a bill-of-quantities line. On the
حضور وملاحظات tab (`eng-dailylog-390.png`) **البند** labels a dropdown whose values are things like
"ملاحظة" — it means "kind of entry". Same word, two meanings, two tabs apart, on the screen used by
the least trained person in the company.

Also on that screen: there is **no date**. The page says "يومية اليوم" and offers no way to see or
choose which day is being written. A foreman recording yesterday evening's pour at 7am has no option.
And the header carries a bare "←" with no label.

**Fix:** copy for البند; flow/layout for the missing date.

### B5. The Supervisor and the junior engineer are the same person on screen. (Side navigation · both site roles · copy)

Both show role **مهندس الموقع** under their name (`sup-workspace.txt`). The entire separation of duties
at the first gate rests on these being two different people, and the system labels them identically.
The only visible difference is the display name.

**Fix:** copy (give the supervisor his own role word, e.g. مشرف الموقع).

### B6. The extract list is a money list with no money. (Extract list · all roles · layout)

Three columns: المستخلص · الفترة · الحالة. No value, no net payable, no collected/outstanding, no
"with whom". An Owner scanning the month's extracts learns nothing he could act on and must open each
one. (This is the inverse of the noise complaint, and worth saying plainly: the problem on this screen
is not too much data, it is none.)

**Fix:** layout.

### B7. The engineer's money-free screen links to the money. (Daily log · Site engineer · layout)

The project workspace deliberately hides المستخلصات from the site engineer — correct. His daily log
then prints a المستخلصات link in its header strip, which opens the extract list. Either the rule or the
link is wrong; a stranger experiences it as the app contradicting itself.

**Fix:** layout (remove the link for roles whose workspace hides it — but see A5: the Supervisor needs
a *deliberate* route, not this accidental one).

---

## C. Every point where I would have had to ask someone

1. How do I create a project? (No control exists.)
2. How do I create this month's extract? (No control exists.)
3. The client paid — where do I record it? (Nowhere.)
4. As Supervisor, where is the extract I have to sign off? (Only via the daily log's link, or the URL.)
5. Extract 3 says "لا يوجد ما تفعله" to four roles — so who is it waiting for? (Not stated anywhere.)
6. Extract 3's trail shows تقرير الجودة as اجتاز but the extract is back at مسودة — which cycle am I
   looking at?
7. What is إثبات الأرقام for, and should "0 من 5" worry me?
8. What is the badge "من قاعدة بيانات الإنتاج" telling me, and why would it ever say anything else?
9. What do the "افتراض A1 … A11" symbols on the proof page mean, and must I do something about them?
10. What does "طلب إنهاء المشروع" do, and is it the same as إعلان الاستلام الابتدائي? (They sit
    together on one screen, and one of them ends the contract.)
11. On the audit trail, what is `ExtractGateRecord`, and what were the "8 fields" that changed?

---

## D. Noise problems — database data on a user's screen

Listed by screen, with what the role actually needs.

### D1. إثبات الأرقام (Owner, Accounts) — the worst offender

This is an internal verification artefact presented as a business screen. On it today:

- **"من قاعدة بيانات الإنتاج"** stamp with the read timestamp — meaningful only to someone who
  worried it might be test data.
- **"محسوب" / "منقول"** provenance tags on every single figure — a developer's distinction.
- **سجل الافتراضات**: a table of A1, A2, A5, A6, A7, A8, A9, A11 with columns "ما غير المحسوم",
  "أين يظهر على الشاشة", "إن حُسم، فما الذي يتغير" — this is an open-questions register for the build
  team, printed on the customer's screen, and the symbols are sprinkled through the figures above it.
- **"غير مصنّف"** card ("أي رقم هنا يعني قيداً لا يعرف دفتره") — a data-integrity probe.
- **"حد أدنى صفر تفرضه قاعدة البيانات"**, "السقف الصلب غير مُنفَّذ بعد", "لا يُستعمل أحدهما لتكوين الآخر"
  — implementation notes.
- Four of the five ledger cards read 0.00 with "لا قيود في هذا المشروع".

**What the Owner needs from this screen:** the hold balance and what it is made of, and a plain line
saying the issued extracts reconcile. Four lines, not four screens. Keep the full version behind a
"للمراجعة الداخلية" toggle if the build team wants it.

### D2. سجل التدقيق (Owner)

- **875 of roughly 900 rows are "تسجيل دخول".** The real history — the gate decisions, the BOQ
  change, the rejections — is buried under sign-in noise.
- **`ExtractGateRecord`, `Extract`, `BoqLine`** appear as raw English class names in an Arabic UI.
- **"تغيّر ⁨8⁩ من الحقول"** — the count of changed fields is a database fact; the user needs *which*
  ones and from what to what.
- The only filter is a date range: no filter by person, project, kind, or "hide sign-ins".

**What the Owner needs:** money and approval events, by project and by person, in business words.

### D3. كتالوج الأسعار (Technical Office)

- **`English description`** — an English column header and a full English duplicate of every row,
  doubling the visual weight of the table in an Arabic-primary UI. Make it a detail-panel field.
- **"داخلي"** tag on the cost-price column is fine; the cost price itself is legitimate for this role.

Otherwise this is the best screen in the application: it has search, filters, grouping, counts, an
import and a create. Everything else in the product lacks all of those.

### D4. المشاريع (Site engineer, Supervisor)

The row shows **قيمة التعاقد 4,672,694.01** to the junior site engineer (`eng-landing-390.png`),
whose every other screen is deliberately money-free. On a 390 px phone it is one of only five things on
his screen. He needs the project name, the status, and his own next task — not the contract value.

### D5. Project workspace (all roles)

- **PRJ-377EC1A2** is given the same prominence as the client name in the header line. A project code
  is a lookup key; it belongs smaller, or in a copy-to-clipboard affordance.
- **"فريق العمل 4"** — a count where a list of names is the useful thing.
- **"منقول"** chip on قيمة التعاقد — same developer provenance vocabulary as D1, and per the
  walkthrough it can claim a value came from a signed BOQ when none exists.

### D6. Extract detail (all roles)

- **"كيف وصلنا إلى هذا الرقم"** repeated on all six figures, plus **"محسوب"** on all six. When every
  row carries the same two tags they stop being information and become a wall.
- The rejection reason is shown raw — today that is an English developer sentence
  ("demo: refresh evidence…") on an Arabic screen. Fine for seed data, but nothing constrains or
  labels the reason, so whatever is typed lands verbatim in front of the client-facing roles.

---

## The three changes I would make first

**1. Record the collection. (A3, and with it the outstanding figure on A1/B1/B6.)**
Build the one screen that turns an issued extract into money received, and show outstanding and
collected on the project screen and the extract list. Everything else here is a usability cost; this
one is a cash cost. The product today automates the paperwork of getting approved and then forgets the
part where Kaff gets paid — and the server code for it is already written and tested, so this is the
largest return for the least build in the whole backlog.

**2. Say what is waiting, and on whom — and route the holder to it. (A5, A6, B1.)**
Replace "لا يوجد ما تفعله في هذه المرحلة" everywhere with the named next step and its owner, mark the
current node on the extract trail, and give the Supervisor a visible route to the extract he must act
on. This is mostly copy and one navigation entry — days of work, not weeks — and it fixes the exact
feeling Nabil described: *"I feel like things is not right."* Right now the system knows what it is
waiting for and refuses to say it.

**3. Take the end-of-project buttons out of the daily path, and take the build team's notes off the
customer's screens. (A4, B2, B3, D1, D2.)**
Demote إعلان الاستلام الابتدائي out of the primary slot on a running project, remove
طلب إنهاء المشروع until Karim rules on what a termination settles, retitle the proof page's "0 من 5",
and hide the assumptions register, the provenance tags and the sign-in rows behind an internal view.
These are the two places where the application most loudly tells a stranger that it was built for the
people who built it — and the first of them can end a live contract with one click.
