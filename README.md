# SmartX ERP + POS Starter

SmartX is a local-first ERP + POS starter built from the supplied dashboard, inventory, POS terminal, and visual form-builder references. This repository is intentionally designed to run on a managed office laptop without requiring SQL Server, Windows services, or database administration.

## What is included

- ASP.NET Core 10 API for auth, tenant context, dashboard, inventory, POS, and customization data
- Angular 22 web portal styled from the supplied visual references
- WPF desktop shell for branch-facing operations
- Local JSON persistence provider with a clean repository seam for a future SQL-backed implementation
- Supabase/Postgres provider for cloud go-live without changing the offline laptop mode

## Visual reference sources

The UI implementation in this repo is based on these local design references:

- `omnibusiness_executive_dashboard/code.html`
- `omnibusiness_inventory_overview/code.html`
- `omnibusiness_universal_pos_terminal/code.html`
- `omnibusiness_visual_form_builder/code.html`
- `omnibusiness/DESIGN.md`

## Why the project does not depend on SQL

Your stated constraint was that the target machine is a domain-joined office laptop where SQL cannot be installed or run reliably. Because of that, the runtime storage strategy defaults to `LocalJson`, not SQL Server.

On first run, the API copies the seed dataset from `src/OmniBusiness.Api/Data/foundation.json` into a writable local file:

- `%LOCALAPPDATA%\OmniBusiness\foundation.local.json`

That local file becomes the runtime data source. The controller layer and application layer only depend on `IWorkspaceRepository`, so a SQL-backed repository can be added later without rewriting the API surface or web UI.

## Run locally

Open a PowerShell terminal in the repository root and prepare local CLI folders:

```powershell
$root = (Get-Location).Path
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet'
$env:NUGET_PACKAGES = Join-Path $root '.nuget\packages'
$env:APPDATA = Join-Path $root '.appdata'
$env:LOCALAPPDATA = Join-Path $root '.localappdata'

New-Item -ItemType Directory -Force -Path (Join-Path $env:APPDATA 'NuGet') | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'NuGet.Config') -Destination (Join-Path $env:APPDATA 'NuGet\NuGet.Config') -Force
```

Restore, build, and test:

```powershell
dotnet restore OmniBusiness.slnx --configfile NuGet.Config --packages .nuget\packages
dotnet build OmniBusiness.slnx --no-restore
dotnet test OmniBusiness.slnx --no-build
```

Run the API:

```powershell
dotnet run --project src/OmniBusiness.Api --launch-profile http
```

The API will start on `http://localhost:5163` and Swagger will be available at `http://localhost:5163/swagger`.

Run the Angular portal in a second terminal:

```powershell
Set-Location web/omnibusiness-web
npm.cmd ci
npm.cmd start
```

The Angular app runs on `http://localhost:4200` and proxies API calls to the local backend.

If PowerShell blocks `npm.ps1` on a domain-managed laptop, use `npm.cmd` instead of `npm`.

## Easiest Windows startup

From the repository root, you can also use the included batch files:

- `run-api.cmd` - starts the backend API
- `run-web.cmd` - starts the Angular frontend using `npm.cmd`
- `run-desktop.cmd` - starts the WPF shell
- `run-demo.cmd` - opens API and web app in separate windows

## Deploy as one cloud service

For the recommended live setup, the built Angular app is served by the ASP.NET Core API from the
same origin. That keeps the browser talking only to `/api/v1/...` and avoids putting any Supabase
key in frontend code.

Build the container image:

```powershell
docker build -t smartx:latest .
```

Run it locally in production mode with the offline provider:

```powershell
docker run --rm -p 8080:8080 -v smartx-data:/data -e Persistence__Provider=LocalJson smartx:latest
```

Run it for cloud mode with Supabase/Postgres:

```powershell
docker run --rm -p 8080:8080 `
  -v smartx-data:/data `
  -e Persistence__Provider=Supabase `
  -e Persistence__ConnectionString="Host=<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=smartx_app;Password=<secret>;SSL Mode=Require;Pooling=true;Maximum Pool Size=10" `
  smartx:latest
```

After startup:

- `/` opens the SmartX web app when `wwwroot` is present in the container image
- `/swagger` opens the API explorer
- `/health` and `/ready` expose health checks

For the full go-live checklist, environment variables, rollback plan, and Supabase migration notes,
see `docs/go-live/`.

## Default login

- Email: `admin@omnibusiness.local`
- Password: `Admin@123`

## Key folders

- `src/OmniBusiness.Domain` - core business models
- `src/OmniBusiness.Application` - contracts, use-case services, repository abstractions
- `src/OmniBusiness.Infrastructure` - auth, persistence providers, DI
- `src/OmniBusiness.Api` - REST API, seed data, Swagger
- `desktop/OmniBusiness.Desktop` - WPF operator shell
- `web/omnibusiness-web` - Angular frontend
- `tests` - unit tests
- `docs` - architecture, storage, API, deployment, and testing notes

## Migration path back to SQL later

When SQL becomes available again, keep the UI and controller layers as they are and change the persistence implementation behind the repository boundary:

1. Add a SQL-backed implementation of `IWorkspaceRepository`.
2. Map the current workspace snapshot into SQL tables or views.
3. Extend the provider switch in `src/OmniBusiness.Infrastructure/DependencyInjection.cs`.
4. Change `Persistence:Provider` in configuration after the SQL repository is ready.

Until that point, the project remains runnable with no SQL dependency.
