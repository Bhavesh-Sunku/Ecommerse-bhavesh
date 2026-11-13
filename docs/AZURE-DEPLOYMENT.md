# Azure deployment preparation

No Azure deployment was performed. No subscription, service connection, resource names, or credentials were supplied or used during this work. The following is preparation guidance, not evidence of a live deployment. The Payment Service and Kubernetes/GitHub Actions extension is described in the newer [Kubernetes](./KUBERNETES.md) and [GitHub Actions](./GITHUB-ACTIONS.md) guides.

## Prerequisites

1. Install Azure CLI using Microsoft's Windows instructions: <https://learn.microsoft.com/cli/azure/install-azure-cli-windows>.
2. Open a new PowerShell window and verify `az version`.
3. Authenticate interactively with `az login`; confirm the intended subscription before provisioning.
4. Obtain approval for Azure resource creation and cost. No Azure resource or deployment command should run until the user confirms the subscription, region, resource group, naming, and budget.

## Provisioning plan

Create:

- Azure Container Registry for six OCI images.
- Azure SQL databases `ECommerceProducts`, `ECommerceUsers`, `ECommerceOrders`, and `ECommercePayments`.
- Azure App Service plans/apps for Product, User, Order, Payment, Gateway, and the frontend (or Static Web Apps with a separate supported frontend flow).
- Azure Key Vault for SQL connection strings and the shared JWT signing key.
- Application Insights/Log Analytics for service and gateway logs.

Configure managed identity for App Service access to Key Vault and ACR where supported. Restrict Azure SQL networking to the required app/agent networks and enable TLS. Configure health-check paths (`/health`), HTTPS-only access, frontend CORS, and service-to-service destination URLs.

## Deployment and cutover sequence

1. Set up protected Azure DevOps service connections and secret variables. Required pipeline variables include the ACR server/credentials, Azure service connection, app names, and `PUBLIC_GATEWAY_URL`. Configure approvals/checks for the `ecommerce-production` environment.
2. Run the `BuildAndTest` pipeline and verify test/build output.
3. Build and publish the versioned OCI images using the Podman stages.
4. Deploy the four APIs and gateway to non-production App Services, configure environment/Key Vault references, and apply reviewed EF migrations to their respective databases.
5. Copy data with the reviewed migration script only where source and target databases are on the same SQL Server instance. For cross-server moves, implement and validate a secure transfer mechanism before copying. Compare row counts, IDs, key values, and references; reconcile writes made during the copy.
6. Smoke-test registration/login, product browse, cart, checkout, order history, authorization, health, logging, and service failure behavior.
7. Switch only validated YARP routes. Retain the old API/database and route configuration for rollback.

Do not direct production checkout to Order Service until stock reservations/decrements are coordinated safely with Product Service and tested under concurrent requests. Do not report a public URL or deployment success until it is actually deployed and smoke-tested.

## Current pipeline limitations

The YAML is a template and is unverified against a live Azure DevOps organization. Hosted agent Podman installation is attempted with the Ubuntu package manager; if unavailable or unsupported, configure a maintained self-hosted Linux agent with Podman and the Compose/build capabilities required. ACR authentication, Azure service connections, App Service naming, Key Vault references, managed identities, and manual environment approvals must be configured outside this repository.

## Azure DevOps setup checklist for UC3.4

1. In Azure DevOps, create/select the project and create a pipeline from this repository, selecting the root `azure-pipelines.yml`. The current triggers run verification on pull requests to `main`; OCI build, ACR publish, and deployment stages are limited to `main`.
2. Create an Azure Resource Manager service connection named to match the protected `AZURE_SERVICE_CONNECTION` variable. Grant only the deployment permissions required for the target resource group.
3. Create an ACR with an approved name and configure push authentication. Prefer a scoped service principal or workload identity; if using the YAML's username/password flow, keep `ACR_USERNAME` and `ACR_PASSWORD` as secret pipeline variables and rotate them. Do not commit credentials.
4. Configure pipeline variables:
   - `PUBLIC_GATEWAY_URL` — the HTTPS URL the frontend must call; it is baked into the frontend image at build time.
   - `ACR_SERVER` — for example, `<registry-name>.azurecr.io`.
   - `ACR_USERNAME`, `ACR_PASSWORD` — protected secret variables for the existing login step.
   - `AZURE_SERVICE_CONNECTION`.
   - `PRODUCT_APP_NAME`, `USER_APP_NAME`, `ORDER_APP_NAME`, `PAYMENT_APP_NAME`, `GATEWAY_APP_NAME`, `FRONTEND_APP_NAME`.
5. Create the `ecommerce-production` Azure DevOps Environment and configure approvals/checks before allowing the `Deploy` stage. Test the build/test and image-publish stages before enabling production deployment.
6. Run a pull request pipeline first. After it passes, merge to `main` and verify the image tags in ACR and deployment logs. The hosted agent's `apt-get install podman` step must be validated in the actual organization; use a maintained self-hosted Linux agent if hosted-image support or time limits are unsuitable.

The current deployment tasks select image tags but do not configure application settings. Configure App Service settings/Key Vault references before deployment or add a separately reviewed configuration step. Required settings by service:

| App | Required runtime settings |
|---|---|
| Product Service | `ConnectionStrings__ProductService`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `InternalServices__ApiKey` |
| User Service | `ConnectionStrings__UserService`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ExpiryMinutes` |
| Order Service | `ConnectionStrings__OrderService`, `ProductService__BaseUrl`, `ProductService__InternalServiceKey`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` |
| Payment Service | `ConnectionStrings__PaymentService`, `OrderService__BaseUrl`, `PaymentService__InternalServiceKey`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` |
| Gateway | `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, and Product/User/Order/Payment YARP destination addresses |
| Frontend | `VITE_API_BASE_URL` must be the public gateway URL at image build time (`PUBLIC_GATEWAY_URL`) |

Use the same JWT key in all API services and gateway. Store database credentials, signing keys, and internal service keys in Key Vault or protected app settings; do not put real values in YAML or variable files. Configure App Service ACR pull access (prefer managed identity), HTTPS-only access, health checks, CORS, and service-to-service destination URLs. The applications do not automatically apply EF migrations at startup; migrate each intended Azure SQL database through an approved, controlled release step before routing production traffic.

## Azure deployment checklist for UC3.5

1. Obtain explicit budget/resource approval and subscription-level access before creating anything. Decide region, resource group, App Service plan tier, ACR tier, SQL tier, retention, and monitoring costs.
2. Provision ACR, Azure SQL with separate Product/User/Order/Payment databases, and six app endpoints (or use Azure Static Web Apps for the frontend with a separately configured build/deploy pipeline). Set private/network access and TLS.
3. Configure managed identities and least-privilege database access where supported; otherwise store SQL credentials in Key Vault. Configure Key Vault references and ACR pull access for each app.
4. Apply the four service EF migrations to the new Azure SQL databases, seed or copy only approved data, and verify migration history, row counts, keys, and references. Payment migrations are additive to the Order Service schema and add a separate Payment database. Do not use the populated legacy database as a target or run a cutover without a backup and rollback plan.
5. Set app settings from the table above, use HTTPS service URLs for YARP and Order-to-Product communication, then deploy to a non-production slot/environment.
6. Smoke-test health, auth/role restrictions, catalog, cart, checkout/reservation, order history, logs, database connectivity, and service failure responses. Verify frontend CORS and browser network requests against the public gateway.
7. Require environment approval before production cutover. Retain the old route/database configuration for rollback. Record the resulting public URL only after live smoke tests pass.

Do not share subscription IDs, tokens, passwords, connection strings, or signing keys in chat. Azure deployment remains unverified until these steps are executed against an approved subscription.
