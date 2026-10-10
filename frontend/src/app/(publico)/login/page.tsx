import type { Metadata } from "next";
import { Suspense } from "react";
import { MarcoPublico } from "@/components/layout/Marca";
import { FormularioLogin } from "@/components/seguridad/FormulariosAcceso";

export const metadata: Metadata = { title: "Iniciar sesión" };

export default function PaginaLogin() {
  return (
    <MarcoPublico titulo="Iniciar sesión" descripcion="Ingrese con su usuario y contraseña.">
      <Suspense>
        <FormularioLogin />
      </Suspense>
    </MarcoPublico>
  );
}
