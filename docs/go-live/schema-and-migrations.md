# Schema and migrations

## Where the DDL lives

```
supabase/migrations/
  0001_init.sql   -- all tables that map WorkspaceSnapshot
  0002_rls.sql    -- enable RLS + revoke anon/authenticated grants (defense-in-depth only)
```

This is the standard Supabase CLI layout, so `supabase db push` (or `supabase migration up`) applies
them in order. Nothing here runs automatically from the app — migrations are an operator step in the
[go-live checklist](go-live-checklist.md).

## Type-mapping convention

The DDL is a mechanical projection of the domain records in `WorkspaceSnapshot`:

| C# type                    | Postgres type      | Notes                                   |
| -------------------------- | ------------------ | --------------------------------------- |
| `Guid`                     | `uuid`             | primary keys, `tenant_id`, FKs          |
| `string`                   | `text`             | nullable when the field is `string?`    |
| money `decimal`            | `numeric(18,2)`    | prices, totals, tax                     |
| `int`                      | `integer`          | quantities, counters                    |
| `bool`                     | `boolean`          | flags incl. `is_admin` (see below)      |
| `DateOnly`                 | `date`             |                                         |
| `DateTimeOffset`           | `timestamptz`      | timezone-aware                          |
| enum (e.g. `FormFieldType`)| `text`             | stored as the enum name string          |

Column names are **snake_case**; Dapper's `MatchNamesWithUnderscores` maps them back to the
PascalCase record constructor parameters, so neither side has to compromise on idiom.

## Table families

- **Singletons (one row per tenant):** `tenants`, `companies`, `pos_active_customer`,
  `workspace_counters` (holds `next_sale_sequence`), `form_definitions`, `subscription_plan_settings`.
- **Unbounded business tables (diff-by-id writes):** `products`, `customers`, `vendors`,
  `purchase_orders`, `stock_transfers`, `stock_adjustments`, `cash_shifts`, `branches`, `app_users`,
  and `sales`.
- **Child tables (cascade from parent, ordered by `ordinal`):** `sale_lines` (→ `sales`),
  `module_entitlements` (→ `subscription_plan_settings`), and the form library/canvas field tables
  (→ `form_definitions`).
- **Analytics / small bounded (replace-all writes):** `daily_figures`, `sales_trend`, `top_selling`,
  `branch_performance`, `cart_lines`.

Every business table carries `tenant_id uuid` so the schema is multi-tenant-ready even though phase 1
runs a single tenant. Child tables FK to their parent with `on delete cascade`, so deleting a sale
removes its lines in one statement.

### Two columns that aren't obvious from the records

- **`app_users.is_admin boolean`** — `WorkspaceSnapshot.AdminUser` is a pointer, not a field on the
  `AppUser` record. It's persisted as a per-row flag and reconciled on save. On load, the admin is
  the `is_admin = true` row (fallback: the Owner-role user, then the first user).
- **`workspace_counters.next_sale_sequence`** — the POS invoice sequence; defaults to `8902` if the
  row is missing (matches the seed).

## Applying migrations

```bash
supabase link --project-ref <your-project-ref>
supabase db push
```

Verify afterwards that the tenant table exists and is empty (`select count(*) from tenants;` → `0`)
so the app's first-run self-seed will populate it. Re-running `db push` is safe — the migrations are
the source of truth; do not hand-edit tables in the dashboard.

## Data seeding

There is **no separate import tool**. On first cloud start with an empty `tenants` table and
`InitializeFromSeedOnFirstRun=true`, the API reads the same `Data/foundation.json` it ships with,
normalizes it through the app's own `WorkspaceSnapshotNormalization`, and writes it via the
diff-from-empty path. This guarantees the seed matches the app's exact object graph.

## Changing the schema later

The provider maps columns by name to record constructor params. If you add a domain field:
1. Add the column in a **new** migration (`0003_*.sql`) — never edit an applied migration.
2. Add the field to the record and to the provider's load/persist SQL for that table.
3. Extend the differ test if the field affects change detection.
