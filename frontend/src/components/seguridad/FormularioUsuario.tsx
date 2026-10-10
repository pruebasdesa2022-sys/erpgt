"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { EstadoCarga, EstadoError } from "@/components/comun/Estados";
import { Campo } from "@/components/comun/Formulario";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SelectNativo } from "@/components/ui/select-nativo";
import { mostrarError } from "@/lib/errores";
import { servicioRoles, servicioUsuarios } from "@/services";
import type { EfectoPermiso, OpcionesUsuario, UsuarioDetalle, UsuarioLista } from "@/types";

const esquema = z.object({
  nombreUsuario: z.string().trim(),
  nombreCompleto: z.string().trim().min(1, "Ingrese el nombre.").max(150, "Máximo 150 caracteres."),
  correo: z.email("El correo no es válido.").max(150, "Máximo 150 caracteres."),
  passwordTemporal: z.string(),
  activo: z.boolean(),
});
type Datos = z.infer<typeof esquema>;
const CAMPOS = ["nombreUsuario", "nombreCompleto", "correo", "passwordTemporal", "activo"] as const;

const alternar = (lista: number[], id: number, marcado: boolean) => (marcado ? [...lista, id] : lista.filter((x) => x !== id));

/** Diálogo de alta o edición de usuario. Carga las opciones (roles, empresas, sucursales) y, al editar, el detalle. */
export function FormularioUsuario({ usuarioId, onCerrar, onGuardado }: { usuarioId: number | null; onCerrar: () => void; onGuardado: () => void }) {
  const opciones = useQuery({ queryKey: ["usuarios", "opciones"], queryFn: servicioUsuarios.opciones });
  const detalle = useQuery({
    queryKey: ["usuarios", "detalle", usuarioId],
    queryFn: () => servicioUsuarios.obtener(usuarioId!),
    enabled: usuarioId !== null,
    gcTime: 0,
  });

  const listo = opciones.data && (usuarioId === null || detalle.data);
  const error = opciones.error ?? detalle.error;

  return (
    <Dialog open onOpenChange={(abierto) => !abierto && onCerrar()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{usuarioId === null ? "Nuevo usuario" : "Editar usuario"}</DialogTitle>
          <DialogDescription>
            {usuarioId === null
              ? "El usuario deberá cambiar la contraseña temporal en su primer ingreso."
              : "El nombre de usuario no se puede cambiar."}
          </DialogDescription>
        </DialogHeader>
        {error ? (
          <EstadoError error={error} />
        ) : listo ? (
          <Contenido opciones={opciones.data} usuario={detalle.data ?? null} onCerrar={onCerrar} onGuardado={onGuardado} />
        ) : (
          <EstadoCarga />
        )}
      </DialogContent>
    </Dialog>
  );
}

function Contenido({ opciones, usuario, onCerrar, onGuardado }: { opciones: OpcionesUsuario; usuario: UsuarioDetalle | null; onCerrar: () => void; onGuardado: () => void }) {
  const edicion = usuario !== null;
  const [rolIds, setRolIds] = useState<number[]>(usuario?.rolIds ?? []);
  const [sucursalIds, setSucursalIds] = useState<number[]>(usuario?.sucursalIds ?? []);
  const [errorAccesos, setErrorAccesos] = useState<string | null>(null);

  const { register, handleSubmit, formState, setError, setValue, watch } = useForm<Datos>({
    resolver: zodResolver(esquema),
    defaultValues: {
      nombreUsuario: usuario?.nombreUsuario ?? "",
      nombreCompleto: usuario?.nombreCompleto ?? "",
      correo: usuario?.correo ?? "",
      passwordTemporal: "",
      activo: usuario?.activo ?? true,
    },
  });

  const guardado = useMutation({
    mutationFn: (datos: Datos) => {
      // Las empresas se deducen de las sucursales marcadas; el orden de selección define la predeterminada.
      const empresaDe = new Map(opciones.sucursales.map((s) => [s.id, s.empresaId]));
      const empresaIds = [...new Set(sucursalIds.map((id) => empresaDe.get(id)!))];
      const comunes = { correo: datos.correo, nombreCompleto: datos.nombreCompleto, rolIds, empresaIds, sucursalIds };
      return edicion
        ? servicioUsuarios.editar(usuario.id, { ...comunes, activo: datos.activo, versionFila: usuario.versionFila })
        : servicioUsuarios.crear({ ...comunes, nombreUsuario: datos.nombreUsuario, passwordTemporal: datos.passwordTemporal });
    },
    onSuccess: () => {
      toast.success(edicion ? "Usuario actualizado." : "Usuario creado.");
      onGuardado();
    },
    onError: (e) => mostrarError(e, { setError, campos: CAMPOS }),
  });

  const enviar = handleSubmit((datos) => {
    if (!edicion && datos.nombreUsuario.length < 3) return setError("nombreUsuario", { message: "Mínimo 3 caracteres." });
    if (!edicion && !datos.passwordTemporal) return setError("passwordTemporal", { message: "Ingrese una contraseña temporal." });
    if (sucursalIds.length === 0) return setErrorAccesos("Seleccione al menos una sucursal.");
    setErrorAccesos(null);
    guardado.mutate(datos);
  });

  const errores = formState.errors;
  // eslint-disable-next-line react-hooks/incompatible-library -- watch() de React Hook Form no es memoizable; solo se usa para pintar la casilla.
  const activo = watch("activo");

  return (
    <form className="grid gap-4" noValidate onSubmit={enviar}>
      <div className="grid gap-4 sm:grid-cols-2">
        <Campo etiqueta="Usuario" id="nombreUsuario" error={errores.nombreUsuario?.message}>
          <Input id="nombreUsuario" disabled={edicion} autoComplete="off" aria-invalid={!!errores.nombreUsuario} {...register("nombreUsuario")} />
        </Campo>
        <Campo etiqueta="Nombre completo" id="nombreCompleto" error={errores.nombreCompleto?.message}>
          <Input id="nombreCompleto" aria-invalid={!!errores.nombreCompleto} {...register("nombreCompleto")} />
        </Campo>
      </div>
      <Campo etiqueta="Correo" id="correo" error={errores.correo?.message}>
        <Input id="correo" type="email" autoComplete="off" aria-invalid={!!errores.correo} {...register("correo")} />
      </Campo>
      {!edicion && (
        <Campo
          etiqueta="Contraseña temporal"
          id="passwordTemporal"
          error={errores.passwordTemporal?.message}
          ayuda="Mínimo 10 caracteres, con mayúscula, minúscula, número y símbolo."
        >
          <Input id="passwordTemporal" type="password" autoComplete="new-password" aria-invalid={!!errores.passwordTemporal} {...register("passwordTemporal")} />
        </Campo>
      )}

      <fieldset className="grid gap-2">
        <legend className="mb-1 text-sm font-medium">Roles</legend>
        {opciones.roles.map((rol) => (
          <div key={rol.id} className="flex items-center gap-2">
            <Checkbox id={`rol-${rol.id}`} checked={rolIds.includes(rol.id)} onCheckedChange={(m) => setRolIds((l) => alternar(l, rol.id, m === true))} />
            <Label htmlFor={`rol-${rol.id}`}>{rol.nombre}</Label>
          </div>
        ))}
      </fieldset>

      <fieldset className="grid gap-2">
        <legend className="mb-1 text-sm font-medium">Sucursales a las que tiene acceso</legend>
        {opciones.empresas.map((empresa) => (
          <div key={empresa.id} className="grid gap-2">
            {opciones.empresas.length > 1 && <p className="text-xs text-muted-foreground">{empresa.nombreComercial}</p>}
            {opciones.sucursales
              .filter((s) => s.empresaId === empresa.id)
              .map((s) => (
                <div key={s.id} className="flex items-center gap-2">
                  <Checkbox id={`suc-${s.id}`} checked={sucursalIds.includes(s.id)} onCheckedChange={(m) => setSucursalIds((l) => alternar(l, s.id, m === true))} />
                  <Label htmlFor={`suc-${s.id}`}>{s.nombre}</Label>
                </div>
              ))}
          </div>
        ))}
        {errorAccesos && (
          <p role="alert" className="text-xs text-destructive">
            {errorAccesos}
          </p>
        )}
      </fieldset>

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

/** El administrador asigna una contraseña temporal; el usuario deberá cambiarla al ingresar. */
export function DialogoRestablecerPassword({ usuario, onCerrar }: { usuario: UsuarioLista; onCerrar: () => void }) {
  const { register, handleSubmit, formState, setError } = useForm<{ passwordTemporal: string }>({ defaultValues: { passwordTemporal: "" } });
  const cambio = useMutation({
    mutationFn: (datos: { passwordTemporal: string }) => servicioUsuarios.restablecerPassword(usuario.id, datos.passwordTemporal),
    onSuccess: () => {
      toast.success("Contraseña temporal asignada. Se cerraron las sesiones del usuario.");
      onCerrar();
    },
    onError: (e) => mostrarError(e, { setError, campos: ["passwordTemporal"] }),
  });

  return (
    <Dialog open onOpenChange={(abierto) => !abierto && !cambio.isPending && onCerrar()}>
      <DialogContent>
        <form
          className="grid gap-4"
          noValidate
          onSubmit={handleSubmit((datos) =>
            datos.passwordTemporal ? cambio.mutate(datos) : setError("passwordTemporal", { message: "Ingrese una contraseña temporal." }),
          )}
        >
          <DialogHeader>
            <DialogTitle>Restablecer contraseña</DialogTitle>
            <DialogDescription>
              {usuario.nombreUsuario} deberá cambiarla en su próximo ingreso. Entréguesela por un medio seguro.
            </DialogDescription>
          </DialogHeader>
          <Campo
            etiqueta="Contraseña temporal"
            id="passwordTemporalNueva"
            error={formState.errors.passwordTemporal?.message}
            ayuda="Mínimo 10 caracteres, con mayúscula, minúscula, número y símbolo."
          >
            <Input id="passwordTemporalNueva" type="password" autoComplete="new-password" autoFocus {...register("passwordTemporal")} />
          </Campo>
          <DialogFooter>
            <Button type="button" variant="outline" disabled={cambio.isPending} onClick={onCerrar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={cambio.isPending}>
              {cambio.isPending ? "Guardando…" : "Asignar"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

type Eleccion = EfectoPermiso | "ROLES";

/**
 * Permisos directos de un usuario. Cada permiso puede seguir lo que digan sus roles,
 * concederse aunque los roles no lo den, o denegarse aunque los roles lo den (la denegación gana).
 */
export function DialogoPermisosDirectos({ usuario, onCerrar }: { usuario: UsuarioLista; onCerrar: () => void }) {
  const catalogo = useQuery({ queryKey: ["permisos"], queryFn: servicioRoles.permisos });
  const actuales = useQuery({ queryKey: ["usuarios", "permisos", usuario.id], queryFn: () => servicioUsuarios.permisosDirectos(usuario.id), gcTime: 0 });
  const [cambios, setCambios] = useState<Record<number, Eleccion>>({});

  const guardado = useMutation({
    mutationFn: () => {
      const previos = new Map((actuales.data ?? []).map((p) => [p.permisoId, p]));
      const permisos = (catalogo.data ?? []).flatMap((permiso) => {
        const eleccion = cambios[permiso.id] ?? previos.get(permiso.id)?.efecto ?? "ROLES";
        if (eleccion === "ROLES") return [];
        const previo = previos.get(permiso.id);
        return [{ permisoId: permiso.id, efecto: eleccion, vigenteHasta: previo?.vigenteHasta ?? null, motivo: previo?.motivo ?? null }];
      });
      return servicioUsuarios.guardarPermisosDirectos(usuario.id, permisos);
    },
    onSuccess: () => {
      toast.success("Permisos directos guardados.");
      onCerrar();
    },
    onError: (e) => mostrarError(e),
  });

  const error = catalogo.error ?? actuales.error;
  const previos = new Map((actuales.data ?? []).map((p) => [p.permisoId, p.efecto]));

  return (
    <Dialog open onOpenChange={(abierto) => !abierto && !guardado.isPending && onCerrar()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Permisos directos de {usuario.nombreUsuario}</DialogTitle>
          <DialogDescription>Excepciones a lo que dan sus roles. Una denegación siempre gana.</DialogDescription>
        </DialogHeader>
        {error ? (
          <EstadoError error={error} />
        ) : !catalogo.data || !actuales.data ? (
          <EstadoCarga />
        ) : (
          <div className="grid gap-2">
            {catalogo.data.map((permiso) => (
              <div key={permiso.id} className="flex items-center justify-between gap-3">
                <Label htmlFor={`permiso-${permiso.id}`} className="font-normal">
                  <span>
                    {permiso.nombre}
                    <span className="block text-xs text-muted-foreground">{permiso.codigo}</span>
                  </span>
                </Label>
                <SelectNativo
                  id={`permiso-${permiso.id}`}
                  className="w-36 shrink-0"
                  value={cambios[permiso.id] ?? previos.get(permiso.id) ?? "ROLES"}
                  onChange={(e) => setCambios((c) => ({ ...c, [permiso.id]: e.target.value as Eleccion }))}
                >
                  <option value="ROLES">Según roles</option>
                  <option value="CONCEDER">Conceder</option>
                  <option value="DENEGAR">Denegar</option>
                </SelectNativo>
              </div>
            ))}
          </div>
        )}
        <DialogFooter>
          <Button type="button" variant="outline" disabled={guardado.isPending} onClick={onCerrar}>
            Cancelar
          </Button>
          <Button type="button" disabled={guardado.isPending || !catalogo.data || !actuales.data} onClick={() => guardado.mutate()}>
            {guardado.isPending ? "Guardando…" : "Guardar"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
