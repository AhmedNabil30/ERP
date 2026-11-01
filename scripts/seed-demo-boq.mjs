// Seeds a villa-finishing BOQ onto the demo project and signs it, through the real API.
// Why: without a BOQ, GET /api/projects/{id}/daily-logs/boq-lines answers 404 boq.not_found, the daily log has
// nothing to record quantities against, and the project has no BOQ-derived contract value to reconcile.
// Lines are copied from the EXISTING seeded catalogue by code (no catalogue item is invented). Each line carries
// an estimated quantity (Sales) and a surveyed quantity (Technical Office), so signing is allowed.
//
// Signing is irreversible (003_boq_freeze.sql): the BOQ is frozen and its total replaces the project's
// contract value. quantity_executed stays writable, so the daily log still works. Pass --no-sign to skip.
//
// Re-runnable. Lines are matched by itemCode on the existing BOQ; a signed BOQ is detected (frozenOn) and left
// alone. A rerun prints "0 created, N existed".
// Usage: node scripts/seed-demo-boq.mjs [baseUrl] [projectId] [--no-sign]   (owner_demo / Demo#Owner1 must exist)
const base = process.argv[2]?.startsWith('http') ? process.argv[2] : 'http://localhost:5080';
const projectId = process.argv.find((a) => /^[0-9a-f-]{36}$/.test(a)) ?? '01a0de49-4a8c-7707-b19a-0f7592bbfdb0';
const wantSign = !process.argv.includes('--no-sign');

// [catalogue code, quantity estimated, quantity surveyed]
const LINES = [
  ['MAS-3004', 1180, 1146],   // hollow cement block 20 cm, m2
  ['MAS-3006', 2520, 2467.5], // interior plaster, m2
  ['MAS-3007', 655, 641.25],  // facade plaster, m2
  ['MAS-3008', 790, 783.5],   // ceiling plaster, m2
  ['FIN-3002', 655, 648.75],  // porcelain 80x80, m2
  ['FIN-3003', 220, 214.5],   // Galala marble, m2
  ['FIN-3005', 315, 308.25],  // bathroom wall ceramic, m2
  ['FIN-3007', 3180, 3126],   // premium paint with putty, m2
  ['FIN-3009', 265, 259.5],   // flat gypsum ceilings, m2
  ['FIN-3014', 430, 418.75],  // Hashemi stone facade, m2
  ['CAR-3001', 14, 14],       // internal beech doors, count
  ['CAR-3005', 28, 26.5],     // bedroom wardrobes, linear m
  ['MET-3001', 98, 96.4],     // aluminium sliding windows, m2
  ['PLU-3010', 6, 6],         // full bathroom set, count
  ['ELE-3002', 96, 94],       // socket points, count
];

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
const si = await call('POST', '/api/auth/sign-in', { userName: 'owner_demo', password: 'Demo#Owner1' });
if (si.status !== 204) throw new Error('sign-in failed ' + si.status + si.text);

const cat = (await call('GET', '/api/catalogue-items')).json?.items ?? [];
const byCode = new Map(cat.map((i) => [i.code, i]));

const boqUrl = `/api/projects/${projectId}/boq`;
let boq = await call('GET', boqUrl);
if (boq.status !== 200 && boq.status !== 404) throw new Error('get boq ' + boq.status + ' ' + boq.text);
let frozenOn = boq.status === 200 ? boq.json.frozenOn : null;
const have = new Set(boq.status === 200 ? boq.json.lines.map((l) => l.itemCode) : []);

let created = 0, existed = 0;
for (const [code, est, sur] of LINES) {
  if (have.has(code)) { existed++; continue; }
  if (frozenOn) { console.log(`skip ${code}: BOQ frozen on ${frozenOn}`); continue; }
  const item = byCode.get(code);
  if (!item) throw new Error('catalogue item missing: ' + code);
  const r = await call('POST', boqUrl + '/lines', {
    catalogueItemId: item.id, quantityEstimated: est, quantitySurveyed: sur,
  });
  if (r.status !== 201 && r.status !== 200) throw new Error(`add ${code}: ${r.status} ${r.text}`);
  created++;
}
console.log(`${created} created, ${existed} existed (BOQ lines -> project ${projectId})`);

boq = await call('GET', boqUrl);
if (boq.status !== 200) throw new Error('get boq ' + boq.status + ' ' + boq.text);
frozenOn = boq.json.frozenOn;
if (frozenOn) console.log(`already signed: BOQ frozen on ${frozenOn}`);
else if (!wantSign) console.log('not signed (--no-sign)');
else {
  const missing = boq.json.lines.filter((l) => l.quantitySurveyed == null);
  if (missing.length) throw new Error('lines without surveyed quantity: ' + missing.map((l) => l.itemCode));
  const s = await call('POST', `/api/projects/${projectId}/sign`, {});
  if (s.status === 200) console.log(`signed: contractValue ${s.json.contractValue}, BOQ frozen on ${s.json.boqFrozenOn}`);
  else if (s.json?.code === 'project.already_signed') console.log('already signed (project; BOQ not frozen)');
  else throw new Error('sign ' + s.status + ' ' + s.text);
}
const fin = await call('GET', boqUrl);
console.log(`BOQ: ${fin.json.lines.length} lines, total ${fin.json.total}, frozenOn ${fin.json.frozenOn}`);
