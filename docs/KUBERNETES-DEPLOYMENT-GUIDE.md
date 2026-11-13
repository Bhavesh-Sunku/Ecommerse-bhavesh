# Kubernetes deployment guide

The `k8s/` Kustomize configuration describes Product, User, Order, Payment, Gateway and frontend workloads. It is a deployment template, not evidence that an Azure or Kubernetes environment is configured. Do not apply it until the prerequisites below are reviewed.

## Prerequisites

- `kubectl` configured for the explicitly approved target cluster and namespace.
- An ingress-nginx controller, approved DNS hostname and TLS secret.
- The six application images published to the configured registry under an immutable Git SHA tag.
- Four reachable SQL databases and least-privilege service-specific principals.
- Registry pull access for the cluster identity.
- Secret values injected through an approved secret manager or protected Kubernetes Secret.

The manifests expect namespace `ecommerce`. SQL Server is external; no database workload is installed in Kubernetes. Kubernetes Secrets are base64 encoded, not encrypted by default. Prefer Key Vault CSI/External Secrets or equivalent secret delivery. Never commit populated secrets or apply `secrets.example.yaml`.

## Configure

1. Confirm the current cluster and subscription before writing anything:

   ```powershell
   kubectl config current-context
   kubectl cluster-info
   ```

2. Update the sample ingress hostname in `k8s/ingress.yaml`, provision DNS/TLS, and ensure the frontend image's `VITE_API_BASE_URL` points to the same HTTPS Gateway host.
3. Ensure the Kubernetes Service and Ingress ports remain consistent. Gateway and frontend Services expose port 80 and forward to container port 8080.
4. Create `ecommerce-secrets` in namespace `ecommerce` using a secret manager. The expected keys are listed in `k8s/secrets.example.yaml`; the example itself is excluded from Kustomize and contains placeholders only.
5. Create four distinct databases: `ECommerceProducts`, `ECommerceUsers`, `ECommerceOrders`, and `ECommercePayments`. Set each service's connection string to its own database. Apply migrations only after reviewing the target database; applications do not run migrations at startup.
6. Confirm cluster pull access to the configured ACR. Do not add a registry password to manifests.

## Validate locally

Install `kubectl`, Python 3, PyYAML, and `kubeconform`. From the repository root:

```powershell
python -m pip install PyYAML
python scripts\validate-k8s.py
kubectl kustomize k8s | kubeconform -strict -summary -
```

The reference validator checks namespace consistency, Kustomize resource paths, Deployment/Service selectors and ports, probe/resource configuration, Secret/ConfigMap references, and Ingress backend Service ports. Kubeconform validates rendered Kubernetes schemas. Neither command proves a cluster admission or runtime deployment.

## Deploy only after explicit approval

First replace the Kustomize image tag `replace-with-git-sha` with the full SHA already published to the registry, configure actual ingress/DNS/TLS and secret values, and review the rendered output. The example secret is deliberately not included in this apply:

```powershell
kubectl kustomize k8s
kubectl apply -k k8s
kubectl -n ecommerce get deployments,services,ingress,pods
kubectl -n ecommerce rollout status deployment/product-service
kubectl -n ecommerce rollout status deployment/user-service
kubectl -n ecommerce rollout status deployment/order-service
kubectl -n ecommerce rollout status deployment/payment-service
kubectl -n ecommerce rollout status deployment/gateway
kubectl -n ecommerce rollout status deployment/frontend
```

Do not run these commands against an unapproved or production context. The GitHub Actions deployment workflow is manually dispatched, requires `confirm-deploy=true`, a full SHA, and approval of the protected `k8s-production` environment.

## GitHub configuration

For CI, no deployment credentials are required. For ACR publishing, configure an Azure federated identity for the repository/environment and the protected `acr-publish` environment with:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- Repository variable `PUBLIC_GATEWAY_URL`

The identity should have only the required ACR push role on the existing registry. For Kubernetes deployment, configure protected environment `k8s-production` and secret `KUBE_CONFIG` for the intended cluster. Never paste secret contents into a commit or chat.

## Verify and roll back

After rollout, verify pod readiness, ingress TLS, Gateway health and service logs. Exercise registration/login, product browsing, cart/order, `mock-success`, `mock-decline`, retry/idempotency and order status. A declined order returns to `Pending` and keeps its stock reserved for retry; canceling it should release the stock. These checks are required; manifest validation alone is not deployment verification.

Record the prior image SHA before a release. To revert an affected Deployment:

```powershell
kubectl -n ecommerce rollout history deployment/payment-service
kubectl -n ecommerce rollout undo deployment/payment-service
kubectl -n ecommerce rollout status deployment/payment-service
```

An application rollback does not reverse database migrations.
