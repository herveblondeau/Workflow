# 5. Wire filigrane's prod nginx to Main.Api, inject X-Api-Key server-side

**Ref:** filigrane PR `#6`, merged

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
