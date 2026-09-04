# 1. PDF watermarking moved into Workflow (tool-based, token download)

**Ref:** `5b9910b` (issue `#4`, PR `#5`)

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
