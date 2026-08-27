# Deployment Notes

## Current recommended target

The current starter is designed for:

- a single managed Windows laptop
- a branch pilot or internal demo
- environments where SQL Server is not allowed

## Practical local deployment shape

- Run the ASP.NET Core API locally.
- Run the Angular app locally during development, or publish it as static assets later.
- Use the WPF desktop shell as the operator-facing entry point for future branch tooling.
- Store runtime data in `%LOCALAPPDATA%\OmniBusiness\foundation.local.json`.

## Why this works better on a domain laptop

- no database server install
- no Windows SQL service dependency
- no instance configuration
- no domain exception request for SQL ports or service accounts

## If you publish the API

Keep these points in mind:

- ensure the process identity can write to the configured local JSON path
- keep `Persistence:Provider` set to `LocalJson` unless a different provider is fully implemented
- back up the local JSON file if operators start editing live data

## Future production hardening

Before broader rollout, plan for:

- mutation endpoints and write workflows
- backup and restore routines
- audit logs
- sync/export flows
- role-based module permissions
- a formal SQL-backed repository if infrastructure restrictions are removed
