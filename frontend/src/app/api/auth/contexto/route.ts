import type { NextRequest } from "next/server";
import { copiarRespuesta, guardarTokens, leerJson, leerSesion, llamarApiJson, origenValido, problema, type Tokens } from "@/lib/servidor/api";

/** Cambia la empresa y sucursal activas: la API emite tokens nuevos con ese contexto. */
export async function POST(solicitud: NextRequest): Promise<Response> {
  if (!origenValido(solicitud)) return problema(403, "ORIGEN_INVALIDO", "Solicitud rechazada.");

  const { refresh } = await leerSesion();
  if (!refresh) return problema(401, "NO_AUTENTICADO", "Debe iniciar sesión.");

  const cuerpo = await leerJson<{ empresaId?: number; sucursalId?: number }>(solicitud);
  const respuestaApi = await llamarApiJson(solicitud, "seguridad/autenticacion/contexto", {
    refreshToken: refresh,
    empresaId: cuerpo?.empresaId ?? 0,
    sucursalId: cuerpo?.sucursalId ?? 0,
  });
  if (!respuestaApi.ok) return copiarRespuesta(respuestaApi);

  const respuesta = new Response(null, { status: 204 });
  guardarTokens(respuesta, (await respuestaApi.json()) as Tokens);
  return respuesta;
}
