# User Service

The User Service owns user registration, login, account records, role claims, and the `ECommerceUsers` database. It has no project dependency on the monolith.

## Configuration

Set `ConnectionStrings__UserService`, `Jwt__Key`, `Jwt__Issuer`, and `Jwt__Audience` through environment variables or a secret provider. Use the same JWT settings as the other services that validate its tokens. The key must contain at least 32 UTF-8 bytes. Never commit production secrets.

Local runtime defaults to a dedicated SQL Server LocalDB database. Containers cannot connect to LocalDB; use an external SQL Server or supported SQL Server container.

## Routes

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/users/me` (authenticated)
- `GET /health`

Public registration only assigns `Customer` or `Vendor`; it cannot create an administrator.

## Migrations

```powershell
dotnet ef migrations add <MigrationName> --project ECommerce.UserService --startup-project ECommerce.UserService --output-dir Migrations
dotnet ef database update --project ECommerce.UserService --startup-project ECommerce.UserService
```

Migration applies only to the configured User Service database. Copy existing users while preserving IDs and password hashes, then validate counts and representative records before routing requests here.
