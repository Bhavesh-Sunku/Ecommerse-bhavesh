# E-Commerce Capstone

This repository contains the existing React/.NET application and a side-by-side Milestone 3 implementation. The original `ECommerce.API` and its database remain available as the rollback target; new services must not receive production traffic until schema setup, data validation, and checkout coordination are verified.

## Project layout

- `ECommerce.API`: existing monolithic API; still the initial gateway target.
- `ECommerce.ProductService`: independently owned product/inventory API and database.
- `ECommerce.UserService`: independently owned registration, login, and user database.
- `ECommerce.OrderService`: independently owned cart/order API and database; uses a Product Service reservation API and compensating release workflow for checkout/cancellation.
- `ECommerce.PaymentService`: independently owned mock-only payment API and database; reserves orders through an internal Order Service API and confirms orders only after a successful sandbox result.
- `ECommerce.Gateway`: YARP gateway with JWT policies and configurable service clusters.
- `ECommerce.Frontend`: React/Vite client configured for `http://localhost:8080` gateway.
- `ECommerce.Tests`: existing backend unit tests.
- `scripts/migrate-data.sql`: repeatable insert-only copy and row-count/reference validation for databases on the same SQL Server instance; it preserves pending-order inventory reservations.
- `compose.yaml` and service `Containerfile`s: Podman-oriented OCI local environment.
- `azure-pipelines.yml`: Azure DevOps build/test and Podman image pipeline template.
- `.github/workflows/`: protected GitHub Actions CI, ACR publishing, and opt-in Kubernetes CD.
- `k8s/`: Kustomize manifests for six workloads and TLS ingress; secret placeholders are intentionally excluded from the applied Kustomization.

See [Milestone3-Completion-Report.md](./Milestone3-Completion-Report.md) for verified status and blockers, [Podman setup](./docs/PODMAN.md), and [Azure deployment preparation](./docs/AZURE-DEPLOYMENT.md).
The new Payment and deployment design is described in the [microservices architecture](./docs/MICROSERVICES-ARCHITECTURE.md), [Kubernetes deployment guide](./docs/KUBERNETES-DEPLOYMENT-GUIDE.md), [GitHub Actions guide](./docs/GITHUB-ACTIONS.md), and [validation report](./docs/VALIDATION-REPORT.md). The detailed [Milestone 3 completion report](./docs/MILESTONE3-COMPLETION-REPORT.md) contains the acceptance checklist and validation evidence.

For the legacy API database, see the [Milestone 1 SQL Server runbook](./docs/SQL-SERVER-MILESTONE-1.md) and run the read-only validation script at `scripts/validate-milestone1.sql` in SSMS before planning schema or data changes.

## Legacy demo

The UI now calls the gateway. To demo the existing backend while preserving the original behavior:

1. Apply any pending local additive migrations only after backing up data you need.
2. Generate a local development signing key and store it in .NET user secrets.
   The application projects share the same local-only secrets ID:

   ```powershell
   $keyBytes = New-Object byte[] 32
   $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
   $rng.GetBytes($keyBytes)
   $jwtKey = [Convert]::ToBase64String($keyBytes)
   dotnet user-secrets set "Jwt:Key" $jwtKey --project ECommerce.API\ECommerce.API.csproj
   $rng.Dispose()
   [Array]::Clear($keyBytes, 0, $keyBytes.Length)
   Remove-Variable jwtKey
   ```

   Do not copy the generated key into source-controlled files or share it.

3. Start the existing API:

   ```powershell
   dotnet run --project ECommerce.API\ECommerce.API.csproj --urls http://0.0.0.0:5166
   ```

4. In a separate PowerShell terminal, configure the gateway's non-secret JWT
   settings, then start it. The signing key is loaded from the shared user-secret
   store while running in Development:

   ```powershell
   $env:Jwt__Issuer = "ECommerceAPI"
   $env:Jwt__Audience = "ECommerceClient"
   $env:ReverseProxy__Clusters__products__Destinations__destination1__Address = "http://localhost:5166/"
   $env:ReverseProxy__Clusters__users__Destinations__destination1__Address = "http://localhost:5166/"
   $env:ReverseProxy__Clusters__orders__Destinations__destination1__Address = "http://localhost:5166/"
   dotnet run --project ECommerce.Gateway\ECommerce.Gateway.csproj
   ```

5. In another terminal:

   ```powershell
   Set-Location ECommerce.Frontend
   npm ci
   npm run dev
   ```

The frontend loads at the Vite URL (usually `http://localhost:5173`); the gateway health endpoint is `http://localhost:8080/health`. The three explicit destination settings above point legacy Product/User/Order routes to the monolith for this demo only. Payment Service is not enabled in the monolith; do not use the payment route unless the standalone Payment Service is configured.

## Local service databases

Each service has its own EF migration history. Generate/update schema only against the intended service database; do not apply the service migrations to the legacy database. Detailed commands and safe cutover prerequisites are in each service README. The SQL data-copy script is additive and insert-only; it does not delete or update the source database. It assumes source and target databases are on the same SQL Server instance, and requires the legacy cart/image/role/shipping migrations and the new service migrations to have already been applied. It maps pending legacy orders to deterministic inventory-reservation IDs without decrementing inventory a second time. Review and back up data before any migration run.

After creating the service databases and applying their service migrations, review the SQL script and run it using SQLCMD against the SQL Server instance containing all four databases:

```powershell
sqlcmd -S <server> -d master -E -i scripts\migrate-data.sql
```

The script defaults to `ECommerceDb`, `ECommerceProducts`, `ECommerceUsers`, and `ECommerceOrders`; edit the SQLCMD defaults if your approved database names differ. It checks for conflicting IDs, inserts missing records in a transaction, checks record counts and key references, and rolls back on validation failure. It does not reconcile concurrent writes or support databases on different SQL Server instances; freeze or reconcile writes and validate the printed counts before any route cutover.

## Build and tests

```powershell
dotnet restore ECommerce.slnx
dotnet build ECommerce.slnx --no-restore
dotnet test ECommerce.Tests\ECommerce.Tests.csproj --no-restore
Set-Location ECommerce.Frontend
npm ci
npm run lint
npm run build
```

The backend test project includes unit/boundary tests for the services. Live service-to-service, database, container, ACR, and Kubernetes behavior requires the additional environment-specific verification described in the deployment guides.
