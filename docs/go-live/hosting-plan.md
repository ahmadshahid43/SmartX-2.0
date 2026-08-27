# Hosting plan

## Components and where they run

| Component        | Where                                             | Notes                                    |
| ---------------- | ------------------------------------------------- | ---------------------------------------- |
| ASP.NET Core API | Managed container PaaS (Fly.io / Render / Railway) or a small Linux VM, **single instance** | Serves `/api/v1` **and** the SPA. Needs a persistent volume. |
| Angular SPA      | Served by the API container at the same origin    | Built output copied into the API's `wwwroot`. |
| Supabase Postgres| Supabase cloud, region near Pakistan (Mumbai/Singapore) | Reached via a dedicated app role, not `service_role`. |
| WPF desktop      | Office laptop, installed separately               | Unchanged; offline `LocalJson`.          |

## Why single instance

The Postgres provider caches the workspace snapshot in memory and diffs against it to avoid rewriting
all history on every write. That cache is authoritative **only if every write goes through one
process.** So phase 1 pins the API to a single instance. This also sidesteps multi-instance
DataProtection key-sharing. Scaling out is a phase-2 change (shared cache invalidation or granular
queries), not a config toggle — do not raise the instance count without that work.

## Same-origin SPA (recommended, zero frontend change)

`Program.cs` calls `UseDefaultFiles()` + `UseStaticFiles()` and `MapFallbackToFile("index.html")`.
Put the Angular production build in the API's `wwwroot/`:

1. `ng build --configuration production`
2. Copy `dist/<app>/` into the API image's `wwwroot/` (Dockerfile `COPY`, or a build step).
3. The API serves the SPA at `/`, its API at `/api/v1`, and falls back unknown routes to
   `index.html` for client-side routing.

Because the SPA calls **relative** `/api/v1/...` URLs, same-origin means **no CORS and no Supabase
key in the browser**. Leave `Cors:AllowedOrigins` empty.

### Alternative: split hosting (only if you must)
Host the SPA on a CDN/static host on a different origin. Then:
- Set `Cors__AllowedOrigins__0=https://app.example.com` on the API.
- Add a runtime `app-config.json` + base-URL interceptor to the SPA so it targets the API origin.
  (Not built in phase 1 — build it only if you choose this path.)
Same-origin is simpler and strictly safer; prefer it.

## DataProtection key ring — the deploy-critical detail

Auth tokens are protected with ASP.NET DataProtection. If the key ring is lost on redeploy, **every
logged-in user is signed out** and issued tokens stop validating.

The app picks the key directory via `WritableStoragePathResolver`, and the **first** candidate it
tries is derived from the `LOCALAPPDATA` environment variable (falling back to the OS local-appdata
path, which on Linux is `$HOME/.local/share`). So the clean, deterministic control is:

1. Attach a **persistent volume** to the single instance, e.g. mounted at `/data`.
2. Set `LOCALAPPDATA=/data`. The key ring then lands at `/data/OmniBusiness/keys` on the volume and
   survives redeploys.

If you can't set `LOCALAPPDATA`, mount the volume at `$HOME/.local/share/OmniBusiness/keys` instead
(the same first-candidate path). Either way, confirm after first deploy that key XML files are being
written under the volume — this is the single easiest thing to get wrong.

## Container image notes

- Base: the .NET 10 ASP.NET runtime image; publish `OmniBusiness.Api` with
  `ASPNETCORE_ENVIRONMENT=Production`.
- `Data/foundation.json` is already copied to the publish output (csproj `CopyToOutputDirectory`), so
  first-run self-seed works in the container with no extra steps.
- Expose the app port; put the platform's TLS/ingress in front (Supabase and the browser both talk
  TLS; see the connection string in [environment-variables.md](environment-variables.md)).

## Region and connectivity

Pick a Supabase region close to Pakistan (Mumbai or Singapore) to keep API↔DB latency low, and host
the API container in or near the same region. Use the Supabase **session-mode pooler** (or direct
connection) for this long-lived single instance — see the connection-string guidance in
[environment-variables.md](environment-variables.md).

## Desktop client

The WPF app stays a separately installed client on the office laptop, running `Provider=LocalJson`
fully offline. It was not modified in phase 1. If you later want the desktop app to talk to the cloud
API instead of its local store, that's a desktop-config + packaging task (point its API base URL at
the hosted origin) — flagged as needs-inspection, not done here.
