"use client";

import type { ReactNode } from "react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Label } from "@/components/ui/label";

/** Etiqueta + control + mensaje de error de un campo de formulario. */
export function Campo({ etiqueta, id, error, ayuda, children }: { etiqueta: string; id: string; error?: string; ayuda?: string; children: ReactNode }) {
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{etiqueta}</Label>
      {children}
      {ayuda && !error && <p className="text-xs text-muted-foreground">{ayuda}</p>}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

/** Confirmación obligatoria antes de una acción destructiva. */
export function DialogoConfirmar({
  abierto,
  titulo,
  descripcion,
  textoConfirmar = "Confirmar",
  ocupado = false,
  onConfirmar,
  onCerrar,
}: {
  abierto: boolean;
  titulo: string;
  descripcion: string;
  textoConfirmar?: string;
  ocupado?: boolean;
  onConfirmar: () => void;
  onCerrar: () => void;
}) {
  return (
    <AlertDialog open={abierto} onOpenChange={(abrir) => !abrir && onCerrar()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{titulo}</AlertDialogTitle>
          <AlertDialogDescription>{descripcion}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={ocupado}>Cancelar</AlertDialogCancel>
          <AlertDialogAction variant="destructive" disabled={ocupado} onClick={onConfirmar}>
            {textoConfirmar}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

/** Encabezado estándar de una pantalla: título, descripción y acciones a la derecha. */
export function EncabezadoPagina({ titulo, descripcion, children }: { titulo: string; descripcion?: string; children?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <h1 className="font-heading text-xl font-semibold">{titulo}</h1>
        {descripcion && <p className="text-sm text-muted-foreground">{descripcion}</p>}
      </div>
      {children && <div className="flex flex-wrap items-center gap-2">{children}</div>}
    </div>
  );
}
