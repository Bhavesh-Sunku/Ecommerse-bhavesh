# Podman local development (Windows)

## Verified local stack — 2026-11-07

The Windows environment has Podman 6.1.4, Python 3.12.10, and `podman-compose` 1.6.0. All five application images were built, and the six-container stack (Product, User, Order, Gateway, frontend, and SQL Server) was started with the `local-sql` profile.

The three service databases were created on the isolated SQL Server container and migrated. HTTP checks through YARP verified registration/login/profile, product catalog, cart, Order-to-Product inventory reservation, checkout, stock decrement, cart clearing, and order history/detail. The SQL Server container was restarted; the named volume retained data and the product remained readable through the API. The populated Windows LocalDB `ECommerceDb` was not used or changed.

The local stack is currently left running as Compose project `ecommerce-capstone-uc33`. Its demo SQL Server volume is `ecommerce-capstone-uc33_sqlserver-data`. The root `.env` contains generated local-only secrets and is ignored by Git. One isolated demo product/order and a smoke-test user are in the demo databases; register a new account for a browser demo because the smoke-test password was intentionally not retained.

This is local container verification only. No Azure resource or production deployment was created.

The verification above predates the Payment Service addition. The current Compose definition now includes Payment Service and its YARP route, but the expanded six-application stack and payment database have not yet been started or verified. Use the separate project name below to keep the already-running demo containers and SQL volume untouched.

## Seed the local demo catalog

The repeatable seed script `scripts/seed-podman-demo-products.sql` adds 10 products to each of five categories in the isolated `ECommerceProducts` database. Each seeded item is set to quantity 10, and whole-number prices end in `999`. Frontend price displays use Indian rupees (`₹`) with Indian digit grouping. The script is guarded to refuse any database other than `ECommerceProducts`. Re-run it only when you intend to reset the stock of those seeded demo products to 10.

```powershell
podman cp .\scripts\seed-podman-demo-products.sql ecommerce-capstone-uc33_sqlserver_1:/tmp/seed-podman-demo-products.sql
podman exec ecommerce-capstone-uc33_sqlserver_1 /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U sa -C -d ECommerceProducts `
  -P $env:MSSQL_SA_PASSWORD -b -i /tmp/seed-podman-demo-products.sql
```

The root `.env` is not automatically loaded into a PowerShell process. To run the SQL command above, set `$env:MSSQL_SA_PASSWORD` in the same PowerShell window using the password from your local, ignored `.env`; do not print or share it.

## 1. Install prerequisites

1. Install or enable WSL 2 from Microsoft’s Windows documentation and restart if requested: <https://learn.microsoft.com/windows/wsl/install>.
2. Install the Podman Windows client from the official instructions: <https://podman.io/docs/installation>.
3. Open a new PowerShell window and confirm:

   ```powershell
   podman --version
   wsl --status
   ```

Do not install Docker Desktop or Docker Engine for this project.

## 2. Initialize the Podman Linux machine

```powershell
podman machine init --provider wsl --cpus 4 --memory 8192 --disk-size 50
podman machine start
podman info
```

If a machine already exists, use `podman machine start` rather than initializing another one. The commands above may require adjustment for the installed Podman release/provider.

## 3. Check Compose support before using Compose

Podman Compose delegates to an installed Compose provider. Check availability first:

```powershell
podman compose version
```

If that reports that no provider is available, install Python and a Podman-compatible provider such as `podman-compose` using their official installation instructions, reopen PowerShell, and re-run the version check. Do not proceed to a Compose command until this check succeeds.

## 4. Configure databases and secrets

```powershell
Copy-Item .env.example .env
notepad .env
```

Set separate `PRODUCT_DB_CONNECTION`, `USER_DB_CONNECTION`, `ORDER_DB_CONNECTION`, and `PAYMENT_DB_CONNECTION` values for a reachable SQL Server. Set the distinct `PAYMENT_INTERNAL_SERVICE_KEY` as well. LocalDB inside Windows is not reachable as `localhost` from Linux containers. For the optional Compose SQL Server profile, use `Server=sqlserver,1433` from application containers. For SQL Server running on the Windows host, use a reachable address such as `host.containers.internal` and verify SQL Server network/firewall configuration. Keep `.env` out of source control; the repository ignores the root `.env`.

The `local-sql` profile describes an optional SQL Server 2022 Linux container on `linux/amd64`, with data persisted in a Podman named volume. It is not enabled by default because SQL Server container compatibility depends on the Podman machine architecture and host resources. If it runs successfully on your system, create the three service databases and apply their EF migrations before starting the APIs. If the image is incompatible, use a reachable external SQL Server—do not substitute another database silently.

For host-run EF commands against a SQL Server container published on localhost, set each design-time connection variable to a host-resolvable SQL Server address before running `dotnet ef database update`. The script `scripts/migrate-data.sql` assumes the source and service databases are on the same SQL Server instance.

## 5. Build and start

Only after Podman and the Compose provider checks succeed, and database connection strings/JWT configuration are set:

```powershell
podman compose -p ecommerce-capstone-uc33 --profile local-sql up -d
podman ps
podman compose logs -f gateway
```

Use the same `-p ecommerce-capstone-uc33 --profile local-sql` options for subsequent Compose commands so they address the verified project and its volume. The frontend is published at `http://localhost:5173`; gateway health is at `http://localhost:8080/health`. The gateway routes directly to Product, User, and Order Services using Compose DNS names; it does not fall back to the monolith. Each service database must be migrated and populated before the purchase journey is available.

The previously verified stack has only Product, User, and Order migrations applied. For a new disposable project, start its SQL Server, set the design-time variables `PRODUCT_SERVICE_CONNECTION`, `USER_SERVICE_CONNECTION`, `ORDER_SERVICE_CONNECTION`, and `PAYMENT_SERVICE_CONNECTION` to that project's host-resolvable `localhost,1433` database addresses, and apply the service migrations before starting the API containers. For the new payment workflow, apply both Order Service payment migrations (`AddPaymentConfirmation` and `AddPaymentPreparation`) to the new Order database and the Payment Service initial migration to the new Payment database. Never point them at the populated legacy database or the prior demo project's volume. Compose health checks only report process health; they do not prove schema/data correctness.

On a fresh isolated database, the migration commands are:

```powershell
dotnet ef database update --project ECommerce.ProductService\ECommerce.ProductService.csproj --startup-project ECommerce.ProductService\ECommerce.ProductService.csproj
dotnet ef database update --project ECommerce.UserService\ECommerce.UserService.csproj --startup-project ECommerce.UserService\ECommerce.UserService.csproj
dotnet ef database update --project ECommerce.OrderService\ECommerce.OrderService.csproj --startup-project ECommerce.OrderService\ECommerce.OrderService.csproj
dotnet ef database update --project ECommerce.PaymentService\ECommerce.PaymentService.csproj --startup-project ECommerce.PaymentService\ECommerce.PaymentService.csproj
```

Set each corresponding design-time variable to the same isolated database and host-resolvable SQL address first. Use a disposable project/volume only; do not run the Order Service update against an existing volume without a backup and explicit approval.

For a clean, isolated stack, use a distinct project and volume. After configuring the local SQL profile and applying the migrations to its fresh databases:

```powershell
podman compose -p ecommerce-capstone-payment-sandbox --profile local-sql up -d --build
podman compose -p ecommerce-capstone-payment-sandbox ps
podman compose -p ecommerce-capstone-payment-sandbox logs --follow payment-service
```

The frontend is published at `http://localhost:5173`, the gateway at `http://localhost:8080`, and mock payment requests use the gateway route `/api/payments`. Use only the `mock-success`/`mock-decline` test modes. Never enter card or bank credentials. Do not remove any named volume unless you explicitly intend to destroy that isolated database.

## 6. Stop without deleting persistent data

```powershell
podman compose -p ecommerce-capstone-uc33 --profile local-sql down
podman ps -a
podman volume ls
```

The command stops this demo stack but preserves its named SQL Server data volume. Do not add `-v` unless you explicitly intend to remove that database volume. Do not remove the volume if you want to retain the local demo records.

## Troubleshooting

- **Podman machine stopped:** `podman machine start`, then `podman info`.
- **Compose provider missing:** repeat `podman compose version`; install/configure a provider before attempting `up`.
- **Database connection refused:** verify SQL Server is reachable from the Podman VM, use the correct host/container DNS name and port, and verify the database exists.
- **SQL Server image fails to start:** check `podman machine info`/architecture and image logs; use external SQL Server if the image is unsupported.
- **Gateway returns 502:** inspect gateway logs and verify the configured destination address is reachable from its container.
- **Health is green but API calls fail:** apply the service EF migrations and verify database permissions/data; health checks intentionally do not query or migrate databases.
