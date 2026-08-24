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

app.use('/api', createProxyMiddleware({ target: apiTarget, changeOrigin: true }));
app.use(express.static(root));

// SPA fallback: the Angular router owns every other path.
app.use((_request, response) => {
  response.sendFile(path.join(root, 'index.html'));
});

app.listen(port, () => {
  console.log(`Serving ${root} on http://localhost:${port}, proxying /api to ${apiTarget}`);
});
