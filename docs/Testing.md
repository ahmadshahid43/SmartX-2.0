# Testing

## Automated tests

Current automated coverage includes:

- domain snapshot integrity tests
- application service tests for workspace queries

Projects:

- `tests/OmniBusiness.Domain.Tests`
- `tests/OmniBusiness.Application.Tests`

## Local verification commands

From the repository root:

```powershell
dotnet restore OmniBusiness.slnx --configfile NuGet.Config --packages .nuget\packages
dotnet build OmniBusiness.slnx --no-restore
dotnet test OmniBusiness.slnx --no-build
```

Web build:

```powershell
Set-Location web/omnibusiness-web
npm ci
npm run build
```

## Manual smoke checks

1. Start the API with `dotnet run --project src/OmniBusiness.Api --launch-profile http`.
2. Open Swagger at `http://localhost:5163/swagger`.
3. Log in through the Angular app at `http://localhost:4200`.
4. Confirm dashboard, inventory, POS, and form-builder screens load data.
5. Confirm `%LOCALAPPDATA%\OmniBusiness\foundation.local.json` is created on first API access.
