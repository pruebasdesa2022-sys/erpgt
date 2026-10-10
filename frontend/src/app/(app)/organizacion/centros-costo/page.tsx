"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { centrosCosto } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={centrosCosto} />;
}
