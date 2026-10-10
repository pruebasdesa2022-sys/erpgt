import type { NextRequest } from "next/server";
import { borrarTokens, leerSesion, llamarApiJson, origenValido, problema } from "@/lib/servidor/api";

/** Cierra la sesión en la API (revoca el refresh token) y borra las cookies aunque la API no responda. */
export async function POST(solicitud: NextRequest): Promise<Response> {
  if (!origenValido(solicitud)) return problema(403, "ORIGEN_INVALIDO", "Solicitud rechazada.");

  const { refresh } = await leerSesion();
  if (refresh) {
    try {
      await llamarApiJson(solicitud, "seguridad/autenticacion/logout", { refreshToken: refresh });
    } catch {
      // Sin conexión con la API la sesión local igual se cierra; el token vence por sí solo.
    }
  }

  const respuesta = new Response(null, { status: 204 });
  borrarTokens(respuesta);
  return respuesta;
}
