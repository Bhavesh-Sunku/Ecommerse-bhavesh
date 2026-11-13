# Milestone 3 Completion Report

## Addendum — Payment and Kubernetes implementation (updated 2026-11-13)

The table below supersedes the earlier local-only completion snapshot for the Payment extension. It includes a successful isolated local Compose checkout, but is not Azure or Kubernetes deployment evidence.

| Use case | Current status | Evidence / remaining work |
|---|---|---|
| UC3.1 — Product/User/Order/Payment boundaries | Completed for local acceptance | Four services own separate databases. An isolated end-to-end flow verified Order-to-Product stock reservation and Payment-to-Order callbacks without direct cross-database access. |
| UC3.2 — YARP Gateway | Completed for local acceptance | Gateway registration/login, product creation, cart/order, payment success, idempotent replay, decline and cancellation were exercised through YARP. |
| UC3.3 — Podman and Kubernetes | Partially Completed | All six application images built; the expanded Compose stack plus isolated SQL Server started and all seven containers reported healthy. Kubernetes manifests and cross-references pass local static checks; no cluster/schema admission or rollout was tested. |
| UC3.4 — CI/CD | Partially Completed | GitHub Actions now covers full backend/frontend checks, Podman builds, workflow/Kubernetes validation and protected OIDC ACR publishing with Buildx caching. GitHub Actions and Azure DevOps pipelines have not run against owner credentials. |
| UC3.5 — Azure deployment | Not Verified | No Azure subscription, ACR push, SQL migration, ingress/DNS/TLS setup, or Kubernetes deployment was performed. |

See the detailed [Milestone 3 checklist](./docs/MILESTONE3-COMPLETION-REPORT.md), [microservices architecture](./docs/MICROSERVICES-ARCHITECTURE.md), [Kubernetes guide](./docs/KUBERNETES-DEPLOYMENT-GUIDE.md), [GitHub Actions guide](./docs/GITHUB-ACTIONS.md), and [validation report](./docs/VALIDATION-REPORT.md). The earlier report sections below record historical work verified before the Payment Service was added.

## Status summary

| Use case | Status | Evidence / remaining work |
|---|---|---|
| UC3.1 — Microservices | Completed for local acceptance | Product, User, and Order have independent contexts, migrations, and databases. Containerized HTTP verification exercised User/Product/Order APIs and Order-to-Product REST reservation during checkout. Production data cutover and durable compensation recovery remain production concerns, not a local service-runtime blocker. |
| UC3.2 — YARP Gateway | Completed for local acceptance | Gateway route/auth/error smoke checks passed previously; the real containerized gateway now routes to all three services, forwards authenticated requests, exposes healthy status, and completed the checkout flow. Azure URLs/settings remain for UC3.5. |
| UC3.3 — Podman | Completed for local acceptance | Built all five application OCI images with Podman 6.1.4. `podman-compose` 1.6.0 ran the five app containers plus an isolated SQL Server container; all reported healthy. Applied service migrations, verified service DNS and purchase flow, then restarted SQL Server and confirmed the named volume retained data. |
| UC3.4 — Azure DevOps CI/CD | Partially Completed | `azure-pipelines.yml` has restore/build/test/frontend and Podman OCI build/publish/deploy stages. The pipeline has not run. Azure service connections, ACR credentials, app names, public gateway URL, and approval environment must be configured. |
| UC3.5 — Azure Deployment | Not verified / awaiting access | Azure subscription, permissions, service connections, cost approval, and production configuration are not supplied. Deployment preparation is in `docs/AZURE-DEPLOYMENT.md`; no Azure resources or public URL are claimed. |

## Files created or modified

- Added independent projects: `ECommerce.ProductService`, `ECommerce.UserService`, `ECommerce.OrderService`, `ECommerce.Gateway`.
- Added EF contexts, service-owned models, API contracts/controllers, JWT validation, health endpoints, and project migrations.
- Added `scripts/migrate-data.sql`, an insert-only same-SQL-Server copy script with conflict detection, count checks, and reference validation. It does not delete or update source rows.
- Added Podman Containerfiles, `compose.yaml`, `.containerignore`, `.env.example`, `ECommerce.Frontend/nginx.conf`, and changed frontend `.env`/`.env.example` to use the gateway.
- Added `azure-pipelines.yml`, root `README.md`, `docs/PODMAN.md`, and `docs/AZURE-DEPLOYMENT.md`.
- Added `docs/SQL-SERVER-MILESTONE-1.md` and the read-only `scripts/validate-milestone1.sql`; expanded Milestone 1 unit and SQL-backed API verification.
- Updated `ECommerce.slnx`, Product Service setup notes, and `CAPSTONE-REQUIREMENTS.md`.

## Commands executed

- `dotnet --version` — .NET SDK 10.0.403.
- Baseline `dotnet build ECommerce.slnx --no-restore` — passed before new services were added.
- Baseline `dotnet test ECommerce.Tests/ECommerce.Tests.csproj --no-restore` — 9 passed.
- Frontend `npm run lint` and `npm run build` — build passed; lint completed with existing React warnings.
- Generated Product, User, and Order Service EF migrations; none were applied to a database.
- Final `dotnet build ECommerce.slnx --configuration Release --no-restore` — passed with 0 warnings and 0 errors.
- Final `dotnet test ECommerce.Tests/ECommerce.Tests.csproj --configuration Release --no-build` — 30 passed after the additional Milestone 1 CRUD/auth/cart tests.
- Legacy API migrations were applied only to a separately named disposable SQL Server database for Milestone 1 end-to-end testing. It passed validation and was removed afterward. No migration or data repair was applied to the populated `ECommerceDb`.
- Gateway smoke checks — `/health` returned 200; anonymous `/api/orders` and `/api/cart` returned 401. The temporary gateway process was stopped afterward.
- `git diff --check` — passed; Git reported existing LF-to-CRLF conversion notices for modified frontend files.
- Initial environment inspection found no Podman Compose provider and no available Azure CLI/subscription credentials. Podman and its Compose provider were subsequently installed and verified as recorded below; no Azure operations were performed.
- On 2026-11-07, installed Python 3.12.10 and `podman-compose` 1.6.0 after approval; verified Podman 6.1.4 and successful `podman compose version`.
- Built Product, User, Order, Gateway, and frontend OCI images using the repository Containerfiles.
- Started uniquely named Compose project `ecommerce-capstone-uc33` with `local-sql`; migrated only its new `ECommerceProducts`, `ECommerceUsers`, and `ECommerceOrders` databases. All six containers reported healthy.
- Containerized API smoke flow passed through YARP: register/login/profile, catalog, cart, inventory reservation from Order to Product Service, checkout, stock decrement, cart clear, order history/details. SQL Server restart retained product data on its named volume. Existing `ECommerceDb` was not touched.

## Remaining blockers

- Add/retain automated cross-service integration tests for concurrent reservations, cancellation, and durable compensation recovery.
- No legacy source data was copied into the new container demo databases. If a data cutover is required, review the insert-only script, use approved source/target databases on the same SQL Server instance, compare parity, and reconcile concurrent writes; the script does not support different SQL Server instances.
- Validate the pipeline against an Azure DevOps organization and configure all protected service connections, secret variables, and environment approvals.
- Obtain Azure subscription/resource approval, install Azure CLI, configure Key Vault/managed identities, deploy, and execute smoke tests. Deployment remains blocked and unverified.
