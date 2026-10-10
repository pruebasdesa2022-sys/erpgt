import { describe, expect, it } from "vitest";
import { esquemaSucursal } from "@/components/organizacion/FormularioSucursal";

const valida = { codigo: "SUC02", nombre: "Sucursal Norte", direccion: "", telefono: "", correo: "", activo: true };

describe("esquemaSucursal", () => {
  it("acepta una sucursal con solo código y nombre", () => {
    expect(esquemaSucursal.safeParse(valida).success).toBe(true);
  });

  it("recorta los espacios sobrantes", () => {
    const resultado = esquemaSucursal.parse({ ...valida, codigo: "  SUC02 ", nombre: "  Sucursal Norte  " });
    expect(resultado.codigo).toBe("SUC02");
    expect(resultado.nombre).toBe("Sucursal Norte");
  });

  it.each([
    ["código vacío", { codigo: "" }, "codigo"],
    ["código con espacios", { codigo: "SUC 02" }, "codigo"],
    ["código de más de 20 caracteres", { codigo: "A".repeat(21) }, "codigo"],
    ["nombre vacío", { nombre: "   " }, "nombre"],
    ["nombre de más de 150 caracteres", { nombre: "A".repeat(151) }, "nombre"],
    ["correo mal escrito", { correo: "no-es-correo" }, "correo"],
    ["teléfono demasiado largo", { telefono: "1".repeat(31) }, "telefono"],
  ])("rechaza %s", (_caso, cambio, campo) => {
    const resultado = esquemaSucursal.safeParse({ ...valida, ...cambio });
    expect(resultado.success).toBe(false);
    expect(resultado.error?.issues.map((i) => i.path[0])).toContain(campo);
  });

  it("acepta un correo válido", () => {
    expect(esquemaSucursal.safeParse({ ...valida, correo: "norte@empresa.com.gt" }).success).toBe(true);
  });
});
