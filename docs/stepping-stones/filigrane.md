# Filigrane: watermark + anonymize (backend in Workflow)

**Status:** active

## Target

First guess:
- Move filigrane's PDF watermarking backend into Workflow
- Add anonymization starting with metadata-only
- Later: support image files, and expose both features through the filigrane frontend (likely renamed)

## Constraints

- Backend lives in `/home/tigrou/Dev/Workflow` (`Main.Api`)
- Endpoints require `X-Api-Key` (Workflow API key auth)
- Keep filigrane's current async token flow
  - `POST /api/watermark` returns `{ token, downloadUrl, expiresAt, expiresInSeconds }`
  - `GET /api/download/{token}` returns the processed file (single use, expiry)
- Commits per stone, implemented in a git worktree, with a PR opened per stone
- Watermarking (and future anonymization) logic is implemented as `ITool<TIn, TOut>` tools under `Infrastructure/Tools/<Name>/`, following the repo's existing convention (see `Infrastructure/Tools/CLAUDE.md`, e.g. `TextTransformers/AITextTransformer.cs`). API controllers are thin: they build the input stream, call the tool, and translate `Result<T>` into an HTTP response. Per-request options are passed into the tool's constructor rather than as method arguments.
- File-type-specific streams (`PdfStream`, `ImageStream`, `AudioStream`) live in `Core/Models/` as thin `Stream` wrappers, so tools can be typed by content kind rather than by raw `Stream`

## Related efforts

None yet

## Stones laid

### 1. PDF watermarking moved into Workflow (tool-based, token download) - `5b9910b` (issue `#4`, PR `#5`)

- **Added:**
  - API (requires `X-Api-Key`)
    - `POST /api/watermark` (multipart/form-data, PDF-only)
    - `GET /api/download/{token}` (single use, expiry)
  - Tool-based implementation following Workflow conventions
    - `Core/Models/PdfStream.cs` (typed stream wrapper)
    - `Core/Models/WatermarkOptions.cs` (shared options contract)
    - `Infrastructure/Tools/Watermarking/PdfWatermarkTool.cs` implementing `ITool<PdfStream, PdfStream>`
  - Token + disk storage + cleanup background service under `Main.Api/Filigrane/Services/`
- **Revealed:** the HTTP contract can stay compatible with filigrane's existing frontend, but the browser will need a BFF/proxy to inject `X-Api-Key`
- **Demo:**
  - Run API: `cd /home/tigrou/Dev/Workflow/Main.Api && API_KEY=devkey dotnet run --launch-profile https`
  - Upload: `curl -k -H "X-Api-Key: devkey" -F "file=@/path/to/input.pdf" -F "watermarkType=Invisible" -F "contentType=Custom" -F "customText=HELLO" https://localhost:7156/api/watermark`
  - Download: `curl -k -H "X-Api-Key: devkey" -L -o out.pdf https://localhost:7156/api/download/<token>`
- **Test:** `Tests/Infrastructure/PdfWatermarkToolTests.cs`

### 2. Dev proxy injects X-Api-Key so the browser never sees it (filigrane repo, issue `#3`, PR `#4`)

- **Added:**
  - `frontend/vite.config.ts` proxies `/api/*` to Workflow's `Main.Api` (`WORKFLOW_API_URL`, default `http://localhost:5261`) and injects `X-Api-Key` from `WORKFLOW_API_KEY` on the proxied request, server-side only
  - `frontend/vite/apiProxy.ts` (extracted, testable proxy config) + `frontend/vite/apiProxy.test.ts`
  - `vitest` as the frontend's first test runner (`vitest.config.ts`, `npm run test`)
- **Revealed:** filigrane's frontend has no tracked `.env.example` (root's own isn't committed either, gitignored by `.env*`) and no README, so env vars for local dev are documented in the PR body rather than in a tracked file
- **Demo:**
  - Terminal 1: `cd /home/tigrou/Dev/Workflow/Main.Api && dotnet run --launch-profile http` (match `API_KEY` from `Main.Api/.env`)
  - Terminal 2: `cd /home/tigrou/Dev/filigrane/frontend && WORKFLOW_API_KEY=<same key> npm run dev`
  - Upload + watermark works end to end in the browser; DevTools Network tab shows no `X-Api-Key` on the browser's own request
- **Test:** `frontend/vite/apiProxy.test.ts`
- **Note:** scoped to local dev only, deliberately; prod (nginx, docker-compose) still points at filigrane's own `api` container and is untouched

### 3. Dockerize and deploy Main.Api behind Caddy, on its own host - `6049f44` (Workflow PR `#8`), filigrane `4fe52de` (PR `#5`)

- **Added:**
  - `Main.Api/Dockerfile` (multi-stage, `sdk:10.0` -> `aspnet:10.0`, build context is the repo root since `Main.Api` references `Core`/`Infrastructure` via relative `ProjectReference`s)
  - `Program.cs`: trusts `X-Forwarded-*` from Caddy so `UseHttpsRedirection` doesn't loop once TLS is terminated upstream on the container network
  - `deploy/docker-compose.yml` + `deploy/Caddyfile`: `workflow-api` + `caddy` (automatic Let's Encrypt cert from just a domain name)
  - `deploy/provision.sh`: guided wizard (DNS, SSH, secrets, deploy, verify) for the VPS-side steps, re-runnable
  - filigrane: deleted `api/` (the old backend) and `filigrane.sln`; `docker-compose.yml` loses the `api` service
- **Revealed:**
  - `Main.Api` and any client must run on separate hosts (confirmed, not assumed): the API serves arbitrary clients with different needs, not just filigrane
  - Deploying filigrane's frontend publicly is blocked on filigrane getting its own user-level auth first - the shared `X-Api-Key` authenticates "this is filigrane," not individual users, so a public frontend with no login would turn `Main.Api` into an unauthenticated public gateway
  - Single-API vs. splitting `Infrastructure` into per-tool-group NuGet packages is a real open question, deliberately not decided now (one consumer, no second real case yet) - logged below rather than acted on
- **Demo:**
  - `./deploy/provision.sh` (Workflow repo) deploys `Main.Api` to the VPS behind Caddy
  - `cd filigrane/frontend && WORKFLOW_API_URL=https://<domain> WORKFLOW_API_KEY=<same key> npm run dev` - watermarking round-trips through the real deployed `Main.Api` over real HTTPS
- **Test:** no new application logic to pin; verified via `dotnet publish`/`dotnet build` (Docker daemon unavailable in the build sandbox - `docker build -f Main.Api/Dockerfile .` still worth a manual run)
- **Note:** filigrane's *production* `nginx.conf`/`docker-compose.yml` deliberately left unwired to the deployed `Main.Api` - deferred to the stone that also adds filigrane's own user auth

### 4. Drop Caddy, ship Main.Api standalone for the operator's own reverse proxy - `ea01ce7` (PR `#9`)

- **Added:**
  - `deploy/docker-compose.yml` now only runs `workflow-api`, publishing `127.0.0.1:8080` (loopback only) instead of bundling a `caddy` service
  - `deploy/provision.sh`: dropped the DNS/domain stage (5 -> 4 stages); verify stage curls `127.0.0.1:8080` over SSH instead of a public HTTPS domain
  - `deploy/README.md`, `deploy/.env.example`: dropped `DOMAIN`; documented that fronting the API (reverse proxy, TLS) is the hosting operator's decision, not this repo's
- **Removed:** `deploy/Caddyfile`, the `caddy` service and its `caddy-data`/`caddy-config` volumes
- **Revealed:** stone 3's "Main.Api and any client must run on separate hosts" insight didn't go far enough - the reverse proxy in front of the API is also the hosting operator's call, not something this repo's deploy scaffolding should decide for them (in this case, an existing nginx instance on the VPS)
- **Demo:**
  - `./deploy/provision.sh` deploys `Main.Api` to the VPS, bound to `127.0.0.1:8080`
  - Point any reverse proxy at that port and terminate TLS there; verified against the real VPS (a previously-provisioned Caddy container had in fact never started, since host nginx already held 80/443 - cleaned up manually, outside this PR)
- **Test:** no new application logic to pin; `dotnet build` and `docker compose config` verified clean

### 5. Wire filigrane's prod nginx to Main.Api, inject X-Api-Key server-side (filigrane PR `#6`, merged)

- **Added (filigrane repo):**
  - `frontend/nginx.conf` -> `nginx.conf.template`: `/api/*` upstream now `${WORKFLOW_API_URL}` (was the deleted `api:8080`); each `/api` location injects `proxy_set_header X-Api-Key "${WORKFLOW_API_KEY}"`. Existing rate-limit zones kept as-is
  - `frontend/Dockerfile`: template copied to `/etc/nginx/templates/`, `NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx` so the image entrypoint renders it at start (substitutes only the two defined env vars, leaves nginx's own `$vars`)
  - `docker-compose.yml`: passes `WORKFLOW_API_URL`/`WORKFLOW_API_KEY`, `host.docker.internal:host-gateway` for Linux, fails fast if the key is unset
  - `test/nginx/`: `render.test.sh` (envsubst render, runs anywhere) + `proxy.test.sh` (real nginx + stub upstream; Docker, skips otherwise) + README
- **Revealed:**
  - The prod nginx is the prod analog of the vite dev proxy; `X-Api-Key` injection is orthogonal to public exposure. nginx bound to loopback/private is a legitimate non-public setup - "nginx" never meant "public"
  - Wiring nginx also gets "rate limiting parity" for free (the zones were already in `nginx.conf`)
  - The single shared `API_KEY` authenticates "this is filigrane", not a user. A network-reachable frontend still needs a "which humans" gate (edge gating or per-user auth); a purely local frontend needs neither. Per-user keys (LLM-style) would mean each user presents their own key = building auth
- **Demo:**
  - `cd Workflow/deploy && docker compose up` (Main.Api on `127.0.0.1:8080`, separate stack)
  - `WORKFLOW_API_KEY=<key> WORKFLOW_API_URL=http://host.docker.internal:8080 docker compose up` (filigrane)
  - Open `http://localhost`, upload a PDF, watermark round-trips through nginx; DevTools shows no `X-Api-Key` on the browser request
- **Test:** `test/nginx/render.test.sh` (passes locally); `test/nginx/proxy.test.sh` (skips without a Docker daemon - run on a Docker host)
- **Note:** env vars are documented in `docker-compose.yml` + `test/nginx/README.md`; a follow-up commit (`cef1b41`) also negates the `.env*` ignore for `.env.example` so it's tracked, closing the recurring stone-2 gap. Scope is local production-style only
- **Follow-up finding:** Main.Api's watermark action already has `[RequestSizeLimit(4_194_304)]` (4 MB), matching nginx's `client_max_body_size 4M` - so app-level + proxy size guards already agree; no change needed. Open (minor): the old backend's cap was 3 MB, so the canonical max is now 4 MB, not 3 MB

### 6. Make Main.Api's deploy host port + SSH port configurable (Workflow PR `#10`)

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

## Next candidates

- Add metadata-only anonymization (strip PDF `/Info` dict + XMP) as a new `ITool` under `Infrastructure/Tools/Anonymization/`, mirroring the watermark token flow (`POST /api/anonymize` -> token -> existing `GET /api/download/{token}`) - deepens the Target, unblocked
- Deploy filigrane's frontend+nginx to the VPS (e.g. loopback behind host nginx) - edges toward exposure; this is where the "which humans" gate (edge gating vs. per-user auth) finally has to be decided. Deferred again at stone 6 (user kept filigrane local); still on the table
- Edge-gate a network-reachable filigrane (basic auth / IP allowlist / VPN) as a cheap stand-in for user auth, if a shared-but-restricted deploy is wanted before building real auth

## Deliberately deferred

- Image support
- In-content redaction (manual or AI-assisted)
- Rate limiting parity with filigrane nginx (now wired locally via stone 5's nginx; prod deploy still pending)
- Splitting `Infrastructure` into per-tool-group NuGet packages (single API preferred until a second real consumer with different needs shows up)
- Deploying filigrane's frontend publicly (needs its own user-level auth first, not just the shared service-to-service `X-Api-Key`)
