# Go-live checklist

Ordered cutover steps. Steps 1–6 are the integration test the code hasn't yet had against a real
Supabase project — do them in a staging project first, then repeat for production.

If you are taking the cheaper VM-first route, do the Oracle VM deploy first with `LocalJson`, then
repeat the Supabase steps later when you are ready for the database cutover.

## Pre-flight (build side)
- [ ] `dotnet build OmniBusiness.slnx` is clean (with the new Npgsql/Dapper refs).
- [ ] `dotnet test OmniBusiness.slnx` is green (Infrastructure + Application + Domain suites).
- [ ] Angular production build succeeds: `ng build --configuration production`.
- [ ] API image builds with the SPA copied into `wwwroot/` and `Data/foundation.json` present in the
      publish output.

## 1. Supabase project + schema
- [ ] Create the Supabase project in a region near Pakistan (Mumbai/Singapore).
- [ ] `supabase link --project-ref <ref>` then `supabase db push` (applies `0001_init.sql`,
      `0002_rls.sql`).
- [ ] Create the **dedicated least-privilege app role** (e.g. `smartx_app`) and grant it the tenant
      tables. Do **not** plan to use `service_role`.
- [ ] Confirm `select count(*) from tenants;` returns `0` (so first-run seed will populate).

## 2. Configure the API host
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `Persistence__ConnectionString=` the app-role string, `SSL Mode=Require`, session-mode pooler
      (port 5432). (Secret — set in the platform secret manager.)
- [ ] Attach a persistent volume; set `LOCALAPPDATA=/data` (or mount at the key path). 
- [ ] Set `Persistence__BootstrapOwnerPassword=` to a brand-new owner password before first public boot.
- [ ] Same-origin SPA: leave `Cors:AllowedOrigins` empty. (Split hosting only: set
      `Cors__AllowedOrigins__0`.)
- [ ] Pin to a **single instance**.

## 3. First boot + self-seed
- [ ] Deploy and start the API.
- [ ] Watch logs: first-run self-seed reads `Data/foundation.json` and populates the tenant graph.
- [ ] `select count(*) from tenants;` now returns `1`; spot-check `companies`, `products`,
      `app_users`.
- [ ] **Verify DataProtection key files exist on the persistent volume** (under
      `/data/OmniBusiness/keys` or your mounted key path). This is the easiest thing to get wrong.

## 4. Health + auth
- [ ] `GET /health` and `/ready` (or the app's health endpoints) return healthy.
- [ ] Load the web app; it's served same-origin from the API.
- [ ] Log in as owner/admin — succeeds; token validates.

## 5. Functional walk-through (the real integration test)
- [ ] **POS checkout:** ring up a sale → confirm a new row in `sales` + its `sale_lines`, and that
      the sold `products.in_hand` decremented. Confirm the invoice sequence advanced
      (`workspace_counters.next_sale_sequence`).
- [ ] **Inventory:** CSV import / stock adjustment persists and reloads correctly.
- [ ] **Roles:** log in as a cashier/client user → owner-only/admin-only modules are **not**
      accessible (server-side, not just hidden).
- [ ] **Printing / invoice flow:** print/preview works as before (client-side).
- [ ] Redeploy once and confirm you are **still logged in** (proves the key ring persisted).

## 6. Cutover
- [ ] Point the production DNS/origin at the API host.
- [ ] Announce the web app URL.
- [ ] Keep the previous image tag and the pre-cutover state handy (see
      [rollback-checklist.md](rollback-checklist.md)).

## 7. Laptop (confirm untouched)
- [ ] On the office laptop, the app still runs `Provider=LocalJson`, offline, unchanged. No action
      needed beyond confirming it wasn't disturbed.

## Product decisions still open (do NOT ship silently)
These were flagged earlier and are **not** part of phase 1 unless the user decides — don't let go-live
imply they're handled: refund/return endpoint (G1), split/mixed payment (G2), manual discount (G3),
POS customer capture (G4), support-widget copy accuracy (G5), flat-25% gross-profit assumption (G6).
