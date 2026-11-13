import { apiRequest } from "./api";

export function getCart() {
  return apiRequest("/api/Cart");
}

export function addCartItem(productId, quantity = 1) {
  return apiRequest("/api/Cart/items", {
    method: "POST",
    body: JSON.stringify({ productId, quantity })
  });
}

export function updateCartItem(itemId, quantity) {
  return apiRequest(`/api/Cart/items/${itemId}`, {
    method: "PUT",
    body: JSON.stringify({ quantity })
  });
}

export function removeCartItem(itemId) {
  return apiRequest(`/api/Cart/items/${itemId}`, {
    method: "DELETE"
  });
}

export function clearCart() {
  return apiRequest("/api/Cart", { method: "DELETE" });
}
