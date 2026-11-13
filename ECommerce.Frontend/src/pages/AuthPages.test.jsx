import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../context/AuthContext";
import { loginUser, registerUser } from "../services/authService";
import Login from "./Login";
import Register from "./Register";

vi.mock("../services/authService", () => ({
  loginUser: vi.fn(),
  registerUser: vi.fn()
}));

function createToken() {
  const payload = btoa(JSON.stringify({
    name: "Demo Customer",
    role: "Customer",
    exp: Math.floor(Date.now() / 1000) + 3600
  }))
    .replace(/=/g, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_");
  return `header.${payload}.signature`;
}

function renderAuthPage(path, page) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/login" element={page === "login" ? <Login /> : <Register />} />
          <Route path="/register" element={page === "register" ? <Register /> : <Login />} />
          <Route path="/products" element={<h1>Products destination</h1>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("registration form validation", () => {
  it("requires all fields and rejects an invalid email before calling the API", async () => {
    const user = userEvent.setup();
    renderAuthPage("/register", "register");

    const fields = [
      screen.getByLabelText("First name"),
      screen.getByLabelText("Last name"),
      screen.getByLabelText("Email"),
      screen.getByLabelText("Password"),
      screen.getByLabelText("Confirm password")
    ];
    fields.forEach((field) => expect(field).toBeRequired());
    await user.click(screen.getByRole("button", { name: "Create Account" }));
    expect(registerUser).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText("First name"), "Demo");
    await user.type(screen.getByLabelText("Last name"), "Customer");
    await user.type(screen.getByLabelText("Email"), "invalid-email");
    await user.type(screen.getByLabelText("Password"), "password123");
    await user.type(screen.getByLabelText("Confirm password"), "password123");
    expect(screen.getByLabelText("Email").checkValidity()).toBe(false);
    await user.click(screen.getByRole("button", { name: "Create Account" }));
    expect(registerUser).not.toHaveBeenCalled();
  });

  it("rejects a password mismatch without submitting", async () => {
    const user = userEvent.setup();
    renderAuthPage("/register", "register");

    await user.type(screen.getByLabelText("First name"), "Demo");
    await user.type(screen.getByLabelText("Last name"), "Customer");
    await user.type(screen.getByLabelText("Email"), "customer@example.test");
    await user.type(screen.getByLabelText("Password"), "password123");
    await user.type(screen.getByLabelText("Confirm password"), "different123");
    await user.click(screen.getByRole("button", { name: "Create Account" }));

    expect(await screen.findByText("Passwords do not match.")).toBeInTheDocument();
    expect(registerUser).not.toHaveBeenCalled();
  });
});

describe("login form", () => {
  it("requires credentials and displays invalid-login errors", async () => {
    const user = userEvent.setup();
    loginUser.mockRejectedValue(new Error("Invalid email or password."));
    renderAuthPage("/login", "login");

    expect(screen.getByLabelText("Username / Email")).toBeRequired();
    expect(screen.getByLabelText("Password")).toBeRequired();
    await user.click(screen.getByRole("button", { name: "Sign In" }));
    expect(loginUser).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText("Username / Email"), "customer@example.test");
    await user.type(screen.getByLabelText("Password"), "wrong-password");
    await user.click(screen.getByRole("button", { name: "Sign In" }));

    expect(await screen.findByText("Invalid email or password.")).toBeInTheDocument();
  });

  it("logs in successfully and navigates to the product catalog", async () => {
    const user = userEvent.setup();
    const token = createToken();
    loginUser.mockResolvedValue({ token, role: "Customer" });
    renderAuthPage("/login", "login");

    await user.type(screen.getByLabelText("Username / Email"), "customer@example.test");
    await user.type(screen.getByLabelText("Password"), "correct-password");
    await user.click(screen.getByRole("button", { name: "Sign In" }));

    expect(await screen.findByRole("heading", { name: "Products destination" })).toBeInTheDocument();
    expect(localStorage.getItem("token")).toBe(token);
    expect(loginUser).toHaveBeenCalledWith({
      email: "customer@example.test",
      password: "correct-password"
    });
  });
});
