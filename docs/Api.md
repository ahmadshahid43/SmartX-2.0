# API Overview

Base URL during development:

- `http://localhost:5163`

Swagger UI:

- `http://localhost:5163/swagger`

## Authentication

The API uses a protected bearer token generated through ASP.NET Core Data Protection. It is not using JWT in the current starter.

Local development seed credentials:

- Email: `admin@omnibusiness.local`
- Password: `Admin@123`

For public/live deployments, the seed owner password should be overridden via
`Persistence__BootstrapOwnerPassword`. Production seeding now rejects the old demo password path.

## Endpoints

- `POST /api/v1/auth/login`
- `GET /api/v1/auth/me`
- `GET /api/v1/foundation/context`
- `GET /api/v1/dashboard/overview`
- `GET /api/v1/inventory/overview`
- `GET /api/v1/pos/terminal`
- `GET /api/v1/customization/forms/product-custom-fields`
- `GET /health`
- `GET /ready`

## Example login request

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "admin@omnibusiness.local",
  "password": "Admin@123"
}
```

## Response shape

The API returns structured JSON DTOs tailored for the current dashboard, inventory, POS, and form-builder views. The data is seeded from the local runtime storage described in `docs/Storage.md`.
