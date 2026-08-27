# Risk list

Ordered roughly by likelihood × impact. Each has a concrete mitigation; several map to a step in the
[go-live checklist](go-live-checklist.md).

## 1. DataProtection keys not persisted → users logged out on redeploy
**Impact:** high, user-visible. If the key ring isn't on the persistent volume, every redeploy
invalidates issued auth tokens and everyone must log in again.
**Mitigation:** mount a persistent volume and set `LOCALAPPDATA=/data` so keys land on it. **Verify
after first deploy** that key XML files exist under the volume. See [hosting-plan.md](hosting-plan.md).

## 2. Network dependency for the cloud install
**Impact:** the cloud web app is unusable if the API↔Supabase link or the user's internet is down.
**Mitigation:** accepted by design (split-installs decision — the always-offline path is the laptop,
not the cloud). Reduce latency/blips by choosing a Supabase region near Pakistan (Mumbai/Singapore),
co-locating the API, and using the connection pooler.

## 3. Wrong / over-privileged database credential
**Impact:** high. Using `service_role` or a superuser widens blast radius and violates the security
model.
**Mitigation:** create a dedicated least-privilege role, grant it only the tenant tables, and put
**only** that connection string in `Persistence__ConnectionString`. Never the `service_role` JWT.
See [environment-variables.md](environment-variables.md).

## 4. Snapshot load/build cost grows with data
**Impact:** medium, grows over time. `GetWorkspaceSnapshotAsync` loads the whole tenant graph; large
sales history makes the first load and each diff heavier.
**Mitigation:** phase 1 is bounded by the cached singleton + diff writes (a checkout writes one sale
row, not all history). If volume warrants, add a granular sales-history query path in phase 1.5.
This is called out as a known follow-up, not a silent cap.

## 5. Single-instance assumption violated
**Impact:** high, data-correctness. The in-memory cache is authoritative only if all writes go
through one process. Scaling to >1 instance would let caches diverge.
**Mitigation:** pin the API to a single instance in phase 1. Scaling out requires cache invalidation
or granular queries first (phase 2). Documented in [hosting-plan.md](hosting-plan.md).

## 6. Connection pooler mode mismatch
**Impact:** medium. The transaction-mode pooler (6543) doesn't hold named prepared statements.
**Mitigation:** use the session-mode pooler/direct connection (5432); if you must use 6543, leave
Npgsql auto-prepare off (default). See [environment-variables.md](environment-variables.md).

## 7. Migration drift / hand-edited schema
**Impact:** medium. Editing tables in the dashboard diverges the DB from
`supabase/migrations`, and the provider maps columns by name — a rename breaks loads.
**Mitigation:** all schema changes go through new migration files; never edit an applied migration or
the live schema by hand. See [schema-and-migrations.md](schema-and-migrations.md).

## 8. NuGet restore needs network on first cloud build
**Impact:** low, one-time. The new Npgsql/Dapper refs must restore during the image build.
**Mitigation:** build where the network reaches nuget.org (CI/build host). The laptop keeps its
repo-local packages; its offline build is unaffected.

## 9. First-run self-seed misfires
**Impact:** medium. If `tenants` isn't empty (e.g. partial prior run) the seed is skipped; if the
seed file is missing, seeding fails.
**Mitigation:** confirm `select count(*) from tenants` is `0` before first boot; confirm
`Data/foundation.json` shipped in the image (it's copied to output by the csproj). Both are
checklist steps.

## 10. No runtime verification in this workstream
**Impact:** medium. Code and unit tests are green, but nothing here was run against a real Supabase
project — that's the user's environment.
**Mitigation:** the [go-live checklist](go-live-checklist.md) is the integration test script: push
migrations, set the connection string, boot, and walk the POS/inventory/role flows before cutover.

## Non-risks (explicitly)
- **Laptop regression:** the `LocalJson` path and all shared code are unchanged; the differ is
  additive and unit-tested. Laptop behavior is byte-for-byte the same.
- **Secret exposure to browser:** same-origin serving + API-only access means no Supabase key ever
  reaches the client.
