// Serves the built Angular application for the end-to-end job and proxies /api to the running API.
//
// The same shape as src/Web/nginx.conf, which is what the container image uses: static files with an
// SPA fallback, and /api forwarded to the backend. Keeping the two the same means the smoke suite
// exercises the routing the deployed application actually has.

import express from 'express';
import { createProxyMiddleware } from 'http-proxy-middleware';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const port = Number(process.env.PORT ?? 4173);
const apiTarget = process.env.API_TARGET ?? 'http://localhost:5080';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'web-dist');

const app = express();

// pathFilter, NOT app.use('/api', …). Express strips the mount path before the handler sees it, so
// mounting on '/api' forwarded `/api/health` to the API as `/health` — a route that does not exist,
// which the API answers with 401 rather than 404 because authorization runs before routing resolves.
// The SPA then showed the API as unreachable and every smoke assertion failed.
//
// It had never been caught because nothing had ever run this file: locally the suite is pointed at
// the Angular dev server, whose proxy.conf.json preserves the prefix, and in CI the e2e job had
// never got this far. Fixed 2026-08-25, on the first run that reached the tests.
//
// src/Web/nginx.conf must keep the prefix too — the API's routes all begin /api and nothing rewrites
// them at either end.
app.use(createProxyMiddleware({ pathFilter: '/api', target: apiTarget, changeOrigin: true }));
app.use(express.static(root));

// SPA fallback: the Angular router owns every other path.
app.use((_request, response) => {
  response.sendFile(path.join(root, 'index.html'));
});

app.listen(port, () => {
  console.log(`Serving ${root} on http://localhost:${port}, proxying /api to ${apiTarget}`);
});
