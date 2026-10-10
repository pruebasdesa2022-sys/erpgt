import type { NextRequest } from "next/server";
import { copiarRespuesta, leerJson, llamarApiJson, origenValido, problema } from "@/lib/servidor/api";

/** Paso anónimo hacia la API; no usa ni modifica cookies. */
export async function POST(solicitud: NextRequest): Promise<Response> {
  if (!origenValido(solicitud)) return problema(403, "ORIGEN_INVALIDO", "Solicitud rechazada.");

  const cuerpo = (await leerJson<Record<string, unknown>>(solicitud)) ?? {};
  return copiarRespuesta(await llamarApiJson(solicitud, "seguridad/autenticacion/restablecer", cuerpo));
}
