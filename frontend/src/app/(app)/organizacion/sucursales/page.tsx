"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { Download, Pencil, Plus, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { Barra, FiltroActivo, SinPermiso, valorFiltroActivo, type OpcionActivo } from "@/components/comun/Listado";
import { DialogoConfirmar, EncabezadoPagina } from "@/components/comun/Formulario";
import { Puede } from "@/components/comun/Puede";
import { TablaServidor } from "@/components/comun/TablaServidor";
import { FormularioSucursal } from "@/components/organizacion/FormularioSucursal";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { usePermiso } from "@/contexts/sesion";
import { useListado } from "@/hooks/useListado";
import { mostrarError } from "@/lib/errores";
import { servicioSucursales } from "@/services";
import type { Sucursal } from "@/types";

/**
 * Catálogo de referencia de Fase 0. Las demás pantallas de catálogo siguen este patrón:
 * listado en servidor (búsqueda, orden, páginas), alta y edición en diálogo, baja con
 * confirmación, botones según permiso y manejo de 400/403/409/422.
 */
export default function PaginaSucursales() {
  const puedeVer = usePermiso("ORGANIZACION_SUCURSALES_VER");
  const listado = useListado("nombre");
  const [filtro, setFiltro] = useState<OpcionActivo>("todas");
  const [enEdicion, setEnEdicion] = useState<Sucursal | "nueva" | null>(null);
  const [porDesactivar, setPorDesactivar] = useState<Sucursal | null>(null);
  const clienteConsultas = useQueryClient();

  const consulta = { ...listado.consulta, activo: valorFiltroActivo(filtro) };
  const sucursales = useQuery({
    queryKey: ["sucursales", consulta],
    queryFn: () => servicioSucursales.listar(consulta),
    placeholderData: keepPreviousData,
    enabled: puedeVer,
  });

  const refrescar = () => clienteConsultas.invalidateQueries({ queryKey: ["sucursales"] });

  const baja = useMutation({
    mutationFn: (s: Sucursal) => servicioSucursales.desactivar(s.id, s.versionFila),
    onSuccess: () => toast.success("Sucursal desactivada."),
    onError: (error) => mostrarError(error),
    // También tras un 409: la lista se recarga para mostrar la versión vigente.
    onSettled: () => {
      setPorDesactivar(null);
      void refrescar();
    },
  });

  const columnas = useMemo<ColumnDef<Sucursal>[]>(
    () => [
      { id: "codigo", header: "Código", accessorKey: "codigo", enableSorting: true },
      {
        id: "nombre",
        header: "Nombre",
        enableSorting: true,
        cell: ({ row }) => (
          <span className="flex items-center gap-2">
            {row.original.nombre}
            {row.original.esPrincipal && <Badge variant="outline">Principal</Badge>}
          </span>
        ),
      },
      { id: "telefono", header: "Teléfono", cell: ({ row }) => row.original.telefono ?? "—" },
      { id: "correo", header: "Correo", cell: ({ row }) => row.original.correo ?? "—" },
      {
        id: "activo",
        header: "Estado",
        enableSorting: true,
        cell: ({ row }) => <Badge variant={row.original.activo ? "secondary" : "outline"}>{row.original.activo ? "Activa" : "Inactiva"}</Badge>,
      },
      {
        id: "acciones",
        header: () => <span className="sr-only">Acciones</span>,
        cell: ({ row }) => (
          <div className="flex justify-end gap-1">
            <Puede permiso="ORGANIZACION_SUCURSALES_EDITAR">
              <Button variant="ghost" size="icon-sm" aria-label={`Editar ${row.original.nombre}`} onClick={() => setEnEdicion(row.original)}>
                <Pencil />
              </Button>
            </Puede>
            <Puede permiso="ORGANIZACION_SUCURSALES_ELIMINAR">
              {row.original.activo && !row.original.esPrincipal && (
                <Button variant="ghost" size="icon-sm" aria-label={`Desactivar ${row.original.nombre}`} onClick={() => setPorDesactivar(row.original)}>
                  <Trash2 />
                </Button>
              )}
            </Puede>
          </div>
        ),
      },
    ],
    [],
  );

  if (!puedeVer) return <SinPermiso />;

  return (
    <>
      <EncabezadoPagina titulo="Sucursales" descripcion="Sucursales de la empresa activa.">
        <Puede permiso="ORGANIZACION_SUCURSALES_EXPORTAR">
          <Button variant="outline" nativeButton={false} render={<a href={servicioSucursales.urlExportar(consulta)} download />}>
            <Download />
            Exportar
          </Button>
        </Puede>
        <Puede permiso="ORGANIZACION_SUCURSALES_CREAR">
          <Button onClick={() => setEnEdicion("nueva")}>
            <Plus />
            Nueva sucursal
          </Button>
        </Puede>
      </EncabezadoPagina>

      <div className="rounded-xl border bg-card">
        <Barra texto={listado.texto} onTexto={listado.setTexto} marcador="Buscar por código o nombre">
          <FiltroActivo
            valor={filtro}
            etiquetas={{ todas: "Todas", activas: "Activas", inactivas: "Inactivas" }}
            onCambio={(nuevo) => {
              setFiltro(nuevo);
              listado.setPage(1);
            }}
          />
        </Barra>
        <TablaServidor
          columnas={columnas}
          datos={sucursales.data}
          cargando={sucursales.isFetching}
          error={sucursales.error}
          onReintentar={() => void sucursales.refetch()}
          orden={listado.sort}
          onOrden={listado.setSort}
          onPagina={listado.setPage}
          vacio="No hay sucursales que coincidan"
        />
      </div>

      {enEdicion && (
        <FormularioSucursal
          sucursal={enEdicion === "nueva" ? null : enEdicion}
          onCerrar={() => setEnEdicion(null)}
          onGuardada={() => {
            setEnEdicion(null);
            void refrescar();
          }}
        />
      )}

      <DialogoConfirmar
        abierto={porDesactivar !== null}
        titulo="Desactivar sucursal"
        descripcion={`La sucursal ${porDesactivar?.nombre ?? ""} quedará inactiva y no se podrá seleccionar. Su código seguirá reservado.`}
        textoConfirmar="Desactivar"
        ocupado={baja.isPending}
        onConfirmar={() => porDesactivar && baja.mutate(porDesactivar)}
        onCerrar={() => setPorDesactivar(null)}
      />
    </>
  );
}
