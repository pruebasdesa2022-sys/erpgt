"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { EstadoCarga, EstadoError } from "@/components/comun/Estados";
import { Campo, DialogoConfirmar, EncabezadoPagina } from "@/components/comun/Formulario";
import { Barra, FiltroActivo, SinPermiso, valorFiltroActivo, type OpcionActivo } from "@/components/comun/Listado";
import { Puede } from "@/components/comun/Puede";
import { TablaServidor } from "@/components/comun/TablaServidor";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { usePermiso } from "@/contexts/sesion";
import { useListado } from "@/hooks/useListado";
import { mostrarError } from "@/lib/errores";
import { servicioRoles } from "@/services";
import type { Permiso, RolDetalle, RolLista } from "@/types";

export default function PaginaRoles() {
  const puedeVer = usePermiso("SEGURIDAD_ROLES_VER");
  const listado = useListado("nombre");
  const [filtro, setFiltro] = useState<OpcionActivo>("todas");
  const [enEdicion, setEnEdicion] = useState<number | "nuevo" | null>(null);
  const [porDesactivar, setPorDesactivar] = useState<RolLista | null>(null);
  const clienteConsultas = useQueryClient();

  const consulta = { ...listado.consulta, activo: valorFiltroActivo(filtro) };
  const roles = useQuery({
    queryKey: ["roles", "lista", consulta],
    queryFn: () => servicioRoles.listar(consulta),
    placeholderData: keepPreviousData,
    enabled: puedeVer,
  });
  const refrescar = () => clienteConsultas.invalidateQueries({ queryKey: ["roles", "lista"] });

  const baja = useMutation({
    mutationFn: (r: RolLista) => servicioRoles.desactivar(r.id, r.versionFila),
    onSuccess: () => toast.success("Rol desactivado."),
    onError: (error) => mostrarError(error),
    onSettled: () => {
      setPorDesactivar(null);
      void refrescar();
    },
  });

  const columnas = useMemo<ColumnDef<RolLista>[]>(
    () => [
      { id: "codigo", header: "Código", accessorKey: "codigo", enableSorting: true },
      {
        id: "nombre",
        header: "Nombre",
        enableSorting: true,
        cell: ({ row }) => (
          <span className="flex items-center gap-2">
            {row.original.nombre}
            {row.original.esSistema && <Badge variant="outline">Sistema</Badge>}
          </span>
        ),
      },
      { id: "permisos", header: "Permisos", accessorKey: "cantidadPermisos" },
      { id: "usuarios", header: "Usuarios", accessorKey: "cantidadUsuarios" },
      {
        id: "activo",
        header: "Estado",
        enableSorting: true,
        cell: ({ row }) => <Badge variant={row.original.activo ? "secondary" : "outline"}>{row.original.activo ? "Activo" : "Inactivo"}</Badge>,
      },
      {
        id: "acciones",
        header: () => <span className="sr-only">Acciones</span>,
        // El rol de sistema no se edita ni se desactiva: garantiza que siempre haya un administrador.
        cell: ({ row }) =>
          row.original.esSistema ? null : (
            <div className="flex justify-end gap-1">
              <Puede permiso="SEGURIDAD_ROLES_EDITAR">
                <Button variant="ghost" size="icon-sm" aria-label={`Editar ${row.original.nombre}`} onClick={() => setEnEdicion(row.original.id)}>
                  <Pencil />
                </Button>
              </Puede>
              <Puede permiso="SEGURIDAD_ROLES_ELIMINAR">
                {row.original.activo && (
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
      <EncabezadoPagina titulo="Roles" descripcion="Conjuntos de permisos que se asignan a los usuarios.">
        <Puede permiso="SEGURIDAD_ROLES_CREAR">
          <Button onClick={() => setEnEdicion("nuevo")}>
            <Plus />
            Nuevo rol
          </Button>
        </Puede>
      </EncabezadoPagina>

      <div className="rounded-xl border bg-card">
        <Barra texto={listado.texto} onTexto={listado.setTexto} marcador="Buscar por código o nombre">
          <FiltroActivo
            valor={filtro}
            etiquetas={{ todas: "Todos", activas: "Activos", inactivas: "Inactivos" }}
            onCambio={(nuevo) => {
              setFiltro(nuevo);
              listado.setPage(1);
            }}
          />
        </Barra>
        <TablaServidor
          columnas={columnas}
          datos={roles.data}
          cargando={roles.isFetching}
          error={roles.error}
          onReintentar={() => void roles.refetch()}
          orden={listado.sort}
          onOrden={listado.setSort}
          onPagina={listado.setPage}
          vacio="No hay roles que coincidan"
        />
      </div>

      {enEdicion !== null && (
        <DialogoRol
          rolId={enEdicion === "nuevo" ? null : enEdicion}
          onCerrar={() => setEnEdicion(null)}
          onGuardado={() => {
            setEnEdicion(null);
            void refrescar();
          }}
        />
      )}

      <DialogoConfirmar
        abierto={porDesactivar !== null}
        titulo="Desactivar rol"
        descripcion={`El rol ${porDesactivar?.nombre ?? ""} dejará de poder asignarse. No se puede desactivar si lo tienen usuarios activos.`}
        textoConfirmar="Desactivar"
        ocupado={baja.isPending}
        onConfirmar={() => porDesactivar && baja.mutate(porDesactivar)}
        onCerrar={() => setPorDesactivar(null)}
      />
    </>
  );
}

function DialogoRol({ rolId, onCerrar, onGuardado }: { rolId: number | null; onCerrar: () => void; onGuardado: () => void }) {
  const permisos = useQuery({ queryKey: ["permisos"], queryFn: servicioRoles.permisos });
  const detalle = useQuery({ queryKey: ["roles", "detalle", rolId], queryFn: () => servicioRoles.obtener(rolId!), enabled: rolId !== null, gcTime: 0 });
  const error = permisos.error ?? detalle.error;
  const listo = permisos.data && (rolId === null || detalle.data);

  return (
    <Dialog open onOpenChange={(abierto) => !abierto && onCerrar()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{rolId === null ? "Nuevo rol" : "Editar rol"}</DialogTitle>
          <DialogDescription>Los cambios de permisos aplican de inmediato a los usuarios que tienen el rol.</DialogDescription>
        </DialogHeader>
        {error ? (
          <EstadoError error={error} />
        ) : listo ? (
          <FormularioRol permisos={permisos.data} rol={detalle.data ?? null} onCerrar={onCerrar} onGuardado={onGuardado} />
        ) : (
          <EstadoCarga />
        )}
      </DialogContent>
    </Dialog>
  );
}

const esquemaRol = z.object({
  codigo: z.string().trim().max(50, "Máximo 50 caracteres."),
  nombre: z.string().trim().min(1, "Ingrese el nombre.").max(100, "Máximo 100 caracteres."),
  descripcion: z.string().trim().max(300, "Máximo 300 caracteres."),
  activo: z.boolean(),
});
type DatosRol = z.infer<typeof esquemaRol>;
const CAMPOS_ROL = ["codigo", "nombre", "descripcion", "activo"] as const;

function FormularioRol({ permisos, rol, onCerrar, onGuardado }: { permisos: Permiso[]; rol: RolDetalle | null; onCerrar: () => void; onGuardado: () => void }) {
  const edicion = rol !== null;
  const [permisoIds, setPermisoIds] = useState<number[]>(rol?.permisoIds ?? []);
  const { register, handleSubmit, formState, setError, setValue, watch } = useForm<DatosRol>({
    resolver: zodResolver(esquemaRol),
    defaultValues: { codigo: rol?.codigo ?? "", nombre: rol?.nombre ?? "", descripcion: rol?.descripcion ?? "", activo: rol?.activo ?? true },
  });

  const guardado = useMutation({
    mutationFn: (datos: DatosRol) => {
      const comunes = { nombre: datos.nombre, descripcion: datos.descripcion || null, permisoIds };
      return edicion
        ? servicioRoles.editar(rol.id, { ...comunes, activo: datos.activo, versionFila: rol.versionFila })
        : servicioRoles.crear({ ...comunes, codigo: datos.codigo });
    },
    onSuccess: () => {
      toast.success(edicion ? "Rol actualizado." : "Rol creado.");
      onGuardado();
    },
    onError: (e) => mostrarError(e, { setError, campos: CAMPOS_ROL }),
  });

  const porModulo = useMemo(() => {
    const grupos = new Map<string, Permiso[]>();
    for (const p of permisos) grupos.set(p.moduloNombre, [...(grupos.get(p.moduloNombre) ?? []), p]);
    return [...grupos.entries()];
  }, [permisos]);

  const errores = formState.errors;
  // eslint-disable-next-line react-hooks/incompatible-library -- watch() de React Hook Form no es memoizable; solo se usa para pintar la casilla.
  const activo = watch("activo");

  return (
    <form
      className="grid gap-4"
      noValidate
      onSubmit={handleSubmit((datos) =>
        !edicion && !/^[A-Za-z0-9_]+$/.test(datos.codigo)
          ? setError("codigo", { message: "Ingrese un código con letras, números o guion bajo." })
          : guardado.mutate(datos),
      )}
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Campo etiqueta="Código" id="codigo" error={errores.codigo?.message}>
          <Input id="codigo" disabled={edicion} aria-invalid={!!errores.codigo} {...register("codigo")} />
        </Campo>
        <Campo etiqueta="Nombre" id="nombre" error={errores.nombre?.message}>
          <Input id="nombre" aria-invalid={!!errores.nombre} {...register("nombre")} />
        </Campo>
      </div>
      <Campo etiqueta="Descripción" id="descripcion" error={errores.descripcion?.message}>
        <Input id="descripcion" aria-invalid={!!errores.descripcion} {...register("descripcion")} />
      </Campo>

      {porModulo.map(([modulo, lista]) => (
        <fieldset key={modulo} className="grid gap-2">
          <legend className="mb-1 text-sm font-medium">{modulo}</legend>
          {lista.map((p) => (
            <div key={p.id} className="flex items-center gap-2">
              <Checkbox
                id={`permiso-${p.id}`}
                checked={permisoIds.includes(p.id)}
                onCheckedChange={(m) => setPermisoIds((l) => (m === true ? [...l, p.id] : l.filter((x) => x !== p.id)))}
              />
              <Label htmlFor={`permiso-${p.id}`} className="font-normal">
                {p.nombre}
              </Label>
            </div>
          ))}
        </fieldset>
      ))}

      {edicion && (
        <div className="flex items-center gap-2">
          <Checkbox id="activo" checked={activo} onCheckedChange={(m) => setValue("activo", m === true, { shouldDirty: true })} />
          <Label htmlFor="activo">Activo</Label>
        </div>
      )}

      <DialogFooter>
        <Button type="button" variant="outline" disabled={guardado.isPending} onClick={onCerrar}>
          Cancelar
        </Button>
        <Button type="submit" disabled={guardado.isPending}>
          {guardado.isPending ? "Guardando…" : "Guardar"}
        </Button>
      </DialogFooter>
    </form>
  );
}
