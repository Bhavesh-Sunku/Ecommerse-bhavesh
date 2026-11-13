# Kubernetes deployment guide

The manifests in `k8s/` describe an isolated `ecommerce` namespace with Product, User, Order, Payment, YARP Gateway, React frontend, ClusterIP services, and TLS ingress. They are templates, not a deployment. No cluster was provisioned or changed during this repository work.

## Prerequisites and costs

Before deploying, obtain approval for an Azure subscription and budget, a Kubernetes cluster (for example AKS), a reachable Azure SQL server with four service databases, the existing ACR, a DNS name, an ingress controller, and a TLS certificate. AKS nodes, SQL databases, ACR storage, public IP/ingress, logging, backups, and network egress may incur charges. Use a disposable non-production environment first.

Use the required service identities and least privilege. Configure an AKS managed identity or equivalent to pull from ACR; do not put ACR passwords in a manifest. Configure SQL networking/private endpoints and one least-privilege database principal per service. Production secrets should be delivered through Azure Key Vault CSI/External Secrets or another approved secret manager. Kubernetes Secrets are base64-encoded and are not encryption by themselves.

## Configure before applying

1. Install `kubectl` and configure the intended cluster context. Confirm the subscription, cluster, and namespace with the resource owner. Do not use a shared or production context for the first run.
2. Provision/authorize the ACR pull identity for `ecommerceacr2026bhavesh.azurecr.io`. Verify that this registry is owned by the project owner and that push permission is approved before configuring the GitHub Actions environment.
3. Install and configure the `nginx` Ingress controller, DNS for `shop.example.com`, and TLS secret `ecommerce-tls`. Replace the sample host in `k8s/ingress.yaml` with the approved host. The frontend image must be built with the same HTTPS URL in the GitHub repository variable `PUBLIC_GATEWAY_URL`; `/api` is routed to Gateway by the ingress.
4. Create `ecommerce-secrets` in namespace `ecommerce` using a protected secret manager. Required keys are listed in `k8s/secrets.example.yaml`. That example is intentionally excluded from `kustomization.yaml`; its placeholders are not valid credentials and must never be applied unchanged. Do not commit populated secret files or paste secrets in chat.
5. Create four distinct databases: `ECommerceProducts`, `ECommerceUsers`, `ECommerceOrders`, and `ECommercePayments`. Use separate database principals/permissions and configure their connection strings in the secret store. The sample values in the secret example are placeholders.
6. Create the ACR SHA-tagged images using the approved CI publish workflow. The deployment workflow accepts only a full 40-character commit SHA, not `latest`.

## Database migrations

Applications do not run migrations at startup. Review and back up the target databases, confirm the exact service connection, then generate reviewed SQL scripts or run the EF migration only against the newly created service database. The Order Service migration adds payment-coordination fields; it must be applied to the Order Service database, not the legacy monolith database. Payment Service has its own new database and migration.

For a local disposable SQL Server, set the design-time variables (`ORDER_SERVICE_CONNECTION` and `PAYMENT_SERVICE_CONNECTION`) to the intended isolated service databases before running:

```powershell
dotnet ef migrations script --idempotent --project ECommerce.OrderService\ECommerce.OrderService.csproj --startup-project ECommerce.OrderService\ECommerce.OrderService.csproj --output $env:TEMP\ECommerceOrders-payment.sql
dotnet ef migrations script --idempotent --project ECommerce.PaymentService\ECommerce.PaymentService.csproj --startup-project ECommerce.PaymentService\ECommerce.PaymentService.csproj --output $env:TEMP\ECommercePayments.sql
```

Review the scripts and target database names before applying. Do not run these commands against the populated legacy `ECommerceDb`; do not apply migrations in this audit or as part of image startup.

## Review and apply manifests

After secrets, service databases, migrations, ACR pull access, and TLS ingress are verified:

```powershell
kubectl config current-context
kubectl apply -k k8s
kubectl -n ecommerce get deployments,services,ingress,pods
kubectl -n ecommerce rollout status deployment/product-service
kubectl -n ecommerce rollout status deployment/user-service
kubectl -n ecommerce rollout status deployment/order-service
kubectl -n ecommerce rollout status deployment/payment-service
kubectl -n ecommerce rollout status deployment/gateway
kubectl -n ecommerce rollout status deployment/frontend
```

The GitHub Actions deploy workflow applies the same Kustomize directory, substitutes the approved immutable SHA tag, and waits for all six rollouts. It runs only when manually dispatched with `confirm-deploy=true` and after the protected `k8s-production` environment approves. Its `KUBE_CONFIG` secret must be scoped to the intended cluster and namespace; prefer workload identity instead of a long-lived kubeconfig when available.

## Smoke test and rollback

From an authorized client, verify the HTTPS frontend and gateway health, then register/login, browse products, create an order, complete `mock-success`, and confirm the order status and payment transaction ID. Verify a separate `mock-decline` attempt leaves the order pending and releases the inventory reservation. These are mock payment checks only.

Before deploying, record the previous image SHAs. To roll back a workload:

```powershell
kubectl -n ecommerce rollout history deployment/payment-service
kubectl -n ecommerce rollout undo deployment/payment-service
kubectl -n ecommerce rollout status deployment/payment-service
```

Repeat for the affected deployment and verify the corresponding database schema remains compatible. Application rollback does not reverse database migrations.
