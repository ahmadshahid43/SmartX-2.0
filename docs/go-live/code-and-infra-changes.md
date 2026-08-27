# Code and infrastructure changes

Everything below is additive or config-driven. **The LocalJson laptop path is untouched**, and no
Application service, controller, or auth code changed. New/edited files only.

## New code

### `src/OmniBusiness.Infrastructure/Persistence/PostgresWorkspaceRepository.cs` (new)
The cloud store. Implements the existing `IWorkspaceRepository` (all four methods) with Dapper +
Npgsql. Registered as a **singleton** with an in-memory cached `WorkspaceSnapshot` (safe under
single-instance hosting — see [architecture.md](architecture.md)).

- `GetWorkspaceSnapshotAsync` / `GetUserBy*` — serve from the cached snapshot, loading it once on
  first use.
- `UpdateWorkspaceSnapshotAsync` — apply the caller's mutation `Func`, `Normalize`, **diff against
  the cache** (`WorkspaceSnapshotDiffer`), and write only changed rows inside one transaction;
  update the cache on commit.
- `LoadOrSeedAsync` — if `tenants` is empty and `InitializeFromSeedOnFirstRun` is set, read
  `Data/foundation.json`, `Normalize`, and persist via the diff-from-empty path. Otherwise load the
  existing tenant graph.
- Persistence strategy per table: **singletons** upserted unconditionally (`tenants`, `companies`,
  `pos_active_customer`, `workspace_counters`); **replace-all** for small bounded/analytics tables
  (`cart_lines`, `daily_figures`, `sales_trend`, `top_selling`, `branch_performance`,
  form/subscription children); **diff-by-id** for unbounded business tables (`products`, `sales` +
  `sale_lines`, `customers`, `vendors`, `purchase_orders`, `stock_transfers`, `stock_adjustments`,
  `cash_shifts`, `branches`, `app_users`).

Dapper specifics worth knowing before editing this file:
- `DefaultTypeMap.MatchNamesWithUnderscores = true` (static ctor) maps snake_case columns to
  PascalCase record ctor params — so DB columns and C# records stay in their idiomatic casing.
- List deletes use `where id in @Ids` (Dapper expands to `in (@Ids0, @Ids1, ...)`), **not**
  `= any(@Ids)`, and are guarded by `DeletedIds.Count > 0` so the empty case never runs.
- `AdminUser` isn't a field on `AppUser`; it's persisted via an `is_admin` column and reconciled
  with `update app_users set is_admin = (id = @AdminId) where tenant_id = @TenantId`.

### `src/OmniBusiness.Infrastructure/Persistence/WorkspaceSnapshotDiffer.cs` (new)
Pure, DB-free, unit-tested. `DiffById(previous, current, keySelector, equals?)` returns
`{ Upserts, DeletedIds }`. Records give value-equality for free; `SalesEqual` is the one custom
comparer (header-by-value + lines-by-sequence) because `Normalize` rebuilds each sale's `Lines`
array and would otherwise mark all history dirty. Covered by
[`WorkspaceSnapshotDifferTests`](../../tests/OmniBusiness.Infrastructure.Tests/WorkspaceSnapshotDifferTests.cs)
(13 tests).

### `src/OmniBusiness.Infrastructure/Persistence/PersistenceOptions.cs` (edited)
Added `ConnectionString` (bound from `Persistence:ConnectionString`).

## Edited code

### `src/OmniBusiness.Infrastructure/DependencyInjection.cs`
Added to the provider switch:
```csharp
"Supabase" or "Postgres" => ActivatorUtilities.CreateInstance<PostgresWorkspaceRepository>(_),
```
`LocalJson` / `EmbeddedSeed` cases are unchanged.

### `src/OmniBusiness.Api/Program.cs` (three changes)
1. **CORS is config-driven.** Reads `Cors:AllowedOrigins` (string array). If populated, those origins
   are allowed; else in Development only, falls back to `http://localhost:4200`; else (same-origin
   production) no cross-origin policy is needed.
2. **Serves the SPA:** `app.UseDefaultFiles(); app.UseStaticFiles();` before the CORS middleware.
3. **Client-side routing:** `app.MapFallbackToFile("index.html");` after `app.MapControllers();`.

### `src/OmniBusiness.Api/appsettings.Production.json` (new)
`Persistence:Provider=Supabase`, `SeedPath=Data/foundation.json`,
`InitializeFromSeedOnFirstRun=true`, empty `ConnectionString` (**supplied at runtime by
`Persistence__ConnectionString`**), empty `Cors:AllowedOrigins`. **No secrets committed.**

## New infrastructure files

- `supabase/migrations/0001_init.sql` — schema DDL (see [schema-and-migrations.md](schema-and-migrations.md)).
- `supabase/migrations/0002_rls.sql` — RLS + grant hardening (defense-in-depth only).

## New tests / solution

- `tests/OmniBusiness.Infrastructure.Tests/` — new xUnit project (mirrors Application.Tests),
  added to `OmniBusiness.slnx` under `/tests/`.

## Dependencies added

`OmniBusiness.Infrastructure.csproj` gains **Dapper 2.1.79** and **Npgsql 10.0.3**. The laptop build
references them but never loads them at runtime under `LocalJson`.

## What did NOT change

Angular frontend (relative `/api/v1` URLs work same-origin), WPF desktop app, all Application
services, all controllers, authentication (DataProtection bearer + PBKDF2),
`LocalJsonWorkspaceRepository`, `WorkspaceSnapshotNormalization`, and every domain record.
