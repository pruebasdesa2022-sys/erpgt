"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { marcas } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={marcas} />;
}
