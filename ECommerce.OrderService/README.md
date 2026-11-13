# Order Service

The Order Service owns carts, cart items, orders, order items, order history, and the `ECommerceOrders` database. It stores `UserId` and `ProductId` values as service references rather than cross-database foreign keys. Product names, image URLs, and unit prices are snapshotted into new order lines.

## Configuration

Set `ConnectionStrings__OrderService`, `ProductService__BaseUrl`, `ProductService__InternalServiceKey`, `Jwt__Key`, `Jwt__Issuer`, and `Jwt__Audience` using environment variables or a secret provider. The Product Service API key must match `InternalServices__ApiKey` and contain at least 32 UTF-8 bytes. Do not commit credentials.

The local database default is SQL Server LocalDB. Containers cannot connect to LocalDB; use external SQL Server or a supported SQL Server container. Product Service URL must resolve from the Order Service process/container.

## Routes

- `GET /api/cart`
- `POST /api/cart/items`
- `PUT /api/cart/items/{itemId}`
- `DELETE /api/cart/items/{itemId}`
- `DELETE /api/cart`
- `POST /api/orders/from-cart`
- `GET /api/orders/my-orders`
- `GET /api/orders/{id}`
- `PUT /api/orders/{id}/cancel`
- `GET /health`

Authenticated routes use the User Service JWT issuer, audience, key, and identity claims.

## Checkout coordination

The service validates products and cart state, asks Product Service to reserve stock in a serializable transaction, persists an order/cart clear in its own transaction, and releases the reservation if the reservation response is ambiguous or order persistence fails. Releasing an unknown or already-released reservation is idempotent. Pending-order cancellation releases the reservation idempotently. This is a compensating workflow across two databases, not a distributed ACID transaction; compensation failures are logged but do not yet have a durable retry queue, so test concurrent requests and recovery before production cutover.

## Migrations

```powershell
dotnet ef migrations add <MigrationName> --project ECommerce.OrderService --startup-project ECommerce.OrderService --output-dir Migrations
dotnet ef database update --project ECommerce.OrderService --startup-project ECommerce.OrderService
```

Apply migrations only to the Order Service database. Copy legacy carts and orders while preserving IDs, user references, item references, and historical line values; validate before routing traffic.
