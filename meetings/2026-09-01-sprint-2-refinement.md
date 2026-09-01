# Sprint 2 — refinement · 2026-09-01

**Scrum Master.** Slice 1 remainder into slice 2. `agents.md` §3b, `process/agile.md` ceremony 1.

**This ceremony was owed.** The sprint-1 close recorded it as a debt in its own words — *"No
refinement ceremony — no agent was asked 'what do you not know?' — one is owed before stories are
pulled."* Sprint 2 then executed for two days with no recorded scope. This is the ceremony, and it
runs before stories are pulled, not after.

**Nabil's instruction opening this run:** *"i fixed the staging and now working fine lets go to
sprint 2."*

**What this meeting produces:** a staging verdict established from the pipeline rather than from the
message; every agent's answer to *"what do you not know?"*, sorted into three buckets; a Definition
of Ready verdict per candidate story; and a **proposed** sprint 2 commitment. **Nabil locks scope.
Nothing here is a commitment.**

---

## 1. Staging — verified, not taken on trust

Nabil says staging is fixed and working. That is a claim about a machine, and on this project a claim
is not a verification. **D-096 exists because an acceptance was a claim about a tree that had moved.**

**Two claims, and the sprint-1 close was careful to keep them apart** — `meetings/2026-08-27-sprint-1-close.md`
§4: *"The application runs there. The pipeline cannot see it."*

| | Claim | Who establishes it |
|---|---|---|
| **a** | The application runs on staging | Nabil, on the box. He has almost certainly done this |
| **b** | **The CI smoke check reaches it and passes on its own** | The pipeline. **This is the Definition of Done line** |

Only **b** is tickable, and only the pipeline can tick it. So I asked the pipeline.

### The measurement

`.github/workflows/deploy-staging.yml` -> `Smoke check` curls `${{ vars.STAGING_URL }}/api/health`
from a GitHub runner — that is, from **outside both of the Oracle firewalls** `deploy/README.md` §4
describes — and greps for `"guardsInstalled":true`, retrying 30 times at 10-second intervals before
failing.

**The same commit, `dc76fe7`, which is HEAD, ran that check twice:**

| Attempt | When | Smoke check | Duration |
|---|---|---|---|
| 1 | 2026-08-30 04:56:47Z → 05:02:01Z | **failure** | 5m 14s — the full retry loop exhausted |
| 2 | 2026-08-30 22:40:59Z → 22:41:10Z | **success** | **11s** — it answered on the first or second curl |

**Nothing in the tree changed between them.** Same SHA, same workflow, same steps. The change was on
the machine, which is exactly what Nabil said he did.

**And the step genuinely ran rather than being gated away.** `Smoke check` carries
`if: vars.STAGING_URL != ''`; a false condition reports the step as `skipped` in zero seconds, not as
`success` in eleven. Attempt 1 closes this off completely: **the same step with the same condition
reported `failure`**, which an unset `STAGING_URL` could not have produced. The variable is set and
the check is live.

### What the passing check actually proves

More than it looks like, because of how staging is wired. In `deploy/docker-compose.staging.yml`,
**only the `web` service publishes a port**; `api` and `db` are `expose` only, reachable on the
internal network alone. So one external 200 carrying `guardsInstalled: true` proves the whole chain:

- a GitHub runner reached the box on port 80 — **both Oracle firewalls are open**, the VCN security
  list and the instance's own iptables REJECT that `deploy/README.md` §4 warns is *"the step that is
  easy to get half-right"*;
- nginx served, and its `KAFF_API_URL` prefix is right, or `/api/health` would arrive at the API as
  `/health` and answer 401;
- the API reached PostgreSQL — `databaseReachable`;
- **D-033's database guards are installed**, which is the field that matters. Without them the
  append-only and non-negative-balance rules are absent and a healthy-looking stack reports a safety
  it does not have.

### What it does not prove, and this is where the per-story answer comes from

**The CI smoke check is a curl against `/api/health`. It never fetches the SPA.** It says nothing
about a screen rendering, nothing about direction being RTL, and nothing about the text being Arabic.
Those are assertions of the eight-check browser smoke in `/run-kaff-erp`, which runs against a
**local** stack, not against staging.

So *"runs on staging"* becomes tickable **per story, by surface**:

| Story | Surface | *Runs on staging* | Why |
|---|---|---|---|
| KAFF-100, 101a, 102, 103, 105a, 106, 108, 109, 110, 111, 112, 113, 114, 116 | API only | ✅ **tickable** | Their surface is the API, and the API is proven reachable and healthy from outside, with guards installed, at HEAD |
| **KAFF-101b** (`f2b995b`) | The sign-in **screen** | ⬜ **not tickable** | The web image is built, pushed and deployed — `Pull and restart` succeeded — but **nothing fetches the page**. Deployed is not the same as rendering |
| **KAFF-103's screen** (`332c160`) | The change-password **screen** | ⬜ **not tickable** | Same reason |

**The honest summary: the line moved from "the pipeline cannot see it" to "the pipeline sees the API."**
That is most of the way, and it is the half that had been blocking every backend story. It is not the
whole line, and I am not ticking the frontend half on the strength of a deploy step that copies files.

### Routed

**The gap is small and it is one step, not a project.** A second smoke assertion — fetch `${STAGING_URL}/`
and check the served document carries `dir="rtl"` and `lang="ar"` — would close the frontend half of
this line for every screen from here on. It is a change to a workflow file and it is **Backend's**,
being CI rather than `src/Web/`. **Not done in this run and not smuggled into the sprint;** raised
here so the next session does not have to rediscover why two of the rows above are unticked.

### The Definition of Done statement is updated rather than left stale

`meetings/2026-08-27-sprint-1-close.md` §4 says of this line: *"⬜ The application runs there. The
pipeline cannot see it."* **That was true when written and is now false.** It is corrected in place,
loudly, per SM-29's own practice — the correction names what changed, what it does not cover, and the
date. Amended in this run.

### One brief correction of my own, under SM-31

The brief opening this session said `scripts/check-citations.ps1` stands at **960 checked**. Re-run
today, it reports **969 checked, 0 broken, 0 legacy**. The floor this ceremony must not regress below
is 969, not 960. Small, and recorded because a figure carried forward unchecked is how the other two
wrong facts in Scrum Master briefs got as far as they did (D-096 §4).
