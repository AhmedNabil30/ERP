// Seeds a SECOND extract on the demo project and leaves it waiting at a gate, so a human can click the
// remaining approvals in the UI. Through the real API only; nothing is edited directly.
//
// Chain (spec.md section 7, verified against src/Api/Features/Extracts): Prepared (Draft) -> QC report
// (ExtractQcReportSubmit, site supervisor) -> Technical Office quantity gate (QuantityGateApprove) ->
// Accounts deductions (ExtractDeductionsApply, Finance) -> Owner approval -> Issue (Owner).
//
// PARKS AT: status Draft, the very first gate. The extract is prepared and waits for the weekly QC report.
// NEXT CLICK: site_sup_demo / Demo#Sup1New submits the QC report. Then tech_office_demo / Demo#Tech1New
// approves the quantity gate, accounts_demo / Demo#Acct1New applies deductions, owner_demo / Demo#Owner1
// approves and issues.
//
// Steps: (1) site_eng_demo records executed-quantity deltas on a few BOQ lines through the daily log (a
// quantity entry moves BoqLine.QuantityExecuted at once, there is no submit step), so the extract has new
// certified work; (2) site_eng_demo prepares extract #2 (the server derives the work value from the frozen
// BOQ, nothing is typed).
//
// Re-runnable. If an extract that is not Issued/Collected already exists, nothing is created and the run
// prints "0 created, N existed". Quantity entries use fixed entry ids, so a replay is also recognised.
// The server refuses a second open extract (extract.another_extract_open); extract #1 must be Issued.
// Usage: node scripts/seed-demo-extract-pending.mjs [baseUrl] [projectId] [--list]
const base = process.argv[2]?.startsWith('http') ? process.argv[2] : 'http://localhost:5080';
const projectId = process.argv.find((a) => /^[0-9a-f-]{36}$/.test(a)) ?? '01a0de49-4a8c-7707-b19a-0f7592bbfdb0';
const listOnly = process.argv.includes('--list');

// [BOQ item code, executed quantity delta for this period, fixed entry id]
const WORK = [
  ['MAS-3006', 120], // interior plaster, m2
  ['FIN-3007', 250], // premium paint with putty, m2
  ['FIN-3002', 40],  // porcelain 80x80, m2
];
// Entry ids are fixed per round (round = extract number - 1; extract #2 was round 1, so its ids are unchanged),
// so a replay of the same round is recognised, while each new extract gets its own fresh quantity deltas.
const entryId = (round, i) => `5eed${String(round).padStart(4, '0')}-0000-4000-8000-00000000000${i + 1}`;

async function session(userName, password) {
  let cookie = '';
  async function call(method, path, body) {
    const r = await fetch(base + path, {
      method,
      headers: { 'Content-Type': 'application/json', ...(cookie ? { Cookie: cookie } : {}) },
      body: body ? JSON.stringify(body) : undefined,
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

const owner = await session('owner_demo', 'Demo#Owner1');
const extractsUrl = `/api/projects/${projectId}/extracts`;
const list = await owner('GET', extractsUrl);
if (list.status !== 200) throw new Error('list extracts ' + list.status + ' ' + list.text);

if (listOnly) {
  for (const e of list.json) {
    const d = await owner('GET', `${extractsUrl}/${e.id}`);
    const f = Object.fromEntries((d.json?.document?.figures ?? []).map((x) => [x.key, x.amount]));
    const ev = d.json?.evidence;
    // The figures document exists only after the Technical Office gate; before that only the evidence is shown.
    console.log(`#${e.number} ${e.id} ${e.periodStart}..${e.periodEnd} ${e.status}  cumulative certified ${ev?.cumulativeCertifiedWork} previous ${ev?.previouslyCertifiedWork}` + (f.workValue ? `  work ${f.workValue} hold ${f.holdThisPeriod} net ${f.netPayable}` : ''));
  }
  process.exit(0);
}

const open = list.json.filter((e) => e.status !== 'Issued' && e.status !== 'Collected');
const nextNumber = open.length ? open[0].number : Math.max(0, ...list.json.map((e) => e.number)) + 1;
const round = nextNumber - 1;

const eng = await session('site_eng_demo', 'Demo#Site1New');
async function recordWork() {
  const lines = (await eng('GET', `/api/projects/${projectId}/daily-logs/boq-lines`)).json?.lines ?? [];
  let made = 0;
  const occurredAt = new Date().toISOString();
  for (const [i, [code, quantity]] of WORK.entries()) {
    const line = lines.find((l) => l.itemCode === code);
    if (!line) throw new Error('BOQ line missing: ' + code);
    const r = await eng('POST', `/api/projects/${projectId}/daily-logs/quantities`, { entryId: entryId(round, i), boqLineId: line.id, quantity, occurredAt });
    if (r.status !== 201 && r.status !== 200) throw new Error(`quantity ${code}: ${r.status} ${r.text}`);
    if (r.status === 201) made++;
  }
  console.log(`${made} quantity entries created (round ${round})`);
}

if (open.length) {
  // An open extract that was prepared with no new work (cumulative equals previous) is topped up with this round's deltas.
  const d = await owner('GET', `${extractsUrl}/${open[0].id}`);
  const ev = d.json?.evidence;
  if (open[0].status === 'Draft' && ev && Number(ev.cumulativeCertifiedWork) === Number(ev.previouslyCertifiedWork)) await recordWork();
  console.log(`0 created, ${open.length} existed (open extract #${open[0].number} ${open[0].id} is ${open[0].status})`);
  process.exit(0);
}

await recordWork();

const today = new Date();
const first = new Date(Date.UTC(today.getFullYear(), today.getMonth(), 1)).toISOString().slice(0, 10);
const last = new Date(Date.UTC(today.getFullYear(), today.getMonth() + 1, 0)).toISOString().slice(0, 10);
const zero = { amount: 0, currency: 'EGP' };
let p = await eng('POST', extractsUrl, { periodStart: first, periodEnd: last, materialValueOnSite: 0, materialInstalled: 0 });
if (p.status === 400) p = await eng('POST', extractsUrl, { periodStart: first, periodEnd: last, materialValueOnSite: zero, materialInstalled: zero });
if (p.status !== 201) { console.log(`STOP: prepare refused ${p.status} ${p.text}`); process.exit(1); }
created++;

const d = await owner('GET', `${extractsUrl}/${p.json.id}`);
const ev = d.json.evidence;
console.log(`${created} created, 0 existed: extract #${p.json.number} ${p.json.id} ${p.json.status}  cumulative certified ${ev.cumulativeCertifiedWork} previous ${ev.previouslyCertifiedWork}`);
console.log('Parked at the QC report gate. Next click: site_sup_demo / Demo#Sup1New.');
