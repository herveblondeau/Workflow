# 2. Dev proxy injects X-Api-Key so the browser never sees it

**Ref:** filigrane repo, issue `#3`, PR `#4`

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
