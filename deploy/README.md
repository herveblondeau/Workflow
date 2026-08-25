# Deploying Main.Api

`Main.Api` runs on its own host, separate from any client (e.g. filigrane).
It's fronted by [Caddy](https://caddyfile.dev), which terminates HTTPS with
an automatic Let's Encrypt certificate and reverse-proxies to the API
container over the internal docker network.

```
client (e.g. filigrane) --HTTPS + X-Api-Key--> Caddy --HTTP--> workflow-api
```

## Guided setup

```
./deploy/provision.sh
```

Walks through pointing a domain at the VPS, generating `API_KEY`, SSHing in,
cloning/updating the repo, and starting the containers. Safe to re-run: it
remembers values already saved in `deploy/.env`.

## Manual steps

1. Point a domain's A record at the VPS's public IP.
2. Copy `deploy/.env.example` to `deploy/.env` on the VPS and fill in
   `DOMAIN`, `API_KEY`, and any AI provider keys you want to enable.
3. From the repo root on the VPS:
   ```
   docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
   ```
4. Verify: `curl https://<domain>/api/system/status` should return `204`.

## Files

- `docker-compose.yml` — `workflow-api` (built from `Main.Api/Dockerfile`) and
  `caddy` (TLS termination)
- `Caddyfile` — reverse proxy config; reads `DOMAIN` from the environment
- `.env.example` — required/optional variables (mirrors the ones documented
  in the repo root `README.md`)
- `provision.sh` — the guided setup wizard above
