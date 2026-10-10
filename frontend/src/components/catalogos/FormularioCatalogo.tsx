"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useMemo } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { aPayload, camposDe, esquemaCatalogo, valoresIniciales, type DatosFormulario } from "@/components/catalogos/esquema";
import type { CampoCatalogo, ConfigCatalogo } from "@/components/catalogos/config";
import { useOpciones } from "@/components/catalogos/useOpciones";
import { Campo } from "@/components/comun/Formulario";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SelectNativo } from "@/components/ui/select-nativo";
import { mostrarError } from "@/lib/errores";
import { servicioCatalogo } from "@/services";
import type { FilaCatalogo } from "@/types/catalogos";

interface Props {
  config: ConfigCatalogo;
  /** Fila a editar; nulo para crear. */
  fila: FilaCatalogo | null;
  onCerrar: () => void;
  onGuardada: () => void;
}

/** Diálogo de alta y edición de cualquier catálogo, armado a partir de su configuración. */
export function FormularioCatalogo({ config, fila, onCerrar, onGuardada }: Props) {
  const edicion = fila !== null;
  const campos = useMemo(() => camposDe(config.campos, edicion ? "editar" : "crear"), [config.campos, edicion]);
  const esquema = useMemo(() => esquemaCatalogo(campos), [campos]);
  const servicio = useMemo(() => servicioCatalogo(config.recurso), [config.recurso]);
  const conActivo = config.conActivo !== false;

  const { register, handleSubmit, formState, setError, setValue, watch } = useForm<DatosFormulario>({
    resolver: zodResolver(esquema),
    defaultValues: valoresIniciales(campos, fila),
  });

  const guardado = useMutation({
    mutationFn: (datos: DatosFormulario) => {
      // Los campos de solo creación no viajan al editar.
      const editables = campos.filter((c) => !c.soloCreacion);
      if (!edicion) return servicio.crear(aPayload(campos, datos));
      const cuerpo = { ...aPayload(editables, datos), versionFila: fila.versionFila, ...(conActivo ? { activo: datos.activo === true } : {}) };
      return servicio.editar(fila.id, cuerpo);
    },
    onSuccess: () => {
      toast.success(edicion ? "Cambios guardados." : "Registro creado.");
      onGuardada();
    },
    onError: (error) => mostrarError(error, { setError, campos: campos.map((c) => c.nombre) }),
  });

  const errores = formState.errors;
  // eslint-disable-next-line react-hooks/incompatible-library -- watch() de React Hook Form no es memoizable; solo pinta casillas.
  const valores = watch();
  const articulo = config.femenino ? "la" : "el";

  return (
    <Dialog open onOpenChange={(abierto) => !abierto && !guardado.isPending && onCerrar()}>
      <DialogContent className="sm:max-w-md">
        <form className="grid gap-4" noValidate onSubmit={handleSubmit((datos) => guardado.mutate(datos))}>
          <DialogHeader>
            <DialogTitle>
              {edicion ? "Editar" : config.femenino ? "Nueva" : "Nuevo"} {config.singular}
            </DialogTitle>
            <DialogDescription>
              {edicion
                ? `Los datos que no se pueden cambiar aparecen deshabilitados.`
                : `Complete los datos d${articulo === "la" ? "e la" : "el"} ${config.singular}. Algunos no se podrán cambiar después.`}
            </DialogDescription>
          </DialogHeader>

          {campos.map((campo, indice) => (
            <CampoFormulario
              key={campo.nombre}
              campo={campo}
              deshabilitado={edicion && campo.soloCreacion === true}
              enfocar={indice === 0 && !edicion}
              error={errores[campo.nombre]?.message as string | undefined}
              valor={valores[campo.nombre]}
              register={register}
              onCasilla={(marcado) => setValue(campo.nombre, marcado, { shouldDirty: true })}
            />
          ))}

          {edicion && conActivo && (
            <div className="flex items-center gap-2">
              <Checkbox id="activo" checked={valores.activo === true} onCheckedChange={(m) => setValue("activo", m === true, { shouldDirty: true })} />
              <Label htmlFor="activo">Activ{config.femenino ? "a" : "o"}</Label>
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

function CampoFormulario({
  campo,
  deshabilitado,
  enfocar,
  error,
  valor,
  register,
  onCasilla,
}: {
  campo: CampoCatalogo;
  deshabilitado: boolean;
  enfocar: boolean;
  error: string | undefined;
  valor: string | boolean | undefined;
  register: ReturnType<typeof useForm<DatosFormulario>>["register"];
  onCasilla: (marcado: boolean) => void;
}) {
  const id = `campo-${campo.nombre}`;

  if (campo.tipo === "booleano") {
    return (
      <div className="flex items-center gap-2">
        <Checkbox id={id} checked={valor === true} disabled={deshabilitado} onCheckedChange={(m) => onCasilla(m === true)} />
        <Label htmlFor={id}>{campo.etiqueta}</Label>
      </div>
    );
  }

  return (
    <Campo etiqueta={campo.etiqueta} id={id} error={error} ayuda={campo.ayuda}>
      {campo.tipo === "lista" ? (
        <ListaCampo id={id} campo={campo} deshabilitado={deshabilitado} invalido={!!error} register={register} />
      ) : (
        <Input
          id={id}
          type={campo.tipo === "fecha" ? "date" : "text"}
          inputMode={campo.tipo === "entero" ? "numeric" : campo.tipo === "decimal" ? "decimal" : undefined}
          autoFocus={enfocar}
          disabled={deshabilitado}
          aria-invalid={!!error}
          {...register(campo.nombre)}
        />
      )}
    </Campo>
  );
}

function ListaCampo({
  id,
  campo,
  deshabilitado,
  invalido,
  register,
}: {
  id: string;
  campo: CampoCatalogo;
  deshabilitado: boolean;
  invalido: boolean;
  register: ReturnType<typeof useForm<DatosFormulario>>["register"];
}) {
  const remotas = useOpciones(campo.fuente);
  const opciones = campo.opciones ?? remotas.data ?? [];
  const cargando = campo.fuente !== undefined && remotas.isPending;

  return (
    <SelectNativo id={id} disabled={deshabilitado || cargando || remotas.isError} aria-invalid={invalido} {...register(campo.nombre)}>
      <option value="">{cargando ? "Cargando…" : remotas.isError ? "No se pudieron cargar las opciones" : campo.opcional ? "— Ninguno —" : "— Seleccione —"}</option>
      {opciones.map((o) => (
        <option key={o.valor} value={o.valor}>
          {o.etiqueta}
        </option>
      ))}
    </SelectNativo>
  );
}
