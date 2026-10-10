import {
  ArrowLeftRight,
  Ban,
  Blocks,
  Building,
  Building2,
  CalendarClock,
  Circle,
  Coins,
  CreditCard,
  FileText,
  Files,
  FolderTree,
  Handshake,
  History,
  KeyRound,
  Landmark,
  Library,
  ListOrdered,
  Network,
  Package,
  Percent,
  PiggyBank,
  Ruler,
  ScrollText,
  Settings,
  Shield,
  Store,
  Tag,
  Target,
  TrendingUp,
  Truck,
  Users,
  Wallet,
  Warehouse,
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
  // Etapa 1A
  building: Building,
  warehouse: Warehouse,
  wallet: Wallet,
  network: Network,
  target: Target,
  library: Library,
  package: Package,
  handshake: Handshake,
  landmark: Landmark,
  "file-text": FileText,
  files: Files,
  tag: Tag,
  "folder-tree": FolderTree,
  badge: Tag,
  ruler: Ruler,
  "arrow-left-right": ArrowLeftRight,
  truck: Truck,
  "credit-card": CreditCard,
  "calendar-clock": CalendarClock,
  ban: Ban,
  coins: Coins,
  "trending-up": TrendingUp,
  percent: Percent,
  "piggy-bank": PiggyBank,
  "list-ordered": ListOrdered,
  settings: Settings,
};

export function IconoMenu({ nombre, className }: { nombre: string | null; className?: string }) {
  const Icono = (nombre && ICONOS[nombre]) || Circle;
  return <Icono className={className} aria-hidden />;
}
