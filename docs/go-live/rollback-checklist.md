# Rollback checklist

Rollback is cheap by design: the cloud store is selected by one config switch, the API image is
versioned, and **the office laptop is never affected by anything here**. Pick the smallest rollback
that addresses the problem.

## Decision guide

| Symptom | Rollback |
| --- | --- |
| Bad app build / regression in the new image | **A. Redeploy previous image tag** |
| Postgres provider misbehaving, need service back fast | **B. Flip provider** (only viable if a reachable LocalJson seed is acceptable for the cloud host) |
| Schema/migration problem | **C. Restore DB from Supabase backup / down-migration** |
| Everything wrong, abort the launch | **A + C**, keep laptops as the source of truth |

## A. Redeploy the previous API image (fastest, most common)
1. Redeploy the last-known-good image tag to the single instance.
2. Keep the **same** `Persistence__ConnectionString` and persistent volume, so data and the
   DataProtection key ring carry over and users stay logged in.
3. Confirm `/health`, login, and a POS checkout.

This is the default rollback for any app-code problem. It does **not** touch the database.

## B. Flip the persistence provider
The provider is chosen by `Persistence:Provider`. Setting it back to `LocalJson` (+ redeploy) makes
the cloud host use a JSON store instead of Postgres.
- Use this only if running the cloud host on a local JSON store is acceptable as a stopgap — note it
  will **not** contain the data written to Postgres since seeding, and it reintroduces a file store
  on the server. For most incidents, **A** (previous image, same Postgres) is preferable.
- No code change required — it's a config/env change (`Persistence__Provider=LocalJson`) plus
  ensuring a seed/`foundation.local.json` is present on the host.

## C. Database rollback
1. If a migration caused the problem, restore from a **Supabase backup** taken before the migration,
   or apply a corrective down-migration as a **new** migration file (never edit an applied one).
2. If seeding wrote bad data on a fresh project, the simplest reset is to restore the empty state and
   re-run first-run self-seed (`tenants` empty + `InitializeFromSeedOnFirstRun=true`).
3. Re-verify with the [go-live checklist](go-live-checklist.md) step 5 flow before reopening.

## The laptop is your ultimate fallback
The office laptop install is a completely independent `LocalJson` deployment. If the entire cloud
launch is aborted, day-to-day operations continue on the laptop exactly as before — no data or config
on it changed. Treat it as the guaranteed-good baseline while the cloud issue is resolved.

## After any rollback
- [ ] Record what failed and which rollback was used.
- [ ] Preserve logs and (if DB-related) a snapshot for diagnosis.
- [ ] Re-run the relevant pre-flight/integration steps before attempting go-live again.
