# 4. Drop Caddy, ship Main.Api standalone for the operator's own reverse proxy

**Ref:** `ea01ce7` (PR `#9`)

- **Added:**
  - `deploy/docker-compose.yml` now only runs `workflow-api`, publishing `127.0.0.1:8080` (loopback only) instead of bundling a `caddy` service
  - `deploy/provision.sh`: dropped the DNS/domain stage (5 -> 4 stages); verify stage curls `127.0.0.1:8080` over SSH instead of a public HTTPS domain
  - `deploy/README.md`, `deploy/.env.example`: dropped `DOMAIN`; documented that fronting the API (reverse proxy, TLS) is the hosting operator's decision, not this repo's
- **Removed:** `deploy/Caddyfile`, the `caddy` service and its `caddy-data`/`caddy-config` volumes
- **Revealed:** stone 3's "Main.Api and any client must run on separate hosts" insight didn't go far enough - the reverse proxy in front of the API is also the hosting operator's call, not something this repo's deploy scaffolding should decide for them (in this case, an existing nginx instance on the VPS)
- **Demo:**
  - `./deploy/provision.sh` deploys `Main.Api` to the VPS, bound to `127.0.0.1:8080`
  - Point any reverse proxy at that port and terminate TLS there; verified against the real VPS (a previously-provisioned Caddy container had in fact never started, since host nginx already held 80/443 - cleaned up manually, outside this PR)
- **Test:** no new application logic to pin; `dotnet build` and `docker compose config` verified clean
