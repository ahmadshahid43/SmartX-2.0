# Target architecture (phase 1)

```
Angular SPA (static)  ─┐
                       ├─►  ASP.NET Core API  ──►  Supabase Postgres (cloud)
WPF desktop client  ───┘     (managed container PaaS,   (dedicated least-privilege
                              single instance,           app role, NOT service_role)
                              serves SPA + /api/v1,
                              persistent volume: DP keys)

Office laptop:  same API binary, Persistence:Provider = LocalJson  (unchanged, offline)
```

## Request flow

1. The browser loads the Angular SPA. In the recommended deployment the **API container serves the
   built SPA at its own origin**, so the SPA's relative `/api/v1/...` calls are same-origin — no CORS,
   no Supabase key in the browser, no frontend code change.
2. The SPA (or the WPF desktop client) calls the ASP.NET Core API with a bearer token issued by the
   existing custom auth (DataProtection-protected token + PBKDF2 password hashing). **Auth is
   unchanged.**
3. The API resolves `IWorkspaceRepository` — bound to `PostgresWorkspaceRepository` in the cloud —
   and reads/writes the workspace.
4. `PostgresWorkspaceRepository` talks to Supabase Postgres as a **dedicated least-privilege
   Postgres role** over a normal connection string with TLS. It never uses the Supabase
   `service_role` JWT and never exposes any key to clients.

## Why the store is a drop-in provider

`IWorkspaceRepository` is snapshot-oriented:

```csharp
Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync(ct);                       // whole graph
Task<WorkspaceSnapshot> UpdateWorkspaceSnapshotAsync(Func<snap,snap>, ct);   // read-modify-write
```

The registration seam already existed (a `Provider` switch in `DependencyInjection`). We added
`Supabase`/`Postgres` cases pointing at the new provider. Application/controllers/auth are unaware
of the store — exactly as the LocalJson provider proved.

## The one non-obvious design decision: diff-based writes

A naïve Postgres implementation of `UpdateWorkspaceSnapshotAsync` would rewrite **every** row of
**every** table (all sales history included) on each POS checkout, because the contract hands you
the whole mutated graph.

Instead the provider keeps an **in-memory cached snapshot** and, on save, **diffs** the mutated
snapshot against the cache (`WorkspaceSnapshotDiffer`) and writes only the changed rows inside a
single transaction. C# `record` value-equality makes the diff cheap. A checkout becomes *one* new
`sales` row + its `sale_lines` + the touched `products`, not a full-table rewrite.

- The cache is **safe because the cloud API runs single-instance** (see hosting-plan.md). Every
  write goes through this one process, so the cache is always authoritative.
- **Sales need a custom comparer** (`WorkspaceSnapshotDiffer.SalesEqual`). `WorkspaceSnapshotNormalization.Normalize`
  rebuilds every `SaleRecord` with a brand-new `Lines` array on every save, so default record
  equality (which compares `Lines` by reference) would flag every historical sale as changed. The
  comparer compares the header by value and the lines by sequence, so untouched history diffs clean.

## Self-seeding

On first cloud start the `tenants` table is empty. Guarded by `InitializeFromSeedOnFirstRun`, the
provider reads the same `Data/foundation.json` the laptop uses, runs it through the shared
`WorkspaceSnapshotNormalization.Normalize`, and persists it via the diff-from-empty path. No
separate importer tool; the seed is faithful to the app's own shape.

## Deferred to phase 2+ (with rationale)

- **Supabase Auth** — the custom bearer auth already works; migrating identities is its own project.
- **Supabase Storage** — no image/file domain fields exist; invoices print client-side.
- **Edge Functions** — business logic stays in ASP.NET; rewriting adds risk, not value.
- **RLS as the security boundary** — the API is the boundary in phase 1; RLS is defense-in-depth
  only (see schema-and-migrations.md).
- **Laptop↔cloud sync** — split installs by decision; a conflict/sync engine is out of scope.
