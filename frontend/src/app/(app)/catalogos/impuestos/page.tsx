"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { impuestos } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={impuestos} />;
}
