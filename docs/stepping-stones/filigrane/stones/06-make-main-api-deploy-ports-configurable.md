# 6. Make Main.Api's deploy host port + SSH port configurable

**Ref:** Workflow PR `#10`

- **Redefined mid-design:** started as "deploy filigrane to the VPS" but the user chose to keep filigrane local and instead close two deploy-config gaps noticed during stone 5
- **Added (Workflow `deploy/`):**
  - `docker-compose.yml`: publishes `127.0.0.1:${WORKFLOW_HOST_PORT:-8080}:8080` (loopback bind + container target 8080 unchanged) so a busy 8080 on the box can be sidestepped
  - `provision.sh`: prompts for `WORKFLOW_HOST_PORT` (default 8080) and `VPS_SSH_PORT` (default 22), saves both to `deploy/.env`, threads `-p`/`-P` through every `ssh`/`scp`, verifies the chosen port
  - `.env.example` + `README.md`: document both vars
  - `deploy/test/compose-port.test.sh`: asserts the published port tracks `WORKFLOW_HOST_PORT` (default 8080)
- **Revealed:** `docker compose config` renders the effective compose file with no daemon - a cheap seam for testing compose templating. The SSH-port path stays untested (no helper refactor, by decision); the user verifies it manually
- **Demo:** `WORKFLOW_HOST_PORT=9090 docker compose -f deploy/docker-compose.yml config` -> `published: "9090"`; re-running `provision.sh` prompts for both new values
- **Test:** `deploy/test/compose-port.test.sh` (passes locally; skips without the compose CLI)
- **Note:** deliberately no SSH-helper refactor, so `provision.sh`'s SSH logic isn't unit-tested
