"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Campo } from "@/components/comun/Formulario";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { mostrarError } from "@/lib/errores";
import { servicioSucursales } from "@/services";
import type { Sucursal } from "@/types";

/** Validación de experiencia de usuario; el servidor repite estas reglas y es quien decide. */
export const esquemaSucursal = z.object({
  codigo: z
    .string()
    .trim()
    .min(1, "Ingrese el código.")
    .max(20, "Máximo 20 caracteres.")
    .regex(/^[A-Za-z0-9_-]+$/, "Solo letras, números, guion y guion bajo."),
  nombre: z.string().trim().min(1, "Ingrese el nombre.").max(150, "Máximo 150 caracteres."),
  direccion: z.string().trim().max(300, "Máximo 300 caracteres."),
  telefono: z.string().trim().max(30, "Máximo 30 caracteres."),
  correo: z.union([z.literal(""), z.email("El correo no es válido.").max(150, "Máximo 150 caracteres.")]),
  activo: z.boolean(),
});
export type DatosSucursal = z.infer<typeof esquemaSucursal>;

const CAMPOS = ["codigo", "nombre", "direccion", "telefono", "correo", "activo"] as const;
const vacioANulo = (valor: string) => (valor === "" ? null : valor);

/** Diálogo para crear una sucursal o editar una existente (`sucursal` definido). */
export function FormularioSucursal({ sucursal, onCerrar, onGuardada }: { sucursal: Sucursal | null; onCerrar: () => void; onGuardada: () => void }) {
  const edicion = sucursal !== null;
  const { register, handleSubmit, formState, setError, setValue, watch } = useForm<DatosSucursal>({
    resolver: zodResolver(esquemaSucursal),
    defaultValues: {
      codigo: sucursal?.codigo ?? "",
      nombre: sucursal?.nombre ?? "",
      direccion: sucursal?.direccion ?? "",
      telefono: sucursal?.telefono ?? "",
      correo: sucursal?.correo ?? "",
      activo: sucursal?.activo ?? true,
    },
  });

  const guardado = useMutation({
    mutationFn: (datos: DatosSucursal) => {
      const comunes = { nombre: datos.nombre, direccion: vacioANulo(datos.direccion), telefono: vacioANulo(datos.telefono), correo: vacioANulo(datos.correo) };
      return edicion
        ? servicioSucursales.editar(sucursal.id, { ...comunes, activo: datos.activo, versionFila: sucursal.versionFila })
        : servicioSucursales.crear({ ...comunes, codigo: datos.codigo });
    },
    onSuccess: () => {
      toast.success(edicion ? "Sucursal actualizada." : "Sucursal creada.");
      onGuardada();
    },
    onError: (error) => mostrarError(error, { setError, campos: CAMPOS }),
  });

  const errores = formState.errors;
  // eslint-disable-next-line react-hooks/incompatible-library -- watch() de React Hook Form no es memoizable; solo se usa para pintar la casilla.
  const activo = watch("activo");

  return (
    <Dialog open onOpenChange={(abierto) => !abierto && !guardado.isPending && onCerrar()}>
      <DialogContent className="sm:max-w-md">
        <form className="grid gap-4" noValidate onSubmit={handleSubmit((datos) => guardado.mutate(datos))}>
          <DialogHeader>
            <DialogTitle>{edicion ? "Editar sucursal" : "Nueva sucursal"}</DialogTitle>
            <DialogDescription>{edicion ? "El código no se puede cambiar." : "El código identifica a la sucursal y no se podrá cambiar después."}</DialogDescription>
          </DialogHeader>

          <Campo etiqueta="Código" id="codigo" error={errores.codigo?.message}>
            <Input id="codigo" disabled={edicion} autoFocus={!edicion} aria-invalid={!!errores.codigo} {...register("codigo")} />
          </Campo>
          <Campo etiqueta="Nombre" id="nombre" error={errores.nombre?.message}>
            <Input id="nombre" autoFocus={edicion} aria-invalid={!!errores.nombre} {...register("nombre")} />
          </Campo>
          <Campo etiqueta="Dirección" id="direccion" error={errores.direccion?.message}>
            <Input id="direccion" aria-invalid={!!errores.direccion} {...register("direccion")} />
          </Campo>
          <div className="grid gap-4 sm:grid-cols-2">
            <Campo etiqueta="Teléfono" id="telefono" error={errores.telefono?.message}>
              <Input id="telefono" type="tel" aria-invalid={!!errores.telefono} {...register("telefono")} />
            </Campo>
            <Campo etiqueta="Correo" id="correo" error={errores.correo?.message}>
              <Input id="correo" type="email" aria-invalid={!!errores.correo} {...register("correo")} />
            </Campo>
          </div>
          {edicion && (
            <div className="flex items-center gap-2">
              <Checkbox
                id="activo"
                checked={activo}
                disabled={sucursal.esPrincipal}
                onCheckedChange={(marcado) => setValue("activo", marcado === true, { shouldDirty: true })}
              />
              <Label htmlFor="activo">Activa{sucursal.esPrincipal ? " (la sucursal principal no se puede desactivar)" : ""}</Label>
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
      </DialogContent>
    </Dialog>
  );
}
