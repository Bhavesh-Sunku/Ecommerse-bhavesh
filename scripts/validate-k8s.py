from pathlib import Path

import yaml


ROOT = Path(__file__).resolve().parents[1]
K8S = ROOT / "k8s"


def load_documents(path):
    with path.open(encoding="utf-8") as file:
        return [item for item in yaml.safe_load_all(file) if item]


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def main():
    kustomization = yaml.safe_load((K8S / "kustomization.yaml").read_text(encoding="utf-8"))
    require("secrets.example.yaml" not in kustomization["resources"],
            "The placeholder secret example must not be applied by Kustomize.")
    for resource_path in kustomization["resources"]:
        require((K8S / resource_path).is_file(),
                f"Kustomize resource does not exist: {resource_path}")

    namespace = load_documents(K8S / "namespace.yaml")[0]
    require(namespace["kind"] == "Namespace" and namespace["metadata"]["name"] == "ecommerce",
            "Expected the ecommerce namespace manifest.")
    config = load_documents(K8S / "configmap.yaml")[0]
    secret = load_documents(K8S / "secrets.example.yaml")[0]
    config_keys = set(config.get("data", {}))
    secret_keys = set(secret.get("stringData", {}))

    deployments = {
        item["metadata"]["name"]: item
        for item in load_documents(K8S / "deployments.yaml")
        if item["kind"] == "Deployment"
    }
    services = {
        item["metadata"]["name"]: item
        for item in load_documents(K8S / "services.yaml")
        if item["kind"] == "Service"
    }
    require(len(deployments) == 6, f"Expected 6 Deployments; found {len(deployments)}.")
    require(len(services) == 6, f"Expected 6 Services; found {len(services)}.")
    expected_images = {
        "product-service": "ecommerce-product-service",
        "user-service": "ecommerce-user-service",
        "order-service": "ecommerce-order-service",
        "payment-service": "ecommerce-payment-service",
        "gateway": "ecommerce-gateway",
        "frontend": "ecommerce-frontend",
    }

    for resource in [*deployments.values(), *services.values(), config, secret]:
        require(resource["metadata"].get("namespace") == "ecommerce",
                f"{resource['kind']} {resource['metadata']['name']} is outside ecommerce.")

    for name, deployment in deployments.items():
        template = deployment["spec"]["template"]
        labels = template["metadata"].get("labels", {})
        selector = deployment["spec"]["selector"]["matchLabels"]
        require(all(labels.get(key) == value for key, value in selector.items()),
                f"Deployment {name} selector does not match pod labels.")
        containers = template["spec"]["containers"]
        require(containers, f"Deployment {name} has no containers.")
        for container in containers:
            image = container.get("image", "")
            image_prefix = (
                "ecommerceacr2026bhavesh.azurecr.io/"
                + expected_images.get(name, "")
                + ":"
            )
            require(image.startswith(image_prefix) and not image.endswith(":latest"),
                    f"Deployment {name} must use its version-tagged ACR image.")
            require("readinessProbe" in container and "livenessProbe" in container,
                    f"Deployment {name}/{container['name']} is missing a health probe.")
            for probe_name in ("readinessProbe", "livenessProbe"):
                probe = container[probe_name]
                require(probe.get("httpGet", {}).get("path") == "/health",
                        f"Deployment {name}/{container['name']} must probe /health.")
            resources = container.get("resources", {})
            require(resources.get("requests") and resources.get("limits"),
                    f"Deployment {name}/{container['name']} needs resource requests and limits.")
            for variable in container.get("env", []):
                if "secretKeyRef" in variable.get("valueFrom", {}):
                    key = variable["valueFrom"]["secretKeyRef"]["key"]
                    require(key in secret_keys,
                            f"Deployment {name} references missing example secret key {key}.")
                if "configMapKeyRef" in variable.get("valueFrom", {}):
                    key = variable["valueFrom"]["configMapKeyRef"]["key"]
                    require(key in config_keys,
                            f"Deployment {name} references missing ConfigMap key {key}.")

    for name, service in services.items():
        selector = service["spec"].get("selector", {})
        matches = [
            deployment for deployment in deployments.values()
            if all(deployment["spec"]["template"]["metadata"]["labels"].get(key) == value
                   for key, value in selector.items())
        ]
        require(len(matches) == 1, f"Service {name} must select exactly one Deployment.")
        container_ports = {
            port.get("name"): port["containerPort"]
            for container in matches[0]["spec"]["template"]["spec"]["containers"]
            for port in container.get("ports", [])
        }
        for port in service["spec"]["ports"]:
            target = port.get("targetPort")
            require(target in container_ports if isinstance(target, str)
                    else target in container_ports.values(),
                    f"Service {name} targets a port not exposed by its Deployment.")

    ingress_docs = load_documents(K8S / "ingress.yaml")
    for ingress in ingress_docs:
        require(ingress["metadata"].get("namespace") == "ecommerce",
                "Ingress must use the ecommerce namespace.")
        for rule in ingress["spec"].get("rules", []):
            for path in rule["http"]["paths"]:
                backend = path["backend"]["service"]
                service = services.get(backend["name"])
                require(service is not None, f"Ingress references unknown Service {backend['name']}.")
                service_ports = {port["port"] for port in service["spec"]["ports"]}
                require(backend["port"]["number"] in service_ports,
                        f"Ingress port does not exist on Service {backend['name']}.")

    print(f"Validated {len(deployments)} Deployments, {len(services)} Services, "
          f"{len(ingress_docs)} Ingress resource(s), and configuration references.")


if __name__ == "__main__":
    main()
