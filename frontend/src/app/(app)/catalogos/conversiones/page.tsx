"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { conversionesUnidad } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={conversionesUnidad} />;
}
