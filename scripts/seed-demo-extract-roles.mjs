// Seeds the three demo users the spec.md section 7 extract chain needs, through the real API, and assigns
// them to the demo project. Why: the demo had one Junior engineer and the Owner, so nobody could work the
// QC report (ExtractQcReportSubmit needs a Supervisor), the quantity gate (QuantityGateApprove is Technical
// Office) or the deductions step (ExtractDeductionsApply is Finance alone, the Owner excluded, D-189).
// sara_finance_demo exists but her password is unknown (neither Demo#Fin123 nor Demo#Fin123New signs in),
// so a second Finance user is created instead of reusing her.
//
// Credentials (final, after the forced change this script performs itself):
//   site_sup_demo   / Demo#Sup1New    SiteEngineer, Operations/Technical, Supervisor on the project
//   tech_office_demo / Demo#Tech1New  TechnicalOffice, Standard on the project
//   accounts_demo   / Demo#Acct1New   Finance, Standard on the project
//
// While mustChangePassword is set the server answers 403 auth.password_change_required to every call, so the
// script completes the change (temp password + "New"). A rerun finds the temp password rejected and leaves
// the account alone; the assignment 409 counts as existing. A rerun prints "0 created, 6 existed".
// Usage: node scripts/seed-demo-extract-roles.mjs [baseUrl] [projectId]   (owner_demo / Demo#Owner1 must exist)
const base = process.argv[2]?.startsWith('http') ? process.argv[2] : 'http://localhost:5080';
const projectId = process.argv.find((a) => /^[0-9a-f-]{36}$/.test(a)) ?? '01a0de49-4a8c-7707-b19a-0f7592bbfdb0';
const OPERATIONS = '00000000-0000-0000-0000-000000000203';
const FINANCE = '00000000-0000-0000-0000-000000000201';
const TECHNICAL_OFFICE = '00000000-0000-0000-0000-000000000202';

const USERS = [
  { userName: 'site_sup_demo', fullName: 'مشرف الموقع التجريبي', phone: '01012340002', role: 'SiteEngineer', departmentId: OPERATIONS, operationsSubDepartment: 'Technical', temp: 'Demo#Sup1', level: 'Supervisor' },
  { userName: 'tech_office_demo', fullName: 'المكتب الفني التجريبي', phone: '01012340003', role: 'TechnicalOffice', departmentId: TECHNICAL_OFFICE, temp: 'Demo#Tech1', level: 'Standard' },
  { userName: 'accounts_demo', fullName: 'حسابات تجريبي', phone: '01012340004', role: 'Finance', departmentId: FINANCE, temp: 'Demo#Acct1', level: 'Standard' },
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

let created = 0, existed = 0;
const users = Object.values((await call('GET', '/api/users')).json ?? {}).find(Array.isArray) ?? [];
for (const u of USERS) {
  let user = users.find((x) => x.userName === u.userName);
  if (user) existed++;
  else {
    const { temp, level, ...body } = u;
    const c = await call('POST', '/api/users', { ...body, temporaryPassword: temp });
    if (c.status !== 201) throw new Error(`create ${u.userName} ${c.status} ${c.text}`);
    user = c.json; created++;
  }
  // Forced password change on a separate cookie so the owner session is untouched.
  const s = await fetch(base + '/api/auth/sign-in', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: u.userName, password: u.temp }) });
  if (s.status === 204) {
    const ck = s.headers.get('set-cookie').split(';')[0];
    const cp = await fetch(base + '/api/auth/change-password', { method: 'POST', headers: { 'Content-Type': 'application/json', Cookie: ck }, body: JSON.stringify({ currentPassword: u.temp, newPassword: u.temp + 'New' }) });
    if (cp.status >= 300) throw new Error(`change-password ${u.userName} ${cp.status}`);
    console.log(`${u.userName}: temporary password replaced by ${u.temp}New`);
  }
  const a = await call('POST', `/api/projects/${projectId}/assignments`, { userId: user.id, level: u.level });
  if (a.status === 201 || a.status === 200) created++;
  else if (a.status === 409) existed++;
  else throw new Error(`assign ${u.userName} ${a.status} ${a.text}`);
}
console.log(`${created} created, ${existed} existed (3 users -> project ${projectId})`);
