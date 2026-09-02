# PR walkthrough: Stone 6 — make Main.Api host port and SSH port configurable

**What this PR does:** two values in Main.Api's `deploy/` were hardcoded — the host port the
container publishes (`8080`) and the SSH port `provision.sh` uses (`22`). This makes both
configurable via `deploy/.env` (`WORKFLOW_HOST_PORT`, `VPS_SSH_PORT`), so a busy 8080 or a
non-standard SSH port no longer blocks a deploy. filigrane stays local; this is a deploy-config stone.

**Where to start:** `deploy/docker-compose.yml`. The `${WORKFLOW_HOST_PORT:-8080}` change is the
load-bearing one — it defines the variable the wizard feeds and the test pins. The SSH-port change is
a parallel, self-contained plumbing thread through `provision.sh`.

## Reading path

1. `deploy/docker-compose.yml` — the core. `- "127.0.0.1:8080:8080"` becomes
   `- "127.0.0.1:${WORKFLOW_HOST_PORT:-8080}:8080"`. Only the host port varies; the loopback bind and
   the container target `8080` stay fixed.
2. `deploy/provision.sh` (Stage 3, Deploy) — prompts for `WORKFLOW_HOST_PORT`, defaults it to 8080,
   and `write_env`s it **before** the `scp` that ships `.env` to the VPS, so the remote
   `docker compose` sees it. Produces the value step 1 consumes.
3. `deploy/provision.sh` (Stage 1 + every `ssh`/`scp`) — the SSH-port thread: a `VPS_SSH_PORT` prompt
   (default 22) and `-p`/`-P "$VPS_SSH_PORT"` threaded through the connectivity check, docker check,
   clone, `scp`, compose-up. Independent of the port thread.
4. `deploy/provision.sh` (Stage 4, Verify) — curls `127.0.0.1:${WORKFLOW_HOST_PORT}` over
   `ssh -p "$VPS_SSH_PORT"`, tying both threads together in the health check.
5. `deploy/test/compose-port.test.sh` — pins step 1: renders via `docker compose config` (no daemon)
   and asserts the published port tracks `WORKFLOW_HOST_PORT`, defaults to 8080, and keeps the
   loopback bind + container target.
6. `deploy/.env.example` + `deploy/README.md` — document both vars.

## Groups

### Core logic

- `deploy/docker-compose.yml` — the single `${WORKFLOW_HOST_PORT:-8080}` substitution. Everything else
  supports or documents it.

### Wiring

- `deploy/provision.sh` — two prompts, two `.env` writes, `-p`/`-P` on five `ssh`/`scp` calls, and the
  verify-stage port/host interpolation.

### Tests

- `deploy/test/compose-port.test.sh` — `docker compose config` render assertions; skips cleanly
  without the compose CLI. Passes locally.

### Docs

- `deploy/.env.example` — adds `WORKFLOW_HOST_PORT=8080` and `VPS_SSH_PORT=22` with comments.
- `deploy/README.md` — mentions both in the intro, guided-setup, manual-steps, and files sections.

## Read carefully

- `deploy/provision.sh:238` — `write_env WORKFLOW_HOST_PORT` runs **before** the `scp "$ENV_FILE" ...`
  in the same stage. That ordering is load-bearing: the remote `docker compose --env-file .env` reads
  the value from the copied `.env`, so writing it after the copy would silently deploy on 8080.
- `deploy/provision.sh` — the defaults (`VPS_SSH_PORT="${VPS_SSH_PORT:-22}"`,
  `WORKFLOW_HOST_PORT="${WORKFLOW_HOST_PORT:-8080}"`) are applied right after `ask`, because `ask`
  returns empty on a first run when the user just presses Enter. Without them the `ssh -p ""` /
  `published: ""` would break.
- Scope: the SSH-port path has **no automated test** (no `remote()` helper refactor, by decision), so
  it's exercised only by the compose-port test's sibling — i.e. not at all. Verified manually against
  a real VPS.

## How to test

- **Automated (no Docker daemon):** `bash deploy/test/compose-port.test.sh` — passes locally; asserts
  the published port tracks `WORKFLOW_HOST_PORT`, defaults to 8080, keeps the loopback bind + target.
- **Demo the render:** `WORKFLOW_HOST_PORT=9090 docker compose -f deploy/docker-compose.yml config`
  → `published: "9090"`; unset it → `published: "8080"`.
- **Exercise the wizard:** re-run `./deploy/provision.sh` — it prompts for `VPS_SSH_PORT` (Stage 1)
  and `WORKFLOW_HOST_PORT` (Stage 3) and writes both to `deploy/.env`.
- **SSH port (manual only):** point `provision.sh` at a VPS whose SSH listens on a non-22 port and
  confirm the connectivity check, clone, `scp`, compose-up, and verify all succeed over `-p/-P`.
