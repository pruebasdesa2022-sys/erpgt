"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { monedas } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={monedas} />;
}
