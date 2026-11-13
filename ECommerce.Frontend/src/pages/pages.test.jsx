import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../context/AuthContext";
import Cart from "./Cart";
import Checkout from "./Checkout";
import OrderDetails from "./OrderDetails";
import Orders from "./Orders";
import ProductDetails from "./ProductDetails";
import Products from "./Products";
import {
  getCart,
  addCartItem,
  removeCartItem
} from "../services/cartService";
import {
  getProductCategories,
  getProductById,
  getProducts
} from "../services/productService";
import {
  getMyOrders,
  getOrderById,
  createOrderFromCart
} from "../services/orderService";
import { createPayment } from "../services/paymentService";

vi.mock("../services/productService", () => ({
  getProductById: vi.fn(),
  getProductCategories: vi.fn(),
  getProducts: vi.fn()
}));

vi.mock("../services/cartService", () => ({
  addCartItem: vi.fn(),
  clearCart: vi.fn(),
  getCart: vi.fn(),
  removeCartItem: vi.fn(),
  updateCartItem: vi.fn()
}));

vi.mock("../services/orderService", () => ({
  cancelOrder: vi.fn(),
  createOrderFromCart: vi.fn(),
  getMyOrders: vi.fn(),
  getOrderById: vi.fn()
}));

vi.mock("../services/paymentService", () => ({
  createPayment: vi.fn()
}));

function renderWithAuth(ui, { path = "/", routes = null } = {}) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[path]}>
        {routes || ui}
      </MemoryRouter>
    </AuthProvider>
  );
}

function createDeferred() {
  let resolve;
  let reject;
  const promise = new Promise((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

const availableProduct = {
  id: 7,
  name: "Trail Shoes",
  description: "Trail running shoes",
  category: "Footwear",
  price: 79.99,
  availableStock: 4
};

const cartWithItem = {
  id: 1,
  items: [{
    id: 12,
    productId: 7,
    productName: "Trail Shoes",
    quantity: 2,
    unitPrice: 10,
    subtotal: 20,
    availableStock: 5
  }],
  totalAmount: 20
};

beforeEach(() => {
  vi.clearAllMocks();
  getProductCategories.mockResolvedValue(["Footwear", "Books"]);
  getProducts.mockResolvedValue({ products: [] });
  getCart.mockResolvedValue(cartWithItem);
  createPayment.mockResolvedValue({
    status: "Successful",
    orderConfirmed: true
  });
});

describe("product listing filters and availability", () => {
  it("requests products for the selected category", async () => {
    const user = userEvent.setup();
    renderWithAuth(<Products />);
    await screen.findByText("No products found.");

    await user.selectOptions(screen.getByLabelText("Category"), "Footwear");

    await waitFor(() => {
      expect(getProducts).toHaveBeenLastCalledWith(expect.objectContaining({
        category: "Footwear",
        pageNumber: 1
      }));
    });
  });

  it("submits the entered search term", async () => {
    const user = userEvent.setup();
    renderWithAuth(<Products />);
    await screen.findByText("No products found.");

    await user.type(screen.getByPlaceholderText("Search products..."), "trail shoes");
    await user.click(screen.getByRole("button", { name: "Search" }));

    await waitFor(() => {
      expect(getProducts).toHaveBeenLastCalledWith(expect.objectContaining({
        search: "trail shoes",
        pageNumber: 1
      }));
    });
  });

  it("shows out-of-stock products and prevents adding them to the cart", async () => {
    getProducts.mockResolvedValue({
      products: [{ ...availableProduct, availableStock: 0 }]
    });
    renderWithAuth(<Products />);

    expect(await screen.findByText("Out of stock")).toBeInTheDocument();
    const addButton = screen.getByRole("button", { name: "Out of Stock" });
    expect(addButton).toBeDisabled();
    fireEvent.click(addButton);
    expect(addCartItem).not.toHaveBeenCalled();
  });
});

describe("product details", () => {
  it("shows the loading state and constrains quantity to available stock", async () => {
    const request = createDeferred();
    getProductById.mockReturnValue(request.promise);
    renderWithAuth(
      <Routes>
        <Route path="/products/:id" element={<ProductDetails />} />
      </Routes>,
      { path: "/products/7" }
    );

    expect(screen.getByText("Loading product...")).toBeInTheDocument();
    request.resolve(availableProduct);

    const quantity = await screen.findByLabelText("Quantity");
    expect(quantity).toHaveAttribute("min", "1");
    expect(quantity).toHaveAttribute("max", "4");
    fireEvent.change(quantity, { target: { value: "0" } });
    expect(quantity).toHaveValue(1);
    fireEvent.change(quantity, { target: { value: "5" } });
    expect(quantity).toHaveValue(1);
    fireEvent.change(quantity, { target: { value: "4" } });
    expect(quantity).toHaveValue(4);
  });

  it("returns to the product listing using the back link", async () => {
    const user = userEvent.setup();
    getProductById.mockResolvedValue(availableProduct);
    renderWithAuth(null, {
      path: "/products/7",
      routes: (
        <Routes>
          <Route path="/products/:id" element={<ProductDetails />} />
          <Route path="/products" element={<h1>Product catalog</h1>} />
        </Routes>
      )
    });

    await screen.findByRole("heading", { name: "Trail Shoes" });
    await user.click(screen.getByRole("link", { name: "Back to products" }));
    expect(await screen.findByRole("heading", { name: "Product catalog" })).toBeInTheDocument();
  });

  it("reports an invalid product ID", async () => {
    getProductById.mockRejectedValue(new Error("Product not found."));
    renderWithAuth(
      <Routes>
        <Route path="/products/:id" element={<ProductDetails />} />
      </Routes>,
      { path: "/products/not-a-number" }
    );

    expect(await screen.findByRole("alert")).toHaveTextContent("Product not found.");
    expect(getProductById).toHaveBeenCalledWith("not-a-number");
  });

  it("disables add-to-cart when product stock is zero", async () => {
    getProductById.mockResolvedValueOnce({ ...availableProduct, availableStock: 0 });
    renderWithAuth(
      <Routes>
        <Route path="/products/:id" element={<ProductDetails />} />
      </Routes>,
      { path: "/products/7" }
    );
    expect(await screen.findByRole("button", { name: "Add to Cart" })).toBeDisabled();
    expect(addCartItem).not.toHaveBeenCalled();
  });
});

describe("cart", () => {
  it("removes a cart item", async () => {
    const user = userEvent.setup();
    removeCartItem.mockResolvedValue({ items: [], totalAmount: 0 });
    renderWithAuth(<Cart />);

    await user.click(await screen.findByRole("button", { name: "Remove" }));

    expect(removeCartItem).toHaveBeenCalledWith(12);
    expect(await screen.findByText("Your cart is empty.")).toBeInTheDocument();
  });

  it("shows an empty cart and surfaces cart API failures", async () => {
    getCart.mockResolvedValueOnce({ items: [], totalAmount: 0 });
    const { unmount } = renderWithAuth(<Cart />);
    expect(await screen.findByText("Your cart is empty.")).toBeInTheDocument();
    unmount();

    getCart.mockRejectedValueOnce(new Error("Cart service unavailable."));
    renderWithAuth(<Cart />);
    expect(await screen.findByRole("alert")).toHaveTextContent("Cart service unavailable.");
  });
});

describe("checkout", () => {
  async function fillShippingForm(user, { address = "123 Testing Avenue" } = {}) {
    await user.type(screen.getByLabelText("Customer name"), "Demo Customer");
    await user.type(screen.getByLabelText("Shipping address"), address);
    await user.type(screen.getByLabelText("Phone number"), "555-0101");
  }

  it("requires shipping details and rejects an invalid short address", async () => {
    const user = userEvent.setup();
    renderWithAuth(<Checkout />, { path: "/checkout" });
    await screen.findByRole("heading", { name: "Checkout" });

    const form = screen.getByRole("button", { name: "Place Order" }).closest("form");
    expect(screen.getByLabelText("Customer name")).toBeRequired();
    expect(screen.getByLabelText("Shipping address")).toBeRequired();
    expect(screen.getByLabelText("Phone number")).toBeRequired();
    expect(screen.getByLabelText("Shipping address")).toHaveAttribute("minLength", "10");
    await user.click(screen.getByRole("button", { name: "Place Order" }));
    expect(form.checkValidity()).toBe(false);
    expect(createOrderFromCart).not.toHaveBeenCalled();

    await fillShippingForm(user, { address: "Too short" });
    const address = screen.getByLabelText("Shipping address");
    expect(address.value.length).toBeLessThan(address.minLength);
    await user.click(screen.getByRole("button", { name: "Place Order" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Please enter valid shipping details.");
    expect(createOrderFromCart).not.toHaveBeenCalled();
  });

  it("displays checkout API failures", async () => {
    const user = userEvent.setup();
    createOrderFromCart.mockRejectedValue(new Error("Checkout failed."));
    renderWithAuth(<Checkout />, { path: "/checkout" });
    await screen.findByRole("heading", { name: "Checkout" });
    await fillShippingForm(user);

    await user.click(screen.getByRole("button", { name: "Place Order" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Checkout failed.");
  });

  it("submits only once when checkout is submitted repeatedly", async () => {
    const user = userEvent.setup();
    const request = createDeferred();
    createOrderFromCart.mockReturnValue(request.promise);
    renderWithAuth(<Checkout />, { path: "/checkout" });
    await screen.findByRole("heading", { name: "Checkout" });
    await fillShippingForm(user);

    const form = screen.getByRole("button", { name: "Place Order" }).closest("form");
    fireEvent.submit(form);
    fireEvent.submit(form);
    expect(createOrderFromCart).toHaveBeenCalledTimes(1);
    request.resolve({ id: 88 });
    expect(await screen.findByText("Your order #88 was placed successfully.")).toBeInTheDocument();
    expect(await screen.findByText("Mock payment confirmed. No real payment was taken.")).toBeInTheDocument();
    expect(createPayment).toHaveBeenCalledWith(
      88,
      expect.any(String)
    );
    expect(createPayment).toHaveBeenCalledTimes(1);
  });

  it("retries payment with the same idempotency key after a transient error", async () => {
    const user = userEvent.setup();
    createPayment
      .mockRejectedValueOnce(Object.assign(
        new Error("Payment service unavailable."),
        { status: 503 }
      ))
      .mockResolvedValueOnce({
        status: "Successful",
        orderConfirmed: true
      });
    createOrderFromCart.mockResolvedValue({ id: 89 });
    renderWithAuth(<Checkout />, { path: "/checkout" });
    await screen.findByRole("heading", { name: "Checkout" });
    await fillShippingForm(user);

    await user.click(screen.getByRole("button", { name: "Place Order" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Payment service unavailable.");
    const firstRequest = createPayment.mock.calls[0];
    await user.click(screen.getByRole("button", { name: "Retry mock payment" }));

    expect(await screen.findByText("Mock payment confirmed. No real payment was taken.")).toBeInTheDocument();
    expect(createPayment).toHaveBeenCalledTimes(2);
    expect(createPayment.mock.calls[1]).toEqual(firstRequest);
    expect(createOrderFromCart).toHaveBeenCalledTimes(1);
  });

  it("allows paying an existing pending order through the checkout route", async () => {
    const user = userEvent.setup();
    getOrderById
      .mockResolvedValueOnce({
        id: 91,
        totalAmount: 39.99,
        status: "Pending"
      })
      .mockResolvedValueOnce({
        id: 91,
        totalAmount: 39.99,
        status: "Confirmed"
      });
    renderWithAuth(
      <Routes>
        <Route path="/checkout/:orderId" element={<Checkout />} />
      </Routes>,
      { path: "/checkout/91" }
    );

    expect(await screen.findByText("Order total: ₹39.99")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Pay with mock provider" }));

    expect(await screen.findByText("Order #91 is confirmed and the mock payment succeeded.")).toBeInTheDocument();
    expect(createPayment).toHaveBeenCalledWith(91, expect.any(String));
  });
});

describe("orders", () => {
  it("shows empty history and reports a history API failure", async () => {
    getMyOrders.mockResolvedValueOnce([]);
    const { unmount } = renderWithAuth(<Orders />);
    expect(await screen.findByText("You have no orders.")).toBeInTheDocument();
    unmount();

    getMyOrders.mockRejectedValueOnce(new Error("Orders service unavailable."));
    renderWithAuth(<Orders />);
    expect(await screen.findByRole("alert")).toHaveTextContent("Orders service unavailable.");
  });

  it("reports a missing order for an invalid order ID", async () => {
    getOrderById.mockRejectedValue(new Error("Order not found."));
    renderWithAuth(
      <Routes>
        <Route path="/orders/:id" element={<OrderDetails />} />
      </Routes>,
      { path: "/orders/invalid-id" }
    );

    expect(await screen.findByRole("alert")).toHaveTextContent("Order not found.");
    expect(getOrderById).toHaveBeenCalledWith("invalid-id");
  });
});
