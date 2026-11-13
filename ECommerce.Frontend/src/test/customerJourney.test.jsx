import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../context/AuthContext";
import { AppRoutes } from "../App";

function createToken() {
  const payload = new TextEncoder().encode(JSON.stringify({
    name: "Demo Customer",
    email: "customer@example.test",
    role: "Customer",
    exp: Math.floor(Date.now() / 1000) + 3600
  }));
  const encoded = btoa(String.fromCharCode(...payload))
    .replace(/=/g, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_");
  return `header.${encoded}.signature`;
}

function jsonResponse(body, status = 200) {
  return Promise.resolve({
    ok: status >= 200 && status < 300,
    status,
    headers: { get: () => "application/json" },
    json: async () => body
  });
}

describe("customer purchase journey", () => {
  it("shops, checks out, views orders, and logs out through API clients", async () => {
    const user = userEvent.setup();
    const token = createToken();
    localStorage.setItem("token", token);
    let cart = { id: 1, items: [], totalAmount: 0 };
    const order = {
      id: 41,
      orderDate: "2026-11-05T10:00:00Z",
      totalAmount: 159.98,
      status: "Pending",
      items: [{
        productId: 7,
        productName: "Trail Shoes",
        quantity: 2,
        unitPrice: 79.99,
        subtotal: 159.98
      }]
    };

    globalThis.fetch = vi.fn(async (url, options = {}) => {
      const requestUrl = new URL(url, "http://api.test");
      const method = options.method || "GET";

      if (requestUrl.pathname === "/api/Products/categories") {
        return jsonResponse(["Footwear"]);
      }
      if (requestUrl.pathname === "/api/Products" && method === "GET") {
        return jsonResponse({ products: [{
          id: 7,
          name: "Trail Shoes",
          description: "Comfortable running shoes",
          category: "Footwear",
          price: 79.99,
          imageUrl: "trail-shoes.png",
          availableStock: 5
        }], pageNumber: 1, totalPages: 1 });
      }
      if (requestUrl.pathname === "/api/Products/7") {
        return jsonResponse({
          id: 7,
          name: "Trail Shoes",
          description: "Comfortable running shoes",
          category: "Footwear",
          price: 79.99,
          imageUrl: "trail-shoes.png",
          availableStock: 5
        });
      }
      if (requestUrl.pathname === "/api/Cart/items" && method === "POST") {
        cart = {
          id: 1,
          items: [{
            id: 12,
            productId: 7,
            productName: "Trail Shoes",
            imageUrl: "trail-shoes.png",
            unitPrice: 79.99,
            quantity: 2,
            availableStock: 5,
            subtotal: 159.98
          }],
          totalAmount: 159.98
        };
        return jsonResponse(cart);
      }
      if (requestUrl.pathname === "/api/Cart" && method === "GET") {
        return jsonResponse(cart);
      }
      if (requestUrl.pathname === "/api/Cart/items/12" && method === "PUT") {
        cart.items[0].quantity = JSON.parse(options.body).quantity;
        cart.items[0].subtotal = cart.items[0].quantity * cart.items[0].unitPrice;
        cart.totalAmount = cart.items[0].subtotal;
        return jsonResponse(cart);
      }
      if (requestUrl.pathname === "/api/Orders/from-cart" && method === "POST") {
        order.totalAmount = cart.totalAmount;
        order.items[0].quantity = cart.items[0].quantity;
        order.items[0].subtotal = cart.items[0].subtotal;
        cart = { id: 1, items: [], totalAmount: 0 };
        return jsonResponse(order, 201);
      }
      if (requestUrl.pathname === "/api/Orders" && method === "GET") {
        return jsonResponse([order]);
      }
      if (requestUrl.pathname === "/api/Orders/41") {
        return jsonResponse(order);
      }

      return jsonResponse({ message: `Unexpected request: ${method} ${requestUrl.pathname}` }, 404);
    });

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={["/products"]}>
          <AppRoutes />
        </MemoryRouter>
      </AuthProvider>
    );

    expect(await screen.findByRole("heading", { name: "Products" })).toBeInTheDocument();
    expect(await screen.findByText("Trail Shoes")).toBeInTheDocument();

    await user.click(screen.getByRole("link", { name: "View Product" }));
    expect(await screen.findByText("Comfortable running shoes")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Quantity"), { target: { value: "2" } });
    await user.click(screen.getByRole("button", { name: "Add to Cart" }));

    expect(await screen.findByRole("heading", { name: "Your Shopping Cart" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Increase Trail Shoes quantity" }));
    expect(await screen.findByText("₹239.97")).toBeInTheDocument();
    await user.click(screen.getByRole("link", { name: "Proceed to Checkout" }));

    expect(await screen.findByRole("heading", { name: "Checkout" })).toBeInTheDocument();
    await user.clear(screen.getByLabelText("Customer name"));
    await user.type(screen.getByLabelText("Customer name"), "Demo Customer");
    await user.type(screen.getByLabelText("Shipping address"), "123 Testing Avenue");
    await user.type(screen.getByLabelText("Phone number"), "555-0101");
    await user.click(screen.getByRole("button", { name: "Place Order" }));
    expect(await screen.findByRole("heading", { name: "Order placed" })).toBeInTheDocument();
    expect(screen.getByText("Your order #41 was placed successfully.")).toBeInTheDocument();

    await user.click(screen.getByRole("link", { name: "View order" }));
    expect(await screen.findByRole("heading", { name: "Order #41" })).toBeInTheDocument();
    expect(screen.getByText("Quantity: 3")).toBeInTheDocument();
    await user.click(screen.getByRole("link", { name: "My Orders" }));
    expect(await screen.findByRole("heading", { name: "My Orders" })).toBeInTheDocument();
    expect(screen.getByText(/239\.97/)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Logout" }));
    expect(await screen.findByRole("heading", { name: "Login" })).toBeInTheDocument();
    await waitFor(() => expect(localStorage.getItem("token")).toBeNull());
  });
});
