import { describe, expect, it } from "vitest";
import type { CampoCatalogo } from "@/components/catalogos/config";
import { aPayload, camposDe, esquemaCatalogo, valoresIniciales } from "@/components/catalogos/esquema";

const campos: CampoCatalogo[] = [
  { nombre: "codigo", etiqueta: "Código", tipo: "texto", max: 5, patron: /^[A-Za-z0-9]+$/, mensajePatron: "Solo letras.", mayusculas: true, soloCreacion: true },
  { nombre: "dias", etiqueta: "Días", tipo: "entero", minimo: 0, maximo: 365 },
  { nombre: "tasa", etiqueta: "Tasa", tipo: "decimal", decimales: 2, minimo: 0.01 },
  { nombre: "fecha", etiqueta: "Fecha", tipo: "fecha", opcional: true },
  { nombre: "sucursalId", etiqueta: "Sucursal", tipo: "lista", numerico: true },
  { nombre: "tipo", etiqueta: "Tipo", tipo: "lista", opciones: [{ valor: "A", etiqueta: "A" }] },
  { nombre: "inventario", etiqueta: "Inventario", tipo: "booleano", porDefecto: true },
  { nombre: "detalle", etiqueta: "Detalle", tipo: "texto", opcional: true, max: 10 },
];

const valido = { activo: true, codigo: "ab1", dias: "30", tasa: "7,65", fecha: "", sucursalId: "3", tipo: "A", inventario: true, detalle: "" };

describe("esquemaCatalogo", () => {
  const esquema = esquemaCatalogo(campos);

  it("acepta valores correctos, con coma decimal y fecha vacía opcional", () => {
    expect(esquema.safeParse(valido).success).toBe(true);
  });

  it.each([
    ["código vacío", { codigo: "" }, "codigo"],
    ["código con símbolos", { codigo: "a-b" }, "codigo"],
    ["código demasiado largo", { codigo: "ABCDEF" }, "codigo"],
    ["entero con decimales", { dias: "3.5" }, "dias"],
    ["entero fuera de rango", { dias: "400" }, "dias"],
    ["entero vacío", { dias: "" }, "dias"],
    ["decimal con demasiados decimales", { tasa: "7.123" }, "tasa"],
    ["decimal por debajo del mínimo", { tasa: "0" }, "tasa"],
    ["fecha mal escrita", { fecha: "10/10/2026" }, "fecha"],
    ["lista sin elegir", { sucursalId: "" }, "sucursalId"],
    ["detalle demasiado largo", { detalle: "x".repeat(11) }, "detalle"],
  ])("rechaza %s", (_caso, cambio, campo) => {
    const resultado = esquema.safeParse({ ...valido, ...cambio });
    expect(resultado.success).toBe(false);
    expect(resultado.error?.issues.map((i) => i.path[0])).toContain(campo);
  });
});

describe("aPayload", () => {
  it("convierte números, nulos, mayúsculas e identificadores", () => {
    const cuerpo = aPayload(campos, { ...valido, codigo: " ab1 ", fecha: "2026-10-10", detalle: "" });
    expect(cuerpo).toEqual({
      codigo: "AB1",
      dias: 30,
      tasa: 7.65,
      fecha: "2026-10-10",
      sucursalId: 3,
      tipo: "A",
      inventario: true,
      detalle: null,
    });
  });
});

describe("campos y valores iniciales", () => {
  it("al crear se piden todos salvo los de solo edición", () => {
    const conEdicion: CampoCatalogo[] = [...campos, { nombre: "extra", etiqueta: "Extra", tipo: "texto", soloEdicion: true }];
    expect(camposDe(conEdicion, "crear").map((c) => c.nombre)).not.toContain("extra");
    expect(camposDe(conEdicion, "editar").map((c) => c.nombre)).toContain("extra");
  });

  it("al crear usa los valores por defecto y al editar los de la fila", () => {
    expect(valoresIniciales(campos, null)).toMatchObject({ activo: true, inventario: true, codigo: "", dias: "" });
    const fila = { id: 1, versionFila: 2, activo: false, codigo: "X", dias: 15, tasa: 7.5, fecha: null, sucursalId: 4, tipo: "A", inventario: false, detalle: null };
    expect(valoresIniciales(campos, fila)).toMatchObject({ activo: false, codigo: "X", dias: "15", tasa: "7.5", fecha: "", sucursalId: "4", inventario: false, detalle: "" });
  });
});
