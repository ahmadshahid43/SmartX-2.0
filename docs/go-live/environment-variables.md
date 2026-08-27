# Environment variables

ASP.NET Core maps `__` (double underscore) in an env var to `:` in config, so
`Persistence__ConnectionString` sets `Persistence:ConnectionString`. Env vars override
`appsettings*.json`. **Secrets are only ever set as env vars — never committed.**

## Required (cloud API)

| Variable | Example | Purpose |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Loads `appsettings.Production.json`; disables dev-only CORS fallback. |
| `Persistence__Provider` | `Supabase` | Selects `PostgresWorkspaceRepository`. (Already set in `appsettings.Production.json`; env is the override knob.) |
| `Persistence__ConnectionString` | *(see below)* | **SECRET.** Npgsql connection string for the dedicated app role. |
| `LOCALAPPDATA` | `/data` | Steers the DataProtection key ring onto the persistent volume. Mount the volume at this path. See [hosting-plan.md](hosting-plan.md). |
| `Persistence__BootstrapOwnerPassword` | `YourStrongOwnerPassword` | **SECRET.** Required for safe first public seed. Replaces the old demo owner password on first boot. |

## Optional

| Variable | When | Notes |
| --- | --- | --- |
| `Persistence__InitializeFromSeedOnFirstRun` | default `true` | Set `false` after the first successful seed if you want to hard-disable re-seeding. Harmless to leave `true` (only seeds when `tenants` is empty). |
| `Persistence__BootstrapOwnerEmail` | public go-live | Override the seeded owner email on first boot. |
| `Persistence__BootstrapOwnerDisplayName` | public go-live | Override the seeded owner display name on first boot. |
| `Cors__AllowedOrigins__0` | **split hosting only** | e.g. `https://app.example.com`. Leave unset for the recommended same-origin deployment. Add `__1`, `__2`, … for more origins. |

## The connection string

Use a **dedicated least-privilege Postgres role** created by the migration/hardening step — **never**
the Supabase `service_role` JWT, and never any Supabase API key. TLS is required.

```
Host=<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=smartx_app;Password=<secret>;SSL Mode=Require;Pooling=true;Maximum Pool Size=10
```

- **Port 5432 = session-mode pooler / direct connection.** Recommended for this long-lived single
  instance — it supports everything Npgsql does.
- The **transaction-mode pooler (port 6543)** works too, but keep Npgsql auto-prepare off (it is off
  by default — don't set `Max Auto Prepare`) since pgBouncer transaction mode doesn't hold named
  prepared statements. Given a single small instance, session mode (5432) is simpler; prefer it.
- `SSL Mode=Require` encrypts the connection. For certificate verification use `SSL Mode=VerifyFull`
  plus Supabase's CA root cert (`Root Certificate=...`); optional in phase 1 but recommended.
- Keep `Maximum Pool Size` modest (single instance, cached snapshot → few concurrent DB ops).

## Hard rules

- **No Supabase key in the browser.** The SPA calls the API only; it never holds anon, service_role,
  or any Supabase credential.
- **`service_role` is never used by the app.** The API authenticates as `smartx_app` (or your chosen
  dedicated role), which owns/has grants on the tenant tables and bypasses RLS by role, not by JWT.
- **The connection string is the only real secret** here. Store it in the platform's secret manager,
  not in the image, repo, or logs.

## Laptop (unchanged, for contrast)

The office laptop sets `Persistence:Provider=LocalJson` (its existing config) and needs **none** of
the above — no connection string, no Supabase, no network. Nothing in this project changes it.
