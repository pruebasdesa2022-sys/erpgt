import { afterEach, describe, expect, it, vi } from "vitest";
import { api, cadenaConsulta, ErrorApi } from "@/services/http";

function responder(estado: number, cuerpo?: unknown) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), { status: estado })),
  );
}

describe("api", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("llama al BFF, nunca a la API directamente", async () => {
    responder(200, { items: [] });
    await api("organizacion/sucursales", { consulta: { q: "norte", page: 2, activo: undefined } });

    expect(fetch).toHaveBeenCalledWith("/api/bff/organizacion/sucursales?q=norte&page=2", expect.objectContaining({ method: "GET" }));
  });

  it("devuelve el cuerpo en las respuestas correctas", async () => {
    responder(200, { id: 7 });
    await expect(api<{ id: number }>("organizacion/sucursales/7")).resolves.toEqual({ id: 7 });
  });

  it("no intenta leer cuerpo en un 204", async () => {
    responder(204);
    await expect(api("organizacion/sucursales/7", { metodo: "DELETE" })).resolves.toBeUndefined();
  });

  it("convierte un ProblemDetails en ErrorApi con sus errores por campo", async () => {
    responder(400, { errorCode: "VALIDACION", detail: "Datos inválidos", traceId: "abc", errors: { nombre: ["Obligatorio"] } });

    const error = await api("organizacion/sucursales", { metodo: "POST", cuerpo: {} }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ErrorApi);
    expect(error).toMatchObject({ estado: 400, errorCode: "VALIDACION", traceId: "abc", errores: { nombre: ["Obligatorio"] } });
  });

  it.each([409, 422, 403])("conserva el código de estado %i para que la pantalla lo maneje", async (estado) => {
    responder(estado, { errorCode: "X", detail: "Mensaje del servidor" });
    await expect(api("organizacion/sucursales/1", { metodo: "PUT", cuerpo: {} })).rejects.toMatchObject({ estado, message: "Mensaje del servidor" });
  });

  it("informa la falta de conexión sin romper la pantalla", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    await expect(api("seguridad/menu")).rejects.toMatchObject({ estado: 0, errorCode: "SIN_CONEXION" });
  });
});

describe("cadenaConsulta", () => {
  it("omite los valores vacíos", () => {
    expect(cadenaConsulta({ q: "", page: 1, sort: undefined, activo: null })).toBe("?page=1");
  });

  it("conserva false, que es un filtro válido", () => {
    expect(cadenaConsulta({ activo: false })).toBe("?activo=false");
  });

  it("devuelve vacío cuando no hay parámetros", () => {
    expect(cadenaConsulta()).toBe("");
  });
});
