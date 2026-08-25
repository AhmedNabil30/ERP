#!/usr/bin/env node
// Kaff ERP driver — drives the running stack from a script, for agents.
//
// Zero dependencies on purpose. It speaks the Chrome DevTools Protocol over Node's global WebSocket
// (Node 18+) to the chromium Playwright already downloaded for tests/E2E.Tests, so there is nothing
// to npm install and no second browser on disk.
//
// It does NOT start the API or the web server. Those are long-running and belong to the agent's own
// process management; this drives what is already up. See SKILL.md.
//
// Usage:
//   node .claude/skills/run-kaff-erp/driver.mjs health
//   node .claude/skills/run-kaff-erp/driver.mjs api GET /api/health
//   node .claude/skills/run-kaff-erp/driver.mjs shot http://localhost:4200/ out.png
//   node .claude/skills/run-kaff-erp/driver.mjs eval http://localhost:4200/ "document.title"
//   node .claude/skills/run-kaff-erp/driver.mjs smoke

import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import { homedir } from 'node:os';
import path from 'node:path';

const API = process.env.KAFF_API ?? 'http://localhost:5080';
const WEB = process.env.KAFF_WEB ?? 'http://localhost:4200';

// ---------------------------------------------------------------- chromium

/**
 * Playwright's browser cache, which the .NET E2E project populates via
 * `pwsh tests/E2E.Tests/bin/Release/net10.0/playwright.ps1 install chromium`.
 * Set CHROME to override.
 */
function findChrome() {
  if (process.env.CHROME) return process.env.CHROME;

  const roots = {
    win32: path.join(homedir(), 'AppData', 'Local', 'ms-playwright'),
    darwin: path.join(homedir(), 'Library', 'Caches', 'ms-playwright'),
    linux: path.join(homedir(), '.cache', 'ms-playwright'),
  };
  const root = roots[process.platform];
  if (!root || !existsSync(root)) {
    throw new Error(
      `No Playwright browser cache at ${root}. Install one with:\n` +
        '  pwsh tests/E2E.Tests/bin/Release/net10.0/playwright.ps1 install chromium\n' +
        'or point CHROME at any Chrome/Chromium binary.',
    );
  }

  // Prefer the full chromium over chromium_headless_shell: it is the build tests/E2E.Tests drives,
  // so a difference between this driver and the E2E suite is one fewer thing to explain.
  // (The headless shell was tried on 2026-08-22 and rendered Arabic correctly on Windows too —
  // CHROME=...chrome-headless-shell.exe works if you want the smaller binary.)
  const dirs = readdirSync(root)
    .filter((d) => d.startsWith('chromium-'))
    .sort()
    .reverse();

  for (const dir of dirs) {
    for (const rel of [
      ['chrome-win64', 'chrome.exe'],
      ['chrome-linux', 'chrome'],
      ['chrome-mac', 'Chromium.app', 'Contents', 'MacOS', 'Chromium'],
    ]) {
      const candidate = path.join(root, dir, ...rel);
      if (existsSync(candidate)) return candidate;
    }
  }
  throw new Error(`Found ${root} but no chromium binary inside it.`);
}

/** Launches headless chromium and resolves the browser-level CDP endpoint it prints on stderr. */
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
      // Kaff renders Arabic; without a stable locale the digit shaping in screenshots varies by host.
      '--lang=ar-EG',
      'about:blank',
    ],
    { stdio: ['ignore', 'ignore', 'pipe'] },
  );

  return new Promise((resolve, reject) => {
    let buffered = '';
    const timer = setTimeout(
      () => reject(new Error(`chromium printed no DevTools endpoint in 30s. stderr:\n${buffered}`)),
      30_000,
    );

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
      reject(new Error(`chromium exited with ${code} before listening. stderr:\n${buffered}`));
    });
  });
}

/** A minimal CDP client: send a command, await its reply; subscribe to events by name. */
class Cdp {
  constructor(socket) {
    this.socket = socket;
    this.nextId = 1;
    this.pending = new Map();
    this.listeners = new Map();

    socket.addEventListener('message', (event) => {
      const message = JSON.parse(event.data);
      if (message.id !== undefined) {
        const slot = this.pending.get(message.id);
        if (!slot) return;
        this.pending.delete(message.id);
        message.error ? slot.reject(new Error(JSON.stringify(message.error))) : slot.resolve(message.result);
        return;
      }
      for (const handler of this.listeners.get(message.method) ?? []) handler(message.params);
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

  on(method, handler) {
    if (!this.listeners.has(method)) this.listeners.set(method, []);
    this.listeners.get(method).push(handler);
  }

  once(method, predicate = () => true) {
    return new Promise((resolve) => this.on(method, (params) => predicate(params) && resolve(params)));
  }
}

/**
 * Opens `url` in a fresh tab, waits for the SPA to settle, and hands the page to `body`.
 *
 * "Settled" is Page.loadEventFired plus a poll for Angular having rendered something into the app
 * root. loadEventFired alone fires before the lazy route chunk has executed, so a screenshot taken
 * on it catches a blank page. See SKILL.md Gotchas.
 */
async function withPage(url, body, { width = 1280, height = 900 } = {}) {
  const { child, browserWs } = await launchChrome();
  const browser = await Cdp.connect(browserWs);
  try {
    const { targetId } = await browser.send('Target.createTarget', { url: 'about:blank' });
    const { sessionId } = await browser.send('Target.attachToTarget', { targetId, flatten: true });

    const call = (method, params) => browser.send(method, params, sessionId);

    await call('Page.enable');
    await call('Runtime.enable');
    await call('Emulation.setDeviceMetricsOverride', {
      width,
      height,
      deviceScaleFactor: 1,
      mobile: false,
    });

    const loaded = browser.once('Page.loadEventFired');
    await call('Page.navigate', { url });
    await loaded;

    const evaluate = async (expression) => {
      const { result, exceptionDetails } = await call('Runtime.evaluate', {
        expression,
        returnByValue: true,
        awaitPromise: true,
      });
      if (exceptionDetails) throw new Error(exceptionDetails.exception?.description ?? 'evaluate threw');
      return result.value;
    };

    // Angular is zoneless and the first route is lazy: wait for real content, not just load.
    const deadline = Date.now() + 20_000;
    while (Date.now() < deadline) {
      const ready = await evaluate('document.body.innerText.trim().length > 0');
      if (ready) break;
      await new Promise((r) => setTimeout(r, 250));
    }

    /**
     * Clicks the first element whose trimmed text equals `text`, or that matches it as a CSS
     * selector. Text first because the UI is Arabic and the visible label is the stable thing —
     * CLAUDE.md forbids hardcoded strings, so class names change more often than i18n output.
     *
     * Dispatched through element.click() rather than Input.dispatchMouseEvent: coordinates are the
     * wrong tool under RTL, where the visual and logical order of a row differ.
     */
    const click = async (text) => {
      const clicked = await evaluate(`(() => {
        const wanted = ${JSON.stringify(text)};
        const all = Array.from(document.querySelectorAll('button, a, [role="button"]'));
        const target = all.find((e) => e.innerText.trim() === wanted)
          ?? document.querySelector(wanted);
        if (!target) return false;
        target.click();
        return true;
      })()`);
      if (!clicked) throw new Error(`nothing to click matching ${JSON.stringify(text)}`);
      // Zoneless Angular: signals flush on the microtask queue, so one frame is enough.
      await evaluate('new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)))');
    };

    const shoot = async (out) => {
      const { data } = await call('Page.captureScreenshot', { format: 'png' });
      mkdirSync(path.dirname(path.resolve(out)), { recursive: true });
      writeFileSync(out, Buffer.from(data, 'base64'));
      return path.resolve(out);
    };

    return await body({ call, evaluate, click, shoot });
  } finally {
    try {
      browser.socket.close();
    } catch {
      /* closing a dead socket is not interesting */
    }
    child.kill();
  }
}

// ---------------------------------------------------------------- commands

async function apiRequest(method, urlPath, body) {
  const response = await fetch(`${API}${urlPath}`, {
    method,
    headers: body ? { 'content-type': 'application/json' } : undefined,
    body,
  });
  const text = await response.text();
  return { status: response.status, body: text };
}

async function screenshot(url, out) {
  return withPage(url, async ({ call }) => {
    const { data } = await call('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true });
    mkdirSync(path.dirname(path.resolve(out)), { recursive: true });
    writeFileSync(out, Buffer.from(data, 'base64'));
    return path.resolve(out);
  });
}

/** The end-to-end check: API healthy, guards installed, and the SPA rendering real content. */
async function smoke() {
  const failures = [];
  const say = (ok, label, detail) => {
    console.log(`${ok ? 'PASS' : 'FAIL'}  ${label}${detail ? `  — ${detail}` : ''}`);
    if (!ok) failures.push(label);
  };

  const health = await apiRequest('GET', '/api/health');
  say(health.status === 200, 'API /api/health returns 200', `got ${health.status}`);

  let parsed = {};
  try {
    parsed = JSON.parse(health.body);
  } catch {
    say(false, 'health body is JSON', health.body.slice(0, 120));
  }
  say(parsed.status === 'healthy', 'health reports healthy', parsed.status);
  say(parsed.databaseReachable === true, 'database reachable', String(parsed.databaseReachable));

  // Not decoration. decisions.md D-033: the application refuses to start when the database guards
  // are missing, because the append-only and non-negative-balance rules live in PostgreSQL. A stack
  // that is "up" without them is reporting a safety it does not have.
  say(parsed.guardsInstalled === true, 'database guards installed', JSON.stringify(parsed.missingGuards ?? []));

  const page = await withPage(WEB, async ({ evaluate }) => ({
    title: await evaluate('document.title'),
    dir: await evaluate('document.documentElement.getAttribute("dir")'),
    lang: await evaluate('document.documentElement.getAttribute("lang")'),
    text: await evaluate('document.body.innerText.trim()'),
    mounted: await evaluate('document.querySelector("kaff-root") !== null'),
  }));

  // This check is the reason the other three can be trusted. Chromium's own "site can't be reached"
  // page is served in the browser's UI locale — which here is Arabic, RTL, and non-empty — so it
  // passes "renders content", "direction is RTL" and "contains Arabic text" while the application is
  // not running at all. Caught 2026-08-25 against a dev server that had not finished building yet.
  // kaff-root is ours (src/Web/src/index.html), and no error page has one.
  say(page.mounted === true, 'the Angular application mounted', `kaff-root present=${page.mounted}`);

  say(page.text.length > 0, 'SPA renders content', `${page.text.length} chars`);
  say(page.dir === 'rtl', 'document direction is RTL', `dir=${page.dir}`);
  // CLAUDE.md: "The UI is Arabic." A page of Latin text means i18n did not resolve.
  say(/[؀-ۿ]/.test(page.text), 'page contains Arabic text', page.text.slice(0, 60));

  console.log(`\ntitle=${JSON.stringify(page.title)} lang=${page.lang} dir=${page.dir}`);
  console.log(failures.length === 0 ? '\nAll checks passed.' : `\n${failures.length} check(s) failed.`);
  return failures.length === 0 ? 0 : 1;
}

/**
 * The one user flow slice 0 has: switch the UI language and watch the document direction follow.
 *
 * Worth having as a flow rather than a unit test because RTL is the primary direction here, not a
 * mirror (CLAUDE.md), and the failure it catches — direction not following the locale — is only
 * visible in a rendered document.
 */
async function flow(outDir) {
  return withPage(WEB, async ({ evaluate, click, shoot }) => {
    const before = {
      dir: await evaluate('document.documentElement.getAttribute("dir")'),
      lang: await evaluate('document.documentElement.getAttribute("lang")'),
      shot: await shoot(path.join(outDir, 'status-ar.png')),
    };

    await click('English');

    const after = {
      dir: await evaluate('document.documentElement.getAttribute("dir")'),
      lang: await evaluate('document.documentElement.getAttribute("lang")'),
      text: await evaluate('document.body.innerText.trim()'),
      shot: await shoot(path.join(outDir, 'status-en.png')),
    };

    console.log(`before  lang=${before.lang} dir=${before.dir}  ${before.shot}`);
    console.log(`after   lang=${after.lang} dir=${after.dir}  ${after.shot}`);

    const ok = before.dir === 'rtl' && after.dir === 'ltr' && after.lang === 'en';
    console.log(ok ? '\nPASS  language switch flips direction' : '\nFAIL  direction did not follow the locale');
    return ok ? 0 : 1;
  });
}

// ---------------------------------------------------------------- entry

const [command, ...args] = process.argv.slice(2);

try {
  switch (command) {
    case 'health': {
      const result = await apiRequest('GET', '/api/health');
      console.log(result.status, result.body);
      process.exit(result.status === 200 ? 0 : 1);
      break;
    }
    case 'api': {
      const [method, urlPath, body] = args;
      if (!method || !urlPath) throw new Error('usage: api <METHOD> <path> [jsonBody]');
      const result = await apiRequest(method.toUpperCase(), urlPath, body);
      console.log(result.status, result.body);
      process.exit(result.status < 400 ? 0 : 1);
      break;
    }
    case 'shot': {
      const [url, out = 'screenshot.png'] = args;
      console.log(await screenshot(url ?? WEB, out));
      break;
    }
    case 'eval': {
      const [url, expression] = args;
      if (!expression) throw new Error('usage: eval <url> <javascript>');
      console.log(JSON.stringify(await withPage(url, ({ evaluate }) => evaluate(expression)), null, 2));
      break;
    }
    case 'smoke':
      process.exit(await smoke());
      break;
    case 'flow':
      process.exit(await flow(args[0] ?? '.'));
      break;
    default:
      console.log(
        'commands:\n' +
          '  health                      GET /api/health\n' +
          '  api <METHOD> <path> [json]  any API call\n' +
          '  shot <url> <out.png>        screenshot a page\n' +
          '  eval <url> <js>             evaluate JavaScript in the page\n' +
          '  smoke                       API + guards + app mounted + SPA render + RTL + Arabic\n' +
          '  flow <outDir>               language switch, two screenshots, asserts dir flips\n' +
          `\nKAFF_API=${API}  KAFF_WEB=${WEB}  (override with env vars)`,
      );
      process.exit(command ? 1 : 0);
  }
} catch (error) {
  console.error(`driver: ${error.message}`);
  process.exit(1);
}
