# Microservices Acceptance Audit

Audit scope: the existing repository before adding a standalone Payment Service, Kubernetes manifests, or GitHub Actions. Existing local changes and demo data are preserved.

## Current components

| Area | Existing implementation | Assessment |
|---|---|---|
| Product Service | `ECommerce.ProductService` has its own ASP.NET Core API, EF Core context, SQL Server migrations, catalog/inventory APIs, and reservation endpoints. | Implemented; previously built and exercised locally and in Podman. |
| User Service | `ECommerce.UserService` owns registration, login, password hashing, JWT creation, profile API, context, and migration. | Implemented; previously exercised locally and in Podman. |
| Order Service | `ECommerce.OrderService` owns cart/order models, context, migrations, authenticated routes, and REST clients for Product Service. | Implemented; previously exercised in local Podman checkout. |
| Payment | The monolith contains a Payment entity, enum, DTOs, repository, controller, and `PaymentService`, but the service deliberately throws `NotSupportedException`. There is no independently deployable Payment Service, payment database, provider implementation, or payment migration. | Missing as a microservice; monolith endpoint is a safe non-payment stub, not working payment. |
| Gateway | `ECommerce.Gateway` uses YARP and validates JWTs. Its configured clusters route to Product, User, and Order only; no Payment route exists. | Existing; needs Payment route and destination. |
| Containers | Product, User, Order, Gateway, and React Containerfiles exist; root `.containerignore` and Podman Compose topology exist. No Payment Containerfile/service exists. | Existing five images; Payment container missing. |
| Kubernetes | No `k8s/` manifests were found. | Missing. |
| CI/CD | Root `azure-pipelines.yml` builds/tests the current solution, builds and publishes five images to ACR, and has Azure App Service deployment steps. No GitHub Actions workflows were found. | Azure pipeline exists; GitHub Actions missing; cloud stages are not verified. |
| Tests | `ECommerce.Tests` uses xUnit/VSTest and references the monolith and current service projects. A monolithic Payment service test verifies payment remains disabled. | Existing service tests; new Payment Service tests required. |
| Databases and secrets | Product, User, and Order have separate contexts/migrations. The isolated Podman demo uses a separate SQL Server volume. No Azure credentials or cluster configuration are present in the repository. | Payment database and migration required; external deployment configuration must be supplied by an authorized owner. |

## Acceptance status before this implementation

| Criterion | Status | Gap |
|---|---|---|
| AC1 — Product/Order/User service boundaries | PASS for local acceptance | Preserve current services and service-owned databases. |
| AC1 — Payment service boundary | FAIL / not implemented | Add an independent payment API and database; coordinate with Order only over authenticated REST. |
| AC2 — Product/Order/User/Gateway/frontend container builds | PASS for local Podman acceptance | Preserve current Containerfiles. |
| AC2 — Payment container | NOT STARTED | Add a multi-stage .NET 10 Containerfile and Compose wiring. |
| AC2 — Kubernetes Deployments/Services | NOT STARTED | Add manifests for four microservices and Gateway (plus frontend), configuration, probes, resource limits, and secret references. |
| AC3 — Azure DevOps pipeline | PARTIAL | Existing pipeline covers the current five images and has not been run against the user's Azure organization. |
| AC3 — GitHub Actions CI | NOT STARTED | Add independently filtered .NET build/test and OCI image-build jobs for Product, Order, User, and Payment. |
| AC3 — image publishing | BLOCKED pending authorized GitHub/Azure configuration | Add OIDC-based workflow; do not publish from pull requests. |
| AC3 — Kubernetes CD | BLOCKED pending approved cluster | Add manually dispatched, environment-approval-gated deployment; default deployment must be disabled. |

## Files and areas to change

- Add `ECommerce.PaymentService/` with API, contracts, domain/persistence, migration, mock provider, Order client, tests, `.http` requests, and a Containerfile.
- Add `PaymentService` to `ECommerce.slnx` and `ECommerce.Tests`.
- Add an internal payment-confirmation endpoint to Order Service and a Payment Service destination/route in YARP.
- Add the Payment Service and dedicated database setting to `compose.yaml` and `.env.example` without modifying existing database contents.
- Add `k8s/` namespace, ConfigMap, example-only Secret manifest, Deployments/Services, and operating instructions.
- Add `.github/workflows/` CI, image publishing, and opt-in Kubernetes deployment workflows.
- Add architecture, Kubernetes, GitHub Actions, validation, and acceptance-audit documentation.

## Risks and constraints

- Payment must be a sandbox mock only. Do not collect/store card numbers, CVVs, or production payment credentials.
- Order and Payment databases must not access each other's tables. Order ownership/amount are read over its authenticated API; the Payment Service calls a key-protected Order confirmation endpoint only after the mock provider returns a verified success.
- A cross-service payment/order update cannot be one SQL transaction. Idempotency and retry-safe confirmation are required; durable background reconciliation is a production hardening item.
- Existing user/order/product databases and the active isolated Podman demo volume must be preserved. Only a new Payment database may be created for local verification.
- No ACR push, GitHub secret configuration, Azure resource creation, Kubernetes cluster deployment, or paid service is authorized by this audit.
- Image builds, YAML parsing, static manifest validation, unit tests, and local Podman verification are possible. Azure OIDC and live Kubernetes rollout require external configuration and approval.

## Implementation follow-up — 2026-11-12

The local implementation has since added the independent Payment Service and database migrations, the YARP payment route, Podman Compose wiring, React mock-payment checkout, six-workload Kubernetes manifests, GitHub Actions, and an extension to the existing Azure DevOps pipeline. The backend solution builds and the full backend suite passes; the frontend suite/build/lint and static YAML/Compose checks also pass. See `docs/VALIDATION-REPORT.md` for precise commands and counts.

This follow-up does not upgrade the external-system criteria to completed: no SQL database migration was applied, no payment checkout was exercised over live HTTP, no image was pushed, and no Kubernetes admission or rollout was run. Those remain blocked on the owner's registry, subscription, database, domain/TLS, and cluster configuration and authorization.
