"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { KeyRound, Pencil, Plus, ShieldCheck, Trash2, Warehouse } from "lucide-react";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { DialogoConfirmar, EncabezadoPagina } from "@/components/comun/Formulario";
import { Barra, FiltroActivo, SinPermiso, valorFiltroActivo, type OpcionActivo } from "@/components/comun/Listado";
import { Puede } from "@/components/comun/Puede";
import { TablaServidor } from "@/components/comun/TablaServidor";
import { DialogoAccesosOperativos } from "@/components/seguridad/DialogoAccesosOperativos";
import { DialogoPermisosDirectos, DialogoRestablecerPassword, FormularioUsuario } from "@/components/seguridad/FormularioUsuario";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { usePermiso, useSesion } from "@/contexts/sesion";
import { useListado } from "@/hooks/useListado";
import { mostrarError } from "@/lib/errores";
import { formatoFechaHora } from "@/lib/formato";
import { servicioUsuarios } from "@/services";
import type { UsuarioLista } from "@/types";

type Dialogo = { tipo: "formulario"; id: number | null } | { tipo: "password" | "permisos" | "baja" | "accesos"; usuario: UsuarioLista } | null;

function Estado({ usuario }: { usuario: UsuarioLista }) {
  if (!usuario.activo) return <Badge variant="outline">Inactivo</Badge>;
  if (usuario.bloqueadoHasta && new Date(usuario.bloqueadoHasta) > new Date()) return <Badge variant="destructive">Bloqueado</Badge>;
  if (usuario.debeCambiarPassword) return <Badge variant="outline">Debe cambiar contraseña</Badge>;
  return <Badge variant="secondary">Activo</Badge>;
}

export default function PaginaUsuarios() {
  const puedeVer = usePermiso("SEGURIDAD_USUARIOS_VER");
  const { sesion } = useSesion();
  const listado = useListado("nombreUsuario");
  const [filtro, setFiltro] = useState<OpcionActivo>("todas");
  const [dialogo, setDialogo] = useState<Dialogo>(null);
  const clienteConsultas = useQueryClient();

  const consulta = { ...listado.consulta, activo: valorFiltroActivo(filtro) };
  const usuarios = useQuery({
    queryKey: ["usuarios", "lista", consulta],
    queryFn: () => servicioUsuarios.listar(consulta),
    placeholderData: keepPreviousData,
    enabled: puedeVer,
  });
  const refrescar = () => clienteConsultas.invalidateQueries({ queryKey: ["usuarios", "lista"] });

  const baja = useMutation({
    mutationFn: (u: UsuarioLista) => servicioUsuarios.desactivar(u.id, u.versionFila),
    onSuccess: () => toast.success("Usuario desactivado. Se cerraron sus sesiones."),
    onError: (error) => mostrarError(error),
    onSettled: () => {
      setDialogo(null);
      void refrescar();
    },
  });

  const columnas = useMemo<ColumnDef<UsuarioLista>[]>(
    () => [
      { id: "nombreUsuario", header: "Usuario", accessorKey: "nombreUsuario", enableSorting: true },
      { id: "nombreCompleto", header: "Nombre", accessorKey: "nombreCompleto", enableSorting: true },
      { id: "correo", header: "Correo", accessorKey: "correo", enableSorting: true },
      { id: "roles", header: "Roles", cell: ({ row }) => row.original.roles ?? "—" },
      { id: "ultimoAccesoEn", header: "Último acceso", enableSorting: true, cell: ({ row }) => formatoFechaHora(row.original.ultimoAccesoEn) },
      { id: "activo", header: "Estado", enableSorting: true, cell: ({ row }) => <Estado usuario={row.original} /> },
      {
        id: "acciones",
        header: () => <span className="sr-only">Acciones</span>,
        cell: ({ row }) => {
          const u = row.original;
          return (
            <div className="flex justify-end gap-1">
              <Puede permiso="SEGURIDAD_USUARIOS_EDITAR">
                <Button variant="ghost" size="icon-sm" aria-label={`Editar ${u.nombreUsuario}`} onClick={() => setDialogo({ tipo: "formulario", id: u.id })}>
                  <Pencil />
                </Button>
                <Button variant="ghost" size="icon-sm" aria-label={`Bodegas y cajas de ${u.nombreUsuario}`} onClick={() => setDialogo({ tipo: "accesos", usuario: u })}>
                  <Warehouse />
                </Button>
                <Button variant="ghost" size="icon-sm" aria-label={`Restablecer contraseña de ${u.nombreUsuario}`} onClick={() => setDialogo({ tipo: "password", usuario: u })}>
                  <KeyRound />
                </Button>
              </Puede>
              <Puede permiso="SEGURIDAD_PERMISOS_EDITAR">
                <Button variant="ghost" size="icon-sm" aria-label={`Permisos directos de ${u.nombreUsuario}`} onClick={() => setDialogo({ tipo: "permisos", usuario: u })}>
                  <ShieldCheck />
                </Button>
              </Puede>
              <Puede permiso="SEGURIDAD_USUARIOS_ELIMINAR">
                {u.activo && u.id !== sesion.usuarioId && (
                  <Button variant="ghost" size="icon-sm" aria-label={`Desactivar ${u.nombreUsuario}`} onClick={() => setDialogo({ tipo: "baja", usuario: u })}>
                    <Trash2 />
                  </Button>
                )}
              </Puede>
            </div>
          );
        },
      },
    ],
    [sesion.usuarioId],
  );

  if (!puedeVer) return <SinPermiso />;

  return (
    <>
      <EncabezadoPagina titulo="Usuarios" descripcion="Cuentas, roles y accesos a empresas y sucursales.">
        <Puede permiso="SEGURIDAD_USUARIOS_CREAR">
          <Button onClick={() => setDialogo({ tipo: "formulario", id: null })}>
            <Plus />
            Nuevo usuario
          </Button>
        </Puede>
      </EncabezadoPagina>

      <div className="rounded-xl border bg-card">
        <Barra texto={listado.texto} onTexto={listado.setTexto} marcador="Buscar por usuario, nombre o correo">
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
          datos={usuarios.data}
          cargando={usuarios.isFetching}
          error={usuarios.error}
          onReintentar={() => void usuarios.refetch()}
          orden={listado.sort}
          onOrden={listado.setSort}
          onPagina={listado.setPage}
          vacio="No hay usuarios que coincidan"
        />
      </div>

      {dialogo?.tipo === "formulario" && (
        <FormularioUsuario
          usuarioId={dialogo.id}
          onCerrar={() => setDialogo(null)}
          onGuardado={() => {
            setDialogo(null);
            void refrescar();
          }}
        />
      )}
      {dialogo?.tipo === "password" && (
        <DialogoRestablecerPassword
          usuario={dialogo.usuario}
          onCerrar={() => {
            setDialogo(null);
            void refrescar();
          }}
        />
      )}
      {dialogo?.tipo === "permisos" && <DialogoPermisosDirectos usuario={dialogo.usuario} onCerrar={() => setDialogo(null)} />}
      {dialogo?.tipo === "accesos" && <DialogoAccesosOperativos usuario={dialogo.usuario} onCerrar={() => setDialogo(null)} />}

      <DialogoConfirmar
        abierto={dialogo?.tipo === "baja"}
        titulo="Desactivar usuario"
        descripcion={`${dialogo?.tipo === "baja" ? dialogo.usuario.nombreUsuario : "El usuario"} ya no podrá ingresar y se cerrarán sus sesiones abiertas.`}
        textoConfirmar="Desactivar"
        ocupado={baja.isPending}
        onConfirmar={() => dialogo?.tipo === "baja" && baja.mutate(dialogo.usuario)}
        onCerrar={() => setDialogo(null)}
      />
    </>
  );
}
