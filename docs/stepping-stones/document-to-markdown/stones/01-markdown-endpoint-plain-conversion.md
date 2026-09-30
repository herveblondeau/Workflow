# 1. Markdown endpoint (plain, text-only conversion)

**PR:** `#17`

- **Added:** `POST /api/analysis/markdown` accepts a multipart file upload, converts it to markdown
  via the existing `DoclingStreamConverter` (plain `docling convert --to md`), and returns
  `{ success, result }`. Text-only for now: embedded images stay as `<!-- image -->` placeholders.
  `IProcessRunner` is now registered in DI and injected into `AnalysisController`; the converter is
  constructed per request with the uploaded file's extension (docling picks its parser from it).
- **Revealed:**
  - The repo had no controller tests and the `Tests` project didn't reference `Main.Api`. Pinning
    the endpoint meant adding a `Main.Api` project reference plus a `Microsoft.AspNetCore.App`
    framework reference to `Tests` - a new controller-test seam future endpoint stones can reuse.
  - The extension must be derived from the upload filename and is required (docling has no reliable
    format sniffing on a bare stream); a missing extension is a 400, not a silent failure.
  - docling's CLI **defaults to embedding images as base64 data URIs**, not to placeholders. The
    demo surfaced huge base64 blobs in the output. Fixed by passing `--image-export-mode
    placeholder` in `DoclingConverter` so v1 truly returns text-only markdown (`<!-- image -->`).
    Stone 2 will switch its own tool to `referenced` mode to actually process the images.
- **Demo:**
  ```bash
  API_KEY=test ASPNETCORE_URLS=http://localhost:5261 DOTNET_ENVIRONMENT=Development \
    dotnet run --project Main.Api &
  printf '# Sample\n\nHello **world**.\n' > /tmp/sample.md
  curl -s -H "X-Api-Key: test" -F "file=@/tmp/sample.md" \
    http://localhost:5261/api/analysis/markdown
  # -> {"success":true,"result":"# Sample\n\nHello **world**."}
  ```
- **Test:** `Tests/Api/AnalysisControllerMarkdownTests.cs` - null/empty/extension-less uploads -> 400,
  a successful conversion -> 200 `{ success, result }`, and a docling failure -> 500. Uses a stub
  `IProcessRunner` that writes the markdown a real `docling convert` would produce. The underlying
  `DoclingStreamConverter` was already covered by `Tests/Infrastructure/DoclingStreamConverterTests.cs`.
  `Tests/Infrastructure/DoclingConverterTests.cs` gains a case pinning the `--image-export-mode
  placeholder` argument.
