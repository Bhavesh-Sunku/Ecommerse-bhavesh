export function parseJwt(token) {
  try {
    const payload = token.split(".")[1];
    if (!payload) {
      return null;
    }

    const base64 = payload
      .replace(/-/g, "+")
      .replace(/_/g, "/");
    const paddedBase64 = base64.padEnd(
      Math.ceil(base64.length / 4) * 4,
      "="
    );
    const bytes = Uint8Array.from(
      atob(paddedBase64),
      (character) => character.charCodeAt(0)
    );
    const claims = JSON.parse(new TextDecoder().decode(bytes));

    if (
      typeof claims.exp !== "number" ||
      claims.exp <= Date.now() / 1000
    ) {
      return null;
    }

    return claims;
  } catch {
    return null;
  }
}