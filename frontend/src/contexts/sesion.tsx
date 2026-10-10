"use client";

import { useQuery } from "@tanstack/react-query";
import { createContext, useContext, useMemo, type ReactNode } from "react";
import { servicioSesion } from "@/services";
import type { MenuItem, Sesion } from "@/types";

interface ContextoSesion {
  sesion: Sesion;
  menu: MenuItem[];
  permisos: ReadonlySet<string>;
}

const Contexto = createContext<ContextoSesion | null>(null);

export const CLAVE_SESION = ["sesion"] as const;

/**
 * Carga la sesión, el menú y los permisos del usuario desde la API. El menú y los botones
 * se construyen con estos datos, pero son solo presentación: la API vuelve a validar todo.
 */
export function useCargaSesion() {
  return useQuery({
    queryKey: CLAVE_SESION,
    queryFn: async () => {
      const [sesion, menu, permisos] = await Promise.all([servicioSesion.obtener(), servicioSesion.menu(), servicioSesion.misPermisos()]);
      return { sesion, menu, permisos };
    },
    staleTime: 60_000,
    retry: false,
  });
}

export function ProveedorSesion({ datos, children }: { datos: { sesion: Sesion; menu: MenuItem[]; permisos: string[] }; children: ReactNode }) {
  const valor = useMemo(() => ({ sesion: datos.sesion, menu: datos.menu, permisos: new Set(datos.permisos) }), [datos]);
  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useSesion(): ContextoSesion {
  const contexto = useContext(Contexto);
  if (!contexto) throw new Error("useSesion debe usarse dentro de ProveedorSesion.");
  return contexto;
}

/** Indica si el usuario puede ejercer el permiso. Solo decide qué se muestra; no protege nada. */
export function usePermiso(permiso: string): boolean {
  return useSesion().permisos.has(permiso);
}
