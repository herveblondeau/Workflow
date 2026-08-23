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

## Related efforts

None yet

## Stones laid

### 1. Port PDF watermarking API into Workflow - issue `#4`

- **Added:** `POST /api/watermark` + `GET /api/download/{token}` in `Workflow/Main.Api` (PDF-only), requiring `X-Api-Key`
- **Revealed:** keeping the existing token-based flow works fine in Workflow; frontend can stay unchanged once a BFF/proxy injects the API key
- **Demo:**
  - Run API: `cd /home/tigrou/Dev/Workflow/Main.Api && API_KEY=devkey dotnet run --launch-profile https`
  - Upload: `curl -k -H "X-Api-Key: devkey" -F "file=@/path/to/input.pdf" -F "watermarkType=Invisible" -F "contentType=Custom" -F "customText=HELLO" https://localhost:7156/api/watermark`
  - Download: `curl -k -H "X-Api-Key: devkey" -L -o out.pdf https://localhost:7156/api/download/<token>`
- **Test:** `Tests/Infrastructure/PdfWatermarkerTests.cs`

## Next candidates

- Port PDF watermarking API into Workflow (stone 1)
- Add BFF proxy header injection for local dev + prod so browser never sees `X-Api-Key`
- Add metadata-only anonymization (strip PDF metadata) behind a new endpoint

## Deliberately deferred

- Image support
- In-content redaction (manual or AI-assisted)
- Rate limiting parity with filigrane nginx
