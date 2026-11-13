import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "./AuthContext";
import { useAuth } from "../hooks/useAuth";
import Navbar from "../components/Navbar";

function createToken(expiry, claims = {}) {
  const payload = btoa(JSON.stringify({
    name: "Demo Customer",
    exp: expiry,
    ...claims
  }))
    .replace(/=/g, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_");
  return `header.${payload}.signature`;
}

function AuthProbe() {
  const { isLoggedIn, user, login, logout } = useAuth();

  return (
    <div>
      <span>{isLoggedIn ? "Logged in" : "Logged out"}</span>
      <span>{user?.name || "No user"}</span>
      <button onClick={() => login(createToken(Math.floor(Date.now() / 1000) + 60))}>Sign in</button>
      <button onClick={logout}>Sign out</button>
    </div>
  );
}

describe("AuthProvider", () => {
  it("clears expired tokens stored by a previous session", () => {
    localStorage.setItem("token", createToken(Math.floor(Date.now() / 1000) - 1));

    render(<AuthProvider><AuthProbe /></AuthProvider>);

    expect(screen.getByText("Logged out")).toBeInTheDocument();
    expect(localStorage.getItem("token")).toBeNull();
  });

  it("stores and clears a valid token on login and logout", async () => {
    const user = userEvent.setup();
    render(<AuthProvider><AuthProbe /></AuthProvider>);

    await user.click(screen.getByRole("button", { name: "Sign in" }));
    expect(screen.getByText("Logged in")).toBeInTheDocument();
    expect(localStorage.getItem("token")).not.toBeNull();

    await user.click(screen.getByRole("button", { name: "Sign out" }));
    expect(screen.getByText("Logged out")).toBeInTheDocument();
    expect(localStorage.getItem("token")).toBeNull();
  });

  it("shows a JWT unique_name claim in the navigation bar", () => {
    localStorage.setItem(
      "token",
      createToken(Math.floor(Date.now() / 1000) + 60, {
        name: undefined,
        unique_name: "Demo Customer"
      })
    );

    render(
      <AuthProvider>
        <MemoryRouter>
          <Navbar />
        </MemoryRouter>
      </AuthProvider>
    );

    expect(screen.getByText("Demo Customer")).toBeInTheDocument();
  });

  it("logs out automatically when the token expires", () => {
    vi.useFakeTimers();
    const expiry = Math.floor(Date.now() / 1000) + 3;
    localStorage.setItem("token", createToken(expiry));

    render(<AuthProvider><AuthProbe /></AuthProvider>);
    expect(screen.getByText("Logged in")).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(4000);
    });

    expect(screen.getByText("Logged out")).toBeInTheDocument();
    expect(localStorage.getItem("token")).toBeNull();
    vi.useRealTimers();
  });
});
