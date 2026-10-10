import type { Metadata } from "next";
import { MarcoPublico } from "@/components/layout/Marca";
import { FormularioRecuperar } from "@/components/seguridad/FormulariosAcceso";

export const metadata: Metadata = { title: "Recuperar contraseña" };

export default function PaginaRecuperar() {
  return (
    <MarcoPublico titulo="Recuperar contraseña" descripcion="Le enviaremos un enlace para elegir una contraseña nueva.">
      <FormularioRecuperar />
    </MarcoPublico>
  );
}
