# Milestone 3 completion report

## Scope and evidence boundary

This report covers the microservices and deployment extension in this repository. Local builds and tests are not evidence of Azure deployment, registry publishing, Kubernetes admission, or a live Payment-enabled checkout. No cloud resources were changed, no image was pushed, and no existing database was migrated or reset.

## Acceptance checklist

| Acceptance criterion | Status | Evidence |
|---|---|---|
| Product, Order, User and Payment service boundaries | COMPLETED | Four separate service databases were migrated in an isolated Podman SQL Server. Gateway checkout exercised Order-to-Product stock reservation and Payment-to-Order verification/prepare/confirm/release callbacks. |
| Dockerfile for Product Service | COMPLETED | `ECommerce.ProductService/Containerfile`; Podman image build succeeded. |
| Dockerfile for User Service | COMPLETED | `ECommerce.UserService/Containerfile`; Podman image build succeeded. |
| Dockerfile for Order Service | COMPLETED | `ECommerce.OrderService/Containerfile`; Podman image build succeeded. |
| Dockerfile for Payment Service | COMPLETED | `ECommerce.PaymentService/Containerfile`; Podman image build succeeded. |
| Dockerfile for API Gateway | COMPLETED | `ECommerce.Gateway/Containerfile`; Podman image build succeeded. |
| Kubernetes Deployment for all services | PARTIALLY COMPLETED | Six Deployments (four services, Gateway and frontend) are declared with non-root contexts, resources and probes. Schema admission/runtime have not been tested against a cluster. |
| Kubernetes Service for all services | PARTIALLY COMPLETED | Six Services are declared. Local reference validation passes; cluster networking remains unverified. |
| Kubernetes configuration and Secret references | PARTIALLY COMPLETED | ConfigMap and placeholder secret reference keys exist; secret example is excluded from Kustomize. Real secret injection and external SQL connectivity are not configured here. |
| Health probes and resource limits | COMPLETED | Static validation confirms every workload declares readiness/liveness probes and requests/limits. The local Compose stack also reported healthy for all seven containers. |
| GitHub Actions CI pipeline | PARTIALLY COMPLETED | Workflow now defines full backend build/test, frontend checks, Podman image builds and Kubernetes validation. It has not been run on GitHub. |
| Container build and publishing pipeline | PARTIALLY COMPLETED | Local Podman builds passed; protected ACR OIDC publishing uses Buildx SHA/main tags and cache. No ACR credentials/push or GitHub run is verified. |
| Kubernetes validation pipeline | PARTIALLY COMPLETED | Local cross-reference checks and Kubeconform schema validation passed. CI includes the same validation plus Kustomize rendering, but has not run; no live cluster validation is claimed. |
| Expanded Podman Compose integration | COMPLETED | The isolated Compose project started Product, User, Order, Payment, Gateway, frontend and SQL Server; all seven reported healthy. Host Gateway and frontend health endpoints returned HTTP 200. |
| End-to-end Payment-enabled flow verification | COMPLETED | Through the Gateway, mock success confirmed an order; a same-key replay reused the same attempt/transaction; mock decline returned a failed attempt and left the order Pending with stock reserved; canceling that pending order restored inventory. |

### Summary

- Fully completed checklist items: **9**
- Partially completed checklist items: **6**
- Pending: **0**
- Blocked: **0**

Items that need GitHub, registry, cluster or external SQL access are partial rather than complete because their implementation exists but the relevant environment execution is unavailable or not authorized. The counts classify the 15 checklist rows above; they are not a project completion percentage.

## Architecture

The architecture and data ownership are described in [MICROSERVICES-ARCHITECTURE.md](./MICROSERVICES-ARCHITECTURE.md). Product, User, Order and Payment Services own separate databases. YARP is the external API entry point; service-to-service communication uses REST and service-specific credentials. Payment processing is deterministic mock behavior, not real payment processing.

## Container and Kubernetes implementation

Each .NET service and Gateway has a multi-stage .NET 10 Containerfile that publishes Release output and runs as the non-root runtime identity. Frontend has a separate Nginx Containerfile. These Containerfiles use Dockerfile-compatible syntax and can be built with either `podman build -f ...` or `docker build -f ...`; duplicate Dockerfile copies are intentionally avoided. The expanded Podman Compose file uses internal service DNS, health checks, and an isolated optional SQL Server profile.

Kustomize manifests define six Deployments and Services, a namespace, ConfigMap, secret references, resource requests/limits, probes, and TLS ingress. The Ingress references the Gateway and frontend Service ports. `secrets.example.yaml` is a placeholder reference only and is not included in the applied Kustomization.

## CI/CD implementation

`.github/workflows/ci.yml` runs Release backend build/tests, frontend tests/lint/build, Podman image builds, Kustomize reference validation and kubeconform schema checks. ACR publishing is restricted to main-branch push or explicitly confirmed manual dispatch on main; it uses GitHub OIDC, Buildx, SHA and `main` tags, and GitHub Actions cache.

`.github/workflows/deploy-kubernetes.yml` is manual, requires an immutable SHA, a `confirm-deploy` input and protected `k8s-production` approval. GitHub Actions execution, ACR publishing and a cluster rollout remain unverified. Existing Azure DevOps configuration is separate and also requires owner environment setup.

## Validation performed

| Command/check | Result |
|---|---|
| `dotnet build ECommerce.slnx --configuration Release` | Passed, 0 warnings and 0 errors. |
| `dotnet test ECommerce.Tests/ECommerce.Tests.csproj --configuration Release --no-build` | Passed, 45 tests, 0 failures. |
| Frontend `npm test` | Passed, 27 tests across 6 files. |
| Frontend `npm run build` | Passed. |
| Frontend `npm run lint` | Passed without warnings. |
| Podman build/inspect for Product, User, Order, Payment, Gateway and frontend | Passed locally; no image was pushed. |
| `podman compose --env-file .env.example -p ecommerce-config-check config --quiet` | Passed; this validates configuration and does not start services. |
| `python scripts/validate-k8s.py` | Passed locally; verifies references, selectors, service ports, probes, resource limits and secret/config keys. It caught and led to correction of the Gateway/frontend Ingress service-port mismatch. |
| Podman Compose local integration | Passed in unique project `ecommerce-m3-acceptance-20261113`: all seven containers healthy; Gateway and frontend host health checks returned HTTP 200. The existing `ecommerce-capstone-uc33` volume was left untouched. |
| Isolated service database migrations | Passed for all four service databases in the new SQL Server container. Temporary test databases/volume were removed after the end-to-end checks; the legacy database was not contacted. |
| Gateway checkout smoke flow | Passed: registration/login, admin-only disposable product setup, cart/order, mock success/confirmation, idempotent replay, mock decline/pending order, and cancellation/inventory release. |
| Kubernetes schema validation | Kubeconform v0.6.7 validated 15 resources in 5 manifest files: 15 valid, 0 invalid, 0 errors. `kubectl`/Kustomize rendering was not run locally; CI is configured to run it. |
| GitHub Actions / ACR / cluster deployment | Not run; no credentials or deployment authorization were used. |

## Files created for this completion

- `scripts/validate-k8s.py`
- `docs/MICROSERVICES-ARCHITECTURE.md`
- `docs/KUBERNETES-DEPLOYMENT-GUIDE.md`
- `docs/MILESTONE3-COMPLETION-REPORT.md`

## Files modified for this completion

- `.github/workflows/ci.yml`
- `.env.example`
- `compose.yaml`
- `k8s/ingress.yaml`
- `README.md`
- `docs/ARCHITECTURE.md`
- `docs/GITHUB-ACTIONS.md`
- `docs/VALIDATION-REPORT.md`
- `Milestone3-Completion-Report.md`

The repository already contained the service projects, Containerfiles, Compose configuration, Kustomize manifests, and deployment workflow; they were inspected and reused rather than duplicated.

## Build and run commands

From the repository root:

```powershell
dotnet restore ECommerce.slnx
dotnet build ECommerce.slnx --configuration Release --no-restore
dotnet test ECommerce.Tests\ECommerce.Tests.csproj --configuration Release --no-build
Set-Location ECommerce.Frontend
npm ci
npm test
npm run lint
npm run build
```

For Podman, copy `.env.example` to a local `.env`, generate distinct random values for all required keys, build the six images from the repository root, and use only a disposable local SQL profile when appropriate. The Compose `IMAGE_TAG` defaults to `local`; set it to `milestone3-verify` if using the example tags below. On this Windows/Podman Compose setup, `up --build` did not resolve the custom Containerfile paths, so the images were built explicitly and Compose was started from those images.

```powershell
podman build -t localhost/ecommerce-product-service:milestone3-verify -f ECommerce.ProductService\Containerfile .
podman build -t localhost/ecommerce-user-service:milestone3-verify -f ECommerce.UserService\Containerfile .
podman build -t localhost/ecommerce-order-service:milestone3-verify -f ECommerce.OrderService\Containerfile .
podman build -t localhost/ecommerce-payment-service:milestone3-verify -f ECommerce.PaymentService\Containerfile .
podman build -t localhost/ecommerce-gateway:milestone3-verify -f ECommerce.Gateway\Containerfile .
podman build -t localhost/ecommerce-frontend:milestone3-verify -f ECommerce.Frontend\Containerfile --build-arg VITE_API_BASE_URL=http://localhost:8080 .
podman compose --env-file .env -p ecommerce-milestone3 --profile local-sql up -d
podman compose --env-file .env -p ecommerce-milestone3 ps
podman compose --env-file .env -p ecommerce-milestone3 logs --follow
```

Before starting, set `IMAGE_TAG=milestone3-verify` in the local `.env` file.

Do not reuse a database volume or connection string that contains data you need to preserve. To stop only this named test project after verification:

```powershell
podman compose --env-file .env -p ecommerce-milestone3 down
```

## Kubernetes validation and deployment commands

```powershell
python -m pip install PyYAML
python scripts\validate-k8s.py
kubectl kustomize k8s | kubeconform -strict -summary -
kubectl config current-context
kubectl apply -k k8s
kubectl -n ecommerce get deployments,services,ingress,pods
```

Before `kubectl apply`, replace the sample hostname, set the immutable image tag, create secrets through an approved secret manager, prepare the external databases/migrations, and verify the current context. Never apply the placeholder secret file.

## Required GitHub settings and remaining work

1. Protect `main` and require CI checks before merge.
2. Configure protected environment `acr-publish` and federated Azure OIDC trust; add `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` as environment secrets.
3. Set repository variable `PUBLIC_GATEWAY_URL` to the final HTTPS Gateway URL before publishing the frontend image.
4. Ensure the workload identity has least-privilege push rights on the existing ACR and the cluster identity has pull rights.
5. Configure protected environment `k8s-production`, approval reviewers, and secret `KUBE_CONFIG` only when the target cluster is approved. Prefer short-lived workload identity where available.
6. Install/configure `kubectl` locally for Kustomize rendering and confirm the CI workflow passes in GitHub.
7. Test a SQL Server-backed concurrent idempotency case; EF InMemory does not enforce SQL unique indexes.
8. Apply reviewed Order and Payment migrations only to their respective approved cloud databases. No Azure resource, ACR push or Kubernetes deployment was performed during this work.
