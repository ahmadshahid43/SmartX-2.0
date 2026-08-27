# Storage Strategy

## Current provider

The project currently uses:

- Provider: `LocalJson`
- Seed file: `src/OmniBusiness.Api/Data/foundation.json`
- Runtime file: `%LOCALAPPDATA%\OmniBusiness\foundation.local.json`

This is configured in:

- `src/OmniBusiness.Api/appsettings.json`
- `src/OmniBusiness.Infrastructure/Persistence/PersistenceOptions.cs`
- `src/OmniBusiness.Infrastructure/Persistence/LocalJsonWorkspaceRepository.cs`

## Why this provider was chosen

The target environment is a domain-joined office laptop where SQL is not a safe default. A local JSON provider avoids:

- SQL Server installation
- Windows service setup
- database instance permissions
- domain policy conflicts around local server software

## How first run works

1. The API starts.
2. `LocalJsonWorkspaceRepository` resolves the configured local path.
3. If the local file does not exist, the repository copies the seed JSON into that path.
4. The copied file becomes the runtime store for subsequent reads.

## Other supported provider today

- `EmbeddedSeed`

`EmbeddedSeed` reads directly from the seed file in the API project. It is useful for demos, but `LocalJson` is the better default for real local usage because it moves runtime state to a writable user-controlled location.

## How to move back to SQL later

When SQL is available again:

1. Create a new repository such as `SqlWorkspaceRepository` that implements `IWorkspaceRepository`.
2. Keep the application DTOs and controller contracts unchanged.
3. Extend the provider switch in `src/OmniBusiness.Infrastructure/DependencyInjection.cs`.
4. Add a new provider setting in configuration after the SQL implementation is complete.
5. Import or transform data from `foundation.local.json` into the new SQL schema.

This keeps the migration contained to infrastructure and configuration instead of forcing a rewrite across the stack.

## About `docs/sql`

The file under `docs/sql` is kept only as historical or reporting reference. It is not required for the app to run.
