"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { useOpciones } from "@/components/catalogos/useOpciones";
import { EstadoCarga, EstadoError } from "@/components/comun/Estados";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { mostrarError } from "@/lib/errores";
import { servicioAccesosOperativos } from "@/services";
import type { UsuarioLista } from "@/types";

/**
 * Bodegas y cajas a las que accede un usuario. La API exige que pertenezcan a una sucursal a la
 * que el usuario ya tiene acceso; aquí solo se ofrecen las activas.
 */
export function DialogoAccesosOperativos({ usuario, onCerrar }: { usuario: UsuarioLista; onCerrar: () => void }) {
  const actuales = useQuery({
    queryKey: ["usuarios", "bodegas-cajas", usuario.id],
    queryFn: () => servicioAccesosOperativos.obtener(usuario.id),
    gcTime: 0,
  });
  const bodegas = useOpciones("bodegas");
  const cajas = useOpciones("cajas");
  const [seleccion, setSeleccion] = useState<{ bodegaIds: number[]; cajaIds: number[] } | null>(null);

  const elegido = seleccion ?? actuales.data;

  const guardado = useMutation({
    mutationFn: () => servicioAccesosOperativos.guardar(usuario.id, elegido!),
    onSuccess: () => {
      toast.success("Accesos guardados.");
      onCerrar();
    },
    onError: (error) => mostrarError(error),
  });

  const alternar = (tipo: "bodegaIds" | "cajaIds", id: number) => {
    if (!elegido) return;
    const lista = elegido[tipo];
    setSeleccion({ ...elegido, [tipo]: lista.includes(id) ? lista.filter((x) => x !== id) : [...lista, id] });
  };

  const error = actuales.error ?? bodegas.error ?? cajas.error;
  const cargando = !elegido || !bodegas.data || !cajas.data;

  return (
    <Dialog open onOpenChange={(abierto) => !abierto && !guardado.isPending && onCerrar()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Bodegas y cajas de {usuario.nombreUsuario}</DialogTitle>
          <DialogDescription>
            Solo se admiten las de sucursales a las que el usuario tiene acceso. La primera de cada lista queda como predeterminada.
          </DialogDescription>
        </DialogHeader>

        {error ? (
          <EstadoError error={error} />
        ) : cargando ? (
          <EstadoCarga />
        ) : (
          <div className="grid gap-4 sm:grid-cols-2">
            <fieldset className="grid content-start gap-2">
              <legend className="mb-1 text-sm font-medium">Bodegas</legend>
              {bodegas.data.length === 0 && <p className="text-sm text-muted-foreground">No hay bodegas activas.</p>}
              {bodegas.data.map((o) => (
                <div key={o.valor} className="flex items-center gap-2">
                  <Checkbox id={`bodega-${o.valor}`} checked={elegido.bodegaIds.includes(Number(o.valor))} onCheckedChange={() => alternar("bodegaIds", Number(o.valor))} />
                  <Label htmlFor={`bodega-${o.valor}`} className="font-normal">
                    {o.etiqueta}
                  </Label>
                </div>
              ))}
            </fieldset>
            <fieldset className="grid content-start gap-2">
              <legend className="mb-1 text-sm font-medium">Cajas</legend>
              {cajas.data.length === 0 && <p className="text-sm text-muted-foreground">No hay cajas activas.</p>}
              {cajas.data.map((o) => (
                <div key={o.valor} className="flex items-center gap-2">
                  <Checkbox id={`caja-${o.valor}`} checked={elegido.cajaIds.includes(Number(o.valor))} onCheckedChange={() => alternar("cajaIds", Number(o.valor))} />
                  <Label htmlFor={`caja-${o.valor}`} className="font-normal">
                    {o.etiqueta}
                  </Label>
                </div>
              ))}
            </fieldset>
          </div>
        )}

        <DialogFooter>
          <Button type="button" variant="outline" disabled={guardado.isPending} onClick={onCerrar}>
            Cancelar
          </Button>
          <Button type="button" disabled={guardado.isPending || cargando || !!error} onClick={() => guardado.mutate()}>
            {guardado.isPending ? "Guardando…" : "Guardar"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
