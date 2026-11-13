# Product Service

This is an independently deployable ASP.NET Core API. It owns its Product and Inventory models, contracts, repositories, business logic, and `ProductDbContext`; it does not reference the monolith's Business, DataAccess, or Domain projects.

It owns the `Products` and `Inventories` tables in its own SQL Server database. The legacy API remains available during the data-copy and validation period; do not route production traffic here until the service database has been copied and verified.

## Configuration

Set `ConnectionStrings__ProductService` to the product-service database connection string. Configure the same `Jwt__Key`, `Jwt__Issuer`, and `Jwt__Audience` values used by the API that issues customer tokens. The key must contain at least 32 UTF-8 bytes. Do not commit credentials.
Set `InternalServices__ApiKey` to a separate random secret of at least 32 UTF-8 bytes; Order Service must send the same value in `X-Internal-Service-Key` for inventory reservation/release calls. Do not expose this key through the gateway or commit it.

For local development, the runtime and design-time EF factory default to a separate LocalDB database named `ECommerceProductsDb`. Containers cannot use LocalDB; use an external SQL Server or a supported SQL Server container for container-based development.

## Routes

- `GET /health`
- `GET /api/products` and `GET /api/products/{id}`
- `GET /api/products/categories`
- `POST /api/products`, `PUT /api/products/{id}`, `DELETE /api/products/{id}` (Admin role)
- `GET /api/inventory/product/{productId}`
- `POST /api/inventory`, `PUT /api/inventory/product/{productId}` (Admin role)
- `POST /internal/inventory/reservations`, `DELETE /internal/inventory/reservations/{reservationId}` (internal service key)

The public product routes are compatible with the legacy API contract. JWT issuer, audience, key, and role claims must be shared with the token issuer. The Product Service still enforces Admin authorization itself.

## Migrations

From the repository root, create or apply migrations with:

```powershell
dotnet ef migrations add <MigrationName> --project ECommerce.ProductService --startup-project ECommerce.ProductService --output-dir Migrations
dotnet ef database update --project ECommerce.ProductService --startup-project ECommerce.ProductService
```

Applying the service migration does not copy data from the legacy database. Use a reviewed, idempotent copy-and-validation process before switching gateway routes.
