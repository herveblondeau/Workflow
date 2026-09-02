# PR walkthrough: Stone 5 — wire prod nginx to Main.Api, inject X-Api-Key server-side

**What this PR does:** filigrane's production nginx used to proxy `/api/*` to a bundled `api:8080`
backend that stone 3 deleted — so it pointed at nothing. This PR re-aims it at Workflow's Main.Api and
has nginx inject `X-Api-Key` **server-side**, so the app can run production-style locally (real nginx,
built SPA) without the browser ever seeing the key. It's the prod analog of the vite dev proxy.

**Where to start:** `frontend/nginx.conf` → `nginx.conf.template`. This is the load-bearing change —
the upstream rewrite + key injection *is* the new behaviour. Everything else (Dockerfile, compose,
tests) exists to render it, feed it values, run it, or prove it.

## Reading path

1. `frontend/nginx.conf.template` (3 hunks) — the core. On each of the three `/api` locations,
   `proxy_pass http://api:8080` becomes `proxy_pass ${WORKFLOW_API_URL}`, and a new
   `proxy_set_header X-Api-Key "${WORKFLOW_API_KEY}"` is added. The `${...}` are placeholders resolved
   at container start; nginx's own `$host`/`$remote_addr` stay literal.
2. `frontend/Dockerfile` — *how the template becomes real config.* Instead of copying `nginx.conf`
   straight in, it copies the `.template` to `/etc/nginx/templates/` and sets
   `NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx`, so the official image's entrypoint runs `envsubst` at start
   and writes the rendered `nginx.conf`. Renders step 1.
3. `docker-compose.yml` — *supplies the values step 1 references and step 2 substitutes.* Passes
   `WORKFLOW_API_URL` (default `http://host.docker.internal:8080`) and `WORKFLOW_API_KEY`, adds the
   `host.docker.internal:host-gateway` mapping (Linux), and fails fast if the key is unset. The stale
   NOTE about the dead backend is replaced with run instructions.
4. `test/nginx/render.test.sh` — pins step 1 at the config-generation seam: runs the *same* restricted
   `envsubst` and asserts the upstream is rewritten, `X-Api-Key` is injected on all three locations,
   nginx's own `$vars` survive, and no `${WORKFLOW_*}` placeholder leaks. Needs only `envsubst`.
5. `test/nginx/proxy.test.sh` — the behavioural test: boots real nginx (step 2's mechanism) in front of
   a stub upstream that echoes headers, and asserts a keyless browser request still arrives upstream
   *with* the key, plus a `429` under burst. Exercises the whole pipeline; skips cleanly without Docker.
6. `test/nginx/README.md` — how to run both tests and the manual end-to-end demo.
7. `.gitignore` + `.env.example` — negate the `.env*` ignore for `.env.example` and track it, so this
   PR's new env vars live in git. Follow-up commit `cef1b41`, closing a gap first noticed in stone 2.

## Groups

### Core logic

- `frontend/nginx.conf.template` — the three `proxy_pass` rewrites + three `X-Api-Key` injections. This
  is the entire behaviour change; read it first.

### Wiring

- `frontend/Dockerfile` — switch to the image's envsubst template mechanism (`templates/` +
  `NGINX_ENVSUBST_OUTPUT_DIR`).
- `docker-compose.yml` — env vars in, `host.docker.internal` mapping, `:?` fail-fast on the key.

### Tests

- `test/nginx/render.test.sh` — substitution correctness, runs anywhere.
- `test/nginx/proxy.test.sh` — live injection + rate-limit, Docker-gated with a clean SKIP.
- `test/nginx/README.md` — run instructions + demo.

### Config / onboarding (rides along, in-scope)

- `.gitignore` (+2) and `.env.example` (new, 10 lines) — track the example so `WORKFLOW_API_URL` /
  `WORKFLOW_API_KEY` are documented in git. Serves this PR's feature (its own new env vars) but is a
  separate commit and also fixes an older recurring gap — worth a glance as its own thing.

> **What is envsubst?** A small Unix tool (GNU gettext) that reads text and replaces `${VAR}`
> placeholders with the matching environment variable's value. The nginx image runs it once at
> container start to render `nginx.conf.template` → a real `nginx.conf` with actual values baked in,
> restricted to only the env vars you defined so nginx's own `$host`-style variables are untouched.

## Read carefully

- `frontend/nginx.conf.template` — the design hinges on `envsubst` substituting **only** the two
  braced, env-defined vars (`${WORKFLOW_API_URL}`, `${WORKFLOW_API_KEY}`) and leaving nginx's runtime
  `$host` / `$remote_addr` / `$binary_remote_addr` untouched. The nginx image does this by
  intersecting `${VAR}` refs with defined env vars; `render.test.sh` guards it. A stray env var named
  like an nginx variable, or writing `$WORKFLOW_...` without braces, would break it.
- `frontend/nginx.conf.template` `proxy_pass ${WORKFLOW_API_URL};` — because envsubst renders this to a
  literal at *start*, it stays a **static** `proxy_pass`. It does **not** become nginx's dynamic-variable
  form (which would need a `resolver` and change buffering/keepalive behaviour). Subtle but important.
- `docker-compose.yml` — `WORKFLOW_API_KEY: "${WORKFLOW_API_KEY:?...}"` uses compose's `:?` operator:
  the stack refuses to start if the key is unset. Easy to miss, deliberate.
- `test/nginx/proxy.test.sh` — the assertion greps the echoed JSON for `"x-api-key": "<key>"`; the
  stub server lowercases header names. The test's `host.docker.internal:host-gateway` + host-run stub
  mirror the compose networking, which is why it's a faithful behavioural check.
