# GitHub Actions → Azure CI/CD for Main.Api Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a GitHub Actions CI/CD pipeline that builds/tests every PR and, on every push to `master`, deploys `Main.Api` as a container to Azure App Service via idempotent Bicep infrastructure-as-code and OIDC (no stored Azure credentials, no provider API keys in GitHub).

**Architecture:** `Main.Api` is packaged by a multi-stage Dockerfile (dotnet SDK 10.0 build → aspnet 10.0 runtime + `tesseract-ocr`) because it drives native Whisper.net and Tesseract binaries that App Service's built-in zip-deploy runtime can't host. `infra/main.bicep` declares ACR (Basic), an App Service Plan (B1 Linux), App Service for Containers with a system-assigned managed identity, and an RBAC-enabled Key Vault, wiring the managed identity to `AcrPull` (image pull) and `Key Vault Secrets User` (Key Vault-referenced app settings) via two role assignments. `.github/workflows/ci.yml` gates pull requests (build+test only); `.github/workflows/deploy.yml` gates pushes to `master` (build+test, then OIDC login, `az deployment group create`, `az acr build`, `az webapp config container set`).

**Tech Stack:** GitHub Actions (`actions/checkout@v4`, `actions/setup-dotnet@v4`, `azure/login@v2`), .NET 10 SDK, Docker, Azure Bicep, Azure CLI (`az`), Azure Container Registry, Azure App Service (Linux containers), Azure Key Vault (RBAC mode), Azure AD workload identity federation (OIDC).

## Global Constraints

- Work happens in `/home/tigrou/Dev/Workflow` (GitHub `herveblondeau/Workflow`, default branch `master`). Run all commands from the repo root unless stated otherwise.
- `Directory.Build.props` sets `TreatWarningsAsErrors=true` solution-wide — any compiler warning fails `dotnet build`, and therefore fails CI and the pre-deploy build step in `deploy.yml`.
- All new Azure resources live in resource group `rg-workflow`, region France Central (`francecentral`). No other resource group or region is used anywhere in this plan.
- GitHub Actions secrets are limited to exactly `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (OIDC login only). Never add provider API keys as GitHub secrets.
- Azure Key Vault secret names may only contain alphanumerics and dashes (no underscores). Runtime secrets are therefore stored in Key Vault with dash-cased names (e.g. `API-KEY`) while the App Service app setting that references them keeps the underscored name (e.g. `API_KEY`) that `Main.Api`'s flat `IConfiguration["..."]` lookups expect (see `Infrastructure/ChatAgents/ChatClientFactory.cs`, `Infrastructure/ChatAgents/Providers/*ModelSource.cs`).
- Do not modify `Main.Api/Program.cs`. The HTTPS-redirect-behind-proxy issue (`app.UseHttpsRedirection()`) is fixed purely by the `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` app setting set in Bicep — no code change.
- Explicitly out of scope: staging environment/deployment slots, Application Insights/Log Analytics, custom domain/TLS. Do not add them.
- Any offline verification step that needs a scratch file uses a plain `/tmp/...` path, not this session's ephemeral scratchpad directory — that path won't exist in whatever session executes this plan.
- Steps requiring a real `az login` against a live subscription, or an actual GitHub Actions run, cannot be executed in an offline/sandboxed environment. Each such step says so explicitly and gives the offline-equivalent check (YAML/JSON syntax validation, `az bicep build`, local `dotnet build`/`dotnet test`, `docker build`) plus what to confirm when it's run live.

---

### Task 1: CI workflow (`ci.yml`)

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:** Produces the `dotnet restore Workflow.sln` / `dotnet build Workflow.sln --no-restore --configuration Release` / `dotnet test Workflow.sln --no-build --configuration Release` command sequence that Task 5's `deploy.yml` reuses verbatim as its pre-deploy fail-fast step.

- [ ] **Step 1: Create the workflow directory and file**

Create `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  pull_request:
    branches: [master]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore Workflow.sln

      - name: Build
        run: dotnet build Workflow.sln --no-restore --configuration Release

      - name: Test
        run: dotnet test Workflow.sln --no-build --configuration Release
```

- [ ] **Step 2: Verify YAML is well-formed**

Run: `python3 -c "import yaml, json; d = yaml.safe_load(open('.github/workflows/ci.yml')); print(json.dumps(d, indent=2))"`

Expected: prints the parsed structure with no exception; `d['on']['pull_request']['branches'] == ['master']` and `d['jobs']['build-and-test']['steps']` has 5 entries (checkout, setup-dotnet, restore, build, test).

- [ ] **Step 3: Verify the exact commands the workflow runs actually pass locally**

Run: `dotnet restore Workflow.sln && dotnet build Workflow.sln --no-restore --configuration Release && dotnet test Workflow.sln --no-build --configuration Release`

Expected: restore succeeds; build ends with `Build succeeded.` and 0 warnings/0 errors (a warning would fail the build because of `TreatWarningsAsErrors`); test run ends with all tests passing (`Passed!` summary, 0 failed).

- [ ] **Step 4: Confirm the workflow only triggers on PRs against master and never touches Azure**

Run: `grep -n "azure/login\|az deployment\|az acr\|az webapp" .github/workflows/ci.yml || echo "NO_AZURE_REFERENCES"`

Expected output: `NO_AZURE_REFERENCES`.

- [ ] **Step 5: Commit**
```bash
git add .github/workflows/ci.yml
git commit -m "Add CI workflow for pull requests"
```

---

### Task 2: `Main.Api` Dockerfile + `.dockerignore`

**Files:**
- Create: `Main.Api/Dockerfile`
- Create: `.dockerignore`

**Interfaces:** Produces `Main.Api/Dockerfile` at the path Task 5's `deploy.yml` passes to `az acr build --file Main.Api/Dockerfile .` (build context = repo root), and the image's runtime `EXPOSE 8080` that Task 3's Bicep `WEBSITES_PORT=8080` app setting matches.

- [ ] **Step 1: Create `.dockerignore`**

Create `.dockerignore`:

```
**/bin/
**/obj/
**/.vs/
**/.vscode/
.git/
.github/
.gitignore
docs/
infra/
*.md
LICENSE.txt
THIRD_PARTY_NOTICES.txt
```

- [ ] **Step 2: Create `Main.Api/Dockerfile`**

Create `Main.Api/Dockerfile`:

```dockerfile
# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore Main.Api/Main.Api.csproj

RUN dotnet publish Main.Api/Main.Api.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained false \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# TesseractOcrTranscriber (Infrastructure/Tools/Transcribers/TesseractOcrTranscriber.cs) shells out
# to the native `tesseract` binary at runtime. Whisper.net.Runtime's native binaries, by contrast,
# ship inside its NuGet package and land in the linux-x64 publish output automatically above, so no
# extra apt package is needed for Whisper.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr \
    && rm -rf /var/lib/apt/lists/*

# Known trade-off (not fixed here): WhisperTranscriber (Infrastructure/Tools/Transcribers/
# WhisperTranscriber.cs) downloads its GGML model into Path.GetTempPath() lazily on first
# transcription request. The container filesystem is ephemeral, so the model re-downloads after
# every redeploy or restart. Acceptable at current scale.

COPY --from=build /app/publish .

# .NET 8+ container base images already default ASPNETCORE_HTTP_PORTS (or ASPNETCORE_URLS) to 8080;
# just expose it and don't override.
EXPOSE 8080

ENTRYPOINT ["dotnet", "Main.Api.dll"]
```

- [ ] **Step 3: Verify the image builds**

Run: `docker build -f Main.Api/Dockerfile -t workflow-api:local .`

Expected: This requires network access to pull `mcr.microsoft.com/dotnet/sdk:10.0` (~800 MB) and `mcr.microsoft.com/dotnet/aspnet:10.0`, and to `apt-get update`/install `tesseract-ocr` in the runtime stage. If the environment executing this step has no egress to `mcr.microsoft.com`/Debian package mirrors, this step **cannot be executed there** — run it later in an environment with network access (locally, or implicitly as part of Task 5's `az acr build` in the real GitHub Actions run). When it does run, expect the build to finish with `Successfully tagged workflow-api:local` (classic builder) or `naming to docker.io/library/workflow-api:local done` (BuildKit), and 0 errors from `dotnet publish` (a build warning would fail it, since `Directory.Build.props`'s `TreatWarningsAsErrors=true` applies during `dotnet restore`/`publish` inside the image).

- [ ] **Step 4: Verify `.dockerignore` excludes build output and VCS metadata**

Run: `docker build -f Main.Api/Dockerfile --no-cache -t workflow-api:context-check --progress=plain . 2>&1 | grep -i "transferring context" || true`

Expected: If network access allows the build to reach the context-transfer step, the reported context size should be small (a few MB of source, not hundreds of MB of `bin/`/`obj/`). If Step 3 could not run due to no network, skip this and instead run `git check-ignore -v Main.Api/bin/Debug 2>&1; cat .dockerignore` and manually confirm `**/bin/` and `**/obj/` are present in `.dockerignore`.

- [ ] **Step 5: Commit**
```bash
git add Main.Api/Dockerfile .dockerignore
git commit -m "Add multi-stage Dockerfile for Main.Api"
```

---

### Task 3: Infrastructure as code (`infra/main.bicep` + `infra/main.parameters.json`)

**Files:**
- Create: `infra/main.bicep`
- Create: `infra/main.parameters.json`

**Interfaces:** Produces resource-group-scoped Bicep declaring ACR (`acrworkflow<uniqueString>`), App Service Plan (`plan-workflow-api`, B1 Linux), App Service (`app-workflow-api`, system-assigned identity), Key Vault (`kv-workflow<uniqueString>`, RBAC-enabled), and 2 role assignments (`AcrPull`, `Key Vault Secrets User`) both scoped to the App Service's managed identity. Outputs `acrName`, `acrLoginServer`, `appServiceName`, `keyVaultName`, `keyVaultUri` are consumed by Task 5's `deploy.yml` (via `az deployment group show ... --query properties.outputs...`) and by Task 4/6's documented `az keyvault` commands (via `az keyvault list --resource-group rg-workflow`). Key Vault-referenced app settings use dash-cased secret names (`API-KEY`, `ANTHROPIC-API-KEY`, `OPENAI-API-KEY`, `GEMINI-API-KEY`, `OPENROUTER-API-KEY`) that Task 4/6 must seed with matching names.

- [ ] **Step 1: Create `infra/main.bicep`**

Create `infra/main.bicep`:

```bicep
targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Full container image reference (including tag) used the first time the App Service is provisioned. Overwritten on every deploy by the CD workflow once a real image has been pushed to ACR.')
param containerImage string = 'mcr.microsoft.com/appsvc/staticsite:latest'

@description('Default Anthropic model id (e.g. claude-sonnet-4-6). Empty string until the operator sets a value.')
param anthropicDefaultModel string = ''

@description('Default OpenAI model id (e.g. gpt-4o-mini). Empty string until the operator sets a value.')
param openAIDefaultModel string = ''

@description('Default Gemini model id (e.g. gemini-2.5-flash). Empty string until the operator sets a value.')
param geminiDefaultModel string = ''

@description('Default OpenRouter model id. Empty string until the operator sets a value.')
param openRouterDefaultModel string = ''

var acrName = 'acrworkflow${uniqueString(resourceGroup().id)}'
var keyVaultName = 'kv-workflow${uniqueString(resourceGroup().id)}'
var planName = 'plan-workflow-api'
var appServiceName = 'app-workflow-api'

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
  }
}

resource plan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: appServiceName
  location: location
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerImage}'
      acrUseManagedIdentityCreds: true
      appSettings: [
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
          value: 'true'
        }
        {
          name: 'ANTHROPIC_DEFAULT_MODEL'
          value: anthropicDefaultModel
        }
        {
          name: 'OPENAI_DEFAULT_MODEL'
          value: openAIDefaultModel
        }
        {
          name: 'GEMINI_DEFAULT_MODEL'
          value: geminiDefaultModel
        }
        {
          name: 'OPENROUTER_DEFAULT_MODEL'
          value: openRouterDefaultModel
        }
        {
          name: 'API_KEY'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/API-KEY/)'
        }
        {
          name: 'ANTHROPIC_API_KEY'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/ANTHROPIC-API-KEY/)'
        }
        {
          name: 'OPENAI_API_KEY'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/OPENAI-API-KEY/)'
        }
        {
          name: 'GEMINI_API_KEY'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/GEMINI-API-KEY/)'
        }
        {
          name: 'OPENROUTER_API_KEY'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/OPENROUTER-API-KEY/)'
        }
      ]
    }
  }
}

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, appService.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource keyVaultSecretsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appService.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer
output appServiceName string = appService.name
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
```

- [ ] **Step 2: Create `infra/main.parameters.json`**

Create `infra/main.parameters.json`:

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "containerImage": {
      "value": "mcr.microsoft.com/appsvc/staticsite:latest"
    },
    "anthropicDefaultModel": {
      "value": ""
    },
    "openAIDefaultModel": {
      "value": ""
    },
    "geminiDefaultModel": {
      "value": ""
    },
    "openRouterDefaultModel": {
      "value": ""
    }
  }
}
```

- [ ] **Step 3: Verify `main.parameters.json` is valid JSON**

Run: `python3 -c "import json; json.load(open('infra/main.parameters.json'))" && echo VALID_JSON`

Expected: `VALID_JSON`.

- [ ] **Step 4: Verify the Bicep template compiles (offline, no Azure login required)**

Run: `az bicep install && az bicep build --file infra/main.bicep --outfile /tmp/workflow-main.json && echo BICEP_BUILD_OK`

Expected: `az bicep install` needs network access the first time (downloads the Bicep CLI); once installed, `az bicep build` compiles `infra/main.bicep` to ARM JSON with no errors, and the command prints `BICEP_BUILD_OK`. If the environment executing this step has no network access to install the Bicep CLI, this step cannot run there — it must be run once by whoever executes this task, and the expected outcome when it does run is a clean compile with zero `az bicep build` errors/warnings (in particular, no "type mismatch" or "resource not found" diagnostics referencing `Microsoft.ContainerRegistry/registries@2023-07-01`, `Microsoft.KeyVault/vaults@2023-07-01`, `Microsoft.Web/serverfarms@2022-09-01`, `Microsoft.Web/sites@2022-09-01`, or `Microsoft.Authorization/roleAssignments@2022-04-01`).

- [ ] **Step 5: Verify the compiled ARM template contains the 6 expected resources**

Run: `python3 -c "import json; t = json.load(open('/tmp/workflow-main.json')); types = sorted(r['type'] for r in t['resources']); print('\n'.join(types))"`

Expected output (order may vary, but exactly these 6 lines):
```
Microsoft.Authorization/roleAssignments
Microsoft.Authorization/roleAssignments
Microsoft.ContainerRegistry/registries
Microsoft.KeyVault/vaults
Microsoft.Web/serverfarms
Microsoft.Web/sites
```

- [ ] **Step 6: Commit**
```bash
git add infra/main.bicep infra/main.parameters.json
git commit -m "Add Azure infrastructure as Bicep (ACR, App Service, Key Vault)"
```

---

### Task 4: OIDC bootstrap documentation (`docs/azure-oidc-bootstrap.md`)

**Files:**
- Create: `docs/azure-oidc-bootstrap.md`

**Interfaces:** Documents the exact one-time `az` command sequence a human operator runs (never automated in a workflow) that produces `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` — the 3 GitHub Actions secrets Task 5's `deploy.yml` reads via `${{ secrets.* }}` — and creates resource group `rg-workflow` that Task 3's Bicep and Task 5's `az deployment group create` target. Also documents seeding the dash-cased Key Vault secret names defined in Task 3's Bicep, referenced again in Task 6's README section.

- [ ] **Step 1: Create `docs/azure-oidc-bootstrap.md`**

Create `docs/azure-oidc-bootstrap.md`:

```markdown
# Azure OIDC Bootstrap (one-time, manual)

This is a **one-time manual procedure**, not run by any GitHub Actions workflow. It needs Azure AD
directory privileges (create an app registration, add a federated credential) that a CI pipeline
should never hold. Run these commands yourself, once, before the first push to `master` triggers
`deploy.yml`.

Commands below assume a `bash`-compatible shell and that you're already signed in as a user with
rights to create resource groups, app registrations, and role assignments (`az login` first if not).

## 1. Create the resource group

```bash
az group create \
  --name rg-workflow \
  --location francecentral
```

## 2. Create the Azure AD app registration used by GitHub Actions

```bash
APP_NAME="gh-workflow-deploy"
APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
echo "APP_ID=$APP_ID"
```

## 3. Create the service principal for that app registration

```bash
az ad sp create --id "$APP_ID"
```

## 4. Add a federated credential trusting `deploy.yml` runs on `master`

This trusts only pushes to `master` (which is what `deploy.yml` triggers on). The PR workflow
(`ci.yml`) never logs in to Azure, so it needs no federated credential.

```bash
cat > /tmp/federated-credential.json <<'EOF'
{
  "name": "workflow-deploy-master",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:herveblondeau/Workflow:ref:refs/heads/master",
  "audiences": ["api://AzureADTokenExchange"],
  "description": "OIDC trust for deploy.yml running on pushes to master"
}
EOF

az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters @/tmp/federated-credential.json
```

## 5. Grant the service principal Contributor on `rg-workflow` only

Scoped to the resource group, not the subscription — the pipeline can manage `rg-workflow`'s
resources and cannot touch anything else.

```bash
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

az role assignment create \
  --assignee "$APP_ID" \
  --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/rg-workflow"
```

## 6. Print the 3 values to add as GitHub Actions repository secrets

```bash
echo "AZURE_CLIENT_ID=$APP_ID"
echo "AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID"
```

Add these three as **repository secrets** in GitHub: Settings → Secrets and variables → Actions →
New repository secret. Name them exactly `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
`AZURE_SUBSCRIPTION_ID`. Also create a GitHub **Environment** named `production` (Settings →
Environments → New environment) — `deploy.yml` runs under it.

## 7. After the first successful deploy: seed runtime secrets into Key Vault

`deploy.yml`'s first run creates the Key Vault (name has a random suffix, e.g.
`kv-workflowabc1234567`). This vault has RBAC authorization enabled, so grant yourself
`Key Vault Secrets Officer` on it before you can write secrets:

```bash
KV_NAME=$(az keyvault list --resource-group rg-workflow --query "[0].name" -o tsv)
KV_ID=$(az keyvault show --name "$KV_NAME" --resource-group rg-workflow --query id -o tsv)
MY_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

az role assignment create \
  --assignee "$MY_OBJECT_ID" \
  --role "Key Vault Secrets Officer" \
  --scope "$KV_ID"
```

Then seed the 5 runtime secrets (values from your own provider accounts — never commit these).
Secret names use dashes because Azure Key Vault secret names cannot contain underscores; the
corresponding App Service app settings (`API_KEY`, `ANTHROPIC_API_KEY`, etc., wired up by
`infra/main.bicep`) keep the underscored names `Main.Api` reads via `IConfiguration`:

```bash
az keyvault secret set --vault-name "$KV_NAME" --name API-KEY --value "<your-api-key>"
az keyvault secret set --vault-name "$KV_NAME" --name ANTHROPIC-API-KEY --value "<your-anthropic-key>"
az keyvault secret set --vault-name "$KV_NAME" --name OPENAI-API-KEY --value "<your-openai-key>"
az keyvault secret set --vault-name "$KV_NAME" --name GEMINI-API-KEY --value "<your-gemini-key>"
az keyvault secret set --vault-name "$KV_NAME" --name OPENROUTER-API-KEY --value "<your-openrouter-key>"
```

Finally, restart the app so it picks up the now-resolvable Key Vault references:

```bash
az webapp restart --name app-workflow-api --resource-group rg-workflow
```
```

- [ ] **Step 2: Verify the embedded JSON snippet is syntactically valid**

Run:
```bash
python3 -c "
import re, json
text = open('docs/azure-oidc-bootstrap.md').read()
block = re.search(r'federated-credential.json <<.EOF.\n(.*?)\nEOF', text, re.S).group(1)
json.loads(block)
print('VALID_FEDERATED_CREDENTIAL_JSON')
"
```

Expected: `VALID_FEDERATED_CREDENTIAL_JSON`.

- [ ] **Step 3: Verify the subject claim matches the exact repo/branch this pipeline trusts**

Run: `grep -n '"subject": "repo:herveblondeau/Workflow:ref:refs/heads/master"' docs/azure-oidc-bootstrap.md`

Expected: one matching line printed (confirms no typo in org/repo/branch).

- [ ] **Step 4: Not automatable — live execution checklist**

This task's actual commands (`az group create`, `az ad app create`, `az ad app federated-credential create`, `az role assignment create`, `az keyvault secret set`, etc.) require a real `az login` against a real Azure subscription and Azure AD tenant. When the user runs Steps 1–7 of `docs/azure-oidc-bootstrap.md` live, they should confirm: (a) `az group show --name rg-workflow` returns the group in `francecentral`; (b) `az ad app show --id $APP_ID` returns the app registration; (c) `az ad app federated-credential list --id $APP_ID` shows the `workflow-deploy-master` credential with the exact subject string; (d) `az role assignment list --assignee $APP_ID --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/rg-workflow` shows `Contributor`; (e) the three secret values are present under GitHub Settings → Secrets and variables → Actions, and a `production` Environment exists.

- [ ] **Step 5: Commit**
```bash
git add docs/azure-oidc-bootstrap.md
git commit -m "Document Azure OIDC bootstrap procedure"
```

---

### Task 5: CD workflow (`deploy.yml`)

**Files:**
- Create: `.github/workflows/deploy.yml`

**Interfaces:** Consumes: Task 1's restore/build/test command sequence, Task 2's `Main.Api/Dockerfile` path, Task 3's Bicep file/parameters and its `acrName`/`acrLoginServer`/`appServiceName` outputs, Task 4's `AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID` GitHub secrets and `production` Environment. Produces the running deployment that Task 6's README documents.

- [ ] **Step 1: Create `.github/workflows/deploy.yml`**

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy

on:
  push:
    branches: [master]

permissions:
  id-token: write
  contents: read

concurrency:
  group: production-deploy
  cancel-in-progress: false

jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: production
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore Workflow.sln

      - name: Build
        run: dotnet build Workflow.sln --no-restore --configuration Release

      - name: Test
        run: dotnet test Workflow.sln --no-build --configuration Release

      - name: Azure login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Deploy infrastructure (Bicep)
        run: |
          az deployment group create \
            --name main \
            --resource-group rg-workflow \
            --template-file infra/main.bicep \
            --parameters infra/main.parameters.json

      - name: Read infra outputs
        id: infra
        run: |
          echo "acr_name=$(az deployment group show --resource-group rg-workflow --name main --query properties.outputs.acrName.value -o tsv)" >> "$GITHUB_OUTPUT"
          echo "acr_login_server=$(az deployment group show --resource-group rg-workflow --name main --query properties.outputs.acrLoginServer.value -o tsv)" >> "$GITHUB_OUTPUT"
          echo "app_service_name=$(az deployment group show --resource-group rg-workflow --name main --query properties.outputs.appServiceName.value -o tsv)" >> "$GITHUB_OUTPUT"

      - name: Build and push container image
        run: |
          az acr build \
            --registry ${{ steps.infra.outputs.acr_name }} \
            --image workflow-api:${{ github.sha }} \
            --file Main.Api/Dockerfile \
            .

      - name: Point App Service at the new image
        run: |
          az webapp config container set \
            --name ${{ steps.infra.outputs.app_service_name }} \
            --resource-group rg-workflow \
            --container-image-name ${{ steps.infra.outputs.acr_login_server }}/workflow-api:${{ github.sha }} \
            --container-registry-url https://${{ steps.infra.outputs.acr_login_server }}

      - name: Restart App Service
        run: |
          az webapp restart \
            --name ${{ steps.infra.outputs.app_service_name }} \
            --resource-group rg-workflow
```

Note on `az acr build` vs `docker build` + `docker push`: `az acr build` performs the equivalent of `docker build` (same `Main.Api/Dockerfile`, same repo-root context) but executes it inside ACR itself via ACR Tasks, which only needs the control-plane `Microsoft.ContainerRegistry/registries/scheduleRun/action` permission — already covered by the `Contributor` role Task 4 grants the deploy service principal on `rg-workflow`. A local `docker build` + `docker push` would additionally require either enabling the ACR admin user or granting the deploy principal a separate `AcrPush` data-plane role, neither of which the approved design calls for, so `az acr build` is used.

- [ ] **Step 2: Verify YAML is well-formed and triggers correctly**

Run:
```bash
python3 -c "
import yaml
d = yaml.safe_load(open('.github/workflows/deploy.yml'))
assert d['on']['push']['branches'] == ['master']
assert d['permissions']['id-token'] == 'write'
assert d['jobs']['deploy']['environment'] == 'production'
print('DEPLOY_YAML_OK')
"
```

Expected: `DEPLOY_YAML_OK`.

- [ ] **Step 3: Verify step ordering — build/test happens before any Azure step**

Run:
```bash
python3 -c "
import yaml
steps = yaml.safe_load(open('.github/workflows/deploy.yml'))['jobs']['deploy']['steps']
names = [s['name'] for s in steps]
test_idx = names.index('Test')
login_idx = names.index('Azure login (OIDC)')
assert test_idx < login_idx, f'Test ({test_idx}) must come before Azure login ({login_idx})'
print('BUILD_TEST_BEFORE_AZURE_OK')
"
```

Expected: `BUILD_TEST_BEFORE_AZURE_OK`.

- [ ] **Step 4: Verify no client secret / password is used for Azure login**

Run: `grep -n "client-secret\|AZURE_CREDENTIALS\|creds:" .github/workflows/deploy.yml || echo "NO_SECRET_BASED_LOGIN"`

Expected: `NO_SECRET_BASED_LOGIN` (confirms OIDC-only login — only `client-id`, `tenant-id`, `subscription-id` are passed to `azure/login@v2`).

- [ ] **Step 5: Not automatable — live execution checklist**

Running this workflow for real requires a live Azure subscription and a real GitHub Actions run. When the user pushes to `master` after completing Tasks 1–4 live, they should confirm in the Actions run log: the `Deploy infrastructure (Bicep)` step completes with `"provisioningState": "Succeeded"`; `Read infra outputs` populates all three `steps.infra.outputs.*` values (non-empty); `az acr build` ends with the image pushed and tagged with the commit SHA; `az webapp config container set` and `az webapp restart` both return success; and finally `curl -i https://app-workflow-api.azurewebsites.net/api/system/status` returns `HTTP/2 204` (per README, this endpoint requires no `X-Api-Key`).

- [ ] **Step 6: Commit**
```bash
git add .github/workflows/deploy.yml
git commit -m "Add CD workflow to deploy Main.Api to Azure on push to master"
```

---

### Task 6: README updates

**Files:**
- Modify: `README.md` (insert a new `## Deployment` section immediately before the existing `## Dependencies` heading)

**Interfaces:** Final task — no later task depends on this one. Documents the deployment process and required GitHub secrets for humans, cross-referencing every artifact produced by Tasks 1–5.

- [ ] **Step 1: Insert the `## Deployment` section**

In `README.md`, insert the following new section immediately before the `## Dependencies` heading:

```markdown
## Deployment

`Main.Api` deploys to Azure as a container: **Azure Container Registry** (image storage) → **App Service for Containers** (runtime), with an RBAC-enabled **Key Vault** holding the provider API keys. Infrastructure is defined as code in [`infra/main.bicep`](infra/main.bicep) and applied automatically on every push to `master`.

### One-time setup

Before the first deploy, an operator with Azure AD directory privileges must run the manual OIDC bootstrap once — see [`docs/azure-oidc-bootstrap.md`](docs/azure-oidc-bootstrap.md). It creates the `rg-workflow` resource group (France Central), an Azure AD app registration with a federated credential trusting GitHub Actions runs of `deploy.yml` on `master`, and a `Contributor` role assignment scoped to `rg-workflow`.

That bootstrap prints three values. Add them as **GitHub Actions repository secrets** (Settings → Secrets and variables → Actions), and create a `production` **Environment** (Settings → Environments):

| Secret | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | App registration's application (client) ID |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |

These are the *only* secrets stored in GitHub — they authenticate `deploy.yml` to Azure via OIDC (no client secret, no long-lived credential). Provider API keys never go into GitHub.

After the first successful deploy (which creates the Key Vault), seed the runtime secrets directly into Key Vault, out of band:

```bash
KV_NAME=$(az keyvault list --resource-group rg-workflow --query "[0].name" -o tsv)

az keyvault secret set --vault-name "$KV_NAME" --name API-KEY --value "<your-api-key>"
az keyvault secret set --vault-name "$KV_NAME" --name ANTHROPIC-API-KEY --value "<your-anthropic-key>"
az keyvault secret set --vault-name "$KV_NAME" --name OPENAI-API-KEY --value "<your-openai-key>"
az keyvault secret set --vault-name "$KV_NAME" --name GEMINI-API-KEY --value "<your-gemini-key>"
az keyvault secret set --vault-name "$KV_NAME" --name OPENROUTER-API-KEY --value "<your-openrouter-key>"

az webapp restart --name app-workflow-api --resource-group rg-workflow
```

The App Service's app settings reference these by Key Vault URI (`@Microsoft.KeyVault(SecretUri=...)`, set up by `infra/main.bicep`); the deploy pipeline never reads or handles the actual secret values. See `docs/azure-oidc-bootstrap.md` for full details, including why Key Vault secret names use dashes (`API-KEY`) while the app setting exposed to the app keeps the underscored name (`API_KEY`).

### Ongoing deploys

Every push to `master` runs [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) under the `production` GitHub Environment: restore/build/test (fails fast before touching Azure), OIDC login to Azure, apply `infra/main.bicep` (idempotent), build the container image in ACR via `az acr build`, then point `app-workflow-api` at the new image tag and restart it.

Every pull request against `master` runs [`.github/workflows/ci.yml`](.github/workflows/ci.yml): restore, build (warnings fail the build — `TreatWarningsAsErrors` is set solution-wide in `Directory.Build.props`), and test. It never touches Azure and needs no secrets.

### Container image

[`Main.Api/Dockerfile`](Main.Api/Dockerfile) is a multi-stage build (`dotnet/sdk:10.0` → `dotnet/aspnet:10.0`, `linux-x64`) that installs `tesseract-ocr` for `TesseractOcrTranscriber`, which shells out to the native `tesseract` binary. `Whisper.net.Runtime`'s native binaries ship via NuGet and land in the `linux-x64` publish output automatically — no extra apt package needed for Whisper.

Known trade-off: the Whisper GGML model file (see `Infrastructure/Tools/Transcribers/WhisperTranscriber.cs`) downloads lazily into the container's local filesystem on first transcription request. That filesystem is ephemeral, so the model re-downloads after every redeploy or restart. Acceptable at current scale — not addressed by the Dockerfile.

Deliberately out of scope for this deployment: staging environment/deployment slots, Application Insights/centralized logging, custom domain/TLS.
```

- [ ] **Step 2: Verify the section was inserted in the right place and nothing else moved**

Run: `grep -n "^## " README.md`

Expected: `## Deployment` appears between the section that immediately preceded `## Dependencies` before this edit and `## Dependencies` itself, and every other heading is unchanged and in its original order.

- [ ] **Step 3: Verify all cross-referenced files exist**

Run: `for f in .github/workflows/ci.yml .github/workflows/deploy.yml infra/main.bicep docs/azure-oidc-bootstrap.md Main.Api/Dockerfile; do [ -f "$f" ] && echo "OK: $f" || echo "MISSING: $f"; done`

Expected: five `OK: ...` lines, no `MISSING` lines.

- [ ] **Step 4: Verify the 3 required GitHub secret names and all 5 Key Vault secret names are documented**

Run: `grep -c "AZURE_CLIENT_ID\|AZURE_TENANT_ID\|AZURE_SUBSCRIPTION_ID" README.md && grep -c "API-KEY\|ANTHROPIC-API-KEY\|OPENAI-API-KEY\|GEMINI-API-KEY\|OPENROUTER-API-KEY" README.md`

Expected: both counts are greater than 0 (first ≥ 3, second ≥ 5).

- [ ] **Step 5: Commit**
```bash
git add README.md
git commit -m "Document Azure deployment process and required GitHub secrets"
```

---

### Critical Files for Implementation
- `/home/tigrou/Dev/Workflow/infra/main.bicep`
- `/home/tigrou/Dev/Workflow/Main.Api/Dockerfile`
- `/home/tigrou/Dev/Workflow/.github/workflows/deploy.yml`
- `/home/tigrou/Dev/Workflow/.github/workflows/ci.yml`
- `/home/tigrou/Dev/Workflow/docs/azure-oidc-bootstrap.md`
