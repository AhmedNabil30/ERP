// Archives test debris from the demo lists through the real API (never SQL, never delete).
// Re-runnable. Usage: node scripts/demo-tidy.mjs [baseUrl] [--list]   (owner_demo / Demo#Owner1)
const base = process.argv[2]?.startsWith('http') ? process.argv[2] : 'http://localhost:5080';
const listOnly = process.argv.includes('--list');
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
  try { json = JSON.parse(text); } catch { /* empty */ }
  return { status: r.status, json, text };
}
const si = await call('POST', '/api/auth/sign-in', { userName: 'owner_demo', password: 'Demo#Owner1' });
if (si.status !== 204) throw new Error('sign-in failed ' + si.status);

const rows = (j) => Object.values(j ?? {}).find(Array.isArray) ?? [];
// Exact debris. Matched on name or phone; anything not listed here is left alone.
const DEBRIS = [
  { list: '/api/clients', archive: (id) => `/api/clients/${id}/archive`, names: ['عميل KAFF-934'], codes: ['C-10002'] },
  { list: '/api/employees', archive: (id) => `/api/employees/${id}/archive`, names: ['abo kareem'] },
  { list: '/api/subcontractors', archive: (id) => `/api/subcontractors/${id}/archive`, names: ['مقاول اختباري'] },
  { list: '/api/suppliers', archive: (id) => `/api/suppliers/${id}/archive`, names: ['مورد اختباري'] },
  { list: '/api/users', archive: (id) => `/api/users/${id}/deactivate`, body: { reason: 'demo tidy' }, names: ['kaff922_forced'] },
];
const label = (r) => [r.name, r.nameAr, r.fullName, r.displayName, r.userName, r.code].filter(Boolean).join(' | ');
for (const d of DEBRIS) {
  const l = await call('GET', d.list);
  const all = rows(l.json);
  console.log(`${d.list}: ${all.length} rows`);
  if (listOnly) { all.forEach((r) => console.log('   ', r.id, label(r), r.phone ?? '')); continue; }
  for (const r of all) {
    const hit = Object.values(r).some((v) => typeof v === 'string' && d.names?.includes(v)) || d.codes?.includes(r.code);
    if (!hit) continue;
    const a = await call('POST', d.archive(r.id), d.body);
    console.log('  archive', label(r), '->', a.status, a.status >= 300 ? a.text.slice(0, 150) : '');
  }
}
