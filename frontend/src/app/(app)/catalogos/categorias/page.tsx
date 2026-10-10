"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { categorias } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={categorias} />;
}
