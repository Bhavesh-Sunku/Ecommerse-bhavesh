# YARP API Gateway

The gateway validates JWT signature, issuer, audience, and expiration, then forwards the original Authorization header to downstream APIs. It does not trust or forward client-provided identity headers.

## Routing

The gateway routes product and inventory requests to Product Service, registration/login and profiles to User Service, and cart/order requests to Order Service. For local host-run services, the defaults are `http://localhost:5201/`, `http://localhost:5202/`, and `http://localhost:5203/`. Podman Compose overrides those destinations with the service DNS names `product-service`, `user-service`, and `order-service`. Do not point the gateway at the legacy API as a fallback; verify destination service data before cutover.

The gateway applies authentication policies to user, cart, and order routes, and an Admin role policy to product/inventory writes. Downstream services must continue enforcing authorization independently.

## Configuration

Set `Jwt__Key`, `Jwt__Issuer`, and `Jwt__Audience` with the same values used by User Service. The signing key must contain at least 32 UTF-8 bytes. Configure route destinations through `ReverseProxy__Clusters__...`; keep secrets out of configuration files.

The gateway applies a 30-second YARP activity timeout, creates a per-request `X-Correlation-ID`, forwards it to the destination, and logs method, path, status, duration, and correlation ID. Client-provided identity headers are removed. Check `GET /health`; service API Swagger UIs are available in each service's Development environment.
