import { describe, expect, it } from "vitest";
import * as configuraciones from "@/components/catalogos/configuraciones";
import type { ConfigCatalogo } from "@/components/catalogos/config";

const todas = Object.entries(configuraciones) as [string, ConfigCatalogo][];

describe("configuraciones de catálogos", () => {
  it("hay una por cada pantalla de catálogo del menú (20)", () => {
    expect(todas).toHaveLength(20);
  });

  it("claves y recursos no se repiten", () => {
    expect(new Set(todas.map(([, c]) => c.clave)).size).toBe(todas.length);
    expect(new Set(todas.map(([, c]) => c.recurso)).size).toBe(todas.length);
  });

  it.each(todas)("%s: permisos y recursos con el formato de la API", (_nombre, c) => {
    expect(c.permiso).toMatch(/^[A-Z]+_[A-Z]+$/);
    expect(c.recurso).toMatch(/^(organizacion|catalogos)\/[a-z]+(-[a-z]+)*$/);
  });

  it.each(todas)("%s: campos con nombres únicos y listas con opciones o fuente", (_nombre, c) => {
    const nombres = c.campos.map((campo) => campo.nombre);
    expect(new Set(nombres).size).toBe(nombres.length);
    for (const campo of c.campos.filter((x) => x.tipo === "lista")) {
      expect(campo.opciones !== undefined || campo.fuente !== undefined, `${c.clave}.${campo.nombre}`).toBe(true);
    }
  });

  it.each(todas)("%s: el orden inicial es una columna ordenable", (_nombre, c) => {
    const campo = c.ordenInicial.replace(/^-/, "");
    const ordenables = [...c.columnas.filter((col) => col.ordenable).map((col) => col.id), "activo"];
    expect(ordenables, `${c.clave} ordena por ${campo}`).toContain(campo);
  });

  it("los catálogos sin alta o sin baja no exponen esas acciones", () => {
    expect(configuraciones.tiposDocumento.sinCrear).toBe(true);
    expect(configuraciones.tiposCambio.sinBaja).toBe(true);
  });
});
