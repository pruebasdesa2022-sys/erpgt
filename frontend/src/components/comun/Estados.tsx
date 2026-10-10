import { CircleAlert, Inbox, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { mensajeDeError } from "@/lib/errores";

/** Los tres estados que toda pantalla debe cubrir además del contenido: carga, vacío y error. */

export function EstadoCarga({ filas = 5 }: { filas?: number }) {
  return (
    <div className="space-y-2 p-4" role="status" aria-label="Cargando">
      {Array.from({ length: filas }, (_, i) => (
        <Skeleton key={i} className="h-8 w-full" />
      ))}
    </div>
  );
}

function Mensaje({ icono: Icono, titulo, detalle, children }: { icono: LucideIcon; titulo: string; detalle?: string; children?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-2 px-4 py-10 text-center">
      <Icono className="size-8 text-muted-foreground" aria-hidden />
      <p className="font-medium">{titulo}</p>
      {detalle && <p className="max-w-md text-sm text-muted-foreground">{detalle}</p>}
      {children}
    </div>
  );
}

export function EstadoVacio({ titulo = "No hay registros", detalle }: { titulo?: string; detalle?: string }) {
  return <Mensaje icono={Inbox} titulo={titulo} detalle={detalle} />;
}

export function EstadoError({ error, onReintentar }: { error: unknown; onReintentar?: () => void }) {
  return (
    <Mensaje icono={CircleAlert} titulo="No se pudo cargar la información" detalle={mensajeDeError(error)}>
      {onReintentar && (
        <Button variant="outline" size="sm" onClick={onReintentar}>
          Reintentar
        </Button>
      )}
    </Mensaje>
  );
}
