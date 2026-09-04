# Staging — setup and operation

Everything needed to stand staging up again from nothing, or to work out why it is unhappy.

**What staging is:** one **Oracle Cloud ARM64 VPS**, running three containers — PostgreSQL 16, the
API, and nginx serving the Angular build — with **Caddy in front of them holding 80 and 443 and
terminating TLS**. `decisions.md` D-023, D-076 and **D-115**.

> **⚠️ Changed 2026-09-04: staging is HTTPS and nginx has moved off port 80.** Caddy owns 80 (which
> it needs for the ACME challenge) and 443. The `web` container now publishes **8080, bound to
> `127.0.0.1`**, and that port is not the way in. Three things follow, and none of them is optional:
>
> 1. **`STAGING_ORIGIN` and `STAGING_URL` become `https://`.** A stale `http://` origin is a CORS
>    allowlist that does not match the browser's origin.
> 2. **`Kaff__ForwardedProxyHops` is 2.** Caddy is a second proxy, and every hop appends to
>    `X-Forwarded-For`. Left at 1 the API records the address Caddy reached nginx from on **every
>    audit row, for every user** — decisions.md D-115 §2.
> 3. **Sign-in only starts working now.** The auth cookie is `HttpOnly; Secure` (D-050), and a
>    browser discards a `Secure` cookie arriving over plain `http://<ip>`. Staging over HTTP was not
>    a degraded sign-in; it was a broken one.

**How it is deployed:** every push to `main` runs `.github/workflows/deploy-staging.yml`, which
builds and pushes both images to `ghcr.io`, scps `deploy/docker-compose.staging.yml` to the host, and
runs `docker compose pull && up -d` over SSH.

---

## One-time setup

### 1. GitHub repository variables

`Settings → Secrets and variables → Actions → Variables`

| Name | Example | What it does |
|---|---|---|
| `STAGING_DEPLOY_TARGET` | `/home/ubuntu/erp` | Directory on the host. **Every deploy step is gated on this** — unset means the job warns and succeeds rather than failing |
| `STAGING_URL` | `https://staging.example.com` | Where the post-deploy smoke check curls `/api/health`. **`https://` and a name, not an IP** — Caddy cannot get a certificate for a bare address, and the smoke check follows the same TLS path a user does |

### 2. GitHub repository secrets

`Settings → Secrets and variables → Actions → Secrets`

| Name | What it is |
|---|---|
| `STAGING_HOST` | Host or IP |
| `STAGING_USER` | SSH user, e.g. `ubuntu` |
| `STAGING_SSH_KEY` | **Private** key, whole file including the BEGIN/END lines |

Note the asymmetry in the names — two are `STAGING_*` and the key is `STAGING_SSH_KEY`. That is what
exists in GitHub and the workflow was reconciled to it, not the other way round.

### 3. The host's `.env`

**CI never writes or reads this file, and that is deliberate** — a secret that passes through a
workflow can be printed by any step somebody adds later. Create it beside the compose file, in
`STAGING_DEPLOY_TARGET`:

```bash
cat > /home/ubuntu/erp/.env <<'EOF'
POSTGRES_USER=kaff
POSTGRES_PASSWORD=<a real password, not kaff>
POSTGRES_DB=kaff
JWT_SIGNING_KEY=<64+ random characters, nothing like the development one>
STAGING_ORIGIN=https://<the name Caddy serves>
STAGING_HTTP_PORT=8080
STAGING_HTTP_BIND=127.0.0.1
EOF
chmod 600 /home/ubuntu/erp/.env
```

`JWT_SIGNING_KEY` has **no default and must not be given one**. The compose file declares it as
`${JWT_SIGNING_KEY:?...}` so a missing value stops the stack loudly, for the same reason
`appsettings.json` ships an empty key: a placeholder somebody forgets to replace is worse than a
failure.

`.env.images` sits beside it and **is** written by CI on every deploy, holding the two image
references pinned to the commit SHA. Do not edit it by hand.

### 4. Both firewalls

**This is the step that is easy to get half-right.** Oracle Cloud has two, and opening only the first
leaves the box unreachable with no obvious reason.

**Open 80 and 443, and nothing else.** 80 is Caddy's ACME challenge and its redirect to HTTPS; 443 is
the site. **8080 must stay closed** — it is the plain-HTTP origin behind Caddy, it serves the whole
application unencrypted, and the compose file binds it to `127.0.0.1` so that opening it in a
firewall alone is not enough to expose it. Both of those are guards on the same mistake.

**a. The VCN security list or NSG**, in the Oracle console — ingress rules, source `0.0.0.0/0`, TCP,
destination ports `80` **and** `443`.

**b. The instance's own iptables.** Oracle's Ubuntu images ship a REJECT rule that blocks inbound
traffic regardless of what the security list says:

```bash
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save
```

Confirm from a machine that is not the host:

```bash
curl -s https://<the name Caddy serves>/api/health
```

### 4b. Caddy

Caddy is **not** in `docker-compose.staging.yml`. It holds the host's ports and outlives any single
`docker compose down`, and putting it in the same file would mean a stack restart could drop TLS.
Install it on the host (`apt install caddy`) and give it a `Caddyfile` of exactly this shape:

```caddyfile
staging.example.com {
    reverse_proxy 127.0.0.1:8080
}
```

Two lines, and both matter:

- **The name, not an IP.** Caddy provisions the certificate from it automatically. A bare address
  gets no certificate, and DNS must point at the host before the first start or the challenge fails.
- **`reverse_proxy` sets `X-Forwarded-For` itself**, which is what `Kaff__ForwardedProxyHops: "2"`
  in the compose file is counting. If you replace Caddy with something that does not, that number is
  wrong and the audit trail's IP column goes with it — decisions.md D-115 §2.

### 5. Docker on the host

`docker` and the **compose v2 plugin** — the deploy script runs `docker compose`, not
`docker-compose`. The deploy user must be able to run docker without sudo (`usermod -aG docker
ubuntu`, then reconnect).

---

## Checking it

```bash
ssh <user>@<host>
cd /home/ubuntu/erp
docker ps
curl -s http://127.0.0.1:8080/api/health    # behind Caddy, on the box
curl -s https://<the name Caddy serves>/api/health   # the way a user arrives
```

**Check both.** The first says the containers are healthy; the second says Caddy, DNS and the
certificate are too. Only the second is what a browser does, and only the second carries the `Secure`
auth cookie.

A healthy answer:

```json
{"status":"healthy","databaseReachable":true,"guardsInstalled":true,"missingGuards":[]}
```

**`guardsInstalled: true` is the field that matters**, not `status`. D-033 refuses to start the
application when the PostgreSQL guards are missing, so this is what distinguishes a box where
append-only postings and the non-negative balance rule are actually enforced from a container that
merely stayed up. `missingGuards` names any that are absent.

---

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `no matching manifest for linux/arm64/v8` | The images were built amd64-only. Both Dockerfiles cross-compile via `$BUILDPLATFORM`/`$TARGETARCH` and the workflow requests `linux/amd64,linux/arm64` — check both are still present. D-076. |
| Deploy job succeeds but nothing changed on the host | `STAGING_DEPLOY_TARGET` is unset. Every deploy step is gated on it and the job warns rather than failing, by design. |
| `JWT_SIGNING_KEY must be set` from `docker compose` | No `.env`, or the key is missing from it. Step 3. |
| `docker compose: unknown command` | Compose v1 on the host. Install the v2 plugin. |
| Smoke check fails, `curl http://127.0.0.1:8080/api/health` works on the box | Not an application problem — something between GitHub and nginx. **Both** firewalls (step 4, and 443 as well as 80 now), then Caddy: `systemctl status caddy`, `journalctl -u caddy`. |
| Signing in appears to succeed and the next request is a `401` | The browser discarded the auth cookie. It is `HttpOnly; Secure` (D-050), so it is dropped on plain `http://<ip>` — **reach staging by its HTTPS name.** This is why staging was moved behind Caddy. |
| Every audit row carries the same IP address | `Kaff__ForwardedProxyHops` does not match the number of proxies actually in front. Two (Caddy, nginx) is what the compose file declares; a third, or Caddy removed, changes it. D-115 §2. |
| Caddy will not start, or serves its default page | DNS is not pointing at the host yet, or another process holds 80 — `ss -lntp | grep -E ':(80|443)\b'`. The ACME challenge needs 80 reachable from the internet. |
| Web serves but every status row shows an error | nginx cannot reach the API. `KAFF_API_URL` must end in `/api/` — nginx substitutes the matched `location /api/` prefix with this URI, so dropping it forwards `/api/health` as `/health`, which the API answers **401, not 404**. |
| `denied` pulling from ghcr.io | The package is private. Either make it public, or `docker login ghcr.io` on the host with a PAT holding `read:packages`. |
| Deploy is green and staging still runs the old build | Images are pinned to the commit SHA, so this should not happen — check `.env.images` matches the SHA you expect. |

---

## Rollback

The compose file is versioned, and images are tagged with their commit SHA:

```bash
cd /home/ubuntu/erp
echo "REGISTRY_IMAGE_API=ghcr.io/ahmednabil30/erp/api:<good-sha>" > .env.images
echo "REGISTRY_IMAGE_WEB=ghcr.io/ahmednabil30/erp/web:<good-sha>" >> .env.images
docker compose --env-file .env --env-file .env.images -f docker-compose.staging.yml up -d
```

Or re-run the workflow from the commit you want, which does the same thing and leaves a record.

---

## What staging does not have

Named so nobody assumes otherwise. Each is a decision, not an oversight, and none is required by
anything yet.

- ~~**No TLS.** Plain HTTP. Not suitable for real data.~~ **TLS since 2026-09-04**, terminated by
  Caddy on 443 with an automatically provisioned certificate. **This does not make staging suitable
  for real data** — the two reasons below still stand, and the encryption changed how it is reached,
  not what is behind it.
- **No backups** of the staging database. The volume `kaff-staging-db` is the only copy.
- **No log shipping.** `docker logs` on the box is the whole story.
- **One environment.** There is no production, and nothing here assumes there will be.
- **No restart policy beyond `unless-stopped`.** A host reboot brings the stack back; a crash loop
  will not page anybody.
