# Running in Docker

The viewer runs as a container that reads its step library from a mounted
volume — updating the library means replacing the mounted file and
restarting, not rebuilding the image.

## 1. Provide a library file

Drop a valid `step-library.v1.json` at `./data/step-library.json`:

```sh
mkdir -p data
cp triangle-step-library.json data/step-library.json   # or your own library
```

## 2. Build and run

Production-style (auth enabled — requires AzureAd config, see US-12):

```sh
docker compose up --build
```

Development (no auth, dev bypass user "QA"):

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

The app listens on http://localhost:8080.

## 3. Health check

```sh
curl http://localhost:8080/health
# {"status":"healthy"}   (200)  when the library is loaded
# {"status":"unhealthy","reason":"..."}  (503)  otherwise
```

## Configuration

| Env var | Purpose | Default (container) |
|---------|---------|---------------------|
| `DOCVIEW_LIBRARY_PATH` | Path to the step library inside the container | `/data/step-library.json` |
| `ASPNETCORE_ENVIRONMENT` | `Development` bypasses auth; otherwise Entra ID is required | `Production` |

The library file is **not** baked into the image — it is read from the
`./data:/data` read-only mount at runtime.
