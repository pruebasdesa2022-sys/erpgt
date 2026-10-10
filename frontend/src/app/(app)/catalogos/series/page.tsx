"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { seriesDocumento } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={seriesDocumento} />;
}
