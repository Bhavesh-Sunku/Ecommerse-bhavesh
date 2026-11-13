import { describe, expect, it } from "vitest";
import { formatINR } from "./currency";

describe("formatINR", () => {
  it("formats prices with the Indian rupee symbol and Indian digit grouping", () => {
    expect(formatINR(1999)).toBe("₹1,999");
    expect(formatINR(239.97)).toBe("₹239.97");
    expect(formatINR(null)).toBe("₹0");
  });
});
