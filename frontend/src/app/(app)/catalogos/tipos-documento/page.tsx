"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { tiposDocumento } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={tiposDocumento} />;
}
