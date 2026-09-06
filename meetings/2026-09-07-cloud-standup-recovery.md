# Recovery — five cloud standups ran, none could push, and what was salvaged

**2026-09-07 · Scrum Master, on Nabil's machine.** Nabil: *"so all the meetings works is gone cant
you redo it or no?"*

---

## What happened

The 5-hourly cloud routine (`trig_01XetV1e9vKwszz9sFvb7WWz`) fired **five times** on 2026-09-06 —
01:55, 06:55, 11:55, 16:55, 21:55 UTC. Every run completed its six steps. **Not one could push.**

```
remote: Claude doesn't have GitHub access to AhmedNabil30/ERP for your organization.
fatal: unable to access 'https://github.com/AhmedNabil30/ERP/' … 403
```

The 01h run established this was **not** branch protection, by exhausting every write route:

| Route | Result |
|---|---|
| `git push` → `main` | 403, org access denied |
| `git push` → a new branch | 403, **identical** — so not branch protection |
| MCP contents API (`create_or_update_file`) | 403 *Resource not accessible by integration* |
| MCP git refs API (`create_branch`) | 403 *Resource not accessible by integration* |
| Any **read** | ✅ succeeds |

**A read-only Claude GitHub App install.** `persist_session: false`, so each container is discarded
at the end of its run. Roughly **100 minutes of Opus across five runs, and `origin/main` never moved
off `847ddb4`.**

**The routine has been disabled.** Re-enable it after the App is granted write access:
https://github.com/apps/claude/installations/select_target — or reconnect GitHub at
claude.ai → Settings → Connectors.

---

## What was recovered, and how

**The commits are gone.** The containers are ephemeral and there is no way back into them.

**The findings are not gone.** `RemoteTrigger get_run_log` retains each run's transcript, and the
runs also used `SendUserFile` to hand Nabil copies in-session before dying:

| Artefact | Route out | Status |
|---|---|---|
| `meetings/2026-09-06-standup-01h.md` | file, delivered in-session | recoverable from the cloud session |
| `stories/…/KAFF-127-user-management-screens.md` — **`AC-127-J`…`AC-127-Q`, rules 11–15** | file, delivered | recoverable from the cloud session |
| `stories/questions-for-karim.md` — **Q58** | file, delivered | recoverable from the cloud session |
| `meetings/2026-09-06-standup-21h.md` | file, delivered | recoverable from the cloud session |
| **QA's 30 test cases, `TC-1-264`…`TC-1-293`** | ⛔ **none** — committed as `f350064`, never sent as a file | **lost. Must be rewritten.** |

**The one real loss is the 30 QA cases** — 536 insertions into `qa/slice-1/test-cases.md`, closing
`V-34-J` for KAFF-125/126/127/128. That was the first test-case coverage the frontend lane has ever
had. It has to be redone from scratch.

---

## Findings salvaged from the logs and re-derived here

**Nothing below is taken on the runs' word.** Each was checked against the files today, which is the
rule the runs themselves were held to.

### 1. ⛔ Three citations in merged code point at the wrong ruling — **repaired here**

`D-055`'s subsections are **§2 = *"Q42 · `UserRead` — HR may see who exists, and nothing more"***
and **§3 = *"N10 approved · `ProjectCreate` splits from `ProjectManage`"***. Three strings discuss
HR's *names and roles only* grant while citing **§3**:

- `src/Api/Features/Users/ListUsers/Endpoint.cs`
- `tests/Api.Tests/ListUsersTests.cs` — two places

**Corrected to §2.** Build 0/0 `-warnaserror`, `dotnet format` clean.

**How it arose is the part worth keeping.** A BA subagent corrected the Scrum Master's own brief on
this; the cloud Scrum Master then re-derived it from `decisions.md` rather than accepting the
correction on trust, and so did this session. **A correction is a claim like any other.**

### 2. ⛔ `AC-125-C` was false the day it shipped, and passed anyway

The criterion says *"no project or assignment is shown"*. But:

```
git log --oneline -S "session.projects" -- src/Web/src/app/features/landing/landing-page.html
7461332 KAFF-125: the staff shell — S-004's dispatch, chrome, and role-based landing
```

**One commit — KAFF-125's own build.** The landing page has rendered `session.projects` since the
day the story shipped. This is **not** a lapse; nothing moved underneath the verdict. The criterion
and the code disagreed at the moment of the verdict and the verdict was given anyway.

⚠️ **QA reported this as "a second, distinct lapse."** It is not, and recording QA's wording would
have been **D-122's error repeated** — a conclusion from reading code instead of from a diff. The
cloud Scrum Master caught it before it entered the record. **Routed to BA as a criterion defect.**

### 3. Items 1 and 3 of *"What is actually next"* cannot be pulled — they have no story files

`Get-ChildItem stories -Recurse -Filter '*N11*'` → **0**. `KAFF-3*` → **0**.

**N11** and **KAFF-300** are named in `STATUS.md` as the next two things to do, and **neither exists
as a story.** They carry no trailer, so they are invisible to `tools/status.ps1` and cannot be
dispatched. This is the same defect as `V-34-A` — work described in prose that no artefact holds.

### 4. Seven of nine gates cannot run in the cloud container

No .NET SDK, and `builds.dotnet.microsoft.com` is blocked by egress policy so it cannot be
installed. No Docker daemon. No `pwsh`. Node is one patch below what the Angular CLI accepts —
both runs that measured anything had to fetch Node 22/24 into the scratchpad first.

**Measurable there:** `ng build` and vitest. **Not measurable:** build, format, Domain.Tests,
Api.Tests, E2E. The runs reported these as **unknown, not passing**, which is correct.

**Consequence: a cloud standup cannot certify the gates.** Any figure it gives for the .NET suites
is repeated, not derived — and repeating a figure is the thing the whole brief forbids.

---

## What I did not do

1. **Did not rewrite the 30 QA cases.** That is QA's job and it is the largest single piece of lost
   work.
2. **Did not fetch the delivered files** from the cloud sessions — they are in Nabil's session view
   at claude.ai, not reachable from this machine.
3. **Did not write N11's or KAFF-300's story file.** N11 is the Architect's and the BA's, and its
   retention number is **Q54 — Karim's, still unanswered.**
4. **Did not fix `AC-125-C`.** Routed to BA; a criterion is theirs to change, not mine.
5. **Did not re-run the .NET test suites** — only build and format, which is what the citation change
   could affect. Domain.Tests and Api.Tests are unverified since `947c22c`.
6. **Did not certify any of this.** I made the citation repair, so a Verifier decides whether it
   holds.
