// Seeds ONE SiteEngineer demo user and assigns it to the demo project, through the real API.
// Why: seed-demo.ps1 seeds only owner, finance, hr, sales and a portal client, so the TC-6 cases
// (qa/slice-6/test-cases.md, daily log and worker register at 390px) had no user able to reach them.
// Nabil ruled the project (2026-11-01): 01a0de49-4a8c-7707-b19a-0f7592bbfdb0 (PRJ-377EC1A2).
//
// Credentials: site_eng_demo / Demo#Site1New   (SiteEngineer, Operations/Technical, Junior on the project)
// Created with temporary password Demo#Site1. While mustChangePassword is set the server answers 403
// auth.password_change_required to every call, so no TC-6 case could run. This script therefore completes
// the forced change itself (temp password + "New", the same choice screenshot-demo.mjs makes). A rerun finds
// the temporary password rejected and leaves the account alone.
//
// Re-runnable. The user is found by userName; the assignment is attempted every run and a 409
// (UserAlreadyAssignedToProject) counts as existing. A rerun prints "0 created, 2 existed".
// Usage: node scripts/seed-demo-engineer.mjs [baseUrl] [projectId]   (owner_demo / Demo#Owner1 must exist)
const base = process.argv[2]?.startsWith('http') ? process.argv[2] : 'http://localhost:5080';
const projectId = process.argv.find((a) => /^[0-9a-f-]{36}$/.test(a)) ?? '01a0de49-4a8c-7707-b19a-0f7592bbfdb0';
const OPERATIONS_DEPT = '00000000-0000-0000-0000-000000000203'; // WellKnownDepartments, D-162
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
let user = users.find((u) => u.userName === 'site_eng_demo');
if (user) existed++;
else {
  const c = await call('POST', '/api/users', {
    fullName: 'مهندس الموقع التجريبي', userName: 'site_eng_demo', phone: '01012340001',
    role: 'SiteEngineer', departmentId: OPERATIONS_DEPT, operationsSubDepartment: 'Technical',
    temporaryPassword: 'Demo#Site1',
  });
  if (c.status !== 201) throw new Error('create user ' + c.status + ' ' + c.text);
  user = c.json; created++;
}
// Complete the forced password change once, on a separate cookie so the owner session is untouched.
const eng = await fetch(base + '/api/auth/sign-in', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: 'site_eng_demo', password: 'Demo#Site1' }) });
if (eng.status === 204) {
  const ck = eng.headers.get('set-cookie').split(';')[0];
  const cp = await fetch(base + '/api/auth/change-password', { method: 'POST', headers: { 'Content-Type': 'application/json', Cookie: ck }, body: JSON.stringify({ currentPassword: 'Demo#Site1', newPassword: 'Demo#Site1New' }) });
  if (cp.status >= 300) throw new Error('change-password ' + cp.status);
  console.log('temporary password replaced by Demo#Site1New');
}
const a = await call('POST', `/api/projects/${projectId}/assignments`, { userId: user.id, level: 'Junior' });
if (a.status === 201 || a.status === 200) created++;
else if (a.status === 409) existed++;
else throw new Error('assign ' + a.status + ' ' + a.text);
console.log(`${created} created, ${existed} existed (site_eng_demo -> project ${projectId})`);
