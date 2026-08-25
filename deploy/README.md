# Staging — setup and operation

Everything needed to stand staging up again from nothing, or to work out why it is unhappy.

**What staging is:** one **Oracle Cloud ARM64 VPS**, running three containers — PostgreSQL 16, the
API, and nginx serving the Angular build. `decisions.md` D-023 and D-076.

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
| `STAGING_URL` | `http://140.x.x.x` | Where the post-deploy smoke check curls `/api/health` |

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
STAGING_ORIGIN=http://<host-or-ip>
STAGING_HTTP_PORT=80
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

**a. The VCN security list or NSG**, in the Oracle console — ingress rule, source `0.0.0.0/0`, TCP,
destination port `80`.

**b. The instance's own iptables.** Oracle's Ubuntu images ship a REJECT rule that blocks inbound
traffic regardless of what the security list says:

```bash
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT
sudo netfilter-persistent save
```

Confirm from a machine that is not the host:

```bash
curl -s http://<host-or-ip>/api/health
```

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
curl -s http://localhost/api/health
```

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
| Smoke check fails, `curl http://localhost/api/health` works on the box | Not an application problem — GitHub cannot reach the host. **Both** firewalls, step 4. |
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

- **No TLS.** Plain HTTP. Not suitable for real data.
- **No backups** of the staging database. The volume `kaff-staging-db` is the only copy.
- **No log shipping.** `docker logs` on the box is the whole story.
- **One environment.** There is no production, and nothing here assumes there will be.
- **No restart policy beyond `unless-stopped`.** A host reboot brings the stack back; a crash loop
  will not page anybody.
