// Seeds three demo projects whose extracts and change orders stand at every step a reviewer must see (D-257),
// through the real API only. Every act goes through its handler, so each one runs the domain method, the role and
// assignment check, separation of duties and the audit trail. Postings are only ever added by those handlers.
//
// Why three projects: a project holds one extract in flight at a time (extract.another_extract_open, D-194), so Draft,
// QcSubmitted and PartiallyApproved cannot stand on one project together.
//
//   فيلا 12 — الشيخ زايد   #1 Issued, two cheques in hand (one Received, one Deposited); #2 PartiallyApproved, NCR open
//   عمارة 5 — المعادي      #1 Disputed; #2 QcSubmitted (Technical Office to act); change orders: one at the client's
//                           approval, one Approved
//   شقة 9 — مدينة نصر      #1 Issued with a credit note raised and awaiting the Owner; #2 Draft (QC report to submit)
//
// Each project gets a signed BOQ from the seeded catalogue, and executed quantities through the daily log, so every
// extract carries quantity rows. A second Technical Office user, tech_office2_demo / Demo#Tech2New, is created because
// a change order's technical review must be decided by someone other than the Technical Office user who drafted it.
//
// Development only: the script refuses any base URL that is not localhost. Re-runnable: projects are found by name,
// quantity entries carry fixed ids, and each step runs only when its state is not reached yet.
// Usage: node scripts/seed-demo-extract-states.mjs [baseUrl]   (run the other seed-demo scripts first)
const base = process.argv[2]?.startsWith('http') ? process.argv[2] : 'http://localhost:5080';
if (!/^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/.test(base)) {
  console.log(`STOP: ${base} is not a local Development API`);
  process.exit(1);
}

async function session(userName, password) {
  let cookie = '';
  async function call(method, path, body) {
    const r = await fetch(base + path, {
      method,
      headers: { 'Content-Type': 'application/json', ...(cookie ? { Cookie: cookie } : {}) },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const sc = r.headers.get('set-cookie');
    if (sc) cookie = sc.split(';')[0];
    const text = await r.text();
    let json = null;
    try { json = JSON.parse(text); } catch { /* empty body */ }
    return { status: r.status, json, text };
  }
  const si = await call('POST', '/api/auth/sign-in', { userName, password });
  if (si.status !== 204) throw new Error(`sign-in ${userName} failed ${si.status} ${si.text}`);
  return call;
}

function ok(r, what) {
  if (r.status >= 300) throw new Error(`${what}: ${r.status} ${r.text}`);
  return r.json;
}

const owner = await session('owner_demo', 'Demo#Owner1');

// ---- the second Technical Office user ------------------------------------------------------------------------------
const users = ok(await owner('GET', '/api/users'), 'list users').users;
const techOne = users.find((u) => u.userName === 'tech_office_demo');
if (!techOne) throw new Error('tech_office_demo missing: run the earlier seed scripts first');
let techTwo = users.find((u) => u.userName === 'tech_office2_demo');
if (!techTwo) {
  techTwo = ok(await owner('POST', '/api/users', {
    fullName: 'المكتب الفني التجريبي الثاني', userName: 'tech_office2_demo', phone: '01012340007',
    role: 'TechnicalOffice', departmentId: techOne.departmentId, operationsSubDepartment: techOne.operationsSubDepartment,
    temporaryPassword: 'Demo#Tech2',
  }), 'create tech_office2_demo');
  console.log('created tech_office2_demo');
}
{
  const first = await fetch(base + '/api/auth/sign-in', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: 'tech_office2_demo', password: 'Demo#Tech2' }) });
  if (first.status === 204) {
    const ck = first.headers.get('set-cookie').split(';')[0];
    const cp = await fetch(base + '/api/auth/change-password', { method: 'POST', headers: { 'Content-Type': 'application/json', Cookie: ck }, body: JSON.stringify({ currentPassword: 'Demo#Tech2', newPassword: 'Demo#Tech2New' }) });
    if (cp.status >= 300) throw new Error('change-password tech_office2_demo ' + cp.status);
  }
}

const eng = await session('site_eng_demo', 'Demo#Site1New');
const sup = await session('site_sup_demo', 'Demo#Sup1New');
const tech = await session('tech_office_demo', 'Demo#Tech1New');
const tech2 = await session('tech_office2_demo', 'Demo#Tech2New');
const acct = await session('accounts_demo', 'Demo#Acct1New');
const team = [
  ['site_eng_demo', 'Junior'], ['site_sup_demo', 'Supervisor'], ['tech_office_demo', 'Standard'],
  ['tech_office2_demo', 'Standard'], ['accounts_demo', 'Standard'],
];

const catalogue = new Map((ok(await owner('GET', '/api/catalogue-items'), 'catalogue').items).map((i) => [i.code, i]));
const clients = ok(await owner('GET', '/api/clients'), 'clients').clients;

// [catalogue code, surveyed quantity]. The estimate is the same figure.
const LINES = [
  ['MAS-3006', 1400], ['MAS-3008', 520], ['FIN-3002', 410], ['FIN-3007', 1900], ['FIN-3009', 180], ['ELE-3002', 60],
];
// The share of each line's quantity executed before extract one and before extract two.
const ROUNDS = [0.3, 0.2];

async function ensureProject(index, name, clientCode) {
  let project = ok(await owner('GET', '/api/projects'), 'list projects').projects.find((p) => p.name === name);
  if (!project) {
    const client = clients.find((c) => c.code === clientCode) ?? clients[0];
    project = ok(await owner('POST', '/api/projects', {
      name, clientId: client.id, contractType: 'LumpSum', holdPercentage: '0.1', clientAdvance: '0', status: null,
    }), `create ${name}`);
    console.log(`created project ${name}`);
  }
  const id = project.id;
  for (const [userName, level] of team) {
    const user = users.find((u) => u.userName === userName) ?? techTwo;
    const a = await owner('POST', `/api/projects/${id}/assignments`, { userId: user.id, level });
    if (a.status >= 300 && a.status !== 409) throw new Error(`assign ${userName}: ${a.status} ${a.text}`);
  }
  let boq = await owner('GET', `/api/projects/${id}/boq`);
  if (boq.status === 404 || (boq.status === 200 && boq.json.lines.length === 0)) {
    ok(await owner('POST', `/api/projects/${id}/boq/lines/batch`, {
      items: LINES.map(([code, quantity]) => {
        const item = catalogue.get(code);
        if (!item) throw new Error('catalogue item missing: ' + code);
        return { catalogueItemId: item.id, quantityEstimated: quantity, quantitySurveyed: quantity };
      }),
    }), `BOQ lines ${name}`);
    boq = await owner('GET', `/api/projects/${id}/boq`);
  }
  if (!ok(boq, `get BOQ ${name}`).frozenOn) ok(await owner('POST', `/api/projects/${id}/sign`, {
    holdPercentage: '0.1', clientAdvance: '0', advanceRecoveryRate: '0',
  }), `sign ${name}`);
  return { id, index, name };
}

// Executed quantities for one round, as period deltas through the daily log. Fixed entry ids make a replay a no-op.
async function recordWork(project, round) {
  // A quantity needs the engineer's log for the day and a located check-in on it (daily_log.check_in_required).
  const now = new Date().toISOString();
  const opened = await eng('POST', `/api/projects/${project.id}/daily-logs`, { occurredAt: now });
  if (opened.status >= 300 && opened.status !== 409) throw new Error(`open daily log: ${opened.status} ${opened.text}`);
  const checkIn = `5eed57a7-${String(project.index).padStart(4, '0')}-4000-8000-${String(900 + round).padStart(12, '0')}`;
  const ci = await eng('POST', `/api/projects/${project.id}/daily-logs/check-ins`, {
    entryId: checkIn, occurredAt: now, kind: 'CheckIn', latitude: 30.0131, longitude: 31.4089, accuracyMetres: 12,
  });
  if (ci.status >= 300 && ci.status !== 409) throw new Error(`check-in: ${ci.status} ${ci.text}`);
  const lines = ok(await eng('GET', `/api/projects/${project.id}/daily-logs/boq-lines`), 'daily-log BOQ lines').lines;
  for (const [i, [code, quantity]] of LINES.entries()) {
    const line = lines.find((l) => l.itemCode === code);
    const entryId = `5eed57a7-${String(project.index).padStart(4, '0')}-4000-8000-${String(round * 100 + i).padStart(12, '0')}`;
    const r = await eng('POST', `/api/projects/${project.id}/daily-logs/quantities`, {
      entryId, boqLineId: line.id, quantity: Math.round(quantity * ROUNDS[round]), occurredAt: new Date().toISOString(),
    });
    if (r.status >= 300) throw new Error(`quantity ${code}: ${r.status} ${r.text}`);
  }
}

const extractsOf = async (project) => ok(await owner('GET', `/api/projects/${project.id}/extracts`), 'list extracts');
const extractUrl = (project, extract, step = '') => `/api/projects/${project.id}/extracts/${extract.id}${step ? '/' + step : ''}`;

// Extract n of the project, prepared from round n-1's quantities when it does not exist yet.
async function ensureExtract(project, number) {
  let extract = (await extractsOf(project)).find((e) => e.number === number);
  if (extract) return extract;
  await recordWork(project, number - 1);
  const month = 8 + number; // September, then October 2026
  const last = new Date(Date.UTC(2026, month, 0)).getUTCDate();
  extract = ok(await eng('POST', `/api/projects/${project.id}/extracts`, {
    periodStart: `2026-${String(month).padStart(2, '0')}-01`, periodEnd: `2026-${String(month).padStart(2, '0')}-${last}`,
    materialValueOnSite: '0', materialInstalled: '0',
  }), `prepare extract ${number} on ${project.name}`);
  return { ...extract, status: 'Draft' };
}

const CHAIN = [
  ['QcSubmitted', 'qc-report', sup, { materialValueOnSite: '0', materialInstalled: '0' }],
  ['TechnicalApproved', 'technical-approval', tech, {}],
  ['DeductionsApplied', 'deductions', acct, undefined],
  ['OwnerApproved', 'owner-approval', owner, undefined],
  ['Issued', 'issue', owner, undefined],
];
const ORDER = ['Draft', ...CHAIN.map(([status]) => status)];

async function drive(project, extract, target) {
  let at = ORDER.indexOf(extract.status);
  if (extract.status === 'PartiallyApproved') at = ORDER.indexOf('TechnicalApproved');
  if (at < 0 || at >= ORDER.indexOf(target)) return; // already there, or past the chain: Disputed, Collected, Void
  for (const [status, step, actor, body] of CHAIN) {
    if (ORDER.indexOf(status) <= at || ORDER.indexOf(status) > ORDER.indexOf(target)) continue;
    ok(await actor('POST', extractUrl(project, extract, step), body), `${step} extract ${extract.number} on ${project.name}`);
  }
  extract.status = target;
}

async function netPayable(project, extract) {
  const figures = ok(await owner('GET', extractUrl(project, extract)), 'read extract').document.figures;
  return Number(figures.find((f) => f.key === 'netPayable').amount);
}

// ---- فيلا 12: cheques in hand, and a split gate with its NCR open ------------------------------------------------
{
  const project = await ensureProject(1, 'فيلا 12 — الشيخ زايد', 'C-10004');
  const first = await ensureExtract(project, 1);
  await drive(project, first, 'Issued');
  let cheques = ok(await acct('GET', `/api/projects/${project.id}/cheques`), 'list cheques');
  const net = await netPayable(project, first);
  for (const [n, share] of [[1, 0.25], [2, 0.2]].slice(cheques.length)) {
    ok(await acct('POST', extractUrl(project, first, 'collect'), {
      method: 'Cheque', date: '2026-11-15', reference: `شيك رقم 1004${n}`,
      cashReceived: (Math.floor(net * share * 100) / 100).toFixed(2), withheldAtSource: '0',
    }), `cheque ${n}`);
  }
  cheques = ok(await acct('GET', `/api/projects/${project.id}/cheques`), 'list cheques');
  if (!cheques.some((c) => c.state === 'Deposited')) {
    ok(await acct('POST', `/api/projects/${project.id}/cheques/${cheques[0].id}/deposit`, {}), 'deposit cheque');
  }
  const second = await ensureExtract(project, 2);
  await drive(project, second, 'QcSubmitted');
  if (second.status === 'QcSubmitted') {
    ok(await tech('POST', extractUrl(project, second, 'technical-approval'), { approvedPortion: '0.6', ncrDescription: 'تعشيش في خرسانة العمود C3 يحتاج إصلاح' }), 'split gate');
  }
}

// ---- عمارة 5: a dispute, an extract at the Technical Office, and two change orders --------------------------------
{
  const project = await ensureProject(2, 'عمارة 5 — المعادي', 'C-10005');
  const first = await ensureExtract(project, 1);
  await drive(project, first, 'Issued');
  if (first.status === 'Issued') {
    ok(await acct('POST', extractUrl(project, first, 'dispute'), { reason: 'العميل يعترض على كميات البياض في الدور الثاني' }), 'dispute');
  }
  const second = await ensureExtract(project, 2);
  await drive(project, second, 'QcSubmitted');

  const orders = ok(await owner('GET', `/api/projects/${project.id}/change-orders`), 'list change orders');
  const ORDERS = [
    ['تعديل واجهة المدخل الرئيسي', 'ClientApproval', [['حجر هاشمي إضافي للمدخل', 'م٢', 40, '820']]],
    ['إضافة نقاط كهرباء للمطبخ', 'Approved', [['نقطة بريزة إضافية', 'عدد', 12, '450'], ['نقطة إنارة سقف', 'عدد', 6, '380']]],
  ];
  for (const [title, target, lines] of ORDERS) {
    let order = orders.find((o) => o.title === title);
    const coUrl = (step) => `/api/projects/${project.id}/change-orders/${order.id}/${step}`;
    if (!order) {
      order = ok(await tech('POST', `/api/projects/${project.id}/change-orders`, {
        title, lines: lines.map(([description, unit, quantity, unitPrice]) => ({ description, unit, quantity, unitPrice })),
      }), `create change order ${title}`);
      order.status = 'Draft';
    }
    if (order.status === 'Draft') { ok(await tech('POST', coUrl('submit'), {}), 'submit change order'); order.status = 'TechnicalReview'; }
    if (order.status === 'TechnicalReview') { ok(await tech2('POST', coUrl('technical-review'), {}), 'technical review'); order.status = 'ClientApproval'; }
    if (target === 'Approved' && order.status === 'ClientApproval') { ok(await sup('POST', coUrl('client-approval'), {}), 'client approval'); order.status = 'OwnerApproval'; }
    if (target === 'Approved' && order.status === 'OwnerApproval') ok(await owner('POST', coUrl('owner-approval'), {}), 'owner approval');
  }
}

// ---- شقة 9: a credit note awaiting the Owner, and a Draft extract --------------------------------------------------
{
  const project = await ensureProject(3, 'شقة 9 — مدينة نصر', 'C-10006');
  const first = await ensureExtract(project, 1);
  await drive(project, first, 'Issued');
  const read = ok(await owner('GET', extractUrl(project, first)), 'read extract');
  if (read.status === 'Issued' && (read.creditNotes ?? []).length === 0) {
    ok(await acct('POST', extractUrl(project, first, 'dispute'), { reason: 'العميل يطلب خصم تأخير تسليم الدهانات' }), 'dispute');
    ok(await acct('POST', extractUrl(project, first, 'dispute-resolution'), {
      kind: 'CreditNote', reason: 'اتفاق مع العميل على خصم 15,000 ج.م', amount: '15000',
    }), 'credit note');
  }
  await ensureExtract(project, 2);
}

// ---- what stands now ------------------------------------------------------------------------------------------------
for (const p of ok(await owner('GET', '/api/projects'), 'list projects').projects.filter((x) => /^(فيلا 12|عمارة 5|شقة 9) — /.test(x.name))) {
  const list = ok(await owner('GET', `/api/projects/${p.id}/extracts`), 'list extracts');
  const cos = ok(await owner('GET', `/api/projects/${p.id}/change-orders`), 'list change orders');
  const cheques = ok(await acct('GET', `/api/projects/${p.id}/cheques`), 'list cheques');
  console.log(`${p.code} ${p.id} ${p.name}`);
  for (const e of list) console.log(`  extract #${e.number} ${e.id} ${e.status}`);
  for (const c of cheques) console.log(`  cheque ${c.id} ${c.state}`);
  for (const c of cos) console.log(`  change order ${c.id} ${c.status} ${c.title}`);
}
