"use client";

import { EncabezadoPagina } from "@/components/comun/Formulario";
import { FormularioNuevaPassword } from "@/components/seguridad/FormulariosAcceso";
import { useSesion } from "@/contexts/sesion";

export default function PaginaCambiarPassword() {
  const { sesion } = useSesion();

  return (
    <>
      <EncabezadoPagina
        titulo="Cambiar contraseña"
        descripcion={
          sesion.debeCambiarPassword
            ? "Debe cambiar su contraseña antes de continuar."
            : "Al cambiarla se cerrarán todas sus sesiones y deberá ingresar de nuevo."
        }
      />
      <div className="max-w-sm rounded-xl border bg-card p-4">
        <FormularioNuevaPassword />
      </div>
    </>
  );
}
