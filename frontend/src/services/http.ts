import type { ProblemaApi } from "@/types";

/** Error de la API con los datos del ProblemDetails, para que la pantalla decida cómo mostrarlo. */
export class ErrorApi extends Error {
  readonly estado: number;
  readonly errorCode: string;
  readonly errores: Record<string, string[]>;
  readonly traceId?: string;

  constructor(estado: number, problema: ProblemaApi) {
    super(problema.detail ?? problema.title ?? "No se pudo completar la operación.");
    this.name = "ErrorApi";
    this.estado = estado;
    this.errorCode = problema.errorCode ?? "ERROR";
    this.errores = problema.errors ?? {};
    this.traceId = problema.traceId;
  }
}

type Consulta = Record<string, string | number | boolean | null | undefined>;

interface Opciones {
  metodo?: "GET" | "POST" | "PUT" | "DELETE";
  cuerpo?: unknown;
  consulta?: Consulta;
  /** Ruta completa del BFF (por defecto se antepone /api/bff/). */
  absoluta?: boolean;
}

export function cadenaConsulta(consulta?: Consulta): string {
  const parametros = new URLSearchParams();
  for (const [clave, valor] of Object.entries(consulta ?? {})) {
    if (valor !== undefined && valor !== null && valor !== "") parametros.set(clave, String(valor));
  }
  const texto = parametros.toString();
  return texto ? `?${texto}` : "";
}

export function urlBff(ruta: string, consulta?: Consulta): string {
  return `/api/bff/${ruta}${cadenaConsulta(consulta)}`;
}

/**
 * Único punto de salida del navegador: siempre habla con el BFF de Next, nunca con la API.
 * Ante un 401 la sesión terminó y se vuelve al login.
 */
export async function api<T>(ruta: string, opciones: Opciones = {}): Promise<T> {
  const url = opciones.absoluta ? ruta + cadenaConsulta(opciones.consulta) : urlBff(ruta, opciones.consulta);

  let respuesta: Response;
  try {
    respuesta = await fetch(url, {
      method: opciones.metodo ?? "GET",
      headers: opciones.cuerpo === undefined ? undefined : { "Content-Type": "application/json" },
      body: opciones.cuerpo === undefined ? undefined : JSON.stringify(opciones.cuerpo),
      credentials: "same-origin",
      cache: "no-store",
    });
  } catch {
    throw new ErrorApi(0, { errorCode: "SIN_CONEXION", detail: "No hay conexión con el servidor. Revise su red e intente de nuevo." });
  }

  if (respuesta.ok) {
    if (respuesta.status === 204 || respuesta.status === 202) return undefined as T;
    return (await respuesta.json()) as T;
  }

  let problema: ProblemaApi = {};
  try {
    problema = (await respuesta.json()) as ProblemaApi;
  } catch {
    // La respuesta no traía JSON (por ejemplo, un error del proxy): se usa el mensaje genérico.
  }

  const enPantallaPublica = ["/login", "/recuperar", "/restablecer"].some((r) => window.location.pathname.startsWith(r));
  if (respuesta.status === 401 && !opciones.absoluta && !enPantallaPublica) {
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination -- Recarga completa a propósito: descarta todo el estado del cliente.
    window.location.assign("/login?expirada=1");
  }

  throw new ErrorApi(respuesta.status, problema);
}
