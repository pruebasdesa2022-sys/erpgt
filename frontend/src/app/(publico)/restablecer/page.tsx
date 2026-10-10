import type { Metadata } from "next";
import { Suspense } from "react";
import { MarcoPublico } from "@/components/layout/Marca";
import { Restablecer } from "./Restablecer";

export const metadata: Metadata = { title: "Restablecer contraseña" };

export default function PaginaRestablecer() {
  return (
    <MarcoPublico titulo="Restablecer contraseña" descripcion="Elija una contraseña nueva para su cuenta.">
      <Suspense>
        <Restablecer />
      </Suspense>
    </MarcoPublico>
  );
}
