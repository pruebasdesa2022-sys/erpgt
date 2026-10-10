"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { bodegas } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={bodegas} />;
}
