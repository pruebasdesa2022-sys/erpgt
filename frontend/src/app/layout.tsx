import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { Proveedores } from "@/components/comun/Proveedores";
import { marca } from "@/theme/marca";
import "./globals.css";

const fuenteSans = Geist({ variable: "--font-sans", subsets: ["latin"] });
const fuenteMono = Geist_Mono({ variable: "--font-geist-mono", subsets: ["latin"] });

export const metadata: Metadata = {
  title: { default: marca.nombre, template: `%s · ${marca.nombre}` },
  description: marca.descripcion,
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="es-GT" className={`${fuenteSans.variable} ${fuenteMono.variable} h-full antialiased`}>
      <body className="min-h-full bg-background font-sans text-foreground">
        <Proveedores>{children}</Proveedores>
      </body>
    </html>
  );
}
