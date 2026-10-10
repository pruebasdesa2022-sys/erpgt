import type { NextRequest } from "next/server";
import {
  borrarTokens,
  copiarRespuesta,
  guardarTokens,
  leerSesion,
  llamarApi,
  origenValido,
  problema,
  renovarTokens,
  type Tokens,
} from "@/lib/servidor/api";

/**
 * Puerta única del navegador hacia la API: reenvía la solicitud con el token de la cookie
 * httpOnly y renueva la sesión de forma transparente cuando el token de acceso venció.
 * No decide permisos: eso lo hace siempre la API.
 */
async function reenviar(solicitud: NextRequest, contexto: RouteContext<"/api/bff/[...ruta]">): Promise<Response> {
  if (!origenValido(solicitud)) return problema(403, "ORIGEN_INVALIDO", "Solicitud rechazada.");

  const { ruta } = await contexto.params;
  const destino = ruta.map(encodeURIComponent).join("/");

  // Login, refresh y logout manejan cookies y tienen sus propias rutas en /api/auth.
  if (destino.startsWith("seguridad/autenticacion/") && destino !== "seguridad/autenticacion/cambiar-password")
    return problema(404, "NO_ENCONTRADO", "Recurso no encontrado.");

  const sesion = await leerSesion();
  if (!sesion.acceso && !sesion.refresh) return problema(401, "NO_AUTENTICADO", "Debe iniciar sesión.");

  const rutaCompleta = destino + solicitud.nextUrl.search;
  const metodo = solicitud.method;
  const cuerpo = ["GET", "HEAD"].includes(metodo) ? null : await solicitud.arrayBuffer();
  const tipoContenido = solicitud.headers.get("content-type");
  const llamar = (acceso: string) => llamarApi(solicitud, rutaCompleta, { metodo, cuerpo, tipoContenido, acceso });

  let renovados: Tokens | null = null;
  let respuestaApi = sesion.acceso ? await llamar(sesion.acceso) : null;

  if ((!respuestaApi || respuestaApi.status === 401) && sesion.refresh) {
    renovados = await renovarTokens(solicitud, sesion.refresh);
    if (renovados) respuestaApi = await llamar(renovados.accessToken);
  }

  if (!respuestaApi || respuestaApi.status === 401) {
    const respuesta = problema(401, "SESION_INVALIDA", "La sesión expiró. Inicie sesión de nuevo.");
    borrarTokens(respuesta);
    return respuesta;
  }

  const respuesta = await copiarRespuesta(respuestaApi);
  if (renovados) guardarTokens(respuesta, renovados);
  return respuesta;
}

export { reenviar as GET, reenviar as POST, reenviar as PUT, reenviar as PATCH, reenviar as DELETE };
