import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { toast } from "sonner";
import { ErrorApi } from "@/services/http";

/**
 * Muestra un error de la API donde corresponde: los de validación (400) bajo su campo si el
 * formulario lo tiene; el resto (403, 409, 422, 500, sin conexión) como notificación.
 */
export function mostrarError<T extends FieldValues>(error: unknown, formulario?: { setError: UseFormSetError<T>; campos: readonly Path<T>[] }): void {
  if (!(error instanceof ErrorApi)) {
    toast.error("Ocurrió un error inesperado.");
    return;
  }

  if (error.estado === 400 && formulario) {
    const sinCampo: string[] = [];
    for (const [campo, mensajes] of Object.entries(error.errores)) {
      if ((formulario.campos as readonly string[]).includes(campo)) {
        formulario.setError(campo as Path<T>, { type: "servidor", message: mensajes.join(" ") });
      } else {
        sinCampo.push(...mensajes);
      }
    }
    if (sinCampo.length > 0) toast.error(sinCampo.join(" "));
    else if (Object.keys(error.errores).length === 0) toast.error(error.message);
    return;
  }

  const detalle = error.estado >= 500 && error.traceId ? `Operación ${error.traceId}` : undefined;
  toast.error(mensajeDeError(error), { description: detalle });
}

export function mensajeDeError(error: unknown): string {
  if (!(error instanceof ErrorApi)) return "Ocurrió un error inesperado.";
  if (error.estado === 400 && Object.keys(error.errores).length > 0) return Object.values(error.errores).flat().join(" ");
  if (error.estado === 403 && error.errorCode === "PERMISO_DENEGADO") return "No tiene permiso para realizar esta acción.";
  return error.message;
}
