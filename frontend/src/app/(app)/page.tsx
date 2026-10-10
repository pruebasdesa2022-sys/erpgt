"use client";

import Link from "next/link";
import { EncabezadoPagina } from "@/components/comun/Formulario";
import { IconoMenu } from "@/components/layout/Marca";
import { useSesion } from "@/contexts/sesion";

/** Inicio: accesos directos a las pantallas que el usuario tiene disponibles. */
export default function PaginaInicio() {
  const { sesion, menu } = useSesion();
  const pantallas = menu.flatMap((modulo) => modulo.hijos.filter((h) => h.ruta).map((h) => ({ ...h, modulo: modulo.nombre })));

  return (
    <>
      <EncabezadoPagina titulo={`Hola, ${sesion.nombreCompleto}`} descripcion="Elija una opción para comenzar." />
      {pantallas.length === 0 ? (
        <p className="text-sm text-muted-foreground">Aún no tiene opciones asignadas. Contacte al administrador.</p>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {pantallas.map((p) => (
            <Link key={p.id} href={p.ruta!} className="flex items-center gap-3 rounded-xl border bg-card p-4 transition-colors hover:bg-muted/50">
              <IconoMenu nombre={p.icono} className="size-5 text-muted-foreground" />
              <span>
                <span className="block font-medium">{p.nombre}</span>
                <span className="block text-xs text-muted-foreground">{p.modulo}</span>
              </span>
            </Link>
          ))}
        </div>
      )}
    </>
  );
}
