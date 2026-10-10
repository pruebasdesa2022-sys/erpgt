"use client";

import { Search, ShieldOff } from "lucide-react";
import type { ReactNode } from "react";
import { Input } from "@/components/ui/input";
import { SelectNativo } from "@/components/ui/select-nativo";

/** Barra superior de un listado: búsqueda a la izquierda y filtros a la derecha. */
export function Barra({
  texto,
  onTexto,
  marcador,
  ocultarBusqueda = false,
  children,
}: {
  texto: string;
  onTexto: (texto: string) => void;
  marcador: string;
  /** Para listados que no admiten búsqueda de texto (solo filtros). */
  ocultarBusqueda?: boolean;
  children?: ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2 border-b p-3">
      {!ocultarBusqueda && (
        <div className="relative min-w-48 flex-1 sm:max-w-xs">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
          <Input type="search" className="pl-8" placeholder={marcador} aria-label={marcador} value={texto} onChange={(e) => onTexto(e.target.value)} />
        </div>
      )}
      {children}
    </div>
  );
}

export type OpcionActivo = "todas" | "activas" | "inactivas";

export function valorFiltroActivo(opcion: OpcionActivo): boolean | undefined {
  return opcion === "todas" ? undefined : opcion === "activas";
}

export function FiltroActivo({ valor, onCambio, etiquetas }: { valor: OpcionActivo; onCambio: (valor: OpcionActivo) => void; etiquetas: Record<OpcionActivo, string> }) {
  return (
    <SelectNativo aria-label="Filtrar por estado" className="w-auto" value={valor} onChange={(e) => onCambio(e.target.value as OpcionActivo)}>
      {(Object.keys(etiquetas) as OpcionActivo[]).map((opcion) => (
        <option key={opcion} value={opcion}>
          {etiquetas[opcion]}
        </option>
      ))}
    </SelectNativo>
  );
}

/** Se muestra cuando alguien abre por URL una pantalla para la que no tiene permiso. */
export function SinPermiso() {
  return (
    <div className="flex flex-col items-center gap-2 px-4 py-16 text-center">
      <ShieldOff className="size-8 text-muted-foreground" aria-hidden />
      <p className="font-medium">No tiene acceso a esta pantalla</p>
      <p className="max-w-md text-sm text-muted-foreground">Si necesita usarla, solicite el permiso al administrador.</p>
    </div>
  );
}
