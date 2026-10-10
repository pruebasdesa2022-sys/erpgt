"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { useOpciones } from "@/components/catalogos/useOpciones";
import { Campo } from "@/components/comun/Formulario";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SelectNativo } from "@/components/ui/select-nativo";
import { CLAVE_SESION } from "@/contexts/sesion";
import { mostrarError } from "@/lib/errores";
import { nitValido } from "@/lib/nit";
import { servicioEmpresas } from "@/services";
import type { Empresa } from "@/types/catalogos";

/** Validación de experiencia de usuario; el servidor repite estas reglas y es quien decide. */
export const esquemaEmpresa = z.object({
  razonSocial: z.string().trim().min(1, "Ingrese la razón social.").max(200, "Máximo 200 caracteres."),
  nombreComercial: z.string().trim().min(1, "Ingrese el nombre comercial.").max(200, "Máximo 200 caracteres."),
  nit: z.string().trim().min(1, "Ingrese el NIT.").max(20, "Máximo 20 caracteres.").refine(nitValido, "El NIT no es válido. Verifique el número y su dígito verificador."),
  direccion: z.string().trim().max(300, "Máximo 300 caracteres."),
  telefono: z.string().trim().max(30, "Máximo 30 caracteres."),
  correo: z.union([z.literal(""), z.email("El correo no es válido.").max(150, "Máximo 150 caracteres.")]),
  regimenTributario: z.enum(["GENERAL", "PEQUENO_CONTRIBUYENTE"]),
  monedaLocalId: z.string().min(1, "Seleccione la moneda local."),
});
export type DatosEmpresa = z.infer<typeof esquemaEmpresa>;

const CAMPOS = ["razonSocial", "nombreComercial", "nit", "direccion", "telefono", "correo", "regimenTributario", "monedaLocalId"] as const;
const vacioANulo = (valor: string) => (valor === "" ? null : valor);

export function FormularioEmpresa({ empresa, puedeEditar }: { empresa: Empresa; puedeEditar: boolean }) {
  const monedas = useOpciones("monedas");
  const clienteConsultas = useQueryClient();
  const { register, handleSubmit, formState, setError } = useForm<DatosEmpresa>({
    resolver: zodResolver(esquemaEmpresa),
    defaultValues: {
      razonSocial: empresa.razonSocial,
      nombreComercial: empresa.nombreComercial,
      // El NIT sembrado ("CF") no es válido: se deja para que quien configure lo reemplace.
      nit: empresa.nit,
      direccion: empresa.direccion ?? "",
      telefono: empresa.telefono ?? "",
      correo: empresa.correo ?? "",
      regimenTributario: empresa.regimenTributario,
      monedaLocalId: String(empresa.monedaLocalId),
    },
  });

  const guardado = useMutation({
    mutationFn: (datos: DatosEmpresa) =>
      servicioEmpresas.editar(empresa.id, {
        razonSocial: datos.razonSocial,
        nombreComercial: datos.nombreComercial,
        nit: datos.nit,
        direccion: vacioANulo(datos.direccion),
        telefono: vacioANulo(datos.telefono),
        correo: vacioANulo(datos.correo),
        regimenTributario: datos.regimenTributario,
        monedaLocalId: Number(datos.monedaLocalId),
        versionFila: empresa.versionFila,
      }),
    onSuccess: () => {
      toast.success("Datos de la empresa guardados.");
      void clienteConsultas.invalidateQueries({ queryKey: ["empresas"] });
      // El nombre comercial se muestra en la cabecera.
      void clienteConsultas.invalidateQueries({ queryKey: CLAVE_SESION });
    },
    onError: (error) => mostrarError(error, { setError, campos: CAMPOS }),
  });

  const errores = formState.errors;
  const deshabilitado = !puedeEditar || guardado.isPending;

  return (
    <form className="grid max-w-2xl gap-4 rounded-xl border bg-card p-4" noValidate onSubmit={handleSubmit((datos) => guardado.mutate(datos))}>
      <div className="grid gap-4 sm:grid-cols-2">
        <Campo etiqueta="Razón social" id="razonSocial" error={errores.razonSocial?.message}>
          <Input id="razonSocial" disabled={deshabilitado} aria-invalid={!!errores.razonSocial} {...register("razonSocial")} />
        </Campo>
        <Campo etiqueta="Nombre comercial" id="nombreComercial" error={errores.nombreComercial?.message}>
          <Input id="nombreComercial" disabled={deshabilitado} aria-invalid={!!errores.nombreComercial} {...register("nombreComercial")} />
        </Campo>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <Campo etiqueta="NIT" id="nit" error={errores.nit?.message} ayuda="Con su dígito verificador, por ejemplo 1234567-9.">
          <Input id="nit" disabled={deshabilitado} aria-invalid={!!errores.nit} {...register("nit")} />
        </Campo>
        <Campo etiqueta="Régimen tributario" id="regimenTributario" error={errores.regimenTributario?.message}>
          <SelectNativo id="regimenTributario" disabled={deshabilitado} {...register("regimenTributario")}>
            <option value="GENERAL">General</option>
            <option value="PEQUENO_CONTRIBUYENTE">Pequeño contribuyente</option>
          </SelectNativo>
        </Campo>
      </div>
      <Campo etiqueta="Dirección fiscal" id="direccion" error={errores.direccion?.message}>
        <Input id="direccion" disabled={deshabilitado} aria-invalid={!!errores.direccion} {...register("direccion")} />
      </Campo>
      <div className="grid gap-4 sm:grid-cols-2">
        <Campo etiqueta="Teléfono" id="telefono" error={errores.telefono?.message}>
          <Input id="telefono" type="tel" disabled={deshabilitado} aria-invalid={!!errores.telefono} {...register("telefono")} />
        </Campo>
        <Campo etiqueta="Correo" id="correo" error={errores.correo?.message}>
          <Input id="correo" type="email" disabled={deshabilitado} aria-invalid={!!errores.correo} {...register("correo")} />
        </Campo>
      </div>
      <Campo
        etiqueta="Moneda local"
        id="monedaLocalId"
        error={errores.monedaLocalId?.message}
        ayuda="Moneda en la que se llevan los libros. Cámbiela solo antes de registrar documentos."
      >
        <SelectNativo id="monedaLocalId" disabled={deshabilitado || monedas.isPending} {...register("monedaLocalId")}>
          {(monedas.data ?? []).map((m) => (
            <option key={m.valor} value={m.valor}>
              {m.etiqueta}
            </option>
          ))}
          {/* Mientras cargan las opciones, o si la moneda actual quedó inactiva, se conserva la actual. */}
          {!(monedas.data ?? []).some((m) => m.valor === String(empresa.monedaLocalId)) && (
            <option value={String(empresa.monedaLocalId)}>Moneda actual</option>
          )}
        </SelectNativo>
      </Campo>

      {puedeEditar && (
        <div className="flex justify-end">
          <Button type="submit" disabled={guardado.isPending}>
            {guardado.isPending ? "Guardando…" : "Guardar cambios"}
          </Button>
        </div>
      )}
    </form>
  );
}
