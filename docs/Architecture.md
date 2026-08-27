# Architecture

## Design inputs

This starter was built from the supplied local prompt and visual references under:

- `omnibusiness/DESIGN.md`
- `omnibusiness_executive_dashboard`
- `omnibusiness_inventory_overview`
- `omnibusiness_universal_pos_terminal`
- `omnibusiness_visual_form_builder`

Those artifacts shaped the UX direction. The runtime architecture was then adjusted to match the user's stronger requirement: the app must work on a domain-managed office laptop without SQL.

## Solution layout

- `OmniBusiness.Domain`
  - business entities and aggregate-style read models for tenants, branches, products, POS, and custom forms
- `OmniBusiness.Application`
  - service contracts, DTOs, authentication use cases, workspace query use cases, repository abstractions
- `OmniBusiness.Infrastructure`
  - persistence providers, token protection, password hashing, authentication handler, dependency wiring
- `OmniBusiness.Api`
  - REST controllers, exception handling, health checks, Swagger, seed configuration
- `OmniBusiness.Desktop`
  - WPF shell using the same visual language as the web app
- `omnibusiness-web`
  - Angular frontend consuming the local API

## Local-first runtime strategy

The runtime path is intentionally simple:

1. `LocalJsonWorkspaceRepository` is registered by default.
2. On first run, it copies `src/OmniBusiness.Api/Data/foundation.json` into a writable local file under `%LOCALAPPDATA%\OmniBusiness`.
3. Application services query `IWorkspaceRepository`.
4. Controllers stay unaware of whether data came from JSON, seed content, or a future database provider.

This gives the project a working offline-friendly path that does not require SQL Server, service installation, or elevated database access.

## Migration seam

The main seam for future infrastructure changes is `IWorkspaceRepository`. When SQL becomes available again, replace only the infrastructure implementation and DI registration. The application and API layers can remain stable if the new repository continues returning the same workspace models.

## Current limitations

- The current API is seed-data driven and focused on read flows plus authentication.
- The WPF desktop app is a shell and not yet connected to live API calls.
- The legacy SQL file in `docs/sql` is not part of the runtime and should be treated as reference material only.
