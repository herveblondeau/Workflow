# Filigrane (vnext)

**Status:** active

## Target

First guess: evolve the existing **filigrane** app (currently PDF watermarking) into a frontend that can apply **watermarking** and **anonymization** to both **PDFs and images**, backed by the **Workflow** API (watermarking + anonymization tools and endpoints).

## Constraints

- Backend lives in `/home/tigrou/Dev/Workflow` (this repo)
- Frontend remains in `/home/tigrou/Dev/filigrane` (separate repo) for now
- API endpoints require `X-Api-Key` (Workflow auth)
- Frontend must not store secrets; in dev, a server-side proxy injects `X-Api-Key`
- Keep the existing token-based download flow (`POST /api/watermark` -> `GET /api/download/{token}`)
- Work is done on branches via git worktrees; one stone -> one PR (per repo when needed)

## Related efforts

None

## Stones laid

### 1. Port PDF watermarking into Workflow + dev proxy auth injection (Workflow#6, filigrane#1)

- **Added:** Workflow now exposes filigrane-compatible `POST /api/watermark` + `GET /api/download/{token}` for PDFs, including disk storage, single-use tokens, and expiry cleanup. The filigrane frontend dev proxy injects `X-Api-Key` from `WORKFLOW_API_KEY` so the browser never sees the secret
- **Revealed:** Workflow's API-key auth model can work for a browser-based UI during development without changing the frontend API calls (proxy header injection)
- **Demo:**
  - Backend: `cd /home/tigrou/Dev/Workflow/Main.Api && API_KEY=dev dotnet run --launch-profile http`
  - Frontend: `cd /home/tigrou/Dev/filigrane/frontend && WORKFLOW_API_KEY=dev npm run dev`
- **Test:** `Tests/Infrastructure/PdfWatermarkerTests.cs`

## Next candidates

- Add image watermarking (PNG/JPEG upload + watermark output), keeping the same token-download flow
- Add anonymization v1 as metadata stripping only (EXIF for images + PDF metadata), to clarify what "anonymize" means for us
- Add a UI mode switch (Watermark vs Anonymize) and a matching new API endpoint shape, to validate the UX before deeper backend work

## Deliberately deferred

- Exact definition of anonymization (fork 1)
- Production deployment wiring (nginx/BFF) beyond dev proxy
