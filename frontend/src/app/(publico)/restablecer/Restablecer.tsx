"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { FormularioNuevaPassword } from "@/components/seguridad/FormulariosAcceso";

export function Restablecer() {
  const token = useSearchParams().get("token");

  if (!token)
    return (
      <div className="grid gap-4 text-center">
        <p role="alert" className="text-sm">
          El enlace no es válido. Solicite uno nuevo.
        </p>
        <Link href="/recuperar" className="text-sm underline-offset-4 hover:underline">
          Recuperar contraseña
        </Link>
      </div>
    );

  return <FormularioNuevaPassword token={token} />;
}
