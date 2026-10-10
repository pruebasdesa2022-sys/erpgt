"use client";

import { CatalogoPagina } from "@/components/catalogos/CatalogoPagina";
import { tiposProducto } from "@/components/catalogos/configuraciones";

export default function Pagina() {
  return <CatalogoPagina config={tiposProducto} />;
}
