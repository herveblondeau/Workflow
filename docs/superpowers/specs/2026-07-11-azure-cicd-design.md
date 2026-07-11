# Azure CI/CD Pipeline for Main.Api

**Date:** 2026-07-11
**Status:** Approved

## Goal

Deploy `Main.Api` (the ASP.NET Core REST API) to Azure automatically via GitHub Actions, with infrastructure defined as code.

## Why containers, not native App Service runtime

`Main.Api` pulls in `Whisper.net.Runtime` (native whisper.cpp binaries) and drives Tesseract OCR (native `tesseract-ocr` binary) through `Infrastructure`. Zip-deploying to App Service's built-in .NET runtime doesn't reliably give you control over native binaries/OS packages. A Dockerfile we own does.

Azure App Service for Containers was chosen over Azure Container Apps: this is a single low/steady-traffic API with no need for scale-to-zero, multi-container, or Dapr features — Container Apps would add an environment, ingress, and revision management for no benefit here.

## Azure resources

All resources live in one resource group in **France Central**.

| Resource | SKU/tier | Purpose |
| --- | --- | --- |
| Resource Group | — | `rg-workflow` |
| Azure Container Registry | Basic | `acrworkflow<uniqueString>` — holds the built image |
| App Service Plan | B1 Basic, Linux | 1.75GB RAM / 1 core — enough for Whisper model loading at this scale |
| App Service (Web App for Containers) | — | `app-workflow-api`, system-assigned managed identity |
| Key Vault | RBAC-enabled | `kv-workflow<uniqueString>` — holds `API_KEY` and provider API keys |

The App Service's managed identity is granted:
- `AcrPull` on the ACR (pulls images without stored registry credentials)
- `Key Vault Secrets User` on the Key Vault (resolves Key Vault–referenced app settings)

No Application Insights or Log Analytics workspace is provisioned — not required by this scope; can be added later without changing the pipeline shape.

## Infrastructure as code

New `infra/` directory:
- `infra/main.bicep` — declares all resources above and the two role assignments.
- `infra/main.parameters.json` — environment values (region, resource names/prefixes).

The `deploy.yml` workflow applies this Bicep on every push to `master`, so infra changes ship through the same pipeline as code changes (idempotent — reapplying with no changes is a no-op).

## Azure AD app registration (OIDC) — manual one-time bootstrap

Creating an app registration and assigning it a role on the resource group requires directory privileges a CI pipeline shouldn't hold. This is a one-time manual step (documented separately, run once via `az` CLI), not part of any workflow:

1. Create an Azure AD app registration for GitHub Actions.
2. Add a federated credential trusting `repo:herveblondeau/Workflow:ref:refs/heads/master` (used by `deploy.yml`).
3. Assign the app a scoped role (e.g. `Contributor`) on `rg-workflow`.
4. Record `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` as GitHub Actions secrets.

No client secret is created or stored — GitHub Actions exchanges a short-lived OIDC token for Azure access at run time via `azure/login`.

## GitHub Actions workflows

### `ci.yml` — pull requests targeting `master`

1. `dotnet restore`
2. `dotnet build` (warnings are errors solution-wide, per `Directory.Build.props`)
3. `dotnet test`

No Azure login, no deploy. Pure verification gate on every PR.

### `deploy.yml` — push to `master`

Runs under a GitHub `production` Environment (so required reviewers/protection rules can be layered on later without changing the workflow).

1. `dotnet test` — fails fast before touching Azure if tests fail.
2. `azure/login` via OIDC (no stored secret).
3. Deploy `infra/main.bicep` (creates or updates resources; idempotent).
4. Build the Docker image, push to ACR tagged with the commit SHA.
5. Update the App Service to reference the new image tag.

## Dockerfile (new)

Multi-stage build targeting `linux-x64`:
- Build stage: SDK image, `dotnet publish Main.Api`.
- Runtime stage: ASP.NET base image + `apt-get install -y tesseract-ocr` (required by `TesseractOcrTranscriber`, which shells out to the native `tesseract` binary — not present on a bare ASP.NET image). `Whisper.net.Runtime`'s native binaries ship via NuGet and land in the linux-x64 publish output automatically.

**Known trade-off:** the Whisper GGML model file downloads lazily on first transcription request (`WhisperTranscriber`, see `Infrastructure/Tools/Transcribers/WhisperTranscriber.cs`) into the container's local filesystem. Container storage is ephemeral, so the model re-downloads after every redeploy or restart. Acceptable at current traffic/scale; would need a persistent volume or pre-baked model in the image if this becomes a cost/latency problem.

## Secrets flow

- GitHub Actions secrets: only `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (OIDC login). No provider API keys are ever stored in GitHub.
- `API_KEY` and provider keys (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `GEMINI_API_KEY`, `OPENROUTER_API_KEY`, per README) are seeded directly into Key Vault by the operator (`az keyvault secret set`), out of band, one time per environment. The pipeline only wires up Key Vault references as App Service app settings — it never reads or handles secret values.
- Non-secret config (e.g. `{PROVIDER}_DEFAULT_MODEL`) is set as plain App Service app settings via the Bicep deployment.

## Out of scope

- Staging environment / deployment slots — explicitly deferred; single production environment only, gated by PR checks instead.
- Application Insights / centralized logging — not requested; can be added later.
- Custom domain / TLS — App Service's default `*.azurewebsites.net` hostname and managed TLS is sufficient for now.
