"use client";

import { useMutation } from "@tanstack/react-query";
import { ChevronDown, KeyRound, LogOut, Menu as IconoHamburguesa } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState, type ReactNode } from "react";
import { EstadoCarga, EstadoError } from "@/components/comun/Estados";
import { IconoMenu, Logo } from "@/components/layout/Marca";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SelectNativo } from "@/components/ui/select-nativo";
import { ProveedorSesion, useCargaSesion, useSesion } from "@/contexts/sesion";
import { mostrarError } from "@/lib/errores";
import { cn } from "@/lib/utils";
import { servicioSesion } from "@/services";
import type { MenuItem } from "@/types";

const RUTA_CAMBIO_PASSWORD = "/cambiar-password";

/**
 * Marco de la aplicación con sesión: carga sesión, menú y permisos, y dibuja el menú lateral
 * y la cabecera. Todo lo visual de la "carcasa" vive aquí para poder cambiarlo sin tocar pantallas.
 */
export function MarcoApp({ children }: { children: ReactNode }) {
  const carga = useCargaSesion();
  const ruta = usePathname();
  const router = useRouter();
  const debeCambiar = carga.data?.sesion.debeCambiarPassword ?? false;

  useEffect(() => {
    if (debeCambiar && ruta !== RUTA_CAMBIO_PASSWORD) router.replace(RUTA_CAMBIO_PASSWORD);
  }, [debeCambiar, ruta, router]);

  if (carga.isPending) return <EstadoCarga filas={8} />;
  if (carga.isError) return <EstadoError error={carga.error} onReintentar={() => void carga.refetch()} />;
  if (debeCambiar && ruta !== RUTA_CAMBIO_PASSWORD) return <EstadoCarga filas={8} />;

  return (
    <ProveedorSesion datos={carga.data}>
      <Carcasa>{children}</Carcasa>
    </ProveedorSesion>
  );
}

function Carcasa({ children }: { children: ReactNode }) {
  const { menu } = useSesion();
  const ruta = usePathname();
  const [menuAbierto, setMenuAbierto] = useState(false);

  return (
    <div className="flex min-h-dvh">
      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-40 w-60 shrink-0 border-r bg-sidebar text-sidebar-foreground transition-transform md:static md:translate-x-0",
          menuAbierto ? "translate-x-0" : "-translate-x-full",
        )}
      >
        <div className="flex h-14 items-center border-b px-4">
          <Link href="/" onClick={() => setMenuAbierto(false)}>
            <Logo />
          </Link>
        </div>
        <nav aria-label="Menú principal" className="space-y-4 p-3">
          {menu.length === 0 && <p className="px-2 text-sm text-muted-foreground">No tiene opciones disponibles.</p>}
          {menu.map((item) => (
            <GrupoMenu key={item.id} item={item} rutaActual={ruta} onNavegar={() => setMenuAbierto(false)} />
          ))}
        </nav>
      </aside>

      {menuAbierto && (
        <button type="button" aria-label="Cerrar menú" className="fixed inset-0 z-30 bg-foreground/20 md:hidden" onClick={() => setMenuAbierto(false)} />
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 items-center gap-3 border-b bg-background px-4">
          <Button variant="ghost" size="icon" className="md:hidden" aria-label="Abrir menú" onClick={() => setMenuAbierto(true)}>
            <IconoHamburguesa />
          </Button>
          <div className="flex-1" />
          <SelectorContexto />
          <MenuUsuario />
        </header>
        <main className="flex-1 space-y-4 p-4 md:p-6">{children}</main>
      </div>
    </div>
  );
}

function GrupoMenu({ item, rutaActual, onNavegar }: { item: MenuItem; rutaActual: string; onNavegar: () => void }) {
  if (item.tipo === "PANTALLA" && item.ruta) {
    const activa = rutaActual === item.ruta || rutaActual.startsWith(`${item.ruta}/`);
    return (
      <Link
        href={item.ruta}
        onClick={onNavegar}
        aria-current={activa ? "page" : undefined}
        className={cn(
          "flex items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
          activa && "bg-sidebar-accent font-medium text-sidebar-accent-foreground",
        )}
      >
        <IconoMenu nombre={item.icono} className="size-4" />
        {item.nombre}
      </Link>
    );
  }

  return (
    <div>
      <p className="mb-1 flex items-center gap-2 px-2 text-xs font-medium text-muted-foreground uppercase">
        <IconoMenu nombre={item.icono} className="size-3.5" />
        {item.nombre}
      </p>
      <div className="space-y-0.5">
        {item.hijos.map((hijo) => (
          <GrupoMenu key={hijo.id} item={hijo} rutaActual={rutaActual} onNavegar={onNavegar} />
        ))}
      </div>
    </div>
  );
}

/** Empresa y sucursal de trabajo. Cambiarla pide a la API una sesión nueva con ese contexto. */
function SelectorContexto() {
  const { sesion } = useSesion();
  const cambio = useMutation({
    mutationFn: (sucursalId: number) => {
      const sucursal = sesion.sucursales.find((s) => s.id === sucursalId)!;
      return servicioSesion.cambiarContexto(sucursal.empresaId, sucursal.id);
    },
    // Todo lo cargado pertenece al contexto anterior: se recarga la aplicación completa.
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination -- Recarga completa a propósito: descarta todo el estado del cliente.
    onSuccess: () => window.location.assign("/"),
    onError: (error) => mostrarError(error),
  });

  const variasEmpresas = sesion.empresas.length > 1;
  const nombreEmpresa = (id: number) => sesion.empresas.find((e) => e.id === id)?.nombreComercial ?? "";

  if (sesion.sucursales.length <= 1) {
    const unica = sesion.sucursales[0];
    return <span className="hidden text-sm text-muted-foreground sm:inline">{unica ? `${nombreEmpresa(unica.empresaId)} · ${unica.nombre}` : ""}</span>;
  }

  return (
    <SelectNativo
      aria-label="Empresa y sucursal de trabajo"
      className="w-auto max-w-56"
      value={sesion.sucursalId}
      disabled={cambio.isPending}
      onChange={(evento) => cambio.mutate(Number(evento.target.value))}
    >
      {sesion.sucursales.map((s) => (
        <option key={s.id} value={s.id}>
          {variasEmpresas ? `${nombreEmpresa(s.empresaId)} · ${s.nombre}` : s.nombre}
        </option>
      ))}
    </SelectNativo>
  );
}

function MenuUsuario() {
  const { sesion } = useSesion();
  const router = useRouter();
  const salir = useMutation({
    mutationFn: servicioSesion.logout,
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination -- Recarga completa a propósito: descarta todo el estado del cliente.
    onSettled: () => window.location.assign("/login"),
  });

  return (
    <DropdownMenu>
      <DropdownMenuTrigger render={<Button variant="ghost" />}>
        <span className="max-w-40 truncate">{sesion.nombreCompleto}</span>
        <ChevronDown />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <div className="px-1.5 py-1 text-xs text-muted-foreground">
          <p className="truncate">{sesion.nombreUsuario}</p>
          <p className="truncate">{sesion.correo}</p>
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => router.push(RUTA_CAMBIO_PASSWORD)}>
          <KeyRound />
          Cambiar contraseña
        </DropdownMenuItem>
        <DropdownMenuItem variant="destructive" disabled={salir.isPending} onClick={() => salir.mutate()}>
          <LogOut />
          Cerrar sesión
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
