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

## Next candidates

- Prod wiring: retire filigrane's own `api` container/project and point nginx at Workflow's `Main.Api` instead, injecting `X-Api-Key` there (mirrors this stone, but is a bigger call since it kills the old backend)
- Add metadata-only anonymization (strip PDF metadata) as a new `ITool` under `Infrastructure/Tools/Anonymization/`, behind a new endpoint

## Deliberately deferred

- Image support
- In-content redaction (manual or AI-assisted)
- Rate limiting parity with filigrane nginx
