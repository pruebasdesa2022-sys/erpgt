import "server-only";

import { cookies } from "next/headers";
import type { NextRequest } from "next/server";

/**
 * Lado servidor del BFF. El navegador nunca ve los tokens: viven en cookies httpOnly
 * y solo este módulo los lee para hablar con la API interna.
 */

const URL_API = (process.env.API_URL ?? "http://localhost:5080").replace(/\/$/, "");
const COOKIE_ACCESO = "erp_acceso";
export const COOKIE_REFRESH = "erp_refresh";

export interface Tokens {
  accessToken: string;
  accessExpiraEn: string;
  refreshToken: string;
  refreshExpiraEn: string;
  debeCambiarPassword: boolean;
}

export interface Sesion {
  acceso?: string;
  refresh?: string;
}

export async function leerSesion(): Promise<Sesion> {
  const almacen = await cookies();
  return { acceso: almacen.get(COOKIE_ACCESO)?.value, refresh: almacen.get(COOKIE_REFRESH)?.value };
}

const opcionesCookie = {
  httpOnly: true,
  // En desarrollo se sirve por http://localhost; en producción la cookie exige HTTPS.
  secure: process.env.NODE_ENV === "production",
  sameSite: "strict" as const,
  path: "/",
};

export function guardarTokens(respuesta: Response, tokens: Tokens): void {
  const ahora = Date.now();
  agregarCookie(respuesta, COOKIE_ACCESO, tokens.accessToken, new Date(tokens.accessExpiraEn).getTime() - ahora);
  agregarCookie(respuesta, COOKIE_REFRESH, tokens.refreshToken, new Date(tokens.refreshExpiraEn).getTime() - ahora);
}

export function borrarTokens(respuesta: Response): void {
  agregarCookie(respuesta, COOKIE_ACCESO, "", 0);
  agregarCookie(respuesta, COOKIE_REFRESH, "", 0);
}

function agregarCookie(respuesta: Response, nombre: string, valor: string, milisegundos: number): void {
  const partes = [
    `${nombre}=${encodeURIComponent(valor)}`,
    `Path=${opcionesCookie.path}`,
    `Max-Age=${Math.max(0, Math.floor(milisegundos / 1000))}`,
    "HttpOnly",
    "SameSite=Strict",
  ];
  if (opcionesCookie.secure) partes.push("Secure");
  respuesta.headers.append("Set-Cookie", partes.join("; "));
}

/** Cabeceras que identifican al cliente real ante la API (IP, navegador) y enlazan los logs. */
function cabecerasDeCliente(solicitud: NextRequest): Headers {
  const cabeceras = new Headers();
  const ip = solicitud.headers.get("x-forwarded-for")?.split(",")[0]?.trim();
  if (ip) cabeceras.set("X-Forwarded-For", ip);
  const agente = solicitud.headers.get("user-agent");
  if (agente) cabeceras.set("User-Agent", agente);
  cabeceras.set("X-Correlation-Id", crypto.randomUUID().replaceAll("-", ""));
  return cabeceras;
}

export interface OpcionesLlamada {
  metodo?: string;
  cuerpo?: BodyInit | null;
  tipoContenido?: string | null;
  acceso?: string;
}

/** Llama a la API interna. `ruta` es relativa a `/api/v1/` y puede incluir la cadena de consulta. */
export function llamarApi(solicitud: NextRequest, ruta: string, opciones: OpcionesLlamada = {}): Promise<Response> {
  const cabeceras = cabecerasDeCliente(solicitud);
  if (opciones.acceso) cabeceras.set("Authorization", `Bearer ${opciones.acceso}`);
  if (opciones.tipoContenido) cabeceras.set("Content-Type", opciones.tipoContenido);

  return fetch(`${URL_API}/api/v1/${ruta}`, {
    method: opciones.metodo ?? "GET",
    headers: cabeceras,
    body: opciones.cuerpo ?? undefined,
    cache: "no-store",
    redirect: "manual",
  });
}

export function llamarApiJson(solicitud: NextRequest, ruta: string, cuerpo: unknown, acceso?: string): Promise<Response> {
  return llamarApi(solicitud, ruta, { metodo: "POST", cuerpo: JSON.stringify(cuerpo), tipoContenido: "application/json", acceso });
}

/**
 * Renovación con un solo vuelo: varias solicitudes simultáneas con el mismo refresh token
 * comparten una única rotación. El resultado se conserva unos segundos porque el navegador
 * puede enviar todavía la cookie anterior; presentarla dos veces a la API cerraría la sesión
 * (la API trata la reutilización de un refresh token como robo).
 */
const renovaciones = new Map<string, Promise<Tokens | null>>();
const GRACIA_MS = 15_000;

export function renovarTokens(solicitud: NextRequest, refresh: string): Promise<Tokens | null> {
  const enCurso = renovaciones.get(refresh);
  if (enCurso) return enCurso;

  const promesa = (async () => {
    try {
      const respuesta = await llamarApiJson(solicitud, "seguridad/autenticacion/refresh", { refreshToken: refresh });
      return respuesta.ok ? ((await respuesta.json()) as Tokens) : null;
    } catch {
      return null;
    }
  })();

  renovaciones.set(refresh, promesa);
  void promesa.finally(() => setTimeout(() => renovaciones.delete(refresh), GRACIA_MS));
  return promesa;
}

/**
 * Defensa CSRF además de SameSite=Strict: toda solicitud que modifica debe venir de una
 * página de esta misma aplicación.
 */
export function origenValido(solicitud: NextRequest): boolean {
  if (["GET", "HEAD", "OPTIONS"].includes(solicitud.method)) return true;

  const origen = solicitud.headers.get("origin");
  if (!origen) return false;

  const anfitrion = solicitud.headers.get("x-forwarded-host") ?? solicitud.headers.get("host");
  try {
    return new URL(origen).host === anfitrion;
  } catch {
    return false;
  }
}

/** Respuesta de error con la misma forma (ProblemDetails) que usa la API. */
export function problema(estado: number, errorCode: string, detalle: string): Response {
  return Response.json(
    { status: estado, title: detalle, detail: detalle, errorCode },
    { status: estado, headers: { "Content-Type": "application/problem+json", "Cache-Control": "no-store" } },
  );
}

/** Copia la respuesta de la API hacia el navegador sin exponer cabeceras internas. */
export async function copiarRespuesta(respuestaApi: Response): Promise<Response> {
  const cabeceras = new Headers({ "Cache-Control": "no-store" });
  for (const nombre of ["content-type", "content-disposition", "x-correlation-id"]) {
    const valor = respuestaApi.headers.get(nombre);
    if (valor) cabeceras.set(nombre, valor);
  }
  const sinCuerpo = respuestaApi.status === 204 || respuestaApi.status === 304;
  return new Response(sinCuerpo ? null : await respuestaApi.arrayBuffer(), { status: respuestaApi.status, headers: cabeceras });
}

export async function leerJson<T>(solicitud: NextRequest): Promise<T | null> {
  try {
    return (await solicitud.json()) as T;
  } catch {
    return null;
  }
}
