import { describe, expect, it } from "vitest";
import { parseJwt } from "./jwt";

function createToken(claims) {
  const payload = new TextEncoder().encode(JSON.stringify(claims));
  const base64 = btoa(String.fromCharCode(...payload))
    .replace(/=/g, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_");
  return `header.${base64}.signature`;
}

describe("parseJwt", () => {
  it("parses valid UTF-8 claims while rejecting expired or malformed tokens", () => {
    const token = createToken({
      name: "Bhavesh",
      exp: Math.floor(Date.now() / 1000) + 60
    });

    expect(parseJwt(token)?.name).toBe("Bhavesh");
    expect(parseJwt(createToken({ exp: Math.floor(Date.now() / 1000) - 1 }))).toBeNull();
    expect(parseJwt("not-a-jwt")).toBeNull();
  });
});
