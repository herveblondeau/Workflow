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

### 1. Port PDF watermarking API into Workflow - issue `#4`

- **Added:** `POST /api/watermark` + `GET /api/download/{token}` in `Workflow/Main.Api` (PDF-only), requiring `X-Api-Key`
- **Revealed:** keeping the existing token-based flow works fine in Workflow; frontend can stay unchanged once a BFF/proxy injects the API key
- **Demo:**
  - Run API: `cd /home/tigrou/Dev/Workflow/Main.Api && API_KEY=devkey dotnet run --launch-profile https`
  - Upload: `curl -k -H "X-Api-Key: devkey" -F "file=@/path/to/input.pdf" -F "watermarkType=Invisible" -F "contentType=Custom" -F "customText=HELLO" https://localhost:7156/api/watermark`
  - Download: `curl -k -H "X-Api-Key: devkey" -L -o out.pdf https://localhost:7156/api/download/<token>`
- **Test:** `Tests/Infrastructure/PdfWatermarkerTests.cs`

### 1b. Rework watermarking as an `ITool` (post-review correction)

- **Added:** `Core/Models/PdfStream.cs` (thin `Stream` wrapper, mirrors `ImageStream`/`AudioStream`), `Infrastructure/Tools/Watermarking/PdfWatermarkTool.cs` implementing `ITool<PdfStream, PdfStream>`, `Core/Models/WatermarkOptions.cs`. Removed the old `Infrastructure/Filigrane/PdfWatermarker.cs` service class; controller now builds a `PdfStream` and calls the tool, translating `Result<PdfStream>` to HTTP
- **Revealed:** review caught that stone 1 ported filigrane's service-class shape instead of adopting the repo's existing tool convention; the codebase already has a clear pattern (`ITool<TIn, TOut>`, `Result<T>`, constructor-injected options, dedicated stream types) that watermarking should have followed from the start
- **Demo:** same as stone 1 (unchanged HTTP contract)
- **Test:** `Tests/Infrastructure/PdfWatermarkToolTests.cs` (replaces `PdfWatermarkerTests.cs`), tests the tool directly at the `Transform` seam

## Next candidates

- Add BFF proxy header injection for local dev + prod so browser never sees `X-Api-Key`
- Add metadata-only anonymization (strip PDF metadata) as a new `ITool` under `Infrastructure/Tools/Anonymization/`, behind a new endpoint

## Deliberately deferred

- Image support
- In-content redaction (manual or AI-assisted)
- Rate limiting parity with filigrane nginx
