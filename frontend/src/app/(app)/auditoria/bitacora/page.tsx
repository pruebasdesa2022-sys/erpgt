"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { Eye } from "lucide-react";
import { useMemo, useState } from "react";
import { EncabezadoPagina } from "@/components/comun/Formulario";
import { Barra, SinPermiso } from "@/components/comun/Listado";
import { TablaServidor } from "@/components/comun/TablaServidor";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { usePermiso } from "@/contexts/sesion";
import { useListado } from "@/hooks/useListado";
import { finDelDiaUtc, formatoFechaHora, inicioDelDiaUtc } from "@/lib/formato";
import { servicioBitacora } from "@/services";
import type { Bitacora } from "@/types";

/** Bitácora de operaciones de la empresa activa. Solo consulta: los registros son inmutables. */
export default function PaginaBitacora() {
  const puedeVer = usePermiso("AUDITORIA_BITACORA_VER");
  const listado = useListado("-fechaUtc");
  const [desde, setDesde] = useState("");
  const [hasta, setHasta] = useState("");
  const [detalle, setDetalle] = useState<Bitacora | null>(null);

  const consulta = { ...listado.consulta, desde: inicioDelDiaUtc(desde), hasta: finDelDiaUtc(hasta) };
  const bitacora = useQuery({
    queryKey: ["bitacora", consulta],
    queryFn: () => servicioBitacora.listar(consulta),
    placeholderData: keepPreviousData,
    enabled: puedeVer,
  });

  const columnas = useMemo<ColumnDef<Bitacora>[]>(
    () => [
      { id: "fechaUtc", header: "Fecha", enableSorting: true, cell: ({ row }) => formatoFechaHora(row.original.fechaUtc) },
      { id: "usuario", header: "Usuario", cell: ({ row }) => row.original.nombreUsuario ?? "Sistema" },
      { id: "modulo", header: "Módulo", accessorKey: "modulo" },
      {
        id: "entidad",
        header: "Registro",
        enableSorting: true,
        cell: ({ row }) => `${row.original.entidad}${row.original.entidadId ? ` #${row.original.entidadId}` : ""}`,
      },
      { id: "accion", header: "Acción", accessorKey: "accion", enableSorting: true },
      {
        id: "resultado",
        header: "Resultado",
        cell: ({ row }) => <Badge variant={row.original.resultado === "EXITO" ? "secondary" : "destructive"}>{row.original.resultado === "EXITO" ? "Éxito" : "Fallo"}</Badge>,
      },
      {
        id: "acciones",
        header: () => <span className="sr-only">Detalle</span>,
        cell: ({ row }) => (
          <div className="flex justify-end">
            <Button variant="ghost" size="icon-sm" aria-label="Ver detalle" onClick={() => setDetalle(row.original)}>
              <Eye />
            </Button>
          </div>
        ),
      },
    ],
    [],
  );

  if (!puedeVer) return <SinPermiso />;

  const alCambiarFecha = (asignar: (valor: string) => void) => (evento: React.ChangeEvent<HTMLInputElement>) => {
    asignar(evento.target.value);
    listado.setPage(1);
  };

  return (
    <>
      <EncabezadoPagina titulo="Bitácora" descripcion="Registro de las operaciones realizadas en la empresa activa." />

      <div className="rounded-xl border bg-card">
        <Barra texto={listado.texto} onTexto={listado.setTexto} marcador="Buscar por registro, mensaje u operación">
          <Input type="date" className="w-auto" aria-label="Desde" value={desde} max={hasta || undefined} onChange={alCambiarFecha(setDesde)} />
          <Input type="date" className="w-auto" aria-label="Hasta" value={hasta} min={desde || undefined} onChange={alCambiarFecha(setHasta)} />
        </Barra>
        <TablaServidor
          columnas={columnas}
          datos={bitacora.data}
          cargando={bitacora.isFetching}
          error={bitacora.error}
          onReintentar={() => void bitacora.refetch()}
          orden={listado.sort}
          onOrden={listado.setSort}
          onPagina={listado.setPage}
          vacio="No hay registros en el período"
        />
      </div>

      {detalle && <DialogoDetalle registro={detalle} onCerrar={() => setDetalle(null)} />}
    </>
  );
}

function Json({ titulo, valor }: { titulo: string; valor: string | null }) {
  if (!valor) return null;

  let texto = valor;
  try {
    texto = JSON.stringify(JSON.parse(valor), null, 2);
  } catch {
    // Si no es JSON válido se muestra tal cual.
  }

  return (
    <div className="grid gap-1">
      <p className="text-xs font-medium text-muted-foreground">{titulo}</p>
      <pre className="max-h-48 overflow-auto rounded-md bg-muted p-2 font-mono text-xs whitespace-pre-wrap">{texto}</pre>
    </div>
  );
}

function DialogoDetalle({ registro, onCerrar }: { registro: Bitacora; onCerrar: () => void }) {
  return (
    <Dialog open onOpenChange={(abierto) => !abierto && onCerrar()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {registro.accion} · {registro.entidad}
            {registro.entidadId ? ` #${registro.entidadId}` : ""}
          </DialogTitle>
          <DialogDescription>
            {formatoFechaHora(registro.fechaUtc)} · {registro.nombreUsuario ?? "Sistema"}
            {registro.ip ? ` · ${registro.ip}` : ""}
          </DialogDescription>
        </DialogHeader>
        {registro.mensaje && <p className="text-sm">{registro.mensaje}</p>}
        <Json titulo="Valores anteriores" valor={registro.valoresAnteriores} />
        <Json titulo="Valores nuevos" valor={registro.valoresNuevos} />
        {registro.operacionId && <p className="font-mono text-xs break-all text-muted-foreground">Operación {registro.operacionId}</p>}
      </DialogContent>
    </Dialog>
  );
}
