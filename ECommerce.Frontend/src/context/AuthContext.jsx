import { useEffect, useState } from "react";
import { AuthContext } from "./AuthContextValue";
import { parseJwt } from "../utils/jwt";

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => {
    const storedToken = localStorage.getItem("token");
    if (storedToken && !parseJwt(storedToken)) {
      localStorage.removeItem("token");
      return null;
    }

    return storedToken;
  });

  const user = token
    ? parseJwt(token)
    : null;
  const expiresAt = user ? user.exp * 1000 : null;

  function login(newToken) {
    if (!parseJwt(newToken)) {
      logout();
      throw new Error("The login response contained an invalid or expired token.");
    }

    localStorage.setItem(
      "token",
      newToken
    );

    setToken(newToken);
  }

  function logout() {
    localStorage.removeItem("token");
    setToken(null);
  }

  useEffect(() => {
    if (!token || !expiresAt) {
      return undefined;
    }

    let timeoutId;
    function expireSession() {
      const remaining = expiresAt - Date.now();
      if (remaining <= 0) {
        logout();
        return;
      }

      timeoutId = setTimeout(
        expireSession,
        Math.min(remaining, 2147483647)
      );
    }

    expireSession();
    return () => clearTimeout(timeoutId);
  }, [token, expiresAt]);

  return (
    <AuthContext.Provider
      value={{
        token,
        user,
        isLoggedIn: !!token,
        login,
        logout
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}