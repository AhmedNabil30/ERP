#!/usr/bin/env node
// Screenshot driver for the four accounts scripts/seed-demo.ps1 creates. Not a replacement for
// .claude/skills/run-kaff-erp/driver.mjs — that file is the shared skill and is left untouched; this
// borrows its CDP approach (same technique, same chromium) for a flow the skill's own commands do not
// cover: sign in, clear a forced password change, and screenshot the landing that results.
//
// Usage: node scripts/screenshot-demo.mjs <outDir>
//
// Not idempotent, same as the seed script it follows: each demo account's mustChangePassword flag can
// only be cleared once, so a second run against the same already-screenshotted database will fail at
// the change-password step because the "current" password it tries has already been replaced by the
// first run's new one. Reseed (see deploy/DEMO.md) before running this again.

import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import { homedir } from 'node:os';
import path from 'node:path';

const WEB = process.env.KAFF_WEB ?? 'http://localhost:4200';

function findChrome() {
  if (process.env.CHROME) return process.env.CHROME;
  const root = path.join(homedir(), 'AppData', 'Local', 'ms-playwright');
  const dirs = readdirSync(root).filter((d) => d.startsWith('chromium-')).sort().reverse();
  for (const dir of dirs) {
    const candidate = path.join(root, dir, 'chrome-win64', 'chrome.exe');
    if (existsSync(candidate)) return candidate;
  }
  throw new Error(`no chromium under ${root}`);
}

function launchChrome() {
  const exe = findChrome();
  const child = spawn(
    exe,
    [
      '--headless=new',
      '--remote-debugging-port=0',
      '--disable-gpu',
      '--no-sandbox',
      '--hide-scrollbars',
      '--no-first-run',
      '--disable-dev-shm-usage',
      '--lang=ar-EG',
      'about:blank',
    ],
    { stdio: ['ignore', 'ignore', 'pipe'] },
  );
  return new Promise((resolve, reject) => {
    let buffered = '';
    const timer = setTimeout(() => reject(new Error(`chromium printed no DevTools endpoint in 30s: ${buffered}`)), 30_000);
    child.stderr.on('data', (chunk) => {
      buffered += chunk;
      const match = buffered.match(/DevTools listening on (ws:\/\/\S+)/);
      if (match) {
        clearTimeout(timer);
        resolve({ child, browserWs: match[1] });
      }
    });
    child.on('error', reject);
    child.on('exit', (code) => {
      clearTimeout(timer);
      reject(new Error(`chromium exited with ${code} before listening: ${buffered}`));
    });
  });
}

class Cdp {
  constructor(socket) {
    this.socket = socket;
    this.nextId = 1;
    this.pending = new Map();
    socket.addEventListener('message', (event) => {
      const message = JSON.parse(event.data);
      if (message.id !== undefined) {
        const slot = this.pending.get(message.id);
        if (!slot) return;
        this.pending.delete(message.id);
        message.error ? slot.reject(new Error(JSON.stringify(message.error))) : slot.resolve(message.result);
      }
    });
  }
  static async connect(url) {
    const socket = new WebSocket(url);
    await new Promise((resolve, reject) => {
      socket.addEventListener('open', resolve, { once: true });
      socket.addEventListener('error', () => reject(new Error(`could not connect to ${url}`)), { once: true });
    });
    return new Cdp(socket);
  }
  send(method, params = {}, sessionId) {
    const id = this.nextId++;
    this.socket.send(JSON.stringify({ id, method, params, sessionId }));
    return new Promise((resolve, reject) => this.pending.set(id, { resolve, reject }));
  }
}

async function withPage(body) {
  const { child, browserWs } = await launchChrome();
  const browser = await Cdp.connect(browserWs);
  try {
    const { targetId } = await browser.send('Target.createTarget', { url: 'about:blank' });
    const { sessionId } = await browser.send('Target.attachToTarget', { targetId, flatten: true });
    const call = (method, params) => browser.send(method, params, sessionId);

    await call('Page.enable');
    await call('Runtime.enable');
    // 390x844: the phone width CLAUDE.md requires for RTL testing, matching PlaywrightFixture.
    await call('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 2, mobile: false });

    const evaluate = async (expression) => {
      const { result, exceptionDetails } = await call('Runtime.evaluate', {
        expression,
        returnByValue: true,
        awaitPromise: true,
      });
      if (exceptionDetails) throw new Error(exceptionDetails.exception?.description ?? 'evaluate threw');
      return result.value;
    };

    const waitForRender = async () => {
      const deadline = Date.now() + 20_000;
      while (Date.now() < deadline) {
        const ready = await evaluate('document.body.innerText.trim().length > 0');
        if (ready) return;
        await new Promise((r) => setTimeout(r, 250));
      }
      throw new Error('page never rendered content');
    };

    const goto = async (url) => {
      const loaded = new Promise((resolve) => {
        const handler = (event) => {
          const message = JSON.parse(event.data);
          if (message.method === 'Page.loadEventFired') {
            browser.socket.removeEventListener('message', handler);
            resolve();
          }
        };
        browser.socket.addEventListener('message', handler);
      });
      await call('Page.navigate', { url });
      await loaded;
      await waitForRender();
    };

    // Angular signal forms listen for real DOM `input` events; setting `.value` directly does not
    // fire one, so the native setter is invoked explicitly and a bubbling `input` event dispatched —
    // the technique decisions.md D-104 names as already verified for this exact form stack.
    const typeInto = (selector, index, text) => `
      (() => {
        const el = document.querySelectorAll(${JSON.stringify(selector)})[${index}];
        if (!el) return false;
        const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
        setter.call(el, ${JSON.stringify(text)});
        el.dispatchEvent(new Event('input', { bubbles: true }));
        return true;
      })()`;

    const fill = async (selector, index, text) => {
      const ok = await evaluate(typeInto(selector, index, text));
      if (!ok) throw new Error(`no element at ${selector}[${index}]`);
    };

    const clickSubmit = async () => {
      await evaluate(`document.querySelector('button[type="submit"]').click()`);
      await new Promise((r) => setTimeout(r, 400));
      await waitForRender();
    };

    const waitForUrlContains = async (fragment, timeoutMs = 15_000) => {
      const deadline = Date.now() + timeoutMs;
      while (Date.now() < deadline) {
        const url = await evaluate('location.href');
        if (url.includes(fragment)) return url;
        await new Promise((r) => setTimeout(r, 250));
      }
      throw new Error(`URL never contained ${fragment}`);
    };

    const shoot = async (out) => {
      const { data } = await call('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true });
      mkdirSync(path.dirname(path.resolve(out)), { recursive: true });
      writeFileSync(out, Buffer.from(data, 'base64'));
      return path.resolve(out);
    };

    const storageState = () => evaluate(`({
      localStorage: Object.keys(window.localStorage).length,
      sessionStorage: Object.keys(window.sessionStorage).length,
    })`);

    return await body({ evaluate, goto, fill, clickSubmit, waitForUrlContains, shoot, storageState });
  } finally {
    try { browser.socket.close(); } catch { /* dead socket, ignore */ }
    child.kill();
  }
}

// Matches scripts/seed-demo/payload-*.json exactly. If those change, change this too.
const accounts = [
  { label: 'owner', userName: 'owner_demo', password: 'Demo#Owner1', mustChange: false },
  { label: 'hr', userName: 'hend_hr_demo', password: 'Demo#Hr123', mustChange: true, newPassword: 'Demo#Hr123New' },
  { label: 'finance', userName: 'sara_finance_demo', password: 'Demo#Fin123', mustChange: true, newPassword: 'Demo#Fin123New' },
  { label: 'marketing', userName: 'karim_sales_demo', password: 'Demo#Sales123', mustChange: true, newPassword: 'Demo#Sales123New' },
];

async function runOne(outDir, account) {
  return withPage(async ({ evaluate, goto, fill, clickSubmit, waitForUrlContains, shoot, storageState }) => {
    await goto(`${WEB}/`);
    await waitForUrlContains('/sign-in');

    const before = await storageState();

    await fill('input[type="text"]', 0, account.userName);
    await fill('input[type="password"]', 0, account.password);
    await clickSubmit();

    if (account.mustChange) {
      await waitForUrlContains('/change-password');
      await fill('input[type="password"]', 0, account.password);
      await fill('input[type="password"]', 1, account.newPassword);
      await fill('input[type="password"]', 2, account.newPassword);
      await clickSubmit();
    }

    // Give the router a moment to settle on '/' after the redirect.
    const deadline = Date.now() + 15_000;
    while (Date.now() < deadline) {
      const url = await evaluate('location.href');
      if (url.endsWith('/') || url.endsWith('/#')) break;
      await new Promise((r) => setTimeout(r, 250));
    }
    await new Promise((r) => setTimeout(r, 500));

    const after = await storageState();
    const finalUrl = await evaluate('location.href');
    const bodyText = await evaluate('document.body.innerText');

    const out = await shoot(path.join(outDir, `landing-${account.label}.png`));
    console.log(`${account.label}: url=${finalUrl}`);
    console.log(
      `${account.label}: localStorage before=${before.localStorage} after=${after.localStorage}, ` +
        `sessionStorage before=${before.sessionStorage} after=${after.sessionStorage}`,
    );
    console.log(`${account.label}: screenshot -> ${out}`);
    console.log(`${account.label}: body text ->\n${bodyText}\n`);
  });
}

const outDir = process.argv[2] ?? '.';
for (const account of accounts) {
  await runOne(outDir, account);
}
