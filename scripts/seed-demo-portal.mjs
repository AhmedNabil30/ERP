// Seeds ONE portal Client user whose user name is a phone number, linked to the client of the demo project, through the real API.
// Why: KAFF-811 AC-E asks for a portal client the client-facing screens can be driven as, and the portal sign-in takes a phone number.
// portal_client_demo (seed-demo.ps1) belongs to the corporate demo client and is not the owner of the demo project.
//
// Credentials: 01012340010 / Demo#Portal2   (Role.Client, scoped to the client that owns the project below)
// The account is created with mustChangePassword set, as every account is, and this script does NOT change the password: the first
// sign-in must land on the change-password step, which is the behaviour the screens need to show.
//
// Local development only: the script talks to the local API and refuses any other address, so it can never reach Staging or Production.
// Re-runnable: the user is found by user name and left alone when it exists. A rerun prints "0 created, 1 existed".
// Usage: node scripts/seed-demo-portal.mjs [baseUrl] [projectId]   (owner_demo / Demo#Owner1 must exist)
const base = process.argv[2]?.startsWith('http') ? process.argv[2] : 'http://localhost:5080';
if (!/^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/.test(base)) throw new Error('refusing a non-local address: ' + base);
const projectId = process.argv.find((a) => /^[0-9a-f-]{36}$/.test(a)) ?? '01a0de49-4a8c-7707-b19a-0f7592bbfdb0';
const USER_NAME = '01012340010';

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

const projects = Object.values((await call('GET', '/api/projects')).json ?? {}).find(Array.isArray) ?? [];
const project = projects.find((p) => p.id === projectId);
if (!project) throw new Error('demo project not found: ' + projectId);

let created = 0, existed = 0;
const users = Object.values((await call('GET', '/api/users')).json ?? {}).find(Array.isArray) ?? [];
if (users.some((u) => u.userName === USER_NAME)) existed++;
else {
  const c = await call('POST', '/api/users', {
    fullName: 'عميل البوابة التجريبي',
    userName: USER_NAME,
    phone: USER_NAME,
    role: 'Client',
    clientId: project.clientId,
    temporaryPassword: 'Demo#Portal2',
  });
  if (c.status !== 201) throw new Error(`create ${USER_NAME} ${c.status} ${c.text}`);
  created++;
}
console.log(`${created} created, ${existed} existed (portal client ${USER_NAME} -> client of project ${projectId})`);
