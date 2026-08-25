# Deploying Main.Api

`Main.Api` runs on its own host, separate from any client (e.g. filigrane).
It doesn't bring its own reverse proxy: `docker compose` here only starts
`workflow-api`, bound to `127.0.0.1:8080` on the VPS. Fronting it with a
reverse proxy (nginx, Caddy, whatever's already running on the box) and
terminating TLS is left to whoever's hosting the API.

```
client (e.g. filigrane) --HTTPS + X-Api-Key--> your reverse proxy --HTTP--> workflow-api (127.0.0.1:8080)
```

## Guided setup

```
./deploy/provision.sh
```

Walks through generating `API_KEY`, SSHing in, cloning/updating the repo,
and starting the container. Safe to re-run: it remembers values already
saved in `deploy/.env`. It doesn't touch your reverse proxy config; wire
that up yourself (point it at `127.0.0.1:8080`, forward `X-Forwarded-For`/
`X-Forwarded-Proto`, and it doesn't need to be told about `API_KEY`).

## Manual steps

1. Copy `deploy/.env.example` to `deploy/.env` on the VPS and fill in
   `API_KEY` and any AI provider keys you want to enable.
2. From the repo root on the VPS:
   ```
   docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
   ```
3. Point your reverse proxy at `127.0.0.1:8080` and terminate TLS there.
4. Verify: `curl http://127.0.0.1:8080/api/system/status` on the VPS (or
   through your reverse proxy's public URL) should return `204`.

## Files

- `docker-compose.yml` — `workflow-api` only, built from `Main.Api/Dockerfile`,
  published to `127.0.0.1:8080` so only the host's own reverse proxy can
  reach it
- `.env.example` — required/optional variables (mirrors the ones documented
  in the repo root `README.md`)
- `provision.sh` — the guided setup wizard above
