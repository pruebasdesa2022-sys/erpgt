"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { Download, Pencil, Plus, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import type { ColumnaCatalogo, ConfigCatalogo, FiltroCatalogo } from "@/components/catalogos/config";
import { FormularioCatalogo } from "@/components/catalogos/FormularioCatalogo";
import { useOpciones } from "@/components/catalogos/useOpciones";
import { DialogoConfirmar, EncabezadoPagina } from "@/components/comun/Formulario";
import { Barra, FiltroActivo, SinPermiso, valorFiltroActivo, type OpcionActivo } from "@/components/comun/Listado";
import { Puede } from "@/components/comun/Puede";
import { TablaServidor } from "@/components/comun/TablaServidor";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { SelectNativo } from "@/components/ui/select-nativo";
import { usePermiso } from "@/contexts/sesion";
import { useListado } from "@/hooks/useListado";
import { mostrarError } from "@/lib/errores";
import { formatoFechaNegocio, formatoTasa } from "@/lib/formato";
import { servicioCatalogo } from "@/services";
import type { FilaCatalogo } from "@/types/catalogos";

function celda(columna: ColumnaCatalogo, fila: FilaCatalogo) {
  if (columna.render) return columna.render(fila);
  const valor = fila[columna.id];
  if (valor === null || valor === undefined || valor === "") return "—";

  switch (columna.formato) {
    case "fecha":
      return formatoFechaNegocio(String(valor));
    case "sino":
      return valor === true ? "Sí" : "No";
    case "tasa":
      return formatoTasa(Number(valor));
    default:
      return String(valor);
  }
}

function FiltroLista({ filtro, valor, onCambio }: { filtro: FiltroCatalogo; valor: string; onCambio: (valor: string) => void }) {
  const opciones = useOpciones(filtro.fuente);
  return (
    <SelectNativo aria-label={filtro.etiqueta} className="w-auto max-w-56" value={valor} onChange={(e) => onCambio(e.target.value)}>
      <option value="">{filtro.etiqueta}: todas</option>
      {(opciones.data ?? []).map((o) => (
        <option key={o.valor} value={o.valor}>
          {o.etiqueta}
        </option>
      ))}
    </SelectNativo>
  );
}

/**
 * Pantalla estándar de un catálogo (patrón de Sucursales): listado en servidor con búsqueda, orden,
 * filtro de estado y páginas; alta y edición en diálogo; baja con confirmación; botones según permiso.
 */
export function CatalogoPagina({ config }: { config: ConfigCatalogo }) {
  const puedeVer = usePermiso(`${config.permiso}_VER`);
  const conActivo = config.conActivo !== false;
  const buscar = config.buscar !== false;
  const listado = useListado(config.ordenInicial);
  const [filtroActivo, setFiltroActivo] = useState<OpcionActivo>("todas");
  const [filtros, setFiltros] = useState<Record<string, string>>({});
  const [enEdicion, setEnEdicion] = useState<FilaCatalogo | "nueva" | null>(null);
  const [porDesactivar, setPorDesactivar] = useState<FilaCatalogo | null>(null);
  const clienteConsultas = useQueryClient();
  const servicio = useMemo(() => servicioCatalogo(config.recurso), [config.recurso]);

  const consulta = {
    ...listado.consulta,
    q: buscar ? listado.consulta.q : undefined,
    ...(conActivo ? { activo: valorFiltroActivo(filtroActivo) } : {}),
    ...filtros,
  };

  const datos = useQuery({
    queryKey: [config.clave, consulta],
    queryFn: () => servicio.listar(consulta),
    placeholderData: keepPreviousData,
    enabled: puedeVer,
  });

  const refrescar = () => clienteConsultas.invalidateQueries({ queryKey: [config.clave] });
  const nombreDe = (fila: FilaCatalogo) => (config.campoNombre ? config.campoNombre(fila) : String(fila.nombre ?? fila.codigo ?? fila.id));

  const baja = useMutation({
    mutationFn: (fila: FilaCatalogo) => servicio.desactivar(fila.id, fila.versionFila),
    onSuccess: () => toast.success("Registro desactivado."),
    onError: (error) => mostrarError(error),
    // También tras un 409: la lista se recarga para mostrar la versión vigente.
    onSettled: () => {
      setPorDesactivar(null);
      void refrescar();
    },
  });

  const columnas = useMemo<ColumnDef<FilaCatalogo>[]>(() => {
    const lista: ColumnDef<FilaCatalogo>[] = config.columnas.map((c) => ({
      id: c.id,
      header: c.encabezado,
      enableSorting: c.ordenable === true,
      cell: ({ row }) => celda(c, row.original),
    }));

    if (conActivo)
      lista.push({
        id: "activo",
        header: "Estado",
        enableSorting: true,
        cell: ({ row }) => {
          const f = row.original;
          if (config.soloLectura?.(f)) return <Badge variant="outline">Estándar</Badge>;
          return <Badge variant={f.activo ? "secondary" : "outline"}>{f.activo ? "Activo" : "Inactivo"}</Badge>;
        },
      });

    lista.push({
      id: "acciones",
      header: () => <span className="sr-only">Acciones</span>,
      cell: ({ row }) => {
        const f = row.original;
        if (config.soloLectura?.(f)) return null;
        return (
          <div className="flex justify-end gap-1">
            <Puede permiso={`${config.permiso}_EDITAR`}>
              <Button variant="ghost" size="icon-sm" aria-label={`Editar ${nombreDe(f)}`} onClick={() => setEnEdicion(f)}>
                <Pencil />
              </Button>
            </Puede>
            {!config.sinBaja && f.activo !== false && (
              <Puede permiso={`${config.permiso}_ELIMINAR`}>
                <Button variant="ghost" size="icon-sm" aria-label={`Desactivar ${nombreDe(f)}`} onClick={() => setPorDesactivar(f)}>
                  <Trash2 />
                </Button>
              </Puede>
            )}
          </div>
        );
      },
    });

    return lista;
    // eslint-disable-next-line react-hooks/exhaustive-deps -- nombreDe depende solo de config.
  }, [config, conActivo]);

  if (!puedeVer) return <SinPermiso />;

  return (
    <>
      <EncabezadoPagina titulo={config.titulo} descripcion={config.descripcion}>
        {!config.sinExportar && (
          <Puede permiso={`${config.permiso}_EXPORTAR`}>
            <Button variant="outline" nativeButton={false} render={<a href={servicio.urlExportar(consulta)} download />}>
              <Download />
              Exportar
            </Button>
          </Puede>
        )}
        {!config.sinCrear && (
          <Puede permiso={`${config.permiso}_CREAR`}>
            <Button onClick={() => setEnEdicion("nueva")}>
              <Plus />
              {config.femenino ? "Nueva" : "Nuevo"} {config.singular}
            </Button>
          </Puede>
        )}
      </EncabezadoPagina>

      <div className="rounded-xl border bg-card">
        <Barra
          texto={buscar ? listado.texto : ""}
          onTexto={listado.setTexto}
          marcador={config.marcadorBusqueda ?? "Buscar por código o nombre"}
          ocultarBusqueda={!buscar}
        >
          {config.filtros?.map((filtro) => (
            <FiltroLista
              key={filtro.parametro}
              filtro={filtro}
              valor={filtros[filtro.parametro] ?? ""}
              onCambio={(valor) => {
                setFiltros((anteriores) => ({ ...anteriores, [filtro.parametro]: valor }));
                listado.setPage(1);
              }}
            />
          ))}
          {conActivo && (
            <FiltroActivo
              valor={filtroActivo}
              etiquetas={{ todas: "Todos", activas: "Activos", inactivas: "Inactivos" }}
              onCambio={(nuevo) => {
                setFiltroActivo(nuevo);
                listado.setPage(1);
              }}
            />
          )}
        </Barra>
        <TablaServidor
          columnas={columnas}
          datos={datos.data}
          cargando={datos.isFetching}
          error={datos.error}
          onReintentar={() => void datos.refetch()}
          orden={listado.sort}
          onOrden={listado.setSort}
          onPagina={listado.setPage}
          vacio={`No hay registros que coincidan`}
        />
      </div>

      {enEdicion && (
        <FormularioCatalogo
          config={config}
          fila={enEdicion === "nueva" ? null : enEdicion}
          onCerrar={() => setEnEdicion(null)}
          onGuardada={() => {
            setEnEdicion(null);
            void refrescar();
          }}
        />
      )}

      <DialogoConfirmar
        abierto={porDesactivar !== null}
        titulo={`Desactivar ${config.singular}`}
        descripcion={
          porDesactivar
            ? `${nombreDe(porDesactivar)}: ${config.avisoBaja ?? "quedará inactivo y no se podrá seleccionar. Su código seguirá reservado."}`
            : ""
        }
        textoConfirmar="Desactivar"
        ocupado={baja.isPending}
        onConfirmar={() => porDesactivar && baja.mutate(porDesactivar)}
        onCerrar={() => setPorDesactivar(null)}
      />
    </>
  );
}
