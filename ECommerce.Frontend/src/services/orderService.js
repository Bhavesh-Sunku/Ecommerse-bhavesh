import { apiRequest } from "./api";

export function createOrder(data) {
  return apiRequest(
    "/api/Orders/items",
    {
      method: "POST",
      body: JSON.stringify(data)
    }
  );
}

export function createOrderFromCart(data) {
  return apiRequest("/api/Orders/from-cart", {
    method: "POST",
    body: JSON.stringify(data)
  });
}

export function getMyOrders() {
  return apiRequest(
    "/api/Orders"
  );
}

export function getOrderById(id) {
  return apiRequest(
    `/api/Orders/${id}`
  );
}

export function cancelOrder(id) {
  return apiRequest(
    `/api/Orders/${id}/cancel`,
    {
      method: "PUT"
    }
  );
}