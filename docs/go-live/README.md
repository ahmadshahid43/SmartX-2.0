# SmartX — Phase 1 Supabase Go-Live

This folder is the go-live playbook for taking **SmartX** (the OmniBusiness ERP + POS) live while
keeping the office-laptop install running offline and unchanged.

Fastest safe path right now:

- first public deploy on `Render` with `LocalJson` plus a persistent disk
- set a fresh owner password on first boot
- let the app auto-lock non-owner demo users
- cut over to `Supabase Postgres` after the hosted flow is verified

Lowest-cost path right now:

- use an Oracle Cloud **Always Free** VM
- run `docker-compose.oracle-free.yml`
- keep `LocalJson` first
- move to `Supabase` later if needed

## The one idea to hold onto

SmartX has **one codebase and one API image**. The store it talks to is chosen at runtime by a
single config switch, `Persistence:Provider`:

| Install            | `Persistence:Provider` | Store                         | Network |
| ------------------ | ---------------------- | ----------------------------- | ------- |
| **Cloud web app**  | `Supabase`             | Supabase Postgres (cloud)     | Online  |
| **Office laptop**  | `LocalJson`            | `foundation.local.json` file  | Offline |

Nothing about the laptop path changes in this project. The cloud path is a **new persistence
provider** slotted behind the existing `IWorkspaceRepository` interface — the Application services,
controllers, and authentication are untouched.

## Read in this order

1. [architecture.md](architecture.md) — target architecture and request flow.
2. [code-and-infra-changes.md](code-and-infra-changes.md) — exactly what changed and why.
3. [schema-and-migrations.md](schema-and-migrations.md) — the Postgres schema and how to apply it.
4. [hosting-plan.md](hosting-plan.md) — API container, same-origin SPA, desktop client.
5. [environment-variables.md](environment-variables.md) — the full env-var checklist (secrets included).
6. [oracle-free-vm.md](oracle-free-vm.md) — the cheapest practical live path for this stack.
7. [risks.md](risks.md) — what could go wrong and the mitigations.
8. [go-live-checklist.md](go-live-checklist.md) — the ordered cutover steps.
9. [rollback-checklist.md](rollback-checklist.md) — how to back out safely.

## Scope guardrails (phase 1)

**In scope:** Supabase Postgres as the cloud store; a diff-based Postgres provider; migrations under
`supabase/migrations`; config-driven CORS; same-origin SPA serving; the docs in this folder.

**Deliberately deferred** (see each doc for rationale): Supabase Auth, Supabase Storage, Edge
Functions, RLS as the security boundary, and any laptop↔cloud sync engine. The ASP.NET API stays
the security boundary and the browser never holds a Supabase key.
