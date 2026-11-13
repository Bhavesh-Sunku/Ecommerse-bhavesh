# GitHub Actions CI/CD setup

`.github/workflows/ci.yml` restores and builds the full .NET solution and runs the backend suite; tests, lints and builds the React frontend; builds all six application images with Podman; and validates Kubernetes references/schemas and GitHub Actions workflow syntax. A successful push to `main` publishes commit-SHA and `main` images to the already-configured ACR through Buildx, GitHub Actions cache and the protected `acr-publish` OIDC environment. A manual publish is available only from `main` and requires explicit confirmation. Pull requests never publish images.

`.github/workflows/deploy-kubernetes.yml` is manual only. It requires a full commit SHA already present in ACR and `confirm-deploy=true`, then waits for protected environment `k8s-production` approval before applying Kubernetes resources. No automatic cluster deployment is triggered by CI.

## Repository owner setup

1. Verify and approve ownership of `ecommerceacr2026bhavesh.azurecr.io`, its Azure subscription, and the intended non-production registry/repository. The workflow currently targets that exact registry.
2. Create an Azure app/service principal with federated credentials for GitHub OIDC (repository plus the protected environment/branch subject). Grant only ACR push rights to the intended registry; do not use a registry admin password.
3. Add repository or environment secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` to the protected `acr-publish` environment. Configure required reviewers and prevent unreviewed deployment/bypass according to repository policy.
4. Add repository variable `PUBLIC_GATEWAY_URL`, for example `https://shop.example.com`, matching the TLS ingress host. It is embedded in the frontend build because the current Vite client uses a build-time API base URL.
5. For Kubernetes deployment, configure a protected `k8s-production` environment with required reviewers and a least-privilege `KUBE_CONFIG` secret for the intended cluster, or replace it with the organization's approved OIDC/workload-identity flow. Apply the Kubernetes secret and database prerequisites separately; CI does not provision them.
6. Confirm branch protection requires the build/test and image-build checks. Verify the environment approval gates before merging.

Do not store client secrets, SQL connection strings, JWT keys, kubeconfigs, or ACR passwords in the repository or workflow YAML. Never paste them into chat. Do not run publishing or deployment until the project owner authorizes the target subscription, registry, cluster, and budget.

## Artifact names

Each successful publish produces:

- `ecommerce-product-service:<commit-sha>`
- `ecommerce-product-service:main`
- `ecommerce-user-service:<commit-sha>`
- `ecommerce-user-service:main`
- `ecommerce-order-service:<commit-sha>`
- `ecommerce-order-service:main`
- `ecommerce-payment-service:<commit-sha>`
- `ecommerce-payment-service:main`
- `ecommerce-gateway:<commit-sha>`
- `ecommerce-gateway:main`
- `ecommerce-frontend:<commit-sha>`
- `ecommerce-frontend:main`

The CI job does not create a mutable `latest` tag. The deployment workflow validates that the supplied tag is a full SHA. Azure DevOps remains in place and has also been extended to build/publish/deploy Payment Service; configure its `PAYMENT_APP_NAME` and environment settings only if that pipeline is authorized for use.
