import {
  Blocks,
  Building2,
  Circle,
  History,
  KeyRound,
  ScrollText,
  Shield,
  Store,
  Users,
  type LucideIcon,
} from "lucide-react";
import type { ReactNode } from "react";
import { marca } from "@/theme/marca";

/** Logo de la marca: la imagen configurada o, si no hay, las iniciales del nombre. */
export function Logo({ conNombre = true }: { conNombre?: boolean }) {
  return (
    <span className="flex items-center gap-2">
      {marca.logo ? (
        // eslint-disable-next-line @next/next/no-img-element -- El logo es un archivo local configurable; no necesita optimización.
        <img src={marca.logo} alt="" className="size-8 rounded-md" />
      ) : (
        <span aria-hidden className="flex size-8 items-center justify-center rounded-md bg-primary text-sm font-semibold text-primary-foreground">
          {marca.nombre.slice(0, 2).toUpperCase()}
        </span>
      )}
      {conNombre && <span className="font-heading text-base font-semibold">{marca.nombre}</span>}
    </span>
  );
}

/** Marco de las pantallas sin sesión (login, recuperación): una tarjeta centrada con la marca. */
export function MarcoPublico({ titulo, descripcion, children }: { titulo: string; descripcion?: string; children: ReactNode }) {
  return (
    <main className="flex min-h-dvh items-center justify-center bg-muted/40 p-4">
      <div className="w-full max-w-sm rounded-xl border bg-card p-6 text-card-foreground shadow-sm">
        <div className="mb-6 flex flex-col items-center gap-3 text-center">
          <Logo />
          <div>
            <h1 className="font-heading text-lg font-semibold">{titulo}</h1>
            {descripcion && <p className="text-sm text-muted-foreground">{descripcion}</p>}
          </div>
        </div>
        {children}
      </div>
    </main>
  );
}

/**
 * Iconos del menú. La base guarda el nombre (columna menu.icono); un nombre que no esté
 * aquí se muestra con un icono genérico en lugar de fallar.
 */
const ICONOS: Record<string, LucideIcon> = {
  shield: Shield,
  users: Users,
  "key-round": KeyRound,
  blocks: Blocks,
  "building-2": Building2,
  store: Store,
  history: History,
  "scroll-text": ScrollText,
};

export function IconoMenu({ nombre, className }: { nombre: string | null; className?: string }) {
  const Icono = (nombre && ICONOS[nombre]) || Circle;
  return <Icono className={className} aria-hidden />;
}
