import { apiRequest } from "./api";

export function createPayment(
  orderId,
  idempotencyKey,
  paymentMethod = "mock-success"
) {
  return apiRequest("/api/payments", {
    method: "POST",
    body: JSON.stringify({ orderId, idempotencyKey, paymentMethod })
  });
}