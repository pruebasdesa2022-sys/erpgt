import type { NextRequest } from "next/server";
import { copiarRespuesta, guardarTokens, leerJson, llamarApiJson, origenValido, problema, type Tokens } from "@/lib/servidor/api";

/** Inicia sesión: los tokens quedan en cookies httpOnly y al navegador solo llega si debe cambiar la contraseña. */
export async function POST(solicitud: NextRequest): Promise<Response> {
  if (!origenValido(solicitud)) return problema(403, "ORIGEN_INVALIDO", "Solicitud rechazada.");

  const cuerpo = await leerJson<{ nombreUsuario?: string; password?: string }>(solicitud);
  const respuestaApi = await llamarApiJson(solicitud, "seguridad/autenticacion/login", {
    nombreUsuario: cuerpo?.nombreUsuario ?? "",
    password: cuerpo?.password ?? "",
  });
  if (!respuestaApi.ok) return copiarRespuesta(respuestaApi);

  const tokens = (await respuestaApi.json()) as Tokens;
  const respuesta = Response.json({ debeCambiarPassword: tokens.debeCambiarPassword }, { headers: { "Cache-Control": "no-store" } });
  guardarTokens(respuesta, tokens);
  return respuesta;
}
