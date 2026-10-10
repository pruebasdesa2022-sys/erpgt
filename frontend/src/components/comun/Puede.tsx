"use client";

import type { ReactNode } from "react";
import { usePermiso } from "@/contexts/sesion";

/** Muestra su contenido solo si el usuario tiene el permiso. La API valida de nuevo al ejecutar la acción. */
export function Puede({ permiso, children, alternativa = null }: { permiso: string; children: ReactNode; alternativa?: ReactNode }) {
  return usePermiso(permiso) ? <>{children}</> : <>{alternativa}</>;
}
