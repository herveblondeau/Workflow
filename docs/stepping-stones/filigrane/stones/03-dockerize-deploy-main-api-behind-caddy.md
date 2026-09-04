# 3. Dockerize and deploy Main.Api behind Caddy, on its own host

**Ref:** `6049f44` (Workflow PR `#8`), filigrane `4fe52de` (PR `#5`)

- **Added:**
  - `Main.Api/Dockerfile` (multi-stage, `sdk:10.0` -> `aspnet:10.0`, build context is the repo root since `Main.Api` references `Core`/`Infrastructure` via relative `ProjectReference`s)
  - `Program.cs`: trusts `X-Forwarded-*` from Caddy so `UseHttpsRedirection` doesn't loop once TLS is terminated upstream on the container network
  - `deploy/docker-compose.yml` + `deploy/Caddyfile`: `workflow-api` + `caddy` (automatic Let's Encrypt cert from just a domain name)
  - `deploy/provision.sh`: guided wizard (DNS, SSH, secrets, deploy, verify) for the VPS-side steps, re-runnable
  - filigrane: deleted `api/` (the old backend) and `filigrane.sln`; `docker-compose.yml` loses the `api` service
- **Revealed:**
  - `Main.Api` and any client must run on separate hosts (confirmed, not assumed): the API serves arbitrary clients with different needs, not just filigrane
  - Deploying filigrane's frontend publicly is blocked on filigrane getting its own user-level auth first - the shared `X-Api-Key` authenticates "this is filigrane," not individual users, so a public frontend with no login would turn `Main.Api` into an unauthenticated public gateway
  - Single-API vs. splitting `Infrastructure` into per-tool-group NuGet packages is a real open question, deliberately not decided now (one consumer, no second real case yet) - logged in Deliberately deferred rather than acted on
- **Demo:**
  - `./deploy/provision.sh` (Workflow repo) deploys `Main.Api` to the VPS behind Caddy
  - `cd filigrane/frontend && WORKFLOW_API_URL=https://<domain> WORKFLOW_API_KEY=<same key> npm run dev` - watermarking round-trips through the real deployed `Main.Api` over real HTTPS
- **Test:** no new application logic to pin; verified via `dotnet publish`/`dotnet build` (Docker daemon unavailable in the build sandbox - `docker build -f Main.Api/Dockerfile .` still worth a manual run)
- **Note:** filigrane's *production* `nginx.conf`/`docker-compose.yml` deliberately left unwired to the deployed `Main.Api` - deferred to the stone that also adds filigrane's own user auth
