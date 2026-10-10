import { describe, expect, it } from "vitest";
import { nitValido, normalizarNit } from "@/lib/nit";

// Los mismos casos que NitTests del backend (dígitos verificadores calculados a mano).
describe("nitValido", () => {
  it.each(["1234567-9", "12345679", " 1234567 - 9 ", "9-4", "5-1", "6-K", "6-k"])("acepta %s", (nit) => {
    expect(nitValido(nit)).toBe(true);
  });

  it.each(["1234567-8", "1234567-K", "9-5", "ABC1234-5", "1", "", "   ", "CF"])("rechaza %j", (nit) => {
    expect(nitValido(nit)).toBe(false);
  });

  it("normaliza guiones, espacios y mayúsculas", () => {
    expect(normalizarNit(" 1234567-k ")).toBe("1234567K");
  });
});
