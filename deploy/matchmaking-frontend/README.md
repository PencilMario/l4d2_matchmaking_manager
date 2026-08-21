# Matchmaking Frontend Deployment

This Compose service exposes the desktop management frontend without changing the
host's existing port 80 service. It serves static assets and proxies only
`/healthz` and `/v1` to the Core Controller through the existing private
`l4d-matchmaking` Docker network.

For the `100.72.137.92` deployment, create `.env` from `.env.example`, upload the
contents of `frontend/dist` to a versioned directory below
`/home/sirp/l4d-matchmaking-frontend/site/releases/`, then point the `current`
symlink to that release before running:

```sh
docker compose up -d
```

The public management URL is `http://100.72.137.92:8084`. The Core Bearer token
is entered in the browser and is never included in this Compose configuration.
