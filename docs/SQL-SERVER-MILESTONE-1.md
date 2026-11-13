# Milestone 1 SQL Server runbook

This runbook applies to the legacy API database configured in
`ECommerce.API/appsettings.json`. That connection string targets
`(localdb)\MSSQLLocalDB` and database `ECommerceDb`. Do not apply the new
Product, User, or Order Service migrations to this legacy database.

## Check the database in SQL Server Management Studio

1. Connect to server `(localdb)\MSSQLLocalDB` using Windows Authentication.
2. Expand **Databases > ECommerceDb > Tables** and verify the legacy tables are present.
3. Before any schema/data change, right-click `ECommerceDb`, select **Tasks >
   Back Up...**, use **Full** backup, and verify the destination and completion
   message. Keep the backup outside the repository.
4. Open `scripts/validate-milestone1.sql` in a new SSMS query window, select
   `ECommerceDb` in the database selector, and execute it. This script is
   read-only: it lists migration history, expected tables, row counts, missing
   inventory rows, basic data checks, and foreign-key constraint violations.
5. Review the result sets before deciding on any data repair. A product without
   an inventory row is treated as unavailable by the catalog; do not assume its
   intended quantity. Reconcile the correct stock value from an authorized
   source before inserting or changing inventory.

## Provision the first administrator

Public registration intentionally creates Customer accounts only; it cannot
grant the `Admin` or `Vendor` role. To provision a privileged account, register
the designated person through the application, verify their identity, then use
SSMS with an authorized database account to promote exactly that account.
Replace the placeholder with the verified email and the approved role. This is
a write to `Users`; do not expose it through a public API:

```sql
DECLARE @Email nvarchar(200) = N'admin@example.com';

BEGIN TRANSACTION;

UPDATE dbo.Users
SET Role = N'Admin'
WHERE Email = @Email;

IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51000, 'Expected exactly one non-admin user matching the email.', 1;
END;

COMMIT TRANSACTION;
```

For an approved vendor, change the role literal to `N'Vendor'` and verify the
account is authorized for the specific vendor functionality before promotion.
Sign in again after promotion so the JWT contains the new role. Never expose
this update as a public API endpoint.

The equivalent read-only PowerShell command is:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d ECommerceDb -b -i scripts\validate-milestone1.sql
```

## Review and apply schema migrations

First generate an idempotent SQL script for review; this does not connect to or
modify the database:

```powershell
dotnet ef migrations script --idempotent `
  --configuration Release --no-build `
  --project ECommerce.DataAccess\ECommerce.DataAccess.csproj `
  --startup-project ECommerce.API\ECommerce.API.csproj `
  --output $env:TEMP\ECommerceDb-migrations.sql
```

Open the generated SQL in SSMS, confirm the target is `ECommerceDb`, and inspect
every statement. Apply it only after confirming the backup and approving the
change window. Alternatively, the EF command below applies migrations directly,
so do not use it as an inspection command:

```powershell
dotnet ef database update `
  --project ECommerce.DataAccess\ECommerce.DataAccess.csproj `
  --startup-project ECommerce.API\ECommerce.API.csproj
```

Never use `Drop-Database`, `EnsureDeleted`, or a migration `Down` script against
the database containing user data. Do not copy data into the service databases
until the destination schemas are reviewed and the source/target parity plan
has been approved.

## Current observed status

The local `ECommerceDb` was inspected read-only on 2026-11-04. Its four legacy
migrations are recorded in `__EFMigrationsHistory`, including cart/image and
shipping details. The database had 14 products, 15 users, 13 orders, 14 order
items, and 11 inventory rows at inspection time. SQL Server reported no
constraint violations, invalid product prices, negative inventory, or
duplicate normalized emails. Three products had no inventory row; their
intended stock is unknown, so no repair was made. Re-run the validation script
before relying on these counts because database contents can change.

## Application and test checks

The API uses the connection string in `ECommerce.API/appsettings.json`, or an
environment override such as `ConnectionStrings__DefaultConnection`. Keep
production connection strings and credentials out of source control. The JWT
signing key is not stored in `appsettings.json`; use the shared local .NET user
secrets store in Development or an environment/secret provider for deployment.

After an approved migration or repair, build and run the backend tests:

```powershell
dotnet build ECommerce.slnx --configuration Release
dotnet test ECommerce.Tests\ECommerce.Tests.csproj --configuration Release
```

Then verify product/catalog, authentication, cart, and order endpoints against
the intended database. Do not use the populated `ECommerceDb` for tests that
create, update, or delete records unless the test data is explicitly isolated
and the operation is approved.
