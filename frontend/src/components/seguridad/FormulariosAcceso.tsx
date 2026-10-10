"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Campo } from "@/components/comun/Formulario";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { mensajeDeError, mostrarError } from "@/lib/errores";
import { servicioSesion } from "@/services";

/** Solo se aceptan destinos internos, para que el parámetro no sirva de redirección a otro sitio. */
export function destinoSeguro(siguiente: string | null): string {
  return siguiente && siguiente.startsWith("/") && !siguiente.startsWith("//") && !siguiente.includes("\\") ? siguiente : "/";
}

const esquemaLogin = z.object({
  nombreUsuario: z.string().trim().min(1, "Ingrese su usuario."),
  password: z.string().min(1, "Ingrese su contraseña."),
});
type DatosLogin = z.infer<typeof esquemaLogin>;

export function FormularioLogin() {
  const router = useRouter();
  const parametros = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const { register, handleSubmit, formState } = useForm<DatosLogin>({
    resolver: zodResolver(esquemaLogin),
    defaultValues: { nombreUsuario: "", password: "" },
  });

  const ingreso = useMutation({
    mutationFn: (datos: DatosLogin) => servicioSesion.login(datos.nombreUsuario, datos.password),
    onMutate: () => setError(null),
    onSuccess: (resultado) => router.replace(resultado.debeCambiarPassword ? "/cambiar-password" : destinoSeguro(parametros.get("siguiente"))),
    onError: (e) => setError(mensajeDeError(e)),
  });

  const errores = formState.errors;

  return (
    <form className="grid gap-4" noValidate onSubmit={handleSubmit((datos) => ingreso.mutate(datos))}>
      {parametros.get("expirada") && !error && (
        <p role="status" className="rounded-md bg-muted px-3 py-2 text-sm text-muted-foreground">
          Su sesión expiró. Ingrese de nuevo.
        </p>
      )}
      {error && (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      )}
      <Campo etiqueta="Usuario" id="nombreUsuario" error={errores.nombreUsuario?.message}>
        <Input id="nombreUsuario" autoComplete="username" autoFocus aria-invalid={!!errores.nombreUsuario} {...register("nombreUsuario")} />
      </Campo>
      <Campo etiqueta="Contraseña" id="password" error={errores.password?.message}>
        <Input id="password" type="password" autoComplete="current-password" aria-invalid={!!errores.password} {...register("password")} />
      </Campo>
      <Button type="submit" disabled={ingreso.isPending}>
        {ingreso.isPending ? "Ingresando…" : "Ingresar"}
      </Button>
      <Link href="/recuperar" className="text-center text-sm text-muted-foreground underline-offset-4 hover:underline">
        ¿Olvidó su contraseña?
      </Link>
    </form>
  );
}

const esquemaRecuperar = z.object({ usuario: z.string().trim().min(1, "Ingrese su usuario o correo.") });
type DatosRecuperar = z.infer<typeof esquemaRecuperar>;

export function FormularioRecuperar() {
  const [enviado, setEnviado] = useState(false);
  const { register, handleSubmit, formState } = useForm<DatosRecuperar>({ resolver: zodResolver(esquemaRecuperar), defaultValues: { usuario: "" } });
  const envio = useMutation({
    mutationFn: (datos: DatosRecuperar) => servicioSesion.solicitarRecuperacion(datos.usuario),
    onSuccess: () => setEnviado(true),
    onError: (e) => mostrarError(e),
  });

  if (enviado)
    return (
      <div className="grid gap-4 text-center">
        <p role="status" className="text-sm">
          Si los datos corresponden a una cuenta activa, enviamos un correo con las instrucciones para restablecer la contraseña.
        </p>
        <Link href="/login" className="text-sm underline-offset-4 hover:underline">
          Volver al inicio de sesión
        </Link>
      </div>
    );

  return (
    <form className="grid gap-4" noValidate onSubmit={handleSubmit((datos) => envio.mutate(datos))}>
      <Campo etiqueta="Usuario o correo" id="usuario" error={formState.errors.usuario?.message}>
        <Input id="usuario" autoComplete="username" autoFocus aria-invalid={!!formState.errors.usuario} {...register("usuario")} />
      </Campo>
      <Button type="submit" disabled={envio.isPending}>
        {envio.isPending ? "Enviando…" : "Enviar instrucciones"}
      </Button>
      <Link href="/login" className="text-center text-sm text-muted-foreground underline-offset-4 hover:underline">
        Volver al inicio de sesión
      </Link>
    </form>
  );
}

/** La política completa la valida el servidor; aquí solo se evita enviar algo claramente incompleto. */
const esquemaNueva = z
  .object({
    passwordActual: z.string(),
    passwordNueva: z.string().min(10, "Debe tener al menos 10 caracteres."),
    confirmacion: z.string(),
  })
  .refine((d) => d.passwordNueva === d.confirmacion, { path: ["confirmacion"], message: "Las contraseñas no coinciden." });
type DatosNueva = z.infer<typeof esquemaNueva>;
const CAMPOS_NUEVA = ["passwordActual", "passwordNueva", "confirmacion"] as const;

/**
 * Formulario de nueva contraseña. Con `token` restablece una contraseña olvidada; sin él
 * cambia la del usuario autenticado y pide la actual.
 */
export function FormularioNuevaPassword({ token }: { token?: string }) {
  const { register, handleSubmit, formState, setError } = useForm<DatosNueva>({
    resolver: zodResolver(esquemaNueva),
    defaultValues: { passwordActual: "", passwordNueva: "", confirmacion: "" },
  });
  const [hecho, setHecho] = useState(false);

  const cambio = useMutation({
    mutationFn: async (datos: DatosNueva) => {
      if (token) return servicioSesion.restablecer(token, datos.passwordNueva);

      await servicioSesion.cambiarPassword(datos.passwordActual, datos.passwordNueva);
      // La API ya revocó todas las sesiones; aquí solo se borran las cookies de este navegador.
      await servicioSesion.logout().catch(() => undefined);
    },
    onSuccess: () => setHecho(true),
    onError: (e) => mostrarError(e, { setError, campos: CAMPOS_NUEVA }),
  });

  if (hecho)
    return (
      <div className="grid gap-4 text-center">
        <p role="status" className="text-sm">
          La contraseña se cambió. Por seguridad se cerraron todas sus sesiones.
        </p>
        <Button nativeButton={false} render={<a href="/login" />}>
          Iniciar sesión
        </Button>
      </div>
    );

  const errores = formState.errors;

  return (
    <form
      className="grid gap-4"
      noValidate
      onSubmit={handleSubmit((datos) => {
        if (!token && !datos.passwordActual) {
          setError("passwordActual", { message: "Ingrese su contraseña actual." });
          return;
        }
        cambio.mutate(datos);
      })}
    >
      {!token && (
        <Campo etiqueta="Contraseña actual" id="passwordActual" error={errores.passwordActual?.message}>
          <Input id="passwordActual" type="password" autoComplete="current-password" aria-invalid={!!errores.passwordActual} {...register("passwordActual")} />
        </Campo>
      )}
      <Campo
        etiqueta="Contraseña nueva"
        id="passwordNueva"
        error={errores.passwordNueva?.message}
        ayuda="Mínimo 10 caracteres, con mayúscula, minúscula, número y símbolo."
      >
        <Input id="passwordNueva" type="password" autoComplete="new-password" aria-invalid={!!errores.passwordNueva} {...register("passwordNueva")} />
      </Campo>
      <Campo etiqueta="Confirmar contraseña nueva" id="confirmacion" error={errores.confirmacion?.message}>
        <Input id="confirmacion" type="password" autoComplete="new-password" aria-invalid={!!errores.confirmacion} {...register("confirmacion")} />
      </Campo>
      <Button type="submit" disabled={cambio.isPending}>
        {cambio.isPending ? "Guardando…" : "Cambiar contraseña"}
      </Button>
    </form>
  );
}
